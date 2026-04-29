using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public class CpuArithmeticTests : CpuTestBase
{
    [Theory]
    [InlineData(0x80, Reg.B, 0x05, 0x15)] // ADD B
    [InlineData(0x81, Reg.C, 0x05, 0x15)] // ADD C
    [InlineData(0x82, Reg.D, 0x05, 0x15)] // ADD D
    [InlineData(0x83, Reg.E, 0x05, 0x15)] // ADD E
    [InlineData(0x84, Reg.H, 0x05, 0x15)] // ADD H
    [InlineData(0x85, Reg.L, 0x05, 0x15)] // ADD L
    [InlineData(0x87, Reg.A, 0x10, 0x20)] // ADD A
    public void TestAddRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestAddM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0x86);
        Mmu.Write(addr, 0x05);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x15;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x01, 0x01, 0x02, CpuFlags.None)]                    // no flags
    [InlineData(0xFF, 0x01, 0x00, CpuFlags.Z | CpuFlags.H | CpuFlags.C)] // carry to zero, half-carry
    [InlineData(0x7F, 0x01, 0x80, CpuFlags.H)]                       // half-carry only
    [InlineData(0x01, 0x02, 0x03, CpuFlags.None)]
    [InlineData(0xF0, 0x10, 0x00, CpuFlags.Z | CpuFlags.C)]          // carry+zero, no half-carry
    [InlineData(0x70, 0x10, 0x80, CpuFlags.None)]
    [InlineData(0x08, 0x08, 0x10, CpuFlags.H)]                       // half-carry only
    public void TestAddFlags(byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b };

        Mmu.Write(0x00, 0x80); // ADD B

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
    [InlineData(0x88, Reg.B, 0x05, 0x16, CpuFlags.None)] // ADC B
    [InlineData(0x89, Reg.C, 0x05, 0x16, CpuFlags.None)] // ADC C
    [InlineData(0x8A, Reg.D, 0x05, 0x16, CpuFlags.None)] // ADC D
    [InlineData(0x8B, Reg.E, 0x05, 0x16, CpuFlags.None)] // ADC E
    [InlineData(0x8C, Reg.H, 0x05, 0x16, CpuFlags.None)] // ADC H
    [InlineData(0x8D, Reg.L, 0x05, 0x16, CpuFlags.None)] // ADC L
    [InlineData(0x8F, Reg.A, 0x10, 0x21, CpuFlags.None)] // ADC A: 0x10 + 0x10 + 1 = 0x21
    public void TestAdcRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10, Flags = CpuFlags.C };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestAdcM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10, Flags = CpuFlags.C };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0x8E);
        Mmu.Write(addr, 0x05);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x16;
        expectedState.Flags = CpuFlags.None;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(CpuFlags.None, 0x10, 0x05, 0x15, CpuFlags.None)]                                  // carry=0: no effect
    [InlineData(CpuFlags.C,    0x10, 0x05, 0x16, CpuFlags.None)]                                  // carry=1: adds 1
    [InlineData(CpuFlags.C,    0xFF, 0x00, 0x00, CpuFlags.Z | CpuFlags.H | CpuFlags.C)]           // carry causes overflow + half-carry
    [InlineData(CpuFlags.None, 0xFF, 0x00, 0xFF, CpuFlags.None)]                                  // no carry, no overflow
    public void TestAdcFlags(CpuFlags initialFlags, byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b, Flags = initialFlags };

        Mmu.Write(0x00, 0x88); // ADC B

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
    [InlineData(0x98, Reg.B, 0x10, 0x10, CpuFlags.N)] // SBB B
    [InlineData(0x99, Reg.C, 0x10, 0x10, CpuFlags.N)] // SBB C
    [InlineData(0x9A, Reg.D, 0x10, 0x10, CpuFlags.N)] // SBB D
    [InlineData(0x9B, Reg.E, 0x10, 0x10, CpuFlags.N)] // SBB E
    [InlineData(0x9C, Reg.H, 0x10, 0x10, CpuFlags.N)] // SBB H
    [InlineData(0x9D, Reg.L, 0x10, 0x10, CpuFlags.N)] // SBB L
    public void TestSbbRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA, CpuFlags expectedFlags)
    {
        // 0x21 - 0x10 - 1 = 0x10. Low nibble (0x1 - 0x0 - 1) = 0 → no half-borrow.
        var initialState = new CpuState { Pc = 0x00, Ra = 0x21, Flags = CpuFlags.C };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestSbbA()
    {
        // A=0x10, SBB A with C=1 → 0x10 - 0x10 - 1 = -1 = 0xFF, H=1, C=1, Z=0, N=1
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10, Flags = CpuFlags.C };

        Mmu.Write(0x00, 0x9F); // SBB A

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0xFF;
        expectedState.Flags = CpuFlags.N | CpuFlags.H | CpuFlags.C;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestSbbM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x21, Flags = CpuFlags.C };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0x9E); // SBB M
        Mmu.Write(addr, 0x10);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        // 0x21 - 0x10 - 1 = 0x10. Low nibble (0x1 - 0x0 - 1) = 0 → no half-borrow.
        var expectedState = initialState;
        expectedState.Ra = 0x10;
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(CpuFlags.None, 0x11, 0x01, 0x10, CpuFlags.N)]                                       // borrow=0: basic sub
    [InlineData(CpuFlags.C,    0x12, 0x01, 0x10, CpuFlags.N)]                                       // borrow=1: subtracts extra 1
    [InlineData(CpuFlags.C,    0x10, 0x00, 0x0F, CpuFlags.N | CpuFlags.H)]                          // borrow causes half-borrow
    [InlineData(CpuFlags.C,    0x00, 0x00, 0xFF, CpuFlags.N | CpuFlags.H | CpuFlags.C)]            // borrow causes underflow
    [InlineData(CpuFlags.None, 0x00, 0x01, 0xFF, CpuFlags.N | CpuFlags.H | CpuFlags.C)]            // underflow
    public void TestSbbFlags(CpuFlags initialFlags, byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b, Flags = initialFlags };

        Mmu.Write(0x00, 0x98); // SBB B

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
    [InlineData(0x90, Reg.B, 0x05, 0x10)] // SUB B
    [InlineData(0x91, Reg.C, 0x05, 0x10)] // SUB C
    [InlineData(0x92, Reg.D, 0x05, 0x10)] // SUB D
    [InlineData(0x93, Reg.E, 0x05, 0x10)] // SUB E
    [InlineData(0x94, Reg.H, 0x05, 0x10)] // SUB H
    [InlineData(0x95, Reg.L, 0x05, 0x10)] // SUB L
    public void TestSubRegister(byte opcode, Reg srcReg, byte srcVal, byte expectedA)
    {
        // 0x15 - 0x05 = 0x10. Half-nibble (0x5 - 0x5) = 0, no H.
        var initialState = new CpuState { Pc = 0x00, Ra = 0x15 };
        initialState.WriteReg(srcReg, srcVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestSubA()
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10 };

        Mmu.Write(0x00, 0x97); // SUB A

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x00;
        expectedState.Flags = CpuFlags.Z | CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestSubM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00, Ra = 0x15 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0x96); // SUB M
        Mmu.Write(addr, 0x05);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x10;
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x11, 0x01, 0x10, CpuFlags.N)]                                       // no borrow
    [InlineData(0x10, 0x05, 0x0B, CpuFlags.N | CpuFlags.H)]                          // half-borrow
    [InlineData(0x10, 0x10, 0x00, CpuFlags.Z | CpuFlags.N)]                          // zero
    [InlineData(0x05, 0x10, 0xF5, CpuFlags.N | CpuFlags.C)]                          // borrow
    [InlineData(0x00, 0x01, 0xFF, CpuFlags.N | CpuFlags.H | CpuFlags.C)]             // full borrow
    public void TestSubFlags(byte a, byte b, byte expectedResult, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Rb = b };

        Mmu.Write(0x00, 0x90); // SUB B

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
    [InlineData(0x10, 0x05, 0x15, CpuFlags.None)]                                  // basic add
    [InlineData(0xFF, 0x01, 0x00, CpuFlags.Z | CpuFlags.H | CpuFlags.C)]           // carry to zero
    [InlineData(0x7F, 0x01, 0x80, CpuFlags.H)]                                     // half-carry only
    [InlineData(0x08, 0x08, 0x10, CpuFlags.H)]                                     // half-carry only
    public void TestAdi(byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a };

        Mmu.Write(0x00, 0xC6); // ADI
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
    [InlineData(0x15, 0x05, 0x10, CpuFlags.N)]                                       // basic sub
    [InlineData(0x10, 0x05, 0x0B, CpuFlags.N | CpuFlags.H)]                          // half-borrow
    [InlineData(0x00, 0x01, 0xFF, CpuFlags.N | CpuFlags.H | CpuFlags.C)]             // borrow underflow
    [InlineData(0x10, 0x10, 0x00, CpuFlags.Z | CpuFlags.N)]                          // zero
    public void TestSui(byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a };

        Mmu.Write(0x00, 0xD6); // SUI
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
    [InlineData(0x04, Reg.B, 0x01, 0x02)] // INR B
    [InlineData(0x0C, Reg.C, 0x01, 0x02)] // INR C
    [InlineData(0x14, Reg.D, 0x01, 0x02)] // INR D
    [InlineData(0x1C, Reg.E, 0x01, 0x02)] // INR E
    [InlineData(0x24, Reg.H, 0x01, 0x02)] // INR H
    [InlineData(0x2C, Reg.L, 0x01, 0x02)] // INR L
    [InlineData(0x3C, Reg.A, 0x01, 0x02)] // INR A
    public void TestIncRegister(byte opcode, Reg reg, byte initialVal, byte expectedVal)
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteReg(reg, initialVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteReg(reg, expectedVal);
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestIncM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0x34); // INR M
        Mmu.Write(addr, 0x01);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(0x02, Mmu.Read(addr));
    }

    [Theory]
    [InlineData(0x01, 0x02, CpuFlags.None, CpuFlags.None)]                        // no flags
    [InlineData(0x0F, 0x10, CpuFlags.None, CpuFlags.H)]                           // half-carry
    [InlineData(0x7F, 0x80, CpuFlags.None, CpuFlags.H)]                           // half-carry on bit3 → bit4
    [InlineData(0xFF, 0x00, CpuFlags.None, CpuFlags.Z | CpuFlags.H)]              // wraps to zero, half-carry (C NOT set)
    [InlineData(0x01, 0x02, CpuFlags.C,    CpuFlags.C)]                           // carry preserved
    public void TestIncFlags(byte initial, byte expectedResult, CpuFlags initialFlags, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Rb = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x04); // INR B

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Rb = expectedResult;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x05, Reg.B, 0x02, 0x01)] // DCR B
    [InlineData(0x0D, Reg.C, 0x02, 0x01)] // DCR C
    [InlineData(0x15, Reg.D, 0x02, 0x01)] // DCR D
    [InlineData(0x1D, Reg.E, 0x02, 0x01)] // DCR E
    [InlineData(0x25, Reg.H, 0x02, 0x01)] // DCR H
    [InlineData(0x2D, Reg.L, 0x02, 0x01)] // DCR L
    [InlineData(0x3D, Reg.A, 0x02, 0x01)] // DCR A
    public void TestDecRegister(byte opcode, Reg reg, byte initialVal, byte expectedVal)
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteReg(reg, initialVal);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteReg(reg, expectedVal);
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestDecM()
    {
        ushort addr = 0x2000;
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0x35); // DCR M
        Mmu.Write(addr, 0x02);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = CpuFlags.N;
        expectedState.IncrementPcBy(1);

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(0x01, Mmu.Read(addr));
    }

    [Theory]
    [InlineData(0x02, 0x01, CpuFlags.None, CpuFlags.N)]                              // basic dec
    [InlineData(0x10, 0x0F, CpuFlags.None, CpuFlags.N | CpuFlags.H)]                // half-borrow
    [InlineData(0x80, 0x7F, CpuFlags.None, CpuFlags.N | CpuFlags.H)]                // half-borrow
    [InlineData(0x01, 0x00, CpuFlags.None, CpuFlags.Z | CpuFlags.N)]                // zero
    [InlineData(0x00, 0xFF, CpuFlags.None, CpuFlags.N | CpuFlags.H)]                // wrap with half-borrow (C NOT set)
    [InlineData(0x02, 0x01, CpuFlags.C,    CpuFlags.N | CpuFlags.C)]                // carry preserved
    public void TestDecFlags(byte initial, byte expectedResult, CpuFlags initialFlags, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Rb = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x05); // DCR B

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Rb = expectedResult;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB1, CpuFlags.None,            0x63, CpuFlags.C)]                  // bit7=1 wraps to bit0, C=1
    [InlineData(0x31, CpuFlags.None,            0x62, CpuFlags.None)]               // bit7=0, C=0
    [InlineData(0x80, CpuFlags.None,            0x01, CpuFlags.C)]                  // bit7 only
    [InlineData(0x01, CpuFlags.None,            0x02, CpuFlags.None)]               // bit0 only
    [InlineData(0xB1, CpuFlags.Z | CpuFlags.H, 0x63, CpuFlags.C)]                   // Z/H/N cleared (RLCA: Z=0, N=0, H=0)
    public void TestRlc(byte initial, CpuFlags initialFlags, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x07); // RLCA

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB1, CpuFlags.None,            0x62, CpuFlags.C)]                   // bit7=1, Cin=0
    [InlineData(0xB1, CpuFlags.C,               0x63, CpuFlags.C)]                   // bit7=1, Cin=1
    [InlineData(0x80, CpuFlags.C,               0x01, CpuFlags.C)]
    [InlineData(0x00, CpuFlags.C,               0x01, CpuFlags.None)]                // bit7=0, Cin=1
    [InlineData(0x01, CpuFlags.Z | CpuFlags.H, 0x02, CpuFlags.None)]                 // Z/H/N cleared
    public void TestRal(byte initial, CpuFlags initialFlags, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x17); // RLA

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(CpuFlags.None,            CpuFlags.C)]                              // sets C, clears N/H
    [InlineData(CpuFlags.C,               CpuFlags.C)]                              // already set
    [InlineData(CpuFlags.Z,               CpuFlags.Z | CpuFlags.C)]                 // Z preserved
    [InlineData(CpuFlags.N | CpuFlags.H, CpuFlags.C)]                               // N/H cleared
    public void TestScf(CpuFlags initialFlags, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Flags = initialFlags };

        Mmu.Write(0x00, 0x37); // SCF

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB1, CpuFlags.None,            0xD8, CpuFlags.C)]                  // bit0=1 wraps to bit7
    [InlineData(0xB2, CpuFlags.None,            0x59, CpuFlags.None)]
    [InlineData(0x01, CpuFlags.None,            0x80, CpuFlags.C)]
    [InlineData(0x80, CpuFlags.None,            0x40, CpuFlags.None)]
    [InlineData(0xB1, CpuFlags.Z | CpuFlags.H, 0xD8, CpuFlags.C)]                   // Z/H/N cleared
    public void TestRrc(byte initial, CpuFlags initialFlags, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x0F); // RRCA

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB1, CpuFlags.None,            0x58, CpuFlags.C)]
    [InlineData(0xB1, CpuFlags.C,               0xD8, CpuFlags.C)]
    [InlineData(0x01, CpuFlags.C,               0x80, CpuFlags.C)]
    [InlineData(0x00, CpuFlags.C,               0x80, CpuFlags.None)]
    [InlineData(0x02, CpuFlags.Z | CpuFlags.H, 0x01, CpuFlags.None)]                 // Z/H/N cleared
    public void TestRar(byte initial, CpuFlags initialFlags, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x1F); // RRA

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xB1, 0x4E)]
    [InlineData(0x00, 0xFF)]
    [InlineData(0xFF, 0x00)]
    public void TestCpl(byte initial, byte expectedA)
    {
        // CPL: N=1, H=1, Z and C unchanged.
        var initialFlags = CpuFlags.Z | CpuFlags.C;
        var initialState = new CpuState { Pc = 0x00, Ra = initial, Flags = initialFlags };

        Mmu.Write(0x00, 0x2F); // CPL

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = expectedA;
        expectedState.Flags = CpuFlags.Z | CpuFlags.N | CpuFlags.H | CpuFlags.C;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(CpuFlags.None,                          CpuFlags.C)]                  // C=0 → C=1
    [InlineData(CpuFlags.C,                             CpuFlags.None)]               // C=1 → C=0
    [InlineData(CpuFlags.Z,                             CpuFlags.Z | CpuFlags.C)]     // Z preserved
    [InlineData(CpuFlags.Z | CpuFlags.C,               CpuFlags.Z)]                   // clears C
    [InlineData(CpuFlags.N | CpuFlags.H,               CpuFlags.C)]                   // N/H cleared
    public void TestCcf(CpuFlags initialFlags, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Flags = initialFlags };

        Mmu.Write(0x00, 0x3F); // CCF

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestDaaThrowsUntilStep6()
    {
        var initialState = new CpuState { Pc = 0x00, Ra = 0x5C };

        Mmu.Write(0x00, 0x27); // DAA

        Cpu.WriteState(initialState);

        Assert.Throws<NotImplementedException>(() => Cpu.Step());
    }

    [Theory]
    [InlineData(CpuFlags.None, 0x10, 0x05, 0x15, CpuFlags.None)]                                  // carry=0
    [InlineData(CpuFlags.C,    0x10, 0x05, 0x16, CpuFlags.None)]                                  // carry=1
    [InlineData(CpuFlags.C,    0xFF, 0x00, 0x00, CpuFlags.Z | CpuFlags.H | CpuFlags.C)]           // overflow
    [InlineData(CpuFlags.None, 0xFF, 0x00, 0xFF, CpuFlags.None)]
    public void TestAci(CpuFlags initialFlags, byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Flags = initialFlags };

        Mmu.Write(0x00, 0xCE); // ACI
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
    [InlineData(CpuFlags.None, 0x11, 0x01, 0x10, CpuFlags.N)]                                       // basic
    [InlineData(CpuFlags.C,    0x12, 0x01, 0x10, CpuFlags.N)]                                       // borrow=1
    [InlineData(CpuFlags.C,    0x10, 0x00, 0x0F, CpuFlags.N | CpuFlags.H)]                          // half-borrow
    [InlineData(CpuFlags.None, 0x00, 0x01, 0xFF, CpuFlags.N | CpuFlags.H | CpuFlags.C)]            // underflow
    public void TestSbi(CpuFlags initialFlags, byte a, byte imm, byte expectedA, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Ra = a, Flags = initialFlags };

        Mmu.Write(0x00, 0xDE); // SBI
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
    [InlineData(0x03, Reg.B,  0x1234, 0x1235)] // INX B
    [InlineData(0x13, Reg.D,  0x1234, 0x1235)] // INX D
    [InlineData(0x23, Reg.H,  0x1234, 0x1235)] // INX H
    [InlineData(0x33, Reg.Sp, 0x1234, 0x1235)] // INX SP
    public void TestInxRegPair(byte opcode, Reg reg, ushort initialPair, ushort expectedPair)
    {
        var initialState = new CpuState { Pc = 0x00, Flags = CpuFlags.All };
        initialState.WriteRegPair(reg, initialPair);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteRegPair(reg, expectedPair);
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestInxNoFlagsOnOverflow()
    {
        var initialState = new CpuState { Pc = 0x00, Flags = CpuFlags.None };
        initialState.WriteRegPair(Reg.B, 0xFFFF);

        Mmu.Write(0x00, 0x03); // INX B

        Cpu.WriteState(initialState);
        Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteRegPair(Reg.B, 0x0000);
        expectedState.IncrementPcBy(1);

        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x0B, Reg.B,  0x1235, 0x1234)] // DCX B
    [InlineData(0x1B, Reg.D,  0x1235, 0x1234)] // DCX D
    [InlineData(0x2B, Reg.H,  0x1235, 0x1234)] // DCX H
    [InlineData(0x3B, Reg.Sp, 0x1235, 0x1234)] // DCX SP
    public void TestDcxRegPair(byte opcode, Reg reg, ushort initialPair, ushort expectedPair)
    {
        var initialState = new CpuState { Pc = 0x00, Flags = CpuFlags.All };
        initialState.WriteRegPair(reg, initialPair);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteRegPair(reg, expectedPair);
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestDcxNoFlagsOnUnderflow()
    {
        var initialState = new CpuState { Pc = 0x00, Flags = CpuFlags.None };
        initialState.WriteRegPair(Reg.B, 0x0000);

        Mmu.Write(0x00, 0x0B); // DCX B

        Cpu.WriteState(initialState);
        Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteRegPair(Reg.B, 0xFFFF);
        expectedState.IncrementPcBy(1);

        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x09, Reg.B,  0x0234, 0x1234)] // ADD HL, BC
    [InlineData(0x19, Reg.D,  0x0234, 0x1234)] // ADD HL, DE
    [InlineData(0x39, Reg.Sp, 0x0234, 0x1234)] // ADD HL, SP
    public void TestAddHlRegPair(byte opcode, Reg srcReg, ushort srcValue, ushort expectedHL)
    {
        // Z must be preserved by ADD HL on LR35902.
        var initialState = new CpuState { Pc = 0x00, Flags = CpuFlags.Z };
        initialState.WriteRegPair(Reg.H, 0x1000);
        initialState.WriteRegPair(srcReg, srcValue);

        Mmu.Write(0x00, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        // 0x1000 + 0x0234 = 0x1234. No bit-11 carry, no bit-15 carry.
        var expectedState = initialState;
        expectedState.WriteRegPair(Reg.H, expectedHL);
        expectedState.Flags = CpuFlags.Z; // Z preserved, N=0, H=0, C=0
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestAddHlHl()
    {
        // Exit-criteria spot-check: HL=0x0FFF, ADD HL, HL → HL=0x1FFE, N=0 H=1 C=0, Z preserved.
        var initialState = new CpuState { Pc = 0x00, Flags = CpuFlags.Z };
        initialState.WriteRegPair(Reg.H, 0x0FFF);

        Mmu.Write(0x00, 0x29); // ADD HL, HL

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteRegPair(Reg.H, 0x1FFE);
        expectedState.Flags = CpuFlags.Z | CpuFlags.H;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x8000, 0x8000, CpuFlags.Z, 0x0000, CpuFlags.Z | CpuFlags.C)] // bit-15 carry, Z preserved
    [InlineData(0x0FFF, 0x0001, CpuFlags.C, 0x1000, CpuFlags.H)]              // bit-11 carry only
    [InlineData(0x0001, 0x0001, CpuFlags.None, 0x0002, CpuFlags.None)]
    public void TestAddHlFlags(ushort initialHL, ushort bc, CpuFlags initialFlags, ushort expectedHL, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x00, Flags = initialFlags };
        initialState.WriteRegPair(Reg.H, initialHL);
        initialState.WriteRegPair(Reg.B, bc);

        Mmu.Write(0x00, 0x09); // ADD HL, BC

        Cpu.WriteState(initialState);
        Cpu.Step();

        var expectedState = initialState;
        expectedState.WriteRegPair(Reg.H, expectedHL);
        expectedState.Flags = expectedFlags;
        expectedState.IncrementPcBy(1);

        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestIncBExitCriteria()
    {
        // Exit-criteria spot-check: INC B with B=0x0F → B=0x10, Z=0 N=0 H=1, C unchanged.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x0F, Flags = CpuFlags.C };

        Mmu.Write(0x00, 0x04); // INC B

        Cpu.WriteState(initialState);
        Cpu.Step();

        var expectedState = initialState;
        expectedState.Rb = 0x10;
        expectedState.Flags = CpuFlags.H | CpuFlags.C;
        expectedState.IncrementPcBy(1);

        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData((ushort)0xFFF8, (sbyte)2,    (ushort)0xFFFA, CpuFlags.None)]
    [InlineData((ushort)0x000F, (sbyte)1,    (ushort)0x0010, CpuFlags.H)]
    [InlineData((ushort)0x00FF, (sbyte)1,    (ushort)0x0100, CpuFlags.H | CpuFlags.C)]
    [InlineData((ushort)0x0005, (sbyte)-1,   (ushort)0x0004, CpuFlags.H | CpuFlags.C)]
    public void TestAddSpR8(ushort initialSp, sbyte r8, ushort expectedSp, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x10, Sp = initialSp, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xE8);
        Mmu.Write((ushort)(initialState.Pc + 1), (byte)r8);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(2);
        expectedState.Sp = expectedSp;
        expectedState.Flags = expectedFlags;

        Assert.Equal(16, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0xD3)]
    [InlineData(0xDB)]
    [InlineData(0xDD)]
    [InlineData(0xE3)]
    [InlineData(0xE4)]
    [InlineData(0xEB)]
    [InlineData(0xEC)]
    [InlineData(0xED)]
    [InlineData(0xF4)]
    [InlineData(0xFC)]
    [InlineData(0xFD)]
    public void TestIllegalOpcodeThrows(byte opcode)
    {
        var initialState = new CpuState { Pc = 0x10 };

        Mmu.Write(initialState.Pc, opcode);

        Cpu.WriteState(initialState);

        Assert.Throws<InvalidOperationException>(() => Cpu.Step());
    }

    [Fact]
    public void TestSubExitCriteria()
    {
        // Exit-criteria spot-check: A=0x10, SUB 0x01 → A=0x0F, Z=0 N=1 H=1 C=0.
        var initialState = new CpuState { Pc = 0x00, Ra = 0x10, Rb = 0x01 };

        Mmu.Write(0x00, 0x90); // SUB B

        Cpu.WriteState(initialState);
        Cpu.Step();

        var expectedState = initialState;
        expectedState.Ra = 0x0F;
        expectedState.Flags = CpuFlags.N | CpuFlags.H;
        expectedState.IncrementPcBy(1);

        Assert.Equal(expectedState, Cpu.ReadState());
    }
}
