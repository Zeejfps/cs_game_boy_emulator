namespace GameBoyEmulator.Core;

// No-audio APU. Implements register I/O with the correct unused-bit masks
// and DMG NR52 power-down semantics, but generates no samples. Used by tests
// and benchmarks; the real-audio Apu is what the WASM build instantiates.
//
// When NR52 bit 7 is cleared, all sound register writes (other than NR52
// itself and wave RAM) are ignored, matching DMG behavior. Channel-active
// bits 0-3 of NR52 read as 0 — i.e. "no channel currently playing" — so
// games that poll for SFX completion don't hang.
public sealed class SilentApu : IApu
{
    private const ushort Nr10 = 0xFF10;
    private const ushort Nr52 = 0xFF26;
    private const ushort WaveRamStart = 0xFF30;
    private const ushort WaveRamEnd = 0xFF3F;

    private static readonly byte[] ReadMask =
    [
        0x80, 0x3F, 0x00, 0xFF, 0xBF,
        0xFF, 0x3F, 0x00, 0xFF, 0xBF,
        0x7F, 0xFF, 0x9F, 0xFF, 0xBF,
        0xFF, 0xFF, 0x00, 0x00, 0xBF,
        0x00, 0x00, 0x70,
    ];

    private readonly byte[] _regs = new byte[ReadMask.Length];
    private readonly byte[] _waveRam = new byte[16];
    private bool _powered;

    public void WriteRegister(ushort address, byte value)
    {
        if (address >= WaveRamStart && address <= WaveRamEnd)
        {
            _waveRam[address - WaveRamStart] = value;
            return;
        }

        if (address < Nr10 || address > Nr52)
            return;

        if (address == Nr52)
        {
            var nowOn = (value & 0x80) != 0;
            if (!nowOn && _powered)
                Array.Clear(_regs);
            _powered = nowOn;
            _regs[Nr52 - Nr10] = (byte)(nowOn ? 0x80 : 0x00);
            return;
        }

        if (!_powered)
            return;

        _regs[address - Nr10] = value;
    }

    public byte ReadRegister(ushort address)
    {
        if (address >= WaveRamStart && address <= WaveRamEnd)
            return _waveRam[address - WaveRamStart];

        if (address < Nr10 || address > Nr52)
            return 0xFF;

        var idx = address - Nr10;
        return (byte)(_regs[idx] | ReadMask[idx]);
    }

    public void Step(int tStates) { }
    public void OnFrameSequencerTick() { }
    public int DrainAudio(Span<float> dest) => 0;
}
