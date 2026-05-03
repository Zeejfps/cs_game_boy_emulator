using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class SystemClock : ISystemClock
{
    private readonly Ppu _ppu;
    private readonly Timer _timer;
    private readonly OamDmaController _dma;
    private long _accumulated;

    public SystemClock(Ppu ppu, Timer timer, OamDmaController dma)
    {
        _ppu = ppu;
        _timer = timer;
        _dma = dma;
    }

    public void Advance(int ticks)
    {
        // DMA before PPU so OAM bytes written this batch are visible to the
        // PPU's OAM scan during the same batch — otherwise the PPU sees the
        // pre-batch OAM state and freshly DMA'd sprites land one tick late,
        // which manifests as per-frame flicker on rapidly-rewritten OAM
        // entries (chains, shadows under jumping NPCs).
        _dma.Tick(ticks);
        _timer.Tick(ticks);
        _ppu.Step(ticks);
        _accumulated += ticks;
    }

    public long ConsumeAccumulated()
    {
        var c = _accumulated;
        _accumulated = 0;
        return c;
    }
}
