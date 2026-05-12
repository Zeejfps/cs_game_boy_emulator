using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class SystemClock : ISystemClock
{
    private readonly Ppu _ppu;
    private readonly Timer _timer;
    private readonly OamDmaController _dma;
    private readonly IApu _apu;
    private long _accumulated;
    private bool _doubleSpeed;

    public SystemClock(Ppu ppu, Timer timer, OamDmaController dma, IApu apu)
    {
        _ppu = ppu;
        _timer = timer;
        _dma = dma;
        _apu = apu;
    }

    // CGB STOP toggles this through KEY1 (Phase 4). PPU + APU live in the
    // 4.19 MHz bus clock domain and only see half as many ticks per CPU
    // instruction; Timer and OAM DMA live in the CPU clock domain so they
    // run twice as fast in wall time, which is the correct behavior.
    public void SetDoubleSpeed(bool doubleSpeed) => _doubleSpeed = doubleSpeed;

    public bool IsDoubleSpeed => _doubleSpeed;

    public void Advance(int ticks)
    {
        // SM83 instructions are always multiples of 4 T-cycles, so ticks/2 is
        // exact and PPU/APU never lose sub-tick fractions.
        var busTicks = _doubleSpeed ? ticks >> 1 : ticks;

        // DMA before PPU so OAM bytes written this batch are visible to the
        // PPU's OAM scan during the same batch — otherwise the PPU sees the
        // pre-batch OAM state and freshly DMA'd sprites land one tick late,
        // which manifests as per-frame flicker on rapidly-rewritten OAM
        // entries (chains, shadows under jumping NPCs).
        _dma.Tick(ticks);
        _timer.Tick(ticks);
        _ppu.Step(busTicks);
        _apu.Step(busTicks);
        // _accumulated reports wall time (bus domain) to the host clock loop
        // so doubling the CPU rate doesn't double the wall-clock progress.
        _accumulated += busTicks;
    }

    public long ConsumeAccumulated()
    {
        var c = _accumulated;
        _accumulated = 0;
        return c;
    }
}
