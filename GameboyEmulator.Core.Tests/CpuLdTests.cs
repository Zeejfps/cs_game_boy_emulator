using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class CpuLdTests : CpuTestBase
{
    [Fact]
    public void TestNoOp()
    {
        var initialState = new CpuState
        {
            Pc = 0x10,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, 0x00);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        Assert.Equal(4, cycles);
        Assert.Equal(initialState with { Pc = (ushort)(initialState.Pc + 1) }, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x40, Reg.B, Reg.B)]
    [InlineData(0x41, Reg.B, Reg.C)]
    [InlineData(0x42, Reg.B, Reg.D)]
    [InlineData(0x43, Reg.B, Reg.E)]
    [InlineData(0x44, Reg.B, Reg.H)]
    [InlineData(0x45, Reg.B, Reg.L)]
    [InlineData(0x47, Reg.B, Reg.A)]
    [InlineData(0x48, Reg.C, Reg.B)]
    [InlineData(0x49, Reg.C, Reg.C)]
    [InlineData(0x4A, Reg.C, Reg.D)]
    [InlineData(0x4B, Reg.C, Reg.E)]
    [InlineData(0x4C, Reg.C, Reg.H)]
    [InlineData(0x4D, Reg.C, Reg.L)]
    [InlineData(0x4F, Reg.C, Reg.A)]
    [InlineData(0x50, Reg.D, Reg.B)]
    [InlineData(0x51, Reg.D, Reg.C)]
    [InlineData(0x52, Reg.D, Reg.D)]
    [InlineData(0x53, Reg.D, Reg.E)]
    [InlineData(0x54, Reg.D, Reg.H)]
    [InlineData(0x55, Reg.D, Reg.L)]
    [InlineData(0x57, Reg.D, Reg.A)]
    [InlineData(0x58, Reg.E, Reg.B)]
    [InlineData(0x59, Reg.E, Reg.C)]
    [InlineData(0x5A, Reg.E, Reg.D)]
    [InlineData(0x5B, Reg.E, Reg.E)]
    [InlineData(0x5C, Reg.E, Reg.H)]
    [InlineData(0x5D, Reg.E, Reg.L)]
    [InlineData(0x5F, Reg.E, Reg.A)]
    [InlineData(0x60, Reg.H, Reg.B)]
    [InlineData(0x61, Reg.H, Reg.C)]
    [InlineData(0x62, Reg.H, Reg.D)]
    [InlineData(0x63, Reg.H, Reg.E)]
    [InlineData(0x64, Reg.H, Reg.H)]
    [InlineData(0x65, Reg.H, Reg.L)]
    [InlineData(0x67, Reg.H, Reg.A)]
    [InlineData(0x68, Reg.L, Reg.B)]
    [InlineData(0x69, Reg.L, Reg.C)]
    [InlineData(0x6A, Reg.L, Reg.D)]
    [InlineData(0x6B, Reg.L, Reg.E)]
    [InlineData(0x6C, Reg.L, Reg.H)]
    [InlineData(0x6D, Reg.L, Reg.L)]
    [InlineData(0x6F, Reg.L, Reg.A)]
    [InlineData(0x78, Reg.A, Reg.B)]
    [InlineData(0x79, Reg.A, Reg.C)]
    [InlineData(0x7A, Reg.A, Reg.D)]
    [InlineData(0x7B, Reg.A, Reg.E)]
    [InlineData(0x7C, Reg.A, Reg.H)]
    [InlineData(0x7D, Reg.A, Reg.L)]
    [InlineData(0x7F, Reg.A, Reg.A)]
    public void TestLdRr(byte opcode, Reg dst, Reg src)
    {
        var initialState = new CpuState { Pc = 0x10, Flags = CpuFlags.All };
        initialState.WriteReg(dst, 0x11);
        initialState.WriteReg(src, 0x50);

        Mmu.Write(initialState.Pc, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.WriteReg(dst, initialState.ReadReg(src));

        Assert.Equal(4, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x46, Reg.B)]
    [InlineData(0x4E, Reg.C)]
    [InlineData(0x56, Reg.D)]
    [InlineData(0x5E, Reg.E)]
    [InlineData(0x66, Reg.H)]
    [InlineData(0x6E, Reg.L)]
    [InlineData(0x7E, Reg.A)]
    public void TestLdRm(byte opcode, Reg dst)
    {
        var initialState = new CpuState
        {
            Pc = 0x10,
            Rh = 0x20,
            Rl = 0x30,
            Flags = CpuFlags.All
        };
        initialState.WriteReg(dst, 0x11);
        var address = initialState.Rhl;

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write(address, 0x50);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.WriteReg(dst, 0x50);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x70, Reg.B)]
    [InlineData(0x71, Reg.C)]
    [InlineData(0x72, Reg.D)]
    [InlineData(0x73, Reg.E)]
    [InlineData(0x74, Reg.H)]
    [InlineData(0x75, Reg.L)]
    [InlineData(0x77, Reg.A)]
    public void TestLdMr(byte opcode, Reg src)
    {
        var initialState = new CpuState
        {
            Pc = 0x10,
            Rh = 0x20,
            Rl = 0x30,
            Flags = CpuFlags.All
        };
        // Keep the sentinel distinct from H and L so a row asserting "wrote H"
        // can't pass by accidentally writing the sentinel, and vice versa.
        if (src is not Reg.H and not Reg.L)
            initialState.WriteReg(src, 0xAB);

        Mmu.Write(initialState.Pc, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);

        var expectedValueInMem = initialState.ReadReg(src);
        var valueInMem = Mmu.Read(initialState.Rhl);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(expectedValueInMem, valueInMem);
    }

    [Theory]
    [InlineData(0x06, Reg.B)]
    [InlineData(0x0E, Reg.C)]
    [InlineData(0x16, Reg.D)]
    [InlineData(0x1E, Reg.E)]
    [InlineData(0x26, Reg.H)]
    [InlineData(0x2E, Reg.L)]
    [InlineData(0x3E, Reg.A)]
    public void TestLdRn(byte opcode, Reg dst)
    {
        var instructionSize = 2;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write((ushort)(initialState.Pc + 1), 0xAB);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(instructionSize);
        expectedState.WriteReg(dst, 0xAB);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestLdMn()
    {
        byte opcode = 0x36;
        byte sentinel = 0xAB;
        var instructionSize = 2;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Rh = 0x20,
            Rl = 0x30,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write((ushort)(initialState.Pc + 1), sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(instructionSize);

        var memValue = Mmu.Read(expectedState.Rhl);

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, memValue);
    }

    [Theory]
    [InlineData(0x0A, Reg.B)]
    [InlineData(0x1A, Reg.D)]
    public void TestLdAx(byte opcode, Reg src)
    {
        byte sentinel = 0xAB;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Flags = CpuFlags.All
        };
        initialState.WriteRegPair(src, 0x2030);
        var address = initialState.ReadRegPair(src);

        Mmu.Write(initialState.Pc, opcode);
        Mmu.Write(address, sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.Ra = sentinel;

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData(0x02, Reg.B)]
    [InlineData(0x12, Reg.D)]
    public void TestStAx(byte opcode, Reg src)
    {
        byte sentinel = 0xAB;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Ra = sentinel,
            Flags = CpuFlags.All
        };
        initialState.WriteRegPair(src, 0x2030);
        var address = initialState.ReadRegPair(src);

        Mmu.Write(initialState.Pc, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);

        var memValue = Mmu.Read(address);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, memValue);
    }

    [Theory]
    [InlineData(0x01, Reg.B)]
    [InlineData(0x11, Reg.D)]
    [InlineData(0x21, Reg.H)]
    [InlineData(0x31, Reg.Sp)]
    public void TestLdRrNn(byte opcode, Reg dst)
    {
        ushort immediate = 0x2030;
        var instructionSize = 3;
        var initialState = new CpuState { Pc = 0x10, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, opcode);
        Mmu.WriteWord((ushort)(initialState.Pc + 1), immediate);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(instructionSize);
        expectedState.WriteRegPair(dst, immediate);

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestLdSpHl()
    {
        byte opcode = 0xF9;
        var initialState = new CpuState
        {
            Pc = 0x10,
            Rh = 0x20,
            Rl = 0x30,
            Flags = CpuFlags.All
        };

        Mmu.Write(initialState.Pc, opcode);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.Sp = 0x2030;

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData((ushort)0x2030, (ushort)0x2031)]
    [InlineData((ushort)0xFFFF, (ushort)0x0000)]
    public void TestLdHlIncA(ushort initialHl, ushort expectedHl)
    {
        byte sentinel = 0xAB;
        var initialState = new CpuState { Pc = 0x10, Ra = sentinel, Flags = CpuFlags.All };
        initialState.WriteRegPair(Reg.H, initialHl);

        Mmu.Write(initialState.Pc, 0x22);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.WriteRegPair(Reg.H, expectedHl);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, Mmu.Read(initialHl));
    }

    [Theory]
    [InlineData((ushort)0x2030, (ushort)0x2031)]
    [InlineData((ushort)0xFFFF, (ushort)0x0000)]
    public void TestLdAHlInc(ushort initialHl, ushort expectedHl)
    {
        byte sentinel = 0xAB;
        var initialState = new CpuState { Pc = 0x10, Flags = CpuFlags.All };
        initialState.WriteRegPair(Reg.H, initialHl);

        Mmu.Write(initialState.Pc, 0x2A);
        Mmu.Write(initialHl, sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.Ra = sentinel;
        expectedState.WriteRegPair(Reg.H, expectedHl);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Theory]
    [InlineData((ushort)0x2030, (ushort)0x202F)]
    [InlineData((ushort)0x0000, (ushort)0xFFFF)]
    public void TestLdHlDecA(ushort initialHl, ushort expectedHl)
    {
        byte sentinel = 0xAB;
        var initialState = new CpuState { Pc = 0x10, Ra = sentinel, Flags = CpuFlags.All };
        initialState.WriteRegPair(Reg.H, initialHl);

        Mmu.Write(initialState.Pc, 0x32);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.WriteRegPair(Reg.H, expectedHl);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, Mmu.Read(initialHl));
    }

    [Theory]
    [InlineData((ushort)0x2030, (ushort)0x202F)]
    [InlineData((ushort)0x0000, (ushort)0xFFFF)]
    public void TestLdAHlDec(ushort initialHl, ushort expectedHl)
    {
        byte sentinel = 0xAB;
        var initialState = new CpuState { Pc = 0x10, Flags = CpuFlags.All };
        initialState.WriteRegPair(Reg.H, initialHl);

        Mmu.Write(initialState.Pc, 0x3A);
        Mmu.Write(initialHl, sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.Ra = sentinel;
        expectedState.WriteRegPair(Reg.H, expectedHl);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestLdA16A()
    {
        byte sentinel = 0xAB;
        ushort target = 0x2030;
        var initialState = new CpuState { Pc = 0x10, Ra = sentinel, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xEA);
        Mmu.WriteWord((ushort)(initialState.Pc + 1), target);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(3);

        Assert.Equal(16, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, Mmu.Read(target));
    }

    [Fact]
    public void TestLdAA16()
    {
        byte sentinel = 0xAB;
        ushort source = 0x2030;
        var initialState = new CpuState { Pc = 0x10, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xFA);
        Mmu.WriteWord((ushort)(initialState.Pc + 1), source);
        Mmu.Write(source, sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(3);
        expectedState.Ra = sentinel;

        Assert.Equal(16, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestLdhA8A()
    {
        byte sentinel = 0xAB;
        byte offset = 0x55;
        var initialState = new CpuState { Pc = 0x10, Ra = sentinel, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xE0);
        Mmu.Write((ushort)(initialState.Pc + 1), offset);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(2);

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, Mmu.Read((ushort)(0xFF00 + offset)));
    }

    [Fact]
    public void TestLdhAA8()
    {
        byte sentinel = 0xAB;
        byte offset = 0x55;
        var initialState = new CpuState { Pc = 0x10, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xF0);
        Mmu.Write((ushort)(initialState.Pc + 1), offset);
        Mmu.Write((ushort)(0xFF00 + offset), sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(2);
        expectedState.Ra = sentinel;

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestLdCA()
    {
        byte sentinel = 0xAB;
        byte offset = 0x55;
        var initialState = new CpuState { Pc = 0x10, Ra = sentinel, Rc = offset, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xE2);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(sentinel, Mmu.Read((ushort)(0xFF00 + offset)));
    }

    [Fact]
    public void TestLdAC()
    {
        byte sentinel = 0xAB;
        byte offset = 0x55;
        var initialState = new CpuState { Pc = 0x10, Rc = offset, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xF2);
        Mmu.Write((ushort)(0xFF00 + offset), sentinel);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(1);
        expectedState.Ra = sentinel;

        Assert.Equal(8, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }

    [Fact]
    public void TestLdA16Sp()
    {
        ushort target = 0x2030;
        var initialState = new CpuState { Pc = 0x10, Sp = 0xBEEF, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0x08);
        Mmu.WriteWord((ushort)(initialState.Pc + 1), target);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(3);

        Assert.Equal(20, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
        Assert.Equal(0xEF, Mmu.Read(target));
        Assert.Equal(0xBE, Mmu.Read((ushort)(target + 1)));
    }

    [Theory]
    [InlineData((ushort)0xFFF8, (sbyte)2,    (ushort)0xFFFA, CpuFlags.None)]
    [InlineData((ushort)0x000F, (sbyte)1,    (ushort)0x0010, CpuFlags.H)]
    [InlineData((ushort)0x00FF, (sbyte)1,    (ushort)0x0100, CpuFlags.H | CpuFlags.C)]
    [InlineData((ushort)0x0005, (sbyte)-1,   (ushort)0x0004, CpuFlags.H | CpuFlags.C)]
    public void TestLdHlSpR8(ushort initialSp, sbyte r8, ushort expectedHl, CpuFlags expectedFlags)
    {
        var initialState = new CpuState { Pc = 0x10, Sp = initialSp, Flags = CpuFlags.All };

        Mmu.Write(initialState.Pc, 0xF8);
        Mmu.Write((ushort)(initialState.Pc + 1), (byte)r8);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        var expectedState = initialState;
        expectedState.IncrementPcBy(2);
        expectedState.WriteRegPair(Reg.H, expectedHl);
        expectedState.Flags = expectedFlags;

        Assert.Equal(12, cycles);
        Assert.Equal(expectedState, Cpu.ReadState());
    }
}
