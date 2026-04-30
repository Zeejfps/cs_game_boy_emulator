using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class CpuCbTests : CpuTestBase
{
    // One operand-coverage test per operation group: pick a sub-opcode that
    // exercises the operation against a register (mostly B) and verify the
    // resulting byte and the four flag bits.

    [Fact]
    public void Rlc_B()
    {
        // RLC B (0xCB 0x00). B=0x85 (1000_0101) → 0x0B (0000_1011), C = old bit7 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x85, Flags = CpuFlags.N | CpuFlags.H };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x00);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x0B, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Rrc_B()
    {
        // RRC B (0xCB 0x08). B=0x01 → 0x80, C = old bit0 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x01 };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x08);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x80, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Rl_B()
    {
        // RL B (0xCB 0x10). B=0x80, C=1 → 0x01, new C = old bit7 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x80, Flags = CpuFlags.C };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x10);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x01, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Rr_B()
    {
        // RR B (0xCB 0x18). B=0x01, C=1 → 0x80, new C = old bit0 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x01, Flags = CpuFlags.C };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x18);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x80, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Sla_B()
    {
        // SLA B (0xCB 0x20). B=0x81 → 0x02, C = old bit7 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x81, Flags = CpuFlags.N };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x20);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x02, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Sra_B()
    {
        // SRA B (0xCB 0x28). B=0x81 → 0xC0 (bit7 preserved), C = old bit0 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x81 };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x28);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0xC0, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Swap_B()
    {
        // SWAP B (0xCB 0x30). B=0xAB → 0xBA. Z=0, N=H=C=0.
        var initialState = new CpuState { Pc = 0x00, Rb = 0xAB, Flags = CpuFlags.All };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x30);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0xBA, state.Rb);
        Assert.Equal(CpuFlags.None, state.Flags);
    }

    [Fact]
    public void Srl_B()
    {
        // SRL B (0xCB 0x38). B=0x81 → 0x40 (bit7=0), C = old bit0 = 1.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x81 };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x38);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x40, state.Rb);
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Bit_B()
    {
        // BIT 3,B (0xCB 0x58). B=0x08 → bit set, Z=0, N=0, H=1, C unchanged.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x08, Flags = CpuFlags.C | CpuFlags.Z | CpuFlags.N };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x58);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x08, state.Rb); // unchanged
        Assert.Equal(CpuFlags.H | CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Res_B()
    {
        // RES 3,B (0xCB 0x98). B=0xFF → 0xF7. Flags untouched.
        var initialState = new CpuState { Pc = 0x00, Rb = 0xFF, Flags = CpuFlags.All };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x98);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0xF7, state.Rb);
        Assert.Equal(CpuFlags.All, state.Flags);
    }

    [Fact]
    public void Set_B()
    {
        // SET 3,B (0xCB 0xD8). B=0x00 → 0x08. Flags untouched.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x00, Flags = CpuFlags.None };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0xD8);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x08, state.Rb);
        Assert.Equal(CpuFlags.None, state.Flags);
    }

    // Operand decoding: RLC against each of the 8 operand slots writes the
    // expected register or memory location.
    [Theory]
    [InlineData(0x00, Reg.B)] // RLC B
    [InlineData(0x01, Reg.C)] // RLC C
    [InlineData(0x02, Reg.D)] // RLC D
    [InlineData(0x03, Reg.E)] // RLC E
    [InlineData(0x04, Reg.H)] // RLC H
    [InlineData(0x05, Reg.L)] // RLC L
    [InlineData(0x07, Reg.A)] // RLC A
    public void Rlc_OperandDecoding_Register(byte sub, Reg reg)
    {
        // Use HL = 0x4000 so H/L cases don't clash with operand decoding.
        // The H and L tests overwrite the operand register with the expected
        // result after RLC, which is fine — RLC reads first and writes back.
        var initialState = new CpuState { Pc = 0x00, Rh = 0x40, Rl = 0x00 };
        initialState.WriteReg(reg, 0x85);

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, sub);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x0B, state.ReadReg(reg));
        Assert.Equal(CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Rlc_OperandDecoding_HL()
    {
        // RLC (HL) (0xCB 0x06). Memory at HL holds 0x85 → 0x0B.
        ushort addr = 0x4000;
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, addr);

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x06);
        Mmu.Write(addr, 0x85);

        Cpu.WriteState(initialState);
        Cpu.Step();

        Assert.Equal(0x0B, Mmu.Read(addr));
        Assert.Equal(CpuFlags.C, Cpu.ReadState().Flags);
    }

    // Cycle accounting.

    [Fact]
    public void RlcHl_Returns16()
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, 0x4000);

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x06); // RLC (HL)

        Cpu.WriteState(initialState);
        Assert.Equal(16, Cpu.Step());
    }

    [Fact]
    public void Bit0Hl_Returns12()
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, 0x4000);

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x46); // BIT 0,(HL)

        Cpu.WriteState(initialState);
        Assert.Equal(12, Cpu.Step());
    }

    [Fact]
    public void Res0Hl_Returns16()
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, 0x4000);

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x86); // RES 0,(HL)

        Cpu.WriteState(initialState);
        Assert.Equal(16, Cpu.Step());
    }

    [Fact]
    public void Set0Hl_Returns16()
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, 0x4000);

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0xC6); // SET 0,(HL)

        Cpu.WriteState(initialState);
        Assert.Equal(16, Cpu.Step());
    }

    [Fact]
    public void RlcB_Returns8()
    {
        var initialState = new CpuState { Pc = 0x00, Rb = 0x01 };
        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x00); // RLC B

        Cpu.WriteState(initialState);
        Assert.Equal(8, Cpu.Step());
    }

    // Flag-rule regressions.

    [Fact]
    public void Swap_A_Zero_SetsZ_ClearsNHC()
    {
        // SWAP A (0xCB 0x37). A=0x00 → 0x00, Z=1, N=H=C=0.
        var initialState = new CpuState { Pc = 0x00, Ra = 0x00, Flags = CpuFlags.All };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x37);

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x00, state.Ra);
        Assert.Equal(CpuFlags.Z, state.Flags);
    }

    [Fact]
    public void Bit_LeavesCarryUntouched()
    {
        // BIT 0,B with C preset; verify C still 1 after.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x00, Flags = CpuFlags.C };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x40); // BIT 0,B

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        // bit 0 of 0 → Z=1, N=0, H=1, C unchanged (1).
        Assert.Equal(CpuFlags.Z | CpuFlags.H | CpuFlags.C, state.Flags);
    }

    [Fact]
    public void Res_LeavesAllFlagsUntouched()
    {
        var initialState = new CpuState { Pc = 0x00, Rb = 0xFF, Flags = CpuFlags.All };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x80); // RES 0,B

        Cpu.WriteState(initialState);
        Cpu.Step();

        Assert.Equal(CpuFlags.All, Cpu.ReadState().Flags);
    }

    [Fact]
    public void Set_LeavesAllFlagsUntouched()
    {
        var initialState = new CpuState { Pc = 0x00, Rb = 0x00, Flags = CpuFlags.None };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0xC0); // SET 0,B

        Cpu.WriteState(initialState);
        Cpu.Step();

        Assert.Equal(CpuFlags.None, Cpu.ReadState().Flags);
    }

    [Fact]
    public void CbRlcB_WithZero_SetsZ()
    {
        // Distinguishes from RLCA (0x07), which always clears Z.
        var initialState = new CpuState { Pc = 0x00, Rb = 0x00, Flags = CpuFlags.None };

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, 0x00); // RLC B

        Cpu.WriteState(initialState);
        Cpu.Step();

        var state = Cpu.ReadState();
        Assert.Equal(0x00, state.Rb);
        Assert.Equal(CpuFlags.Z, state.Flags);
    }

    // Programmatic decode + cycle sweep over every CB sub-opcode.
    // Expected cycles: register operand (slot != 6) → 8;
    // (HL) operand (slot == 6) → 12 for BIT, 16 for everything else.
    public static TheoryData<byte, int> CbCycleData()
    {
        var data = new TheoryData<byte, int>();
        for (int sub = 0; sub <= 0xFF; sub++)
        {
            int slot = sub & 0x07;
            bool isHl = slot == 6;
            bool isBit = (sub & 0xC0) == 0x40;
            int expected = isHl ? (isBit ? 12 : 16) : 8;
            data.Add((byte)sub, expected);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CbCycleData))]
    public void AllCbOpcodes_DecodeAndReturnSpecCycles(byte sub, int expectedCycles)
    {
        var initialState = new CpuState { Pc = 0x00 };
        initialState.WriteRegPair(Reg.H, 0x4000); // valid (HL) target

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, sub);

        Cpu.WriteState(initialState);
        var cycles = Cpu.Step();

        Assert.Equal(expectedCycles, cycles);
    }

    public static TheoryData<int, int> BitCrossProductData()
    {
        var data = new TheoryData<int, int>();
        for (int n = 0; n < 8; n++)
            for (int slot = 0; slot < 8; slot++)
                data.Add(n, slot);
        return data;
    }

    // BIT n,r derives Z from the operand bit. Flags: Z, N=0, H=1, C untouched.
    // Sweeps all 8 bits × all 8 operand slots in both arms (bit set → Z=0,
    // bit clear → Z=1) — catches operand-decode bugs that the existing
    // single-operand tests would miss.
    [Theory]
    [MemberData(nameof(BitCrossProductData))]
    public void Bit_AllBitsAllOperands_DerivesZ(int n, int slot)
    {
        byte sub = (byte)(0x40 | (n << 3) | slot);
        const ushort hlAddr = 0x4000;

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, sub);

        // Arm 1: operand has bit n set → Z=0.
        var s1 = new CpuState { Pc = 0x00, Flags = CpuFlags.C };
        s1.WriteRegPair(Reg.H, hlAddr);
        byte bitSet = (byte)(1 << n);
        if (slot == 6)
            Mmu.Write(hlAddr, bitSet);
        else
            s1.WriteReg(SlotToReg(slot), bitSet);

        Cpu.WriteState(s1);
        Cpu.Step();
        Assert.Equal(CpuFlags.H | CpuFlags.C, Cpu.Flags);

        // Arm 2: operand has bit n clear → Z=1.
        var s2 = new CpuState { Pc = 0x00, Flags = CpuFlags.C };
        s2.WriteRegPair(Reg.H, hlAddr);
        byte bitClear = (byte)(~(1 << n) & 0xFF);
        if (slot == 6)
            Mmu.Write(hlAddr, bitClear);
        else
            s2.WriteReg(SlotToReg(slot), bitClear);

        Cpu.WriteState(s2);
        Cpu.Step();
        Assert.Equal(CpuFlags.Z | CpuFlags.H | CpuFlags.C, Cpu.Flags);
    }

    public static TheoryData<int, int> ResSetCrossProductData() => BitCrossProductData();

    [Theory]
    [MemberData(nameof(ResSetCrossProductData))]
    public void Res_AllBitsAllOperands_ClearsBit_FlagsUntouched(int n, int slot)
    {
        byte sub = (byte)(0x80 | (n << 3) | slot);
        const ushort hlAddr = 0x4000;

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, sub);

        var initial = new CpuState { Pc = 0x00, Flags = CpuFlags.All };
        initial.WriteRegPair(Reg.H, hlAddr);
        if (slot == 6)
            Mmu.Write(hlAddr, 0xFF);
        else
            initial.WriteReg(SlotToReg(slot), 0xFF);

        Cpu.WriteState(initial);
        Cpu.Step();

        byte expected = (byte)(0xFF & ~(1 << n));
        byte actual = slot == 6 ? Mmu.Read(hlAddr) : Cpu.ReadState().ReadReg(SlotToReg(slot));
        Assert.Equal(expected, actual);
        Assert.Equal(CpuFlags.All, Cpu.Flags);
    }

    [Theory]
    [MemberData(nameof(ResSetCrossProductData))]
    public void Set_AllBitsAllOperands_SetsBit_FlagsUntouched(int n, int slot)
    {
        byte sub = (byte)(0xC0 | (n << 3) | slot);
        const ushort hlAddr = 0x4000;

        Mmu.Write(0x00, 0xCB);
        Mmu.Write(0x01, sub);

        var initial = new CpuState { Pc = 0x00, Flags = CpuFlags.None };
        initial.WriteRegPair(Reg.H, hlAddr);
        if (slot == 6)
            Mmu.Write(hlAddr, 0x00);
        else
            initial.WriteReg(SlotToReg(slot), 0x00);

        Cpu.WriteState(initial);
        Cpu.Step();

        byte expected = (byte)(1 << n);
        byte actual = slot == 6 ? Mmu.Read(hlAddr) : Cpu.ReadState().ReadReg(SlotToReg(slot));
        Assert.Equal(expected, actual);
        Assert.Equal(CpuFlags.None, Cpu.Flags);
    }

    private static Reg SlotToReg(int slot) => slot switch
    {
        0 => Reg.B,
        1 => Reg.C,
        2 => Reg.D,
        3 => Reg.E,
        4 => Reg.H,
        5 => Reg.L,
        7 => Reg.A,
        _ => throw new ArgumentException($"Slot {slot} is not a register operand", nameof(slot)),
    };
}
