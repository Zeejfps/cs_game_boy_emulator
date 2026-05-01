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
    private readonly MbcFactory _mbcFactory;
    private readonly double _cyclesPerTick;

    private long _lastTimestamp;
    private double _tCycles;

    public GameBoy(IClock clock, IBatteryStore batteryStore)
    {
        _clock = clock;
        _mbcFactory = new MbcFactory(batteryStore);

        var interrupts = new Interrupts();
        _ppu = new Ppu(interrupts);
        var mbc = new NoCartridgeMbc();
        var joypad = new Joypad(interrupts);
        var timer = new Timer(interrupts);
        var apu = new Apu();
        var serial = new Serial(interrupts);
        var mmu = new Mmu(mbc, _ppu, joypad, timer, apu, serial, interrupts);

        _timer = timer;
        _joypad = joypad;
        _mmu = mmu;
        _cpu = new Cpu(mmu, interrupts);

        _cyclesPerTick = CpuFrequency / (double)_clock.Frequency;
    }

    public void LoadRom(byte[] rom)
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("Cannot load a ROM while the GameBoy is powered on");

        var mbc = _mbcFactory.Create(rom);
        _mmu.SetMbc(mbc);
    }

    public void PowerOn()
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("GameBoy is already powered on");
        
        IsPoweredOn = true;
        _cpu.SkipBoot();
        _lastTimestamp = _clock.GetTimestamp();
        _clock.Ticked += Clock_OnTicked;
    }

    public void PowerOff()
    {
        if (!IsPoweredOn)
            return;

        _mmu.FlushMbc();
        _mmu.Reset();
        _timer.Reset();
        _joypad.Reset();
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
            var ts = _cpu.Step();
            _ppu.Step(ts);
            _timer.Tick(ts);
            _tCycles -= ts;
        }
    }
}