using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

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
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01); // IE: VBlank
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01); // IF: VBlank

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = true;

        var cycles = Cpu.Step();

        Assert.Equal(20, cycles);
    }
}
