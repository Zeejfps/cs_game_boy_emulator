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
    private readonly double _cyclesPerTick;

    private long _lastTimestamp;
    private double _tCycles;

    public GameBoy(IClock clock)
    {
        _clock = clock;

        var interrupts = new Interrupts();
        _ppu = new Ppu(interrupts);
        var mbc = new Mbc();
        var joypad = new Joypad();
        var timer = new Timer(interrupts);
        var apu = new Apu();
        var serial = new Serial(interrupts);
        var mmu = new Mmu(mbc, _ppu, joypad, timer, apu, serial, interrupts);
        
        _timer = timer;
        _mmu = mmu;
        _cpu = new Cpu(mmu, interrupts);

        _cyclesPerTick = CpuFrequency / (double)_clock.Frequency;
    }

    public void PowerOn()
    {
        if (IsPoweredOn)
            throw new InvalidOperationException("GameBoy is already powered on");
        
        IsPoweredOn = true;
        _lastTimestamp = _clock.GetTimestamp();
        _clock.Ticked += Clock_OnTicked;
    }

    public void PowerOff()
    {
        if (!IsPoweredOn)
            return;

        _mmu.Reset();
        _timer.Reset();
        _cpu.Reset();
        _clock.Ticked -= Clock_OnTicked;
        IsPoweredOn = false;
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
            var ts = _cpu.Step();
            _ppu.Step(ts);
            _timer.Tick(ts);
            _tCycles -= ts;
        }
    }
}