using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

// One representative opcode per category, asserting the LR35902 T-state count
// that Step() must publish. Cheap insurance against drift introduced by future
// refactors of the per-handler return values.
public class CpuTStateCoverageTests : CpuTestBase
{
    [Theory]
    [InlineData("NOP",                  4,  new byte[] { 0x00 },             CpuFlags.None)]
    [InlineData("MOV r,r",              4,  new byte[] { 0x40 },             CpuFlags.None)]
    [InlineData("MOV r,(HL)",           8,  new byte[] { 0x46 },             CpuFlags.None)]
    [InlineData("MOV (HL),r",           8,  new byte[] { 0x70 },             CpuFlags.None)]
    [InlineData("MVI r,d8",             8,  new byte[] { 0x06, 0x00 },       CpuFlags.None)]
    [InlineData("MVI (HL),d8",          12, new byte[] { 0x36, 0x00 },       CpuFlags.None)]
    [InlineData("LXI rr,d16",           12, new byte[] { 0x01, 0x00, 0x00 }, CpuFlags.None)]
    [InlineData("INX rr",               8,  new byte[] { 0x03 },             CpuFlags.None)]
    [InlineData("DCR r",                4,  new byte[] { 0x05 },             CpuFlags.None)]
    [InlineData("INC r",                4,  new byte[] { 0x04 },             CpuFlags.None)]
    [InlineData("INC (HL)",             12, new byte[] { 0x34 },             CpuFlags.None)]
    [InlineData("ADD A,r",              4,  new byte[] { 0x80 },             CpuFlags.None)]
    [InlineData("ADD A,(HL)",           8,  new byte[] { 0x86 },             CpuFlags.None)]
    [InlineData("ADI d8",               8,  new byte[] { 0xC6, 0x00 },       CpuFlags.None)]
    [InlineData("ADD HL,rr",            8,  new byte[] { 0x09 },             CpuFlags.None)]
    [InlineData("DAA",                  4,  new byte[] { 0x27 },             CpuFlags.None)]
    [InlineData("RST n",                16, new byte[] { 0xC7 },             CpuFlags.None)]
    [InlineData("JP a16",               16, new byte[] { 0xC3, 0x00, 0x00 }, CpuFlags.None)]
    [InlineData("JP cc taken",          16, new byte[] { 0xC2, 0x00, 0x00 }, CpuFlags.None)]
    [InlineData("JP cc not-taken",      12, new byte[] { 0xC2, 0x00, 0x00 }, CpuFlags.Z)]
    [InlineData("CALL a16",             24, new byte[] { 0xCD, 0x00, 0x00 }, CpuFlags.None)]
    [InlineData("CALL cc taken",        24, new byte[] { 0xC4, 0x00, 0x00 }, CpuFlags.None)]
    [InlineData("CALL cc not-taken",    12, new byte[] { 0xC4, 0x00, 0x00 }, CpuFlags.Z)]
    [InlineData("RET",                  16, new byte[] { 0xC9 },             CpuFlags.None)]
    [InlineData("RET cc taken",         20, new byte[] { 0xC0 },             CpuFlags.None)]
    [InlineData("RET cc not-taken",     8,  new byte[] { 0xC0 },             CpuFlags.Z)]
    [InlineData("PUSH rr",              16, new byte[] { 0xC5 },             CpuFlags.None)]
    [InlineData("POP rr",               12, new byte[] { 0xC1 },             CpuFlags.None)]
    [InlineData("LDH (a8),A",           12, new byte[] { 0xE0, 0x80 },       CpuFlags.None)]
    [InlineData("JR cc taken",          12, new byte[] { 0x20, 0x00 },       CpuFlags.None)]
    [InlineData("JR cc not-taken",      8,  new byte[] { 0x20, 0x00 },       CpuFlags.Z)]
    [InlineData("CB op r",              8,  new byte[] { 0xCB, 0x00 },       CpuFlags.None)] // RLC B
    [InlineData("CB op (HL)",           16, new byte[] { 0xCB, 0x06 },       CpuFlags.None)] // RLC (HL)
    [InlineData("CB BIT n,(HL)",        12, new byte[] { 0xCB, 0x46 },       CpuFlags.None)] // BIT 0,(HL)
    [InlineData("DI",                   4,  new byte[] { 0xF3 },             CpuFlags.None)]
    [InlineData("EI",                   4,  new byte[] { 0xFB },             CpuFlags.None)]
    [InlineData("HALT",                 4,  new byte[] { 0x76 },             CpuFlags.None)]
    public void StepReturnsExpectedTStates(string label, int expected, byte[] program, CpuFlags flags)
    {
        _ = label;
        ushort start = 0x0100;
        for (var i = 0; i < program.Length; i++)
            Mmu.Write((ushort)(start + i), program[i]);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Flags = flags });

        var cycles = Cpu.Step();

        Assert.Equal(expected, cycles);
    }

    [Fact]
    public void RetiReturns16TStates()
    {
        ushort start = 0x0100;
        ushort sp = 0x4000;
        Mmu.Write(start, 0xD9);
        Mmu.WriteWord(sp, 0x1234);

        Cpu.WriteState(new CpuState { Pc = start, Sp = sp });

        var cycles = Cpu.Step();

        Assert.Equal(16, cycles);
    }

    [Fact]
    public void InterruptDispatchReturns20TStates()
    {
        ushort start = 0x0100;
        Mmu.Write(start, 0x00); // NOP under PC — unreached if dispatch fires
        Mmu.Write(0xFFFF, 0x01); // IE: VBlank
        Mmu.Write(0xFF0F, 0x01); // IF: VBlank

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = true;

        var cycles = Cpu.Step();

        Assert.Equal(20, cycles);
    }

    // Programmatic decode + cycle sweep over every non-illegal, non-CB base
    // opcode. Catches dispatcher gaps and per-handler cycle drift the
    // representative-opcode theory above might miss.
    //
    // Pre-state fixes Z=0, C=0 so each conditional has a single deterministic
    // cycle count: NZ/NC are taken; Z/C are not.
    public static TheoryData<byte, int> BaseOpcodeCycleData() => BuildBaseOpcodeCycleData();

    [Theory]
    [MemberData(nameof(BaseOpcodeCycleData))]
    public void AllBaseOpcodes_DecodeAndReturnSpecCycles(byte opcode, int expectedCycles)
    {
        ushort start = 0x0100;
        // Three operand bytes is enough for the longest base opcode (CALL/JP a16).
        Mmu.Write(start, opcode);
        Mmu.Write((ushort)(start + 1), 0x00);
        Mmu.Write((ushort)(start + 2), 0x00);
        Mmu.Write((ushort)(start + 3), 0x00);

        Cpu.WriteState(new CpuState
        {
            Pc = start,
            Sp = 0x4000,
            Rh = 0x40, Rl = 0x00, // HL = 0x4000 (writable in FakeMmu)
            Flags = CpuFlags.None, // Z=0, C=0
        });

        var cycles = Cpu.Step();

        Assert.Equal(expectedCycles, cycles);
    }

    [Theory]
    [InlineData((byte)0xD3)]
    [InlineData((byte)0xDB)]
    [InlineData((byte)0xDD)]
    [InlineData((byte)0xE3)]
    [InlineData((byte)0xE4)]
    [InlineData((byte)0xEB)]
    [InlineData((byte)0xEC)]
    [InlineData((byte)0xED)]
    [InlineData((byte)0xF4)]
    [InlineData((byte)0xFC)]
    [InlineData((byte)0xFD)]
    public void IllegalOpcodes_Throw(byte opcode)
    {
        ushort start = 0x0100;
        Mmu.Write(start, opcode);
        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });

        Assert.ThrowsAny<Exception>(() => Cpu.Step());
    }

    private static TheoryData<byte, int> BuildBaseOpcodeCycleData()
    {
        var data = new TheoryData<byte, int>();
        var cycles = new int[256];

        // Misc / control
        cycles[0x00] = 4;   // NOP
        cycles[0x10] = 4;   // STOP
        cycles[0x76] = 4;   // HALT (handled below in 0x40-0x7F range; set explicitly here for clarity)
        cycles[0xF3] = 4;   // DI
        cycles[0xFB] = 4;   // EI

        // 16-bit immediate loads
        cycles[0x01] = 12;  // LD BC,d16
        cycles[0x11] = 12;  // LD DE,d16
        cycles[0x21] = 12;  // LD HL,d16
        cycles[0x31] = 12;  // LD SP,d16

        // (BC)/(DE)/(HL+/-) loads/stores
        cycles[0x02] = 8;   // LD (BC),A
        cycles[0x12] = 8;   // LD (DE),A
        cycles[0x22] = 8;   // LD (HL+),A
        cycles[0x32] = 8;   // LD (HL-),A
        cycles[0x0A] = 8;   // LD A,(BC)
        cycles[0x1A] = 8;   // LD A,(DE)
        cycles[0x2A] = 8;   // LD A,(HL+)
        cycles[0x3A] = 8;   // LD A,(HL-)

        // 16-bit INC/DEC
        cycles[0x03] = 8;
        cycles[0x13] = 8;
        cycles[0x23] = 8;
        cycles[0x33] = 8;
        cycles[0x0B] = 8;
        cycles[0x1B] = 8;
        cycles[0x2B] = 8;
        cycles[0x3B] = 8;

        // 8-bit INC/DEC r (regs)
        cycles[0x04] = cycles[0x05] = 4; // B
        cycles[0x0C] = cycles[0x0D] = 4; // C
        cycles[0x14] = cycles[0x15] = 4; // D
        cycles[0x1C] = cycles[0x1D] = 4; // E
        cycles[0x24] = cycles[0x25] = 4; // H
        cycles[0x2C] = cycles[0x2D] = 4; // L
        cycles[0x3C] = cycles[0x3D] = 4; // A
        cycles[0x34] = 12; // INC (HL)
        cycles[0x35] = 12; // DEC (HL)

        // 8-bit immediate loads
        cycles[0x06] = 8;  // LD B,d8
        cycles[0x0E] = 8;  // LD C,d8
        cycles[0x16] = 8;  // LD D,d8
        cycles[0x1E] = 8;  // LD E,d8
        cycles[0x26] = 8;  // LD H,d8
        cycles[0x2E] = 8;  // LD L,d8
        cycles[0x3E] = 8;  // LD A,d8
        cycles[0x36] = 12; // LD (HL),d8

        // Accumulator rotates
        cycles[0x07] = 4;  // RLCA
        cycles[0x0F] = 4;  // RRCA
        cycles[0x17] = 4;  // RLA
        cycles[0x1F] = 4;  // RRA

        // LD (a16),SP
        cycles[0x08] = 20;

        // ADD HL,rr
        cycles[0x09] = 8;
        cycles[0x19] = 8;
        cycles[0x29] = 8;
        cycles[0x39] = 8;

        // JR e8 / JR cc,e8 (cc with Z=0,C=0 → NZ taken, Z not, NC taken, C not)
        cycles[0x18] = 12; // JR e8
        cycles[0x20] = 12; // JR NZ taken
        cycles[0x28] = 8;  // JR Z not taken
        cycles[0x30] = 12; // JR NC taken
        cycles[0x38] = 8;  // JR C not taken

        // Decimal/flag
        cycles[0x27] = 4;  // DAA
        cycles[0x2F] = 4;  // CPL
        cycles[0x37] = 4;  // SCF
        cycles[0x3F] = 4;  // CCF

        // 0x40-0x7F: LD r,r' / LD r,(HL) / LD (HL),r / HALT (0x76)
        for (int op = 0x40; op <= 0x7F; op++)
        {
            int dst = (op >> 3) & 0x07;
            int src = op & 0x07;
            if (op == 0x76) { cycles[op] = 4; continue; } // HALT
            cycles[op] = (dst == 6 || src == 6) ? 8 : 4;
        }

        // 0x80-0xBF: ALU A,r / A,(HL)
        for (int op = 0x80; op <= 0xBF; op++)
        {
            int src = op & 0x07;
            cycles[op] = (src == 6) ? 8 : 4;
        }

        // RET cc / POP / JP cc / JP / CALL cc / PUSH / ALU d8 / RST
        cycles[0xC0] = 20; // RET NZ taken
        cycles[0xC1] = 12; // POP BC
        cycles[0xC2] = 16; // JP NZ taken
        cycles[0xC3] = 16; // JP a16
        cycles[0xC4] = 24; // CALL NZ taken
        cycles[0xC5] = 16; // PUSH BC
        cycles[0xC6] = 8;  // ADD A,d8
        cycles[0xC7] = 16; // RST 00H
        cycles[0xC8] = 8;  // RET Z not taken
        cycles[0xC9] = 16; // RET
        cycles[0xCA] = 12; // JP Z not taken
        // 0xCB is the CB prefix — covered by AllCbOpcodes_* in CpuCbTests.
        cycles[0xCC] = 12; // CALL Z not taken
        cycles[0xCD] = 24; // CALL a16
        cycles[0xCE] = 8;  // ADC A,d8
        cycles[0xCF] = 16; // RST 08H

        cycles[0xD0] = 20; // RET NC taken
        cycles[0xD1] = 12; // POP DE
        cycles[0xD2] = 16; // JP NC taken
        cycles[0xD4] = 24; // CALL NC taken
        cycles[0xD5] = 16; // PUSH DE
        cycles[0xD6] = 8;  // SUB d8
        cycles[0xD7] = 16; // RST 10H
        cycles[0xD8] = 8;  // RET C not taken
        cycles[0xD9] = 16; // RETI
        cycles[0xDA] = 12; // JP C not taken
        cycles[0xDC] = 12; // CALL C not taken
        cycles[0xDE] = 8;  // SBC A,d8
        cycles[0xDF] = 16; // RST 18H

        cycles[0xE0] = 12; // LDH (a8),A
        cycles[0xE1] = 12; // POP HL
        cycles[0xE2] = 8;  // LD (C),A
        cycles[0xE5] = 16; // PUSH HL
        cycles[0xE6] = 8;  // AND d8
        cycles[0xE7] = 16; // RST 20H
        cycles[0xE8] = 16; // ADD SP,r8
        cycles[0xE9] = 4;  // JP HL
        cycles[0xEA] = 16; // LD (a16),A
        cycles[0xEE] = 8;  // XOR d8
        cycles[0xEF] = 16; // RST 28H

        cycles[0xF0] = 12; // LDH A,(a8)
        cycles[0xF1] = 12; // POP AF
        cycles[0xF2] = 8;  // LD A,(C)
        cycles[0xF5] = 16; // PUSH AF
        cycles[0xF6] = 8;  // OR d8
        cycles[0xF7] = 16; // RST 30H
        cycles[0xF8] = 12; // LD HL,SP+r8
        cycles[0xF9] = 8;  // LD SP,HL
        cycles[0xFA] = 16; // LD A,(a16)
        cycles[0xFE] = 8;  // CP d8
        cycles[0xFF] = 16; // RST 38H

        // Skip illegal opcodes and the CB prefix.
        var illegal = new HashSet<int> { 0xCB, 0xD3, 0xDB, 0xDD, 0xE3, 0xE4, 0xEB, 0xEC, 0xED, 0xF4, 0xFC, 0xFD };

        for (int op = 0; op < 256; op++)
        {
            if (illegal.Contains(op)) continue;
            if (cycles[op] == 0)
                throw new InvalidOperationException($"Missing cycle entry for opcode 0x{op:X2}");
            data.Add((byte)op, cycles[op]);
        }

        return data;
    }
}
