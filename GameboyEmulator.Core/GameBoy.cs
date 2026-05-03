using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class GameBoy
{
    public ReadOnlyMemory<byte> FrameBuffer => _ppu.FrameBuffer;
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
    private readonly MbcFactory _mbcFactory;
    private readonly double _cyclesPerTick;

    private long _lastTimestamp;
    private double _tCycles;

    public GameBoy(IClock clock, IBatteryStore batteryStore)
        : this(clock, batteryStore, new SystemTimeProvider())
    {
    }

    public GameBoy(IClock clock, IBatteryStore batteryStore, ITimeProvider timeProvider)
    {
        _clock = clock;
        _mbcFactory = new MbcFactory(batteryStore, timeProvider);

        var interrupts = new Interrupts();
        _ppu = new Ppu(interrupts);
        var mbc = new NoCartridgeMbc();
        var joypad = new Joypad(interrupts);
        var timer = new Timer(interrupts);
        var apu = new Apu();
        var serial = new Serial(interrupts);
        var mmu = new Mmu(mbc, _ppu, joypad, timer, apu, serial, interrupts);
        _dma = new OamDmaController(mmu, _ppu);

        _timer = timer;
        _joypad = joypad;
        _mmu = mmu;
        _systemClock = new SystemClock(_ppu, timer, _dma);
        _cpu = new Cpu(_dma, _systemClock, interrupts);

        _cyclesPerTick = CpuFrequency / (double)_clock.Frequency;
    }

    public void LoadRom(byte[] rom)
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("Cannot load a ROM while the GameBoy is powered on");

        var mbc = _mbcFactory.Create(rom);
        _mmu.SetMbc(mbc);
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
        _cpu.Reset();
        _clock.Ticked -= Clock_OnTicked;
        IsPoweredOn = false;
    }

    public void SetButton(JoypadButton button, bool pressed) => _joypad.SetButton(button, pressed);

    public void FlushBatteryRam() => _mmu.FlushMbc();
    
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