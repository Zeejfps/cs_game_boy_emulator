using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

public class CpuInterruptTests : CpuTestBase
{
    [Theory]
    [InlineData(0x01, 0x0040)] // VBlank
    [InlineData(0x02, 0x0048)] // LCD STAT
    [InlineData(0x04, 0x0050)] // Timer
    [InlineData(0x08, 0x0058)] // Serial
    [InlineData(0x10, 0x0060)] // Joypad
    public void DispatchJumpsToVectorAndClearsBit(byte bit, ushort vector)
    {
        ushort start = 0x0200;
        ushort sp = 0x4000;
        Mmu.Write(start, 0x00); // NOP (unreached)
        Mmu.Write(IoRegisters.InterruptEnableAddress, bit);
        Mmu.Write(IoRegisters.InterruptFlagAddress, bit);

        Cpu.WriteState(new CpuState { Pc = start, Sp = sp });
        Cpu.InterruptMasterEnable = true;

        var cycles = Cpu.Step();

        Assert.Equal(20, cycles);
        Assert.Equal(vector, Cpu.Pc);
        Assert.False(Cpu.InterruptMasterEnable);
        Assert.Equal(0x00, Mmu.Read(IoRegisters.InterruptFlagAddress));
        Assert.Equal((ushort)(sp - 2), Cpu.Sp);
        Assert.Equal(start, Mmu.ReadWord(Cpu.Sp));
    }

    [Fact]
    public void PriorityLowestBitWins()
    {
        // VBlank (bit 0) and Timer (bit 2) both pending → VBlank wins.
        ushort start = 0x0200;
        Mmu.Write(start, 0x00);
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x05); // IE: VBlank | Timer
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x05);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = true;

        Cpu.Step();

        Assert.Equal(0x0040, Cpu.Pc);
        // Only the serviced bit (VBlank) is cleared; Timer remains asserted.
        Assert.Equal(0x04, Mmu.Read(IoRegisters.InterruptFlagAddress));
    }

    [Fact]
    public void NoDispatchWhenImeFalse()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0x3C); // INC A
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x10 });
        Cpu.InterruptMasterEnable = false;

        var cycles = Cpu.Step();

        Assert.Equal(4, cycles);
        Assert.Equal(0x11, Cpu.Ra);
        Assert.Equal((ushort)(start + 1), Cpu.Pc);
        Assert.Equal(0x01, Mmu.Read(IoRegisters.InterruptFlagAddress)); // unchanged
    }

    [Fact]
    public void NoDispatchWhenNoOverlap()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0x3C); // INC A
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01); // IE VBlank
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x02); // IF LCD STAT — no overlap

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x10 });
        Cpu.InterruptMasterEnable = true;

        var cycles = Cpu.Step();

        Assert.Equal(4, cycles);
        Assert.Equal(0x11, Cpu.Ra);
        Assert.Equal((ushort)(start + 1), Cpu.Pc);
    }

    [Fact]
    public void EiHasOneInstructionDelay()
    {
        // Snapshot: EI sets the timer to 2; EI's own step decrements 2→1;
        // the next instruction's step decrements 1→0 and sets IME — so the
        // *third* fetch is the first that sees IME=1.
        ushort start = 0x0200;
        Mmu.Write(start, 0xFB);     // EI
        Mmu.Write((ushort)(start + 1), 0x00); // NOP
        Mmu.Write((ushort)(start + 2), 0x00); // NOP

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = false;

        Cpu.Step(); // EI
        Assert.False(Cpu.InterruptMasterEnable);

        Cpu.Step(); // First NOP — IME flips at end of step
        Assert.True(Cpu.InterruptMasterEnable);
    }

    [Fact]
    public void InstructionAfterEiIsNotPreempted()
    {
        // The externally observable EI delay: the instruction immediately
        // after EI must execute, even if an interrupt is already pending.
        ushort start = 0x0200;
        Mmu.Write(start, 0xFB);              // EI
        Mmu.Write((ushort)(start + 1), 0x3C); // INC A
        Mmu.Write((ushort)(start + 2), 0x00); // NOP
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x00 });
        Cpu.InterruptMasterEnable = false;

        Cpu.Step(); // EI itself (IME was false → no dispatch)
        Assert.Equal((ushort)(start + 1), Cpu.Pc);

        Cpu.Step(); // INC A must run, not be pre-empted
        Assert.Equal(0x01, Cpu.Ra);
        Assert.Equal((ushort)(start + 2), Cpu.Pc);

        Cpu.Step(); // Now dispatch fires
        Assert.Equal(0x0040, Cpu.Pc);
    }

    [Fact]
    public void DiCancelsPendingEi()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0xFB);     // EI
        Mmu.Write((ushort)(start + 1), 0xF3); // DI
        Mmu.Write((ushort)(start + 2), 0x00); // NOP

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = false;

        Cpu.Step(); // EI
        Cpu.Step(); // DI
        Cpu.Step(); // NOP

        Assert.False(Cpu.InterruptMasterEnable);
    }

    [Fact]
    public void RetiPopsPcAndEnablesImeImmediately()
    {
        ushort start = 0x0200;
        ushort sp = 0x4000;
        ushort target = 0x1234;
        Mmu.Write(start, 0xD9);
        Mmu.WriteWord(sp, target);

        Cpu.WriteState(new CpuState { Pc = start, Sp = sp });
        Cpu.InterruptMasterEnable = false;

        var cycles = Cpu.Step();

        Assert.Equal(16, cycles);
        Assert.Equal(target, Cpu.Pc);
        Assert.True(Cpu.InterruptMasterEnable);
        Assert.Equal((ushort)(sp + 2), Cpu.Sp);
    }

    [Fact]
    public void HaltImeOnePendingInterrupt()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0x76); // HALT
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        // IF = 0 at HALT time — pending request arrives from a peripheral later.

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = true;

        var cycles1 = Cpu.Step();
        Assert.Equal(4, cycles1);
        Assert.True(Cpu.IsWaitingForInterrupt);

        // Peripheral asserts VBlank.
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        var cycles2 = Cpu.Step();
        Assert.Equal(20, cycles2);
        Assert.Equal(0x0040, Cpu.Pc);
        Assert.False(Cpu.IsWaitingForInterrupt);
    }

    [Fact]
    public void HaltImeOffPendingInterruptTriggersHaltBug()
    {
        ushort start = 0x0100;
        Mmu.Write(start, 0x76);             // HALT at 0x100
        Mmu.Write((ushort)(start + 1), 0x3C); // INC A at 0x101
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x00 });
        Cpu.InterruptMasterEnable = false;

        Cpu.Step(); // HALT itself
        Assert.False(Cpu.IsWaitingForInterrupt);
        Assert.Equal((ushort)(start + 1), Cpu.Pc);
        Assert.Equal(0x00, Cpu.Ra);

        Cpu.Step(); // First INC A — bug fetch doesn't advance PC
        Assert.Equal(0x01, Cpu.Ra);
        Assert.Equal((ushort)(start + 1), Cpu.Pc);

        Cpu.Step(); // Second INC A — normal fetch this time
        Assert.Equal(0x02, Cpu.Ra);
        Assert.Equal((ushort)(start + 2), Cpu.Pc);
    }

    [Fact]
    public void HaltImeOffNoPendingSleepsThenWakesWithoutDispatch()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0x76);             // HALT
        Mmu.Write((ushort)(start + 1), 0x3C); // INC A

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x00 });
        Cpu.InterruptMasterEnable = false;

        var c1 = Cpu.Step(); // HALT
        Assert.Equal(4, c1);
        Assert.True(Cpu.IsWaitingForInterrupt);

        var c2 = Cpu.Step(); // idling
        Assert.Equal(4, c2);
        Assert.True(Cpu.IsWaitingForInterrupt);
        Assert.Equal((ushort)(start + 1), Cpu.Pc);

        // Pending interrupt enables: wakes and runs INC A *without* dispatching.
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        var c3 = Cpu.Step();
        Assert.False(Cpu.IsWaitingForInterrupt);
        Assert.Equal(4, c3); // INC A's normal cost, not 20
        Assert.Equal(0x01, Cpu.Ra);
        Assert.Equal((ushort)(start + 2), Cpu.Pc);
    }

    [Fact]
    public void StopParksCpuAndWakesOnJoypadIfBit()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0x10);
        Mmu.Write((ushort)(start + 1), 0x00); // padding
        Mmu.Write((ushort)(start + 2), 0x3C); // INC A

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x00 });
        Cpu.InterruptMasterEnable = false;

        var c1 = Cpu.Step();
        Assert.Equal(4, c1);
        Assert.True(Cpu.IsSleeping);
        Assert.Equal((ushort)(start + 2), Cpu.Pc);

        var c2 = Cpu.Step();
        Assert.Equal(4, c2);
        Assert.True(Cpu.IsSleeping);
        Assert.Equal((ushort)(start + 2), Cpu.Pc);

        // Set IF joypad bit to wake.
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x10);

        var c3 = Cpu.Step(); // wake step — clears Stopped, does not run INC A
        Assert.False(Cpu.IsSleeping);
        Assert.Equal(0x00, Cpu.Ra);
        Assert.Equal((ushort)(start + 2), Cpu.Pc);
        Assert.Equal(4, c3);

        var c4 = Cpu.Step(); // step after the wake — INC A runs
        Assert.Equal(0x01, Cpu.Ra);
        Assert.Equal((ushort)(start + 3), Cpu.Pc);
        Assert.Equal(4, c4);
    }

    [Fact]
    public void DiClearsImeAndReturns4()
    {
        ushort start = 0x0200;
        Mmu.Write(start, 0xF3);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = true;

        var cycles = Cpu.Step();

        Assert.Equal(4, cycles);
        Assert.False(Cpu.InterruptMasterEnable);
    }

    [Fact]
    public void ResetLeavesImeFalse()
    {
        Cpu.InterruptMasterEnable = true;
        Cpu.Reset();
        Assert.False(Cpu.InterruptMasterEnable);
    }

    // HALT bug interaction with a two-byte LD A,d8: the opcode byte 0x3E
    // is fetched twice, so the immediate operand reads as 0x3E (the LD
    // opcode itself), not the byte the programmer wrote next.
    [Fact]
    public void HaltBug_DoublesNextOpcodeByte_LdAImmediate()
    {
        ushort start = 0x0100;
        Mmu.Write(start, 0x76);             // HALT
        Mmu.Write((ushort)(start + 1), 0x3E); // LD A,d8 (opcode)
        Mmu.Write((ushort)(start + 2), 0x42); // intended immediate
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000, Ra = 0x00 });
        Cpu.InterruptMasterEnable = false;

        Cpu.Step(); // HALT — sets _haltBugPending
        Assert.False(Cpu.IsWaitingForInterrupt);
        Assert.Equal((ushort)(start + 1), Cpu.Pc);

        Cpu.Step(); // LD A,d8 — bug doubles the 0x3E byte
        Assert.Equal(0x3E, Cpu.Ra);             // not 0x42
        Assert.Equal((ushort)(start + 2), Cpu.Pc); // PC advanced by 1, not 2
    }

    // HALT bug × JP a16: the 0xC3 opcode byte is fetched twice, so the
    // low byte of the target ends up being 0xC3 instead of the intended
    // 0x34 — JP 0x1234 jumps to 0x34C3 instead.
    [Fact]
    public void HaltBug_DoublesNextOpcodeByte_BreaksJpNn()
    {
        ushort start = 0x0100;
        Mmu.Write(start, 0x76);             // HALT
        Mmu.Write((ushort)(start + 1), 0xC3); // JP a16
        Mmu.Write((ushort)(start + 2), 0x34); // low (intended)
        Mmu.Write((ushort)(start + 3), 0x12); // high (intended)
        Mmu.Write(IoRegisters.InterruptEnableAddress, 0x01);
        Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01);

        Cpu.WriteState(new CpuState { Pc = start, Sp = 0x4000 });
        Cpu.InterruptMasterEnable = false;

        Cpu.Step(); // HALT
        Assert.Equal((ushort)(start + 1), Cpu.Pc);

        Cpu.Step(); // JP — opcode byte doubled
        // Bug: PC stays at start+1 for the JP fetch, then operand low =
        // mem[start+1] = 0xC3, operand high = mem[start+2] = 0x34.
        Assert.Equal((ushort)0x34C3, Cpu.Pc);
    }
}
