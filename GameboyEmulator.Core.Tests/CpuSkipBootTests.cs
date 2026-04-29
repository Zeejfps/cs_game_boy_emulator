using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public class CpuSkipBootTests : CpuTestBase
{
    [Fact]
    public void CpuSkipBootMatchesDmg()
    {
        Cpu.SkipBoot();

        Assert.Equal(0x0100, Cpu.Pc);
        Assert.Equal(0xFFFE, Cpu.Sp);
        Assert.Equal(0x01, Cpu.Ra);
        Assert.Equal(CpuFlags.Z | CpuFlags.H | CpuFlags.C, Cpu.Flags);
        Assert.Equal(0x00, Cpu.Rb);
        Assert.Equal(0x13, Cpu.Rc);
        Assert.Equal(0x00, Cpu.Rd);
        Assert.Equal(0xD8, Cpu.Re);
        Assert.Equal(0x01, Cpu.Rh);
        Assert.Equal(0x4D, Cpu.Rl);
        Assert.False(Cpu.InterruptMasterEnable);
        Assert.False(Cpu.IsWaitingForInterrupt);
        Assert.False(Cpu.IsSleeping);
    }
}
