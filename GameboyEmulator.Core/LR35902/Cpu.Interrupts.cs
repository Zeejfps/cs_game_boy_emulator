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
        //
        // Only arm if not already armed: a chain of EIs must let the
        // first EI's pending fire after the second EI completes, rather
        // than each EI resetting the countdown. Mooneye's ei_sequence
        // verifies this.
        if (_enableInterruptsTimer == 0 && !InterruptMasterEnable)
            _enableInterruptsTimer = 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reti()
    {
        Pc = ReadWordFromBus(Sp);
        Sp += 2;
        InterruptMasterEnable = true;
        AdvanceClock(4);
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
        // following byte — advance PC past it without a bus access (STOP is
        // 4 T-cycles total; only the opcode fetch ticks).
        Pc++;

        // CGB speed switch: if the game armed KEY1 bit 0 ("prepare switch")
        // before STOP, the CPU flips its current-speed bit (7) and clears
        // the prepare bit instead of actually stopping. The bus-domain clock
        // is notified so PPU/APU start running at half rate (or back to full
        // when switching down).
        if (_isCgb && (_key1 & 0x01) != 0)
        {
            _key1 = (byte)((_key1 ^ 0x80) & 0xFE);
            _systemClock.SetDoubleSpeed((_key1 & 0x80) != 0);
            return;
        }

        IsSleeping = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ServicePendingInterrupt()
    {
        InterruptMasterEnable = false;

        // 2 internal cycles before push (M1 + M2 of the 5-cycle dispatch).
        AdvanceClock(8);

        // M3: high-byte push. If SP was $0000, this lands at $FFFF and
        // mutates IE — and the *post-write* IE is what hardware uses to
        // re-pick the vector. Mooneye's ie_push verifies this: an IE
        // write here can cancel dispatch entirely (PC forced to $0000,
        // IF untouched) or redirect to a different vector.
        Sp -= 2;
        WriteToBus((ushort)(Sp + 1), (byte)(Pc >> 8));

        var serviced = GetHighestPriority(_interrupts.GetPending());

        // M4: low-byte push happens unconditionally — even when the
        // vector decision was cancelled the dispatch hardware still
        // completes all 5 M-cycles. A write here landing on IE is too
        // late to influence which vector fires.
        WriteToBus(Sp, (byte)(Pc & 0xFF));

        if (serviced == InterruptType.None)
        {
            Pc = 0;
        }
        else
        {
            _interrupts.Clear(serviced);
            Pc = GetInterruptVector(serviced);
        }

        // M5: vector-fetch / PC-update internal cycle.
        AdvanceClock(4);
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
