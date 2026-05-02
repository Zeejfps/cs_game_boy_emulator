using GameBoyEmulator.Core;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public abstract class CpuTestBase
{
    protected readonly FakeMmu Mmu = new();
    protected readonly CountingBusClock BusClock = new();
    protected readonly Cpu Cpu;

    protected CpuTestBase()
    {
        Cpu = new Cpu(Mmu, BusClock, Mmu.Interrupts);
    }

    protected int StepCycles()
    {
        Cpu.Step();
        return (int)BusClock.ConsumeAccumulated();
    }
}

public sealed class CountingBusClock : IBusClock
{
    private long _accumulated;

    public void Tick(int ticks) => _accumulated += ticks;

    public long ConsumeAccumulated()
    {
        var c = _accumulated;
        _accumulated = 0;
        return c;
    }
}
