using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public abstract class CpuTestBase
{
    protected readonly FakeMmu Mmu = new();
    protected readonly Cpu Cpu;

    protected CpuTestBase()
    {
        Cpu = new Cpu(Mmu, new NoOpCpuIO());
    }

    protected Cpu CreateCpu(CpuState state, IIOBus io) => new Cpu(Mmu, io).WriteState(state);
}
