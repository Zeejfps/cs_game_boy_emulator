using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class CpuBranchTests : CpuTestBase
{
    // The LR35902 only branches on Z and C. The 8080 sign/parity-condition opcodes
    // (RP/RM/RPE/RPO, JP/JM/JPE/JPO, CP/CM/CPE/CPO) do not exist on the LR35902 —
    // those opcode slots are repurposed (e.g., LDH, LD A,(C), interrupt control).

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
        var cycles = StepCycles();

        var expectedState = initialState;
        if (taken)
        {
            expectedState.Pc = 0x2030;
            expectedState.Sp = (ushort)(stackAddr + 2);
            Assert.Equal(20, cycles);
        }
        else
        {
            expectedState.IncrementPcBy(1);
            Assert.Equal(8, cycles);
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
        var cycles = StepCycles();

        var expectedState = initialState;
        if (taken)
        {
            expectedState.Pc = 0x2030;
            Assert.Equal(16, cycles);
        }
        else
        {
            expectedState.IncrementPcBy(3);
            Assert.Equal(12, cycles);
        }

        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestJp()
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
        var cycles = StepCycles();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;

        Assert.Equal(16, cycles);
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
        var cycles = StepCycles();

        var expectedState = initialState;
        if (taken)
        {
            expectedState.Pc = 0x2030;
            expectedState.Sp = (ushort)(stackAddr - 2);
            Assert.Equal(24, cycles);
        }
        else
        {
            expectedState.IncrementPcBy(3);
            Assert.Equal(12, cycles);
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
        var cycles = StepCycles();

        var expectedState = initialState;
        expectedState.Pc = target;
        expectedState.Sp = (ushort)(stackAddr - 2);

        Assert.Equal(16, cycles);
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
        var cycles = StepCycles();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;
        expectedState.Sp = (ushort)(stackAddr + 2);

        Assert.Equal(16, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestJpHl()
    {
        var initialState = new CpuState { Pc = 0x10 };
        initialState.WriteRegPair(Reg.H, 0x2030);

        Mmu.Write(initialState.Pc, 0xE9);

        Cpu.WriteState(initialState);
        var cycles = StepCycles();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData((sbyte)0x10)]
    [InlineData((sbyte)-4)]
    public void TestJr(sbyte offset)
    {
        ushort start = 0x0100;
        var initialState = new CpuState { Pc = start, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0x18);
        Mmu.Write((ushort)(initialState.Pc + 1), (byte)offset);

        Cpu.WriteState(initialState);
        var cycles = StepCycles();

        var expectedState = initialState;
        // PC has advanced past the operand, then offset is applied.
        expectedState.Pc = (ushort)(start + 2 + offset);

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x20, CpuFlags.None, true)]   // JR NZ taken
    [InlineData(0x20, CpuFlags.All,  false)]  // JR NZ not taken
    [InlineData(0x28, CpuFlags.All,  true)]   // JR Z taken
    [InlineData(0x28, CpuFlags.None, false)]  // JR Z not taken
    [InlineData(0x30, CpuFlags.Z,    true)]   // JR NC taken (Z set, C clear)
    [InlineData(0x30, CpuFlags.All,  false)]  // JR NC not taken
    [InlineData(0x38, CpuFlags.All,  true)]   // JR C taken
    [InlineData(0x38, CpuFlags.Z,    false)]  // JR C not taken
    public void TestJrConditional(byte opcode, CpuFlags flags, bool taken)
    {
        ushort start = 0x0100;
        sbyte offset = 0x10;
        var initialState = new CpuState { Pc = start, Flags = flags };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write((ushort)(initialState.Pc + 1), (byte)offset);

        Cpu.WriteState(initialState);
        var cycles = StepCycles();

        var expectedState = initialState;
        if (taken)
        {
            expectedState.Pc = (ushort)(start + 2 + offset);
            Assert.Equal(12, cycles);
        }
        else
        {
            // Operand is consumed even when not taken.
            expectedState.Pc = (ushort)(start + 2);
            Assert.Equal(8, cycles);
        }

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
        var cycles = StepCycles();

        var expectedState = initialState;
        expectedState.Pc = 0x2030;
        expectedState.Sp = (ushort)(stackAddr - 2);

        Assert.Equal(24, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(0x13, Mmu.Read((ushort)(stackAddr - 2)));
        Assert.Equal(0x00, Mmu.Read((ushort)(stackAddr - 1)));
    }

    // Regression guard for the pre-step-3.1 bug where conditional JP NZ/Z/NC/C returned
    // the same cycle count regardless of taken-vs-not-taken. LR35902 returns 16 taken,
    // 12 not-taken — these must differ.
    [Theory]
    [InlineData(0xC2, CpuFlags.None, CpuFlags.All)]   // JNZ taken when Z=0, not-taken when Z=1
    [InlineData(0xCA, CpuFlags.All,  CpuFlags.None)]  // JZ
    [InlineData(0xD2, CpuFlags.Z,    CpuFlags.All)]   // JNC taken when C=0, not-taken when C=1
    [InlineData(0xDA, CpuFlags.All,  CpuFlags.Z)]     // JC
    public void ConditionalJumpCyclesDifferOnTakenVsNotTaken(byte opcode, CpuFlags takenFlags, CpuFlags notTakenFlags)
    {
        Mmu.Write(0x10, opcode);
        Mmu.Write(0x11, 0x30);
        Mmu.Write(0x12, 0x20);

        Cpu.WriteState(new CpuState { Pc = 0x10, Flags = takenFlags });
        var takenCycles = StepCycles();

        Cpu.WriteState(new CpuState { Pc = 0x10, Flags = notTakenFlags });
        var notTakenCycles = StepCycles();

        Assert.Equal(16, takenCycles);
        Assert.Equal(12, notTakenCycles);
    }
}
