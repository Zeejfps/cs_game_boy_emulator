using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class GameBoy
{
    public ReadOnlyMemory<uint> FrameBuffer => _ppu.RgbFrameBuffer;
    public event Action? FrameCompleted
    {
        add => _ppu.FrameCompleted += value;
        remove => _ppu.FrameCompleted -= value;
    }

    public bool IsPoweredOn { get; private set; }
    
    private const int CpuFrequency = 4_194_304;
    private const double MaxCatchUpCycles = CpuFrequency / 10.0;
    
    private readonly IClock _clock;
    private readonly Cpu _cpu;
    private readonly Ppu _ppu;
    private readonly Mmu _mmu;
    private readonly Timer _timer;
    private readonly Joypad _joypad;
    private readonly SystemClock _systemClock;
    private readonly OamDmaController _dma;
    private readonly HdmaController _hdma;
    private readonly MbcFactory _mbcFactory;
    private readonly double _cyclesPerTick;

    private long _lastTimestamp;
    private double _tCycles;

    private readonly Apu _apu;

    public GameBoy(IClock clock, IBatteryStore batteryStore)
        : this(clock, batteryStore, new SystemTimeProvider(), 48000)
    {
    }

    public GameBoy(IClock clock, IBatteryStore batteryStore, ITimeProvider timeProvider)
        : this(clock, batteryStore, timeProvider, 48000)
    {
    }

    public GameBoy(IClock clock, IBatteryStore batteryStore, ITimeProvider timeProvider, int audioSampleRate)
    {
        _clock = clock;
        _mbcFactory = new MbcFactory(batteryStore, timeProvider);

        var interrupts = new Interrupts();
        _ppu = new Ppu(interrupts);
        var mbc = new NoCartridgeMbc();
        var joypad = new Joypad(interrupts);
        var timer = new Timer(interrupts);
        var apu = new Apu(audioSampleRate);
        // DIV bit-12 falling edge clocks the APU frame sequencer at 512 Hz
        // (DMG); routing through this hook also captures the WriteDiv-resets-
        // counter glitch that some games use to phase-shift envelopes.
        timer.OnApuFrameSequencerTick = apu.OnFrameSequencerTick;
        var serial = new Serial(interrupts);
        var mmu = new Mmu(mbc, _ppu, joypad, timer, apu, serial, interrupts);
        _dma = new OamDmaController(mmu, _ppu);

        _timer = timer;
        _joypad = joypad;
        _mmu = mmu;
        _apu = apu;
        _systemClock = new SystemClock(_ppu, timer, _dma, apu);
        _cpu = new Cpu(_dma, _systemClock, interrupts);
        // KEY1 lives on the CPU but is reached via the MMU bus, and MMU is
        // built before CPU — wire it up now that both exist.
        _mmu.SetSpeedController(_cpu);

        _hdma = new HdmaController(_mmu, _ppu);
        _mmu.SetHdmaController(_hdma);
        _ppu.OnHBlankEntry = _hdma.OnHBlank;

        _cyclesPerTick = CpuFrequency / (double)_clock.Frequency;
    }

    // Drain stereo float frames produced since the last call. `dest` is
    // interleaved L,R; length must be even. Returns the number of frames
    // (each = 2 floats) actually written. Underruns return 0; the host is
    // responsible for buffering against jitter (e.g. an SAB ring buffer
    // feeding an AudioWorklet).
    public int DrainAudio(Span<float> dest) => _apu.DrainAudio(dest);

    public void LoadRom(byte[] rom)
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("Cannot load a ROM while the GameBoy is powered on");

        var mbc = _mbcFactory.Create(rom);
        _mmu.SetMbc(mbc);

        var isCgb = MbcFactory.IsCgbCartridge(rom);
        _mmu.SetCgbMode(isCgb);
        _ppu.SetCgbMode(isCgb);
        _apu.SetCgbMode(isCgb);
        _cpu.SetCgbMode(isCgb);
    }

    // Optional 256-byte DMG boot ROM. When set, PowerOn starts the CPU at
    // 0x0000 inside the boot ROM (which scrolls the Nintendo logo, verifies
    // the cart, and writes its own post-boot register state) instead of
    // skipping straight to the cart's entry point at 0x0100. Pass null to
    // clear and revert to the SkipBoot path.
    public void SetBootRom(byte[]? bootRom)
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("Cannot set boot ROM while the GameBoy is powered on");
        _mmu.SetBootRom(bootRom);
    }

    public void PowerOn()
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("GameBoy is already powered on");

        IsPoweredOn = true;
        if (_mmu.IsBootRomEnabled)
        {
            // Boot ROM will run from 0x0000 and set its own post-boot state,
            // so leave the CPU and I/O registers cold.
            _cpu.Reset();
        }
        else
        {
            _cpu.SkipBoot();
            SkipBootIo();
        }
        _lastTimestamp = _clock.GetTimestamp();
        _clock.Ticked += Clock_OnTicked;
    }

    // Apply the I/O register state the DMG boot ROM normally leaves behind.
    // Without this, LCDC stays 0 (LCD off) and BGP stays 0 (every BG color
    // maps to white) — games that don't initialize these themselves (Kirby's
    // Dream Land, many others) render a blank screen.
    private void SkipBootIo()
    {
        _mmu.Write(0xFF40, 0x91); // LCDC: LCD on, BG on, tile data 0x8000, tile map 0x9800
        _mmu.Write(0xFF47, 0xFC); // BGP
        _mmu.Write(0xFF48, 0xFF); // OBP0
        _mmu.Write(0xFF49, 0xFF); // OBP1
    }

    public void PowerOff()
    {
        if (!IsPoweredOn)
            return;

        _mmu.FlushMbc();
        _mmu.Reset();
        _timer.Reset();
        _joypad.Reset();
        _dma.Reset();
        _hdma.Reset();
        _cpu.Reset();
        _clock.Ticked -= Clock_OnTicked;
        IsPoweredOn = false;
    }

    public void SetButton(JoypadButton button, bool pressed) => _joypad.SetButton(button, pressed);

    public void FlushBatteryRam() => _mmu.FlushMbc();

    // One-shot CPU/PPU/interrupt snapshot for debugging in-game freezes.
    // Reading registers via the MMU goes through the full bus path, so it'll
    // observe whatever the game would observe — including PPU mode-restricted
    // returns. Memory reads do NOT advance the clock (they bypass the CPU's
    // ReadFromBus); they're a passive peek.
    public string GetDebugState()
    {
        var c = _cpu;
        var pc = c.Pc;
        var sp = c.Sp;
        var ie  = _mmu.Read(0xFFFF);
        var iflag = _mmu.Read(0xFF0F);
        var lcdc = _mmu.Read(0xFF40);
        var stat = _mmu.Read(0xFF41);
        var ly   = _mmu.Read(0xFF44);
        var lyc  = _mmu.Read(0xFF45);
        var scx  = _mmu.Read(0xFF43);
        var scy  = _mmu.Read(0xFF42);
        var wx   = _mmu.Read(0xFF4B);
        var wy   = _mmu.Read(0xFF4A);
        var div  = _mmu.Read(0xFF04);
        var tima = _mmu.Read(0xFF05);
        var tac  = _mmu.Read(0xFF07);
        var hl = (ushort)((c.Rh << 8) | c.Rl);

        var sb = new System.Text.StringBuilder();
        sb.Append($"PC={pc:X4} SP={sp:X4} ");
        sb.Append($"AF={c.Ra:X2}{(byte)c.Flags:X2} BC={c.Rb:X2}{c.Rc:X2} DE={c.Rd:X2}{c.Re:X2} HL={c.Rh:X2}{c.Rl:X2}\n");
        sb.Append($"IME={(c.InterruptMasterEnable?1:0)} IF={iflag:X2} IE={ie:X2} ");
        sb.Append($"halted={(c.IsWaitingForInterrupt?1:0)} stop={(c.IsSleeping?1:0)}\n");
        sb.Append($"LCDC={lcdc:X2} STAT={stat:X2} LY={ly:X2} LYC={lyc:X2} ");
        sb.Append($"SCX={scx:X2} SCY={scy:X2} WX={wx:X2} WY={wy:X2}\n");
        sb.Append($"DIV={div:X2} TIMA={tima:X2} TAC={tac:X2}\n");

        sb.Append($"bytes@PC:");
        for (var i = 0; i < 16; i++) sb.Append($" {_mmu.Read((ushort)(pc + i)):X2}");
        sb.Append('\n');

        sb.Append($"bytes@HL ({c.Rh:X2}{c.Rl:X2}):");
        for (var i = 0; i < 8; i++) sb.Append($" {_mmu.Read((ushort)(hl + i)):X2}");
        sb.Append('\n');

        sb.Append($"stack@SP:");
        for (var i = 0; i < 16; i += 2)
        {
            var lo = _mmu.Read((ushort)(sp + i));
            var hi = _mmu.Read((ushort)(sp + i + 1));
            sb.Append($" {hi:X2}{lo:X2}");
        }

        return sb.ToString();
    }
    
    private void Clock_OnTicked()
    {
        var timestamp = _clock.GetTimestamp();
        var elapsedTime = timestamp - _lastTimestamp;
        _lastTimestamp = timestamp;
        
        _tCycles += elapsedTime * _cyclesPerTick;
        if (_tCycles > MaxCatchUpCycles)
            _tCycles = MaxCatchUpCycles;

        while (_tCycles > 0)
        {
            _cpu.Step();
            _tCycles -= _systemClock.ConsumeAccumulated();
        }
    }
}