using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public class CpuBranchTests : CpuTestBase
{
    // 8080 sign/parity-condition tests (RP/RM/RPE/RPO, JP/JM/JPE/JPO, CP/CM/CPE/CPO)
    // are intentionally dropped — those opcodes are repurposed in step 3 and currently
    // throw NotImplementedException.

    [Theory]
    [InlineData(0xC0, CpuFlags.None,    true)]   // RNZ taken (Z=0)
    [InlineData(0xC0, CpuFlags.All,     false)]  // RNZ not taken
    [InlineData(0xD0, CpuFlags.Z,       true)]   // RNC taken (C=0)
    [InlineData(0xD0, CpuFlags.All,     false)]  // RNC not taken
    [InlineData(0xC8, CpuFlags.All,     true)]   // RZ taken
    [InlineData(0xC8, CpuFlags.None,    false)]  // RZ not taken
    [InlineData(0xD8, CpuFlags.All,     true)]   // RC taken
    [InlineData(0xD8, CpuFlags.Z,       false)]  // RC not taken
    public void TestConditionalReturn(byte opcode, CpuFlags flags, bool taken)
    {
        ushort stackAddr = 0x2002;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Sp = stackAddr,
            Flags = flags
        };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write(stackAddr, 0x30);
        Mmu.Write((ushort)(stackAddr + 1), 0x20);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        if (taken)
        {
            expectedState.Pc = 0x2030;
            expectedState.Sp = (ushort)(stackAddr + 2);
            Assert.Equal(11, cycles);
        }
        else
        {
            expectedState.IncrementPcBy(1);
            Assert.Equal(5, cycles);
        }

        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xC2, CpuFlags.None,    true)]   // JNZ taken
    [InlineData(0xC2, CpuFlags.All,     false)]  // JNZ not taken
    [InlineData(0xD2, CpuFlags.Z,       true)]   // JNC taken
    [InlineData(0xD2, CpuFlags.All,     false)]  // JNC not taken
    [InlineData(0xCA, CpuFlags.All,     true)]   // JZ taken
    [InlineData(0xCA, CpuFlags.None,    false)]  // JZ not taken
    [InlineData(0xDA, CpuFlags.All,     true)]   // JC taken
    [InlineData(0xDA, CpuFlags.Z,       false)]  // JC not taken
    public void TestConditionalJump(byte opcode, CpuFlags flags, bool taken)
    {
        var initialState = new CpuState
        {
            Pc = 0x10,
            Flags = flags
        };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write((ushort)(initialState.Pc + 1), 0x30);
        Mmu.Write((ushort)(initialState.Pc + 2), 0x20);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        if (taken)
            expectedState.Pc = 0x2030;
        else
            expectedState.IncrementPcBy(3);

        Assert.Equal(10, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestJmp()
    {
        byte opcode = 0xC3;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write((ushort)(initialState.Pc + 1), 0x30);
        Mmu.Write((ushort)(initialState.Pc + 2), 0x20);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;

        Assert.Equal(10, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xC4, CpuFlags.None,    true)]   // CNZ taken
    [InlineData(0xC4, CpuFlags.All,     false)]  // CNZ not taken
    [InlineData(0xD4, CpuFlags.Z,       true)]   // CNC taken
    [InlineData(0xD4, CpuFlags.All,     false)]  // CNC not taken
    [InlineData(0xCC, CpuFlags.All,     true)]   // CZ taken
    [InlineData(0xCC, CpuFlags.None,    false)]  // CZ not taken
    [InlineData(0xDC, CpuFlags.All,     true)]   // CC taken
    [InlineData(0xDC, CpuFlags.Z,       false)]  // CC not taken
    public void TestConditionalCall(byte opcode, CpuFlags flags, bool taken)
    {
        ushort stackAddr = 0x2002;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Sp = stackAddr,
            Flags = flags
        };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write((ushort)(initialState.Pc + 1), 0x30);
        Mmu.Write((ushort)(initialState.Pc + 2), 0x20);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        if (taken)
        {
            expectedState.Pc = 0x2030;
            expectedState.Sp = (ushort)(stackAddr - 2);
            Assert.Equal(17, cycles);
        }
        else
        {
            expectedState.IncrementPcBy(3);
            Assert.Equal(11, cycles);
        }

        Assert.Equal(expectedState, Cpu.ReadState());
        if (taken)
        {
            Assert.Equal(0x13, Mmu.Read((ushort)(stackAddr - 2)));
            Assert.Equal(0x00, Mmu.Read((ushort)(stackAddr - 1)));
        }
    }

    [Theory]
    [InlineData(0xC7, 0x0000)]
    [InlineData(0xCF, 0x0008)]
    [InlineData(0xD7, 0x0010)]
    [InlineData(0xDF, 0x0018)]
    [InlineData(0xE7, 0x0020)]
    [InlineData(0xEF, 0x0028)]
    [InlineData(0xF7, 0x0030)]
    [InlineData(0xFF, 0x0038)]
    public void TestRst(byte opcode, ushort target)
    {
        ushort stackAddr = 0x2002;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Sp = stackAddr,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Pc = target;
        expectedState.Sp = (ushort)(stackAddr - 2);

        Assert.Equal(11, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(0x11, Mmu.Read((ushort)(stackAddr - 2)));
        Assert.Equal(0x00, Mmu.Read((ushort)(stackAddr - 1)));
    }

    [Fact]
    public void TestRet()
    {
        ushort stackAddr = 0x2002;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Sp = stackAddr,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, 0xC9);
        Mmu.Write(stackAddr, 0x30);
        Mmu.Write((ushort)(stackAddr + 1), 0x20);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;
        expectedState.Sp = (ushort)(stackAddr + 2);

        Assert.Equal(10, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestPchl()
    {
        var initialState = new CpuState { Pc = 0x10 };
        initialState.WriteRegPair(Reg.H, 0x2030);

        Mmu.Write(initialState.Pc, 0xE9);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;

        Assert.Equal(5, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestCall()
    {
        ushort stackAddr = 0x2002;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Sp = stackAddr,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, 0xCD);
        Mmu.Write((ushort)(initialState.Pc + 1), 0x30);
        Mmu.Write((ushort)(initialState.Pc + 2), 0x20);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;
        expectedState.Sp = (ushort)(stackAddr - 2);

        Assert.Equal(17, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(0x13, Mmu.Read((ushort)(stackAddr - 2)));
        Assert.Equal(0x00, Mmu.Read((ushort)(stackAddr - 1)));
    }
}
