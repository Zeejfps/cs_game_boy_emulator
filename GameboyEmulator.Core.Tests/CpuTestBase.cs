using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public abstract class CpuTestBase
{
    protected readonly FakeMmu Mmu = new();
    protected readonly Cpu Cpu;

    protected CpuTestBase()
    {
        Cpu = new Cpu(Mmu);
    }
}
