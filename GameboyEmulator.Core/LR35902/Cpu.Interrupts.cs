using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Di()
    {
        InterruptMasterEnable = false;
        // Cancel any pending EI delay — an immediate DI overrides it.
        _enableInterruptsTimer = 0;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ei()
    {
        // 2 produces exactly one full instruction of delay with our
        // Execute → UpdateInterruptTimer ordering: EI's own step decrements
        // 2→1, the next step decrements 1→0 and sets IME, so the *third*
        // fetch is the first one that sees IME=1.
        _enableInterruptsTimer = 2;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Reti()
    {
        Pc = _mmu.ReadWord(Sp);
        Sp += 2;
        InterruptMasterEnable = true;
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Hlt()
    {
        var pending = GetPendingInterrupts();
        if (InterruptMasterEnable)
        {
            Halted = true;
        }
        else if (pending != InterruptType.None)
        {
            // HALT bug: don't halt, instead the next Fetch() reads the byte
            // after HALT without incrementing PC, so it gets read twice.
            _haltBugPending = true;
        }
        else
        {
            Halted = true;
        }
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Stop()
    {
        // Per spec encoders write `10 00`, but real hardware ignores the
        // following byte — just consume it without inspecting.
        Fetch();
        Stopped = true;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ServicePendingInterrupt(InterruptType pending)
    {
        var serviced = GetHighestPriority(pending);
        ClearInterruptRequest(serviced);
        InterruptMasterEnable = false;

        Sp -= 2;
        _mmu.Write((ushort)(Sp + 1), (byte)(Pc >> 8));
        _mmu.Write(Sp, (byte)(Pc & 0xFF));

        Pc = GetInterruptVector(serviced);
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private InterruptType GetPendingInterrupts()
    {
        var ie = (InterruptType)_mmu.Read(IoRegisters.InterruptEnableAddress);
        var iff = (InterruptType)_mmu.Read(IoRegisters.InterruptFlagAddress);
        return ie & iff & InterruptType.All;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInterruptRequested(InterruptType interrupt)
    {
        var iff = (InterruptType)_mmu.Read(IoRegisters.InterruptFlagAddress);
        return (iff & interrupt) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearInterruptRequest(InterruptType interrupt)
    {
        var iff = (InterruptType)_mmu.Read(IoRegisters.InterruptFlagAddress);
        _mmu.Write(IoRegisters.InterruptFlagAddress, (byte)(iff & ~interrupt));
    }

    // Lowest bit wins: VBlank > LcdStat > Timer > Serial > Joypad.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static InterruptType GetHighestPriority(InterruptType pending)
    {
        if ((pending & InterruptType.VBlank)  != 0) return InterruptType.VBlank;
        if ((pending & InterruptType.LcdStat) != 0) return InterruptType.LcdStat;
        if ((pending & InterruptType.Timer)   != 0) return InterruptType.Timer;
        if ((pending & InterruptType.Serial)  != 0) return InterruptType.Serial;
        if ((pending & InterruptType.Joypad)  != 0) return InterruptType.Joypad;
        return InterruptType.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetInterruptVector(InterruptType interrupt) => interrupt switch
    {
        InterruptType.VBlank  => 0x40,
        InterruptType.LcdStat => 0x48,
        InterruptType.Timer   => 0x50,
        InterruptType.Serial  => 0x58,
        InterruptType.Joypad  => 0x60,
        _ => 0,
    };
}
