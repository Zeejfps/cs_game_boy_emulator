using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Di()
    {
        InterruptMasterEnable = false;
        // Cancel any pending EI delay — an immediate DI overrides it.
        _enableInterruptsTimer = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Ei()
    {
        // 2 produces exactly one full instruction of delay with our
        // Execute → UpdateInterruptTimer ordering: EI's own step decrements
        // 2→1, the next step decrements 1→0 and sets IME, so the *third*
        // fetch is the first one that sees IME=1.
        _enableInterruptsTimer = 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reti()
    {
        Pc = ReadWord(Sp);
        Sp += 2;
        InterruptMasterEnable = true;
        Tick(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Halt()
    {
        var pending = _interrupts.GetPending();
        if (InterruptMasterEnable)
        {
            IsWaitingForInterrupt = true;
        }
        else if (pending != InterruptType.None)
        {
            // HALT bug: don't halt, instead the next Fetch() reads the byte
            // after HALT without incrementing PC, so it gets read twice.
            _haltBugPending = true;
        }
        else
        {
            IsWaitingForInterrupt = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Stop()
    {
        // Per spec encoders write `10 00`, but real hardware ignores the
        // following byte — just consume it without inspecting.
        Fetch();
        IsSleeping = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ServicePendingInterrupt(InterruptType pending)
    {
        var serviced = GetHighestPriority(pending);
        _interrupts.Clear(serviced);
        InterruptMasterEnable = false;

        // 2 internal cycles before push (M1 + M2 of the 5-cycle dispatch).
        Tick(8);

        Sp -= 2;
        Write((ushort)(Sp + 1), (byte)(Pc >> 8));
        Write(Sp, (byte)(Pc & 0xFF));

        Pc = GetInterruptVector(serviced);
        // M5: vector-fetch / PC-update internal cycle.
        Tick(4);
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
