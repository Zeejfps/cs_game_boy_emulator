namespace GameBoyEmulator.Core;

// DMG audio processing unit. Generates stereo PCM samples by ticking four
// channels in lockstep with the CPU clock, mixing per NR50/NR51, applying
// a DMG-style high-pass filter, and writing host-rate float frames into an
// internal ring buffer that the host drains via DrainAudio().
//
// Step granularity: T-cycle. Each call advances every channel's frequency
// timer (and the noise LFSR's clock) by 1 T-cycle, samples DAC outputs at
// 1 MHz native rate (every 4 T-cycles), accumulates into per-side running
// sums for box-average downsampling, and emits one host frame per
// `cyclesPerSampleQ` T-cycles using a Q16.16 fixed-point period to avoid
// drift.
//
// Frame sequencer: clocked externally by Timer's DIV bit-12 falling-edge
// hook (OnFrameSequencerTick) — that is the real-hardware source, and tying
// it to DIV makes games that poke DIV to phase-shift envelopes/sweep behave
// correctly (blargg dmg_sound 04-sweep et al).
//
// Power semantics: NR52 bit 7 is the master enable. Clearing it resets all
// channel state and all NRxx registers to 0 (wave RAM is preserved on DMG).
// While powered off, writes to NR10..NR25 are ignored. Length-counter
// length-load writes (NRx1) accept while off on CGB but not DMG; we model
// strict DMG behavior.
public sealed class Apu : IApu
{
    private const ushort Nr10 = 0xFF10, Nr11 = 0xFF11, Nr12 = 0xFF12, Nr13 = 0xFF13, Nr14 = 0xFF14;
    private const ushort Nr21 = 0xFF16, Nr22 = 0xFF17, Nr23 = 0xFF18, Nr24 = 0xFF19;
    private const ushort Nr30 = 0xFF1A, Nr31 = 0xFF1B, Nr32 = 0xFF1C, Nr33 = 0xFF1D, Nr34 = 0xFF1E;
    private const ushort Nr41 = 0xFF20, Nr42 = 0xFF21, Nr43 = 0xFF22, Nr44 = 0xFF23;
    private const ushort Nr50 = 0xFF24, Nr51 = 0xFF25, Nr52 = 0xFF26;
    private const ushort WaveRamStart = 0xFF30, WaveRamEnd = 0xFF3F;

    private static readonly byte[] ReadMask =
    [
        0x80, 0x3F, 0x00, 0xFF, 0xBF, // FF10-FF14 (channel 1)
        0xFF, 0x3F, 0x00, 0xFF, 0xBF, // FF15-FF19 (FF15 unused, channel 2)
        0x7F, 0xFF, 0x9F, 0xFF, 0xBF, // FF1A-FF1E (channel 3)
        0xFF, 0xFF, 0x00, 0x00, 0xBF, // FF1F-FF23 (FF1F unused, channel 4)
        0x00, 0x00, 0x70,             // FF24-FF26 (NR50, NR51, NR52)
    ];

    // 8-step duty patterns from gbdev wiki: 12.5%, 25%, 50%, 75% (last is the
    // logical inverse of 25% — sounds the same but inverted polarity).
    private static readonly byte[] DutyPatterns = [0b00000001, 0b10000001, 0b10000111, 0b01111110];

    // Noise channel divisor: bits 2-0 of NR43 select. 0 maps to 8, otherwise n*16.
    private static readonly int[] NoiseDivisors = [8, 16, 32, 48, 64, 80, 96, 112];

    private readonly byte[] _waveRam = new byte[16];
    private bool _powered;
    private bool _isCgb;

    // Per-channel state. Inlined as fields to avoid allocation/dispatch overhead
    // on the hot per-T-cycle path.
    // ---- CH1 (square + sweep + envelope) ----
    private bool _ch1Enabled, _ch1DacEnabled, _ch1LengthEnabled;
    private int _ch1Length;
    private int _ch1DutyIdx, _ch1DutyStep;
    private int _ch1Freq;          // 11-bit
    private int _ch1FreqTimer;     // T-cycles until next duty step
    private int _ch1EnvVol, _ch1EnvPeriod, _ch1EnvTimer;
    private bool _ch1EnvIncrease;
    private int _ch1SweepShift, _ch1SweepPeriod, _ch1SweepTimer, _ch1SweepShadowFreq;
    private bool _ch1SweepNegate, _ch1SweepEnabled, _ch1SweepNegateUsed;

    // ---- CH2 (square + envelope) ----
    private bool _ch2Enabled, _ch2DacEnabled, _ch2LengthEnabled;
    private int _ch2Length;
    private int _ch2DutyIdx, _ch2DutyStep;
    private int _ch2Freq;
    private int _ch2FreqTimer;
    private int _ch2EnvVol, _ch2EnvPeriod, _ch2EnvTimer;
    private bool _ch2EnvIncrease;

    // ---- CH3 (wave RAM) ----
    private bool _ch3Enabled, _ch3DacEnabled, _ch3LengthEnabled;
    private int _ch3Length;        // 8-bit on DMG
    private int _ch3OutputShift;   // 0=mute via shift=4, 1=>>0, 2=>>1, 3=>>2 (we store as effective shift)
    private int _ch3Freq;
    private int _ch3FreqTimer;
    private int _ch3WavePos;       // 0..31, indexes nibbles of wave RAM
    private byte _ch3SampleBuf;    // last nibble fetched (0..15)

    // ---- CH4 (noise + envelope) ----
    private bool _ch4Enabled, _ch4DacEnabled, _ch4LengthEnabled;
    private int _ch4Length;
    private int _ch4FreqTimer;
    private int _ch4Divisor, _ch4Shift;
    private bool _ch4WidthMode;    // false=15-bit LFSR, true=7-bit
    private int _ch4Lfsr;
    private int _ch4EnvVol, _ch4EnvPeriod, _ch4EnvTimer;
    private bool _ch4EnvIncrease;

    // Stored register values for readback (with per-bit mask applied on read).
    private byte _nr10, _nr11, _nr12, _nr13, _nr14;
    private byte _nr21, _nr22, _nr23, _nr24;
    private byte _nr30, _nr31, _nr32, _nr33, _nr34;
    private byte _nr41, _nr42, _nr43, _nr44;
    private byte _nr50, _nr51;

    // Frame sequencer state. Stepped externally via OnFrameSequencerTick (DIV
    // bit-12 falling edge from Timer). Step index walks 0..7; the FSM below
    // dispatches length/envelope/sweep clocks per step.
    private int _frameSeqStep;

    // Native-rate sampling. Sample every 4 T-cycles (1.048576 MHz nominal).
    // Use Q16.16 fixed-point period so 4194304/sampleRate doesn't drift.
    private int _nativeAccumCounter;
    private float _accumL, _accumR;
    private int _accumCount;

    // Q16.16 host-sample period in T-cycles. Set by Reconfigure().
    private long _cyclesPerSampleQ;
    private long _cyclesAccumQ;

    // High-pass filter state (one-pole, applied per-side at host rate).
    private float _hpfPrevInL, _hpfPrevOutL;
    private float _hpfPrevInR, _hpfPrevOutR;
    private float _hpfAlpha = 0.996f; // overwritten by Reconfigure()

    // Stereo float ring buffer for host drain. Single-producer / single-consumer
    // (both run on the same thread — main thread inside the WASM module — so
    // no atomics needed). On overrun the producer drops newest samples.
    private const int RingFrames = 8192;
    private readonly float[] _ring = new float[RingFrames * 2];
    private int _ringWrite, _ringRead, _ringCount;

    public Apu() : this(48000) { }

    public Apu(int sampleRate)
    {
        Reconfigure(sampleRate);
    }

    public void Reconfigure(int sampleRate)
    {
        if (sampleRate <= 0) sampleRate = 48000;
        // Q16.16: cyclesPerSample = 4194304 / sampleRate
        _cyclesPerSampleQ = (4194304L << 16) / sampleRate;
        // alpha = exp(-2*pi*60/sampleRate). Hardcoded approximation table is
        // overkill; this only runs once per construction.
        _hpfAlpha = (float)Math.Exp(-2.0 * Math.PI * 60.0 / sampleRate);
    }

    public void SetCgbMode(bool isCgb)
    {
        _isCgb = isCgb;
    }

    // ---- Bus interface ---------------------------------------------------

    public void WriteRegister(ushort address, byte value)
    {
        if (address >= WaveRamStart && address <= WaveRamEnd)
        {
            // CH3 active wave-RAM read quirk: on DMG, an external write while
            // the channel is reading aliases to whichever byte the channel is
            // currently fetching. Modeling that quirk requires knowing the
            // exact T-cycle of the active read; without it the simple
            // "writes only land when channel is off" approximation is closer
            // to correct for most games.
            if (!_ch3Enabled)
                _waveRam[address - WaveRamStart] = value;
            return;
        }

        if (address == Nr52)
        {
            var nowOn = (value & 0x80) != 0;
            if (!nowOn && _powered) PowerOff();
            else if (nowOn && !_powered) PowerOn();
            return;
        }

        // Length-load writes (NR11/NR21/NR31/NR41) are accepted while powered
        // off on DMG too — but only the length bits, register reads still
        // come back zeroed. Most games don't care; matches blargg expectations.
        if (!_powered)
        {
            if (address == Nr11) _ch1Length = 64 - (value & 0x3F);
            else if (address == Nr21) _ch2Length = 64 - (value & 0x3F);
            else if (address == Nr31) _ch3Length = 256 - value;
            else if (address == Nr41) _ch4Length = 64 - (value & 0x3F);
            return;
        }

        switch (address)
        {
            case Nr10: WriteNr10(value); break;
            case Nr11: WriteNr11(value); break;
            case Nr12: WriteNr12(value); break;
            case Nr13: WriteNr13(value); break;
            case Nr14: WriteNr14(value); break;
            case Nr21: WriteNr21(value); break;
            case Nr22: WriteNr22(value); break;
            case Nr23: WriteNr23(value); break;
            case Nr24: WriteNr24(value); break;
            case Nr30: WriteNr30(value); break;
            case Nr31: WriteNr31(value); break;
            case Nr32: WriteNr32(value); break;
            case Nr33: WriteNr33(value); break;
            case Nr34: WriteNr34(value); break;
            case Nr41: WriteNr41(value); break;
            case Nr42: WriteNr42(value); break;
            case Nr43: WriteNr43(value); break;
            case Nr44: WriteNr44(value); break;
            case Nr50: _nr50 = value; break;
            case Nr51: _nr51 = value; break;
        }
    }

    public byte ReadRegister(ushort address)
    {
        if (address >= WaveRamStart && address <= WaveRamEnd)
        {
            // While CH3 is active, DMG reads return the byte the channel is
            // currently fetching (same quirk as writes). Approximated as the
            // last-fetched byte; for inactive channel, return the stored byte.
            if (_ch3Enabled) return _waveRam[(_ch3WavePos >> 1) & 0x0F];
            return _waveRam[address - WaveRamStart];
        }

        if (address < Nr10 || address > Nr52) return 0xFF;

        var idx = address - Nr10;
        var mask = ReadMask[idx];
        byte raw = address switch
        {
            Nr10 => _nr10,
            Nr11 => _nr11,
            Nr12 => _nr12,
            Nr13 => _nr13,
            Nr14 => _nr14,
            Nr21 => _nr21,
            Nr22 => _nr22,
            Nr23 => _nr23,
            Nr24 => _nr24,
            Nr30 => _nr30,
            Nr31 => _nr31,
            Nr32 => _nr32,
            Nr33 => _nr33,
            Nr34 => _nr34,
            Nr41 => _nr41,
            Nr42 => _nr42,
            Nr43 => _nr43,
            Nr44 => _nr44,
            Nr50 => _nr50,
            Nr51 => _nr51,
            Nr52 => (byte)((_powered ? 0x80 : 0)
                          | (_ch4Enabled ? 0x08 : 0)
                          | (_ch3Enabled ? 0x04 : 0)
                          | (_ch2Enabled ? 0x02 : 0)
                          | (_ch1Enabled ? 0x01 : 0)),
            _ => 0,
        };
        return (byte)(raw | mask);
    }

    private void PowerOn()
    {
        _powered = true;
        _frameSeqStep = 0;
    }

    private void PowerOff()
    {
        _powered = false;
        // Zero everything except wave RAM and length counters (DMG quirk —
        // length values survive power-off; everything else resets).
        _nr10 = _nr11 = _nr12 = _nr13 = _nr14 = 0;
        _nr21 = _nr22 = _nr23 = _nr24 = 0;
        _nr30 = _nr31 = _nr32 = _nr33 = _nr34 = 0;
        _nr41 = _nr42 = _nr43 = _nr44 = 0;
        _nr50 = _nr51 = 0;

        _ch1Enabled = _ch1DacEnabled = _ch1LengthEnabled = false;
        _ch1DutyIdx = _ch1DutyStep = _ch1Freq = _ch1FreqTimer = 0;
        _ch1EnvVol = _ch1EnvPeriod = _ch1EnvTimer = 0;
        _ch1EnvIncrease = false;
        _ch1SweepShift = _ch1SweepPeriod = _ch1SweepTimer = _ch1SweepShadowFreq = 0;
        _ch1SweepNegate = _ch1SweepEnabled = _ch1SweepNegateUsed = false;

        _ch2Enabled = _ch2DacEnabled = _ch2LengthEnabled = false;
        _ch2DutyIdx = _ch2DutyStep = _ch2Freq = _ch2FreqTimer = 0;
        _ch2EnvVol = _ch2EnvPeriod = _ch2EnvTimer = 0;
        _ch2EnvIncrease = false;

        _ch3Enabled = _ch3DacEnabled = _ch3LengthEnabled = false;
        _ch3OutputShift = 4; // mute
        _ch3Freq = _ch3FreqTimer = _ch3WavePos = 0;
        _ch3SampleBuf = 0;

        _ch4Enabled = _ch4DacEnabled = _ch4LengthEnabled = false;
        _ch4FreqTimer = 0;
        _ch4Divisor = NoiseDivisors[0];
        _ch4Shift = 0;
        _ch4WidthMode = false;
        _ch4Lfsr = 0x7FFF;
        _ch4EnvVol = _ch4EnvPeriod = _ch4EnvTimer = 0;
        _ch4EnvIncrease = false;
    }

    // ---- NRxx writers ----------------------------------------------------

    private void WriteNr10(byte v)
    {
        _nr10 = v;
        _ch1SweepPeriod = (v >> 4) & 0x07;
        _ch1SweepNegate = (v & 0x08) != 0;
        _ch1SweepShift = v & 0x07;
        // Disabling negate after a sweep calculation that used negate kills
        // the channel — this is the "obscure_behavior" sweep test in blargg.
        if (_ch1SweepNegateUsed && !_ch1SweepNegate) _ch1Enabled = false;
    }

    private void WriteNr11(byte v)
    {
        _nr11 = v;
        _ch1DutyIdx = (v >> 6) & 0x03;
        _ch1Length = 64 - (v & 0x3F);
    }

    private void WriteNr12(byte v)
    {
        _nr12 = v;
        _ch1EnvIncrease = (v & 0x08) != 0;
        _ch1EnvPeriod = v & 0x07;
        // Top 5 bits all zero disables the DAC, which kills the channel.
        _ch1DacEnabled = (v & 0xF8) != 0;
        if (!_ch1DacEnabled) _ch1Enabled = false;
    }

    private void WriteNr13(byte v)
    {
        _nr13 = v;
        _ch1Freq = (_ch1Freq & 0x700) | v;
    }

    private void WriteNr14(byte v)
    {
        _nr14 = v;
        var prevLengthEnable = _ch1LengthEnabled;
        _ch1LengthEnabled = (v & 0x40) != 0;
        _ch1Freq = (_ch1Freq & 0xFF) | ((v & 0x07) << 8);

        // Length-extra-clock quirk: when length-enable transitions 0→1 in the
        // first half of a frame-sequencer length-clock cycle (i.e. the *next*
        // FS step would NOT clock length), the length counter clocks an extra
        // time. This is required to pass blargg 03-trigger.
        if (!prevLengthEnable && _ch1LengthEnabled && _ch1Length > 0 && !FrameSeqNextClocksLength())
        {
            _ch1Length--;
            if (_ch1Length == 0) _ch1Enabled = false;
        }

        if ((v & 0x80) != 0) TriggerCh1();
    }

    private void TriggerCh1()
    {
        _ch1Enabled = _ch1DacEnabled;
        if (_ch1Length == 0)
        {
            _ch1Length = 64;
            // Trigger-during-first-half-of-length-period also clocks length once.
            if (_ch1LengthEnabled && !FrameSeqNextClocksLength())
                _ch1Length--;
        }
        _ch1FreqTimer = (2048 - _ch1Freq) * 4;
        _ch1EnvTimer = _ch1EnvPeriod == 0 ? 8 : _ch1EnvPeriod;
        _ch1EnvVol = (_nr12 >> 4) & 0x0F;
        _ch1SweepShadowFreq = _ch1Freq;
        _ch1SweepTimer = _ch1SweepPeriod == 0 ? 8 : _ch1SweepPeriod;
        _ch1SweepEnabled = _ch1SweepPeriod != 0 || _ch1SweepShift != 0;
        _ch1SweepNegateUsed = false;
        if (_ch1SweepShift != 0) SweepCalc(applyToShadow: false);
    }

    private void WriteNr21(byte v)
    {
        _nr21 = v;
        _ch2DutyIdx = (v >> 6) & 0x03;
        _ch2Length = 64 - (v & 0x3F);
    }

    private void WriteNr22(byte v)
    {
        _nr22 = v;
        _ch2EnvIncrease = (v & 0x08) != 0;
        _ch2EnvPeriod = v & 0x07;
        _ch2DacEnabled = (v & 0xF8) != 0;
        if (!_ch2DacEnabled) _ch2Enabled = false;
    }

    private void WriteNr23(byte v)
    {
        _nr23 = v;
        _ch2Freq = (_ch2Freq & 0x700) | v;
    }

    private void WriteNr24(byte v)
    {
        _nr24 = v;
        var prevLengthEnable = _ch2LengthEnabled;
        _ch2LengthEnabled = (v & 0x40) != 0;
        _ch2Freq = (_ch2Freq & 0xFF) | ((v & 0x07) << 8);
        if (!prevLengthEnable && _ch2LengthEnabled && _ch2Length > 0 && !FrameSeqNextClocksLength())
        {
            _ch2Length--;
            if (_ch2Length == 0) _ch2Enabled = false;
        }
        if ((v & 0x80) != 0) TriggerCh2();
    }

    private void TriggerCh2()
    {
        _ch2Enabled = _ch2DacEnabled;
        if (_ch2Length == 0)
        {
            _ch2Length = 64;
            if (_ch2LengthEnabled && !FrameSeqNextClocksLength()) _ch2Length--;
        }
        _ch2FreqTimer = (2048 - _ch2Freq) * 4;
        _ch2EnvTimer = _ch2EnvPeriod == 0 ? 8 : _ch2EnvPeriod;
        _ch2EnvVol = (_nr22 >> 4) & 0x0F;
    }

    private void WriteNr30(byte v)
    {
        _nr30 = v;
        _ch3DacEnabled = (v & 0x80) != 0;
        if (!_ch3DacEnabled) _ch3Enabled = false;
    }

    private void WriteNr31(byte v)
    {
        _nr31 = v;
        _ch3Length = 256 - v;
    }

    private void WriteNr32(byte v)
    {
        _nr32 = v;
        var code = (v >> 5) & 0x03;
        _ch3OutputShift = code switch
        {
            0 => 4,  // mute (shift right by 4 → always 0)
            1 => 0,
            2 => 1,
            3 => 2,
            _ => 4,
        };
    }

    private void WriteNr33(byte v)
    {
        _nr33 = v;
        _ch3Freq = (_ch3Freq & 0x700) | v;
    }

    private void WriteNr34(byte v)
    {
        _nr34 = v;
        var prevLengthEnable = _ch3LengthEnabled;
        _ch3LengthEnabled = (v & 0x40) != 0;
        _ch3Freq = (_ch3Freq & 0xFF) | ((v & 0x07) << 8);
        if (!prevLengthEnable && _ch3LengthEnabled && _ch3Length > 0 && !FrameSeqNextClocksLength())
        {
            _ch3Length--;
            if (_ch3Length == 0) _ch3Enabled = false;
        }
        if ((v & 0x80) != 0) TriggerCh3();
    }

    private void TriggerCh3()
    {
        _ch3Enabled = _ch3DacEnabled;
        if (_ch3Length == 0)
        {
            _ch3Length = 256;
            if (_ch3LengthEnabled && !FrameSeqNextClocksLength()) _ch3Length--;
        }
        _ch3FreqTimer = (2048 - _ch3Freq) * 2 + 6; // small startup delay matches DMG
        _ch3WavePos = 0;
    }

    private void WriteNr41(byte v)
    {
        _nr41 = v;
        _ch4Length = 64 - (v & 0x3F);
    }

    private void WriteNr42(byte v)
    {
        _nr42 = v;
        _ch4EnvIncrease = (v & 0x08) != 0;
        _ch4EnvPeriod = v & 0x07;
        _ch4DacEnabled = (v & 0xF8) != 0;
        if (!_ch4DacEnabled) _ch4Enabled = false;
    }

    private void WriteNr43(byte v)
    {
        _nr43 = v;
        _ch4Shift = (v >> 4) & 0x0F;
        _ch4WidthMode = (v & 0x08) != 0;
        _ch4Divisor = NoiseDivisors[v & 0x07];
    }

    private void WriteNr44(byte v)
    {
        _nr44 = v;
        var prevLengthEnable = _ch4LengthEnabled;
        _ch4LengthEnabled = (v & 0x40) != 0;
        if (!prevLengthEnable && _ch4LengthEnabled && _ch4Length > 0 && !FrameSeqNextClocksLength())
        {
            _ch4Length--;
            if (_ch4Length == 0) _ch4Enabled = false;
        }
        if ((v & 0x80) != 0) TriggerCh4();
    }

    private void TriggerCh4()
    {
        _ch4Enabled = _ch4DacEnabled;
        if (_ch4Length == 0)
        {
            _ch4Length = 64;
            if (_ch4LengthEnabled && !FrameSeqNextClocksLength()) _ch4Length--;
        }
        _ch4FreqTimer = _ch4Divisor << _ch4Shift;
        _ch4EnvTimer = _ch4EnvPeriod == 0 ? 8 : _ch4EnvPeriod;
        _ch4EnvVol = (_nr42 >> 4) & 0x0F;
        _ch4Lfsr = 0x7FFF;
    }

    // ---- Frame sequencer dispatch ---------------------------------------

    public void OnFrameSequencerTick()
    {
        if (!_powered) return;

        // Step → which sub-clocks fire:
        //   0,2,4,6 → length;  2,6 → sweep;  7 → envelope.
        switch (_frameSeqStep)
        {
            case 0: ClockLength(); break;
            case 2: ClockLength(); ClockSweep(); break;
            case 4: ClockLength(); break;
            case 6: ClockLength(); ClockSweep(); break;
            case 7: ClockEnvelope(); break;
        }
        _frameSeqStep = (_frameSeqStep + 1) & 7;
    }

    // True iff the *next* frame-seq tick will clock the length counter.
    // Used to gate the "extra length clock" quirk on enable/trigger writes.
    private bool FrameSeqNextClocksLength()
    {
        var next = (_frameSeqStep) & 7;
        return next == 0 || next == 2 || next == 4 || next == 6;
    }

    private void ClockLength()
    {
        if (_ch1LengthEnabled && _ch1Length > 0 && --_ch1Length == 0) _ch1Enabled = false;
        if (_ch2LengthEnabled && _ch2Length > 0 && --_ch2Length == 0) _ch2Enabled = false;
        if (_ch3LengthEnabled && _ch3Length > 0 && --_ch3Length == 0) _ch3Enabled = false;
        if (_ch4LengthEnabled && _ch4Length > 0 && --_ch4Length == 0) _ch4Enabled = false;
    }

    private void ClockEnvelope()
    {
        ClockEnv(ref _ch1EnvTimer, _ch1EnvPeriod, _ch1EnvIncrease, ref _ch1EnvVol);
        ClockEnv(ref _ch2EnvTimer, _ch2EnvPeriod, _ch2EnvIncrease, ref _ch2EnvVol);
        ClockEnv(ref _ch4EnvTimer, _ch4EnvPeriod, _ch4EnvIncrease, ref _ch4EnvVol);
    }

    private static void ClockEnv(ref int timer, int period, bool increase, ref int vol)
    {
        if (period == 0) return; // disabled
        if (--timer > 0) return;
        timer = period;
        if (increase && vol < 15) vol++;
        else if (!increase && vol > 0) vol--;
    }

    private void ClockSweep()
    {
        if (--_ch1SweepTimer > 0) return;
        _ch1SweepTimer = _ch1SweepPeriod == 0 ? 8 : _ch1SweepPeriod;
        if (!_ch1SweepEnabled || _ch1SweepPeriod == 0) return;
        var newFreq = SweepCalc(applyToShadow: true);
        if (newFreq < 2048 && _ch1SweepShift > 0)
        {
            _ch1SweepShadowFreq = newFreq;
            _ch1Freq = newFreq;
            // Second computation for overflow check, results discarded.
            SweepCalc(applyToShadow: false);
        }
    }

    private int SweepCalc(bool applyToShadow)
    {
        var delta = _ch1SweepShadowFreq >> _ch1SweepShift;
        var n = _ch1SweepNegate ? _ch1SweepShadowFreq - delta : _ch1SweepShadowFreq + delta;
        if (_ch1SweepNegate) _ch1SweepNegateUsed = true;
        if (n > 2047) _ch1Enabled = false;
        return n;
    }

    // ---- Per-T-cycle stepping --------------------------------------------

    public void Step(int tStates)
    {
        if (!_powered)
        {
            // Even when powered off, we still need to emit silence into the
            // ring buffer so the host worklet doesn't underrun. That's just
            // pushing zero samples at host rate.
            EmitSilence(tStates);
            return;
        }

        for (var i = 0; i < tStates; i++)
        {
            // CH1 frequency timer
            if (--_ch1FreqTimer <= 0)
            {
                _ch1FreqTimer = (2048 - _ch1Freq) * 4;
                _ch1DutyStep = (_ch1DutyStep + 1) & 7;
            }
            // CH2 frequency timer
            if (--_ch2FreqTimer <= 0)
            {
                _ch2FreqTimer = (2048 - _ch2Freq) * 4;
                _ch2DutyStep = (_ch2DutyStep + 1) & 7;
            }
            // CH3 frequency timer
            if (--_ch3FreqTimer <= 0)
            {
                _ch3FreqTimer = (2048 - _ch3Freq) * 2;
                _ch3WavePos = (_ch3WavePos + 1) & 31;
                var b = _waveRam[_ch3WavePos >> 1];
                _ch3SampleBuf = (byte)(((_ch3WavePos & 1) == 0 ? (b >> 4) : (b & 0x0F)));
            }
            // CH4 LFSR clock
            if (--_ch4FreqTimer <= 0)
            {
                _ch4FreqTimer = _ch4Divisor << _ch4Shift;
                var x = ((_ch4Lfsr & 1) ^ ((_ch4Lfsr >> 1) & 1));
                _ch4Lfsr = (_ch4Lfsr >> 1) | (x << 14);
                if (_ch4WidthMode)
                    _ch4Lfsr = (_ch4Lfsr & ~0x40) | (x << 6);
            }

            // Native-rate sample every 4 T-cycles. Box-average accumulate
            // into per-side running sums.
            if (++_nativeAccumCounter >= 4)
            {
                _nativeAccumCounter = 0;
                MixOneNativeSample();
            }

            // Host-rate sample emission. cyclesAccumQ counts T-cycles in
            // Q16.16; emit when it reaches cyclesPerSampleQ.
            _cyclesAccumQ += 1L << 16;
            if (_cyclesAccumQ >= _cyclesPerSampleQ)
            {
                _cyclesAccumQ -= _cyclesPerSampleQ;
                EmitHostSample();
            }
        }
    }

    private void MixOneNativeSample()
    {
        // Per-channel DAC outputs in [-1, +1] (or 0 when DAC off).
        // Square: amplitude = duty bit (0/1) * envelope volume (0..15)
        // Wave: sample (0..15) >> output shift
        // Noise: (~lfsr & 1) * envelope volume
        float d1 = 0f, d2 = 0f, d3 = 0f, d4 = 0f;

        if (_ch1DacEnabled)
        {
            int amp = 0;
            if (_ch1Enabled)
            {
                var bit = (DutyPatterns[_ch1DutyIdx] >> _ch1DutyStep) & 1;
                amp = bit * _ch1EnvVol;
            }
            d1 = amp / 7.5f - 1f;
        }
        if (_ch2DacEnabled)
        {
            int amp = 0;
            if (_ch2Enabled)
            {
                var bit = (DutyPatterns[_ch2DutyIdx] >> _ch2DutyStep) & 1;
                amp = bit * _ch2EnvVol;
            }
            d2 = amp / 7.5f - 1f;
        }
        if (_ch3DacEnabled)
        {
            int amp = _ch3Enabled ? (_ch3SampleBuf >> _ch3OutputShift) : 0;
            d3 = amp / 7.5f - 1f;
        }
        if (_ch4DacEnabled)
        {
            int amp = 0;
            if (_ch4Enabled)
            {
                var bit = (~_ch4Lfsr) & 1;
                amp = bit * _ch4EnvVol;
            }
            d4 = amp / 7.5f - 1f;
        }

        // NR51 routing: bit 0..3 = right (CH1..CH4), bit 4..7 = left.
        float l = 0f, r = 0f;
        var nr51 = _nr51;
        if ((nr51 & 0x10) != 0) l += d1;
        if ((nr51 & 0x20) != 0) l += d2;
        if ((nr51 & 0x40) != 0) l += d3;
        if ((nr51 & 0x80) != 0) l += d4;
        if ((nr51 & 0x01) != 0) r += d1;
        if ((nr51 & 0x02) != 0) r += d2;
        if ((nr51 & 0x04) != 0) r += d3;
        if ((nr51 & 0x08) != 0) r += d4;

        var leftVol = (_nr50 >> 4) & 0x07;
        var rightVol = _nr50 & 0x07;
        // (vol+1)/8 in [1/8, 8/8]; divide mix by 4 to keep total in [-1,+1].
        l *= (leftVol + 1) / 32f;
        r *= (rightVol + 1) / 32f;

        _accumL += l;
        _accumR += r;
        _accumCount++;
    }

    private void EmitHostSample()
    {
        float l, r;
        if (_accumCount > 0)
        {
            l = _accumL / _accumCount;
            r = _accumR / _accumCount;
        }
        else
        {
            l = 0f; r = 0f;
        }
        _accumL = 0f; _accumR = 0f; _accumCount = 0;

        // One-pole DC-blocking high-pass: y[n] = x[n] - x[n-1] + alpha * y[n-1]
        var ol = l - _hpfPrevInL + _hpfAlpha * _hpfPrevOutL;
        _hpfPrevInL = l; _hpfPrevOutL = ol;
        var or_ = r - _hpfPrevInR + _hpfAlpha * _hpfPrevOutR;
        _hpfPrevInR = r; _hpfPrevOutR = or_;

        WriteRing(ol, or_);
    }

    private void EmitSilence(int tStates)
    {
        // Powered-off path. Keep host-rate emission running so the worklet
        // doesn't starve, but skip channel work entirely.
        for (var i = 0; i < tStates; i++)
        {
            _cyclesAccumQ += 1L << 16;
            if (_cyclesAccumQ >= _cyclesPerSampleQ)
            {
                _cyclesAccumQ -= _cyclesPerSampleQ;
                // Apply HPF to silence so any DC trapped in the filter bleeds out.
                var ol = 0f - _hpfPrevInL + _hpfAlpha * _hpfPrevOutL;
                _hpfPrevInL = 0f; _hpfPrevOutL = ol;
                var or_ = 0f - _hpfPrevInR + _hpfAlpha * _hpfPrevOutR;
                _hpfPrevInR = 0f; _hpfPrevOutR = or_;
                WriteRing(ol, or_);
            }
        }
    }

    private void WriteRing(float l, float r)
    {
        // Drop-newest on overrun: if the buffer is full, discard this frame.
        if (_ringCount >= RingFrames) return;
        var idx = _ringWrite * 2;
        _ring[idx] = l;
        _ring[idx + 1] = r;
        _ringWrite = (_ringWrite + 1) % RingFrames;
        _ringCount++;
    }

    public int DrainAudio(Span<float> dest)
    {
        // dest holds interleaved L,R floats; length must be even.
        var capFrames = dest.Length / 2;
        var n = capFrames < _ringCount ? capFrames : _ringCount;
        for (var i = 0; i < n; i++)
        {
            var idx = _ringRead * 2;
            dest[i * 2] = _ring[idx];
            dest[i * 2 + 1] = _ring[idx + 1];
            _ringRead = (_ringRead + 1) % RingFrames;
        }
        _ringCount -= n;
        return n;
    }
}
