using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class GameBoy
{
    public bool IsPoweredOn { get; private set; }
    
    private const int CpuFrequency = 4_194_304;
    
    private readonly IClock _clock;
    
    private readonly ICpu _cpu;
    
    private long _lastTimestamp;
    private double _cycleCount;
    private double _cyclesPerTick;
    
    public GameBoy(IClock clock)
    {
        _clock = clock;
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
        
        _cycleCount += elapsedTime * _cyclesPerTick;
        while (_cycleCount > 0)
        {
            var cycles = _cpu.Step();
            _cycleCount -= cycles;
        }
    }
}