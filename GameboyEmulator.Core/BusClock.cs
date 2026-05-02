using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Core;

public sealed class BusClock : IBusClock
{
    private readonly Ppu _ppu;
    private readonly Timer _timer;
    private long _accumulated;

    public BusClock(Ppu ppu, Timer timer)
    {
        _ppu = ppu;
        _timer = timer;
    }

    public void Tick(int ticks)
    {
        _ppu.Step(ticks);
        _timer.Tick(ticks);
        _accumulated += ticks;
    }

    public long ConsumeAccumulated()
    {
        var c = _accumulated;
        _accumulated = 0;
        return c;
    }
}
