using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Core;

public sealed class BusClock : IBusClock
{
    private readonly Ppu _ppu;
    private readonly Timer _timer;
    private readonly OamDmaController _dma;
    private long _accumulated;

    public BusClock(Ppu ppu, Timer timer, OamDmaController dma)
    {
        _ppu = ppu;
        _timer = timer;
        _dma = dma;
    }

    public void Tick(int ticks)
    {
        _ppu.Step(ticks);
        _timer.Tick(ticks);
        _dma.Tick(ticks);
        _accumulated += ticks;
    }

    public long ConsumeAccumulated()
    {
        var c = _accumulated;
        _accumulated = 0;
        return c;
    }
}
