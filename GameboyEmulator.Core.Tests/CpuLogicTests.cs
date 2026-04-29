using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public class CpuLogicTests : CpuTestBase
{
    [Theory]
    [InlineData(0xA0, Reg.B, 0x15, 0x15)] // ANA B
    [InlineData(0xA1, Reg.C, 0x15, 0x15)] // ANA C
    [InlineData(0xA2, Reg.D, 0x15, 0x15)] // ANA D
    [InlineData(0xA3, Reg.E, 0x15, 0x15)] // ANA E
    [InlineData(0xA4, Reg.H, 0x15, 0x15)] // ANA H
    [InlineData(0xA5, Reg.L, 0x15, 0x15)] // ANA L
    [InlineData(0xA7, Reg.A, 0x15, 0x15)] // ANA A
    public void TestAnaRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x15 };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = CpuFlags.H; // AND on LR35902: Z, N=0, H=1, C=0
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestAnaM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x15 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0xA6); // ANA M
        Mmu.Write(addr, 0x15);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x15;
        expectedState.Flags = CpuFlags.H;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x11, 0x01, 0x01, CpuFlags.H)]                      // result non-zero
    [InlineData(0xF0, 0x0F, 0x00, CpuFlags.Z | CpuFlags.H)]         // zero
    [InlineData(0xFF, 0x80, 0x80, CpuFlags.H)]
    [InlineData(0x0F, 0x0F, 0x0F, CpuFlags.H)]
    [InlineData(0x08, 0x08, 0x08, CpuFlags.H)]
    public void TestAnaFlags(byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b };

        Mmu.Write(0x00, 0xA0); // ANA B

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedResult;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xA8, Reg.B, 0x07, 0x08)] // XRA B
    [InlineData(0xA9, Reg.C, 0x07, 0x08)] // XRA C
    [InlineData(0xAA, Reg.D, 0x07, 0x08)] // XRA D
    [InlineData(0xAB, Reg.E, 0x07, 0x08)] // XRA E
    [InlineData(0xAC, Reg.H, 0x07, 0x08)] // XRA H
    [InlineData(0xAD, Reg.L, 0x07, 0x08)] // XRA L
    public void TestXraRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x0F };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = CpuFlags.None; // XOR: Z based on result, N=0, H=0, C=0
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestXraA()
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };

        Mmu.Write(0x00, 0xAF); // XRA A

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x00;
        expectedState.Flags = CpuFlags.Z;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestXraM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x0F };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0xAE); // XRA M
        Mmu.Write(addr, 0x07);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x08;
        expectedState.Flags = CpuFlags.None;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x0F, 0x07, 0x08, CpuFlags.None)]
    [InlineData(0xFF, 0xFF, 0x00, CpuFlags.Z)]
    [InlineData(0xFF, 0x7F, 0x80, CpuFlags.None)]
    [InlineData(0x3C, 0x0F, 0x33, CpuFlags.None)]
    [InlineData(0x00, 0xFF, 0xFF, CpuFlags.None)]
    public void TestXraFlags(byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b };

        Mmu.Write(0x00, 0xA8); // XRA B

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedResult;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB0, Reg.B, 0x10, 0x10)] // ORA B
    [InlineData(0xB1, Reg.C, 0x10, 0x10)] // ORA C
    [InlineData(0xB2, Reg.D, 0x10, 0x10)] // ORA D
    [InlineData(0xB3, Reg.E, 0x10, 0x10)] // ORA E
    [InlineData(0xB4, Reg.H, 0x10, 0x10)] // ORA H
    [InlineData(0xB5, Reg.L, 0x10, 0x10)] // ORA L
    [InlineData(0xB7, Reg.A, 0x10, 0x10)] // ORA A
    public void TestOraRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = CpuFlags.None;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestOraM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0xB6); // ORA M
        Mmu.Write(addr, 0x10);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x10;
        expectedState.Flags = CpuFlags.None;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x10, 0x10, 0x10, CpuFlags.None)]
    [InlineData(0xF0, 0x0F, 0xFF, CpuFlags.None)]
    [InlineData(0x00, 0x00, 0x00, CpuFlags.Z)]
    [InlineData(0x80, 0x80, 0x80, CpuFlags.None)]
    [InlineData(0x01, 0x02, 0x03, CpuFlags.None)]
    public void TestOraFlags(byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b };

        Mmu.Write(0x00, 0xB0); // ORA B

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedResult;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB8, Reg.B, 0x01)] // CMP B
    [InlineData(0xB9, Reg.C, 0x01)] // CMP C
    [InlineData(0xBA, Reg.D, 0x01)] // CMP D
    [InlineData(0xBB, Reg.E, 0x01)] // CMP E
    [InlineData(0xBC, Reg.H, 0x01)] // CMP H
    [InlineData(0xBD, Reg.L, 0x01)] // CMP L
    public void TestCmpRegister(byte opcode, Reg srcReg, byte srcVal)
    {
        // 0x11 - 0x01 = 0x10, no half-borrow, no borrow, non-zero, N=1.
        var initialState = new CpuState { Pc = 0x00, Ra = 0x11 };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestCmpA()
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };

        Mmu.Write(0x00, 0xBF); // CMP A

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = CpuFlags.Z | CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestCmpM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x11 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0xBE); // CMP M
        Mmu.Write(addr, 0x01);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x11, 0x01, CpuFlags.N)]
    [InlineData(0x10, 0x05, CpuFlags.N | CpuFlags.H)]
    [InlineData(0x10, 0x10, CpuFlags.Z | CpuFlags.N)]
    [InlineData(0x05, 0x10, CpuFlags.N | CpuFlags.C)]
    [InlineData(0x00, 0x01, CpuFlags.N | CpuFlags.H | CpuFlags.C)]
    public void TestCmpFlags(byte a, byte b, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b };

        Mmu.Write(0x00, 0xB8); // CMP B

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xFF, 0xFF, 0x00, CpuFlags.Z)]
    [InlineData(0xFF, 0x0F, 0xF0, CpuFlags.None)]
    [InlineData(0x80, 0x00, 0x80, CpuFlags.None)]
    [InlineData(0x01, 0x02, 0x03, CpuFlags.None)]
    public void TestXri(byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a };

        Mmu.Write(0x00, 0xEE); // XRI
        Mmu.Write(0x01, imm);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(2);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x11, 0x01, CpuFlags.N)]
    [InlineData(0x10, 0x05, CpuFlags.N | CpuFlags.H)]
    [InlineData(0x10, 0x10, CpuFlags.Z | CpuFlags.N)]
    [InlineData(0x05, 0x10, CpuFlags.N | CpuFlags.C)]
    [InlineData(0x00, 0x01, CpuFlags.N | CpuFlags.H | CpuFlags.C)]
    public void TestCpi(byte a, byte imm, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a };

        Mmu.Write(0x00, 0xFE); // CPI
        Mmu.Write(0x01, imm);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(2);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xFF, 0x0F, 0x0F, CpuFlags.H)]
    [InlineData(0xFF, 0xF0, 0xF0, CpuFlags.H)]
    [InlineData(0xF0, 0x00, 0x00, CpuFlags.Z | CpuFlags.H)]
    [InlineData(0x0F, 0x08, 0x08, CpuFlags.H)]
    public void TestAni(byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a };

        Mmu.Write(0x00, 0xE6); // ANI
        Mmu.Write(0x01, imm);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(2);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x10, 0x05, 0x15, CpuFlags.None)]
    [InlineData(0xC0, 0x00, 0xC0, CpuFlags.None)]
    [InlineData(0x00, 0x00, 0x00, CpuFlags.Z)]
    [InlineData(0x01, 0x02, 0x03, CpuFlags.None)]
    public void TestOri(byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a };

        Mmu.Write(0x00, 0xF6); // ORI
        Mmu.Write(0x01, imm);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(2);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }
}
