using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class GameBoy
{
    public bool IsPoweredOn { get; private set; }
    
    private const int CpuFrequency = 4_194_304;
    private const double CyclesPerFrame = CpuFrequency / 60.0;
    
    private readonly IClock _clock;
    private readonly ICpu _cpu;
    private readonly Ppu _ppu;
    private readonly double _cyclesPerTick;

    private long _lastTimestamp;
    private double _tCount;

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
        
        IsPoweredOn = false;
        _clock.Ticked -= Clock_OnTicked;
    }
    
    private void Clock_OnTicked()
    {
        var timestamp = _clock.GetTimestamp();
        var elapsedTime = timestamp - _lastTimestamp;
        _lastTimestamp = timestamp;
        
        _tCount += elapsedTime * _cyclesPerTick;
        while (_tCount > 0)
        {
            var ts = _cpu.Step();
            _ppu.Step(ts);
            _tCount -= ts;
        }
    }
}