using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public class CpuIoTests : CpuTestBase
{
    [Fact]
    public void TestDi()
    {
        var initialState = new CpuState { Pc = 0x00 };
        Mmu.Write(0x00, 0xF3); // DI

        Cpu.WriteState(initialState);
        Cpu.InterruptEnabled = true;
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.False(Cpu.InterruptEnabled);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestDiIsIdempotentWhenAlreadyDisabled()
    {
        Mmu.Write(0x00, 0xF3); // DI

        Cpu.WriteState(new CpuState { Pc = 0x00 });
        Cpu.InterruptEnabled = false;
        Cpu.Step();

        Assert.False(Cpu.InterruptEnabled);
    }

    [Fact]
    public void TestEiDoesNotEnableImmediately()
    {
        Mmu.Write(0x00, 0xFB); // EI

        Cpu.WriteState(new CpuState { Pc = 0x00 });
        var cycles = Cpu.Step();

        Assert.Equal(4, cycles);
        Assert.False(Cpu.InterruptEnabled); // still disabled right after EI
    }

    [Fact]
    public void TestEiEnablesOnFollowingStep()
    {
        Mmu.Write(0x00, 0xFB); // EI
        Mmu.Write(0x01, 0x00); // NOP

        Cpu.WriteState(new CpuState { Pc = 0x00 });
        Cpu.Step(); // EI
        
        Assert.False(Cpu.InterruptEnabled);
        
        Cpu.Step(); // next instruction — interrupts enabled at start of this step

        Assert.True(Cpu.InterruptEnabled);
    }

    [Fact]
    public void TestInReturnsZeroWhenNoPortBusAttached()
    {
        Mmu.Write(0x00, 0xDB); // IN
        Mmu.Write(0x01, 0x01);

        Cpu.WriteState(new CpuState { Pc = 0x00 });
        Cpu.Step();

        Assert.Equal(0x00, Cpu.Ra);
    }
    
    [Fact]
    public void TestOutDoesNotModifyRegistersOrFlags()
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x77, Flags = CpuFlags.C | CpuFlags.Z };
        Mmu.Write(0x00, 0xD3); // OUT
        Mmu.Write(0x01, 0x00);

        var cpu = CreateCpu(initialState, new NoOpCpuIO());
        cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(2);

        Assert.Equal(expectedState, cpu.ReadState());
    }
}

class StubCpuIO(byte readValue, byte expectedPort) : IIOBus
{
    public byte ReadPort(byte port) => port == expectedPort ? readValue : (byte)0;
    public void WritePort(byte port, byte value) { }
}