using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    public const ushort InterruptFlagAddress = 0xFF0F;
    public const ushort InterruptEnableAddress = 0xFFFF;

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
    private int Halt()
    {
        var pending = GetPendingInterrupts();
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
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Stop()
    {
        // Per spec encoders write `10 00`, but real hardware ignores the
        // following byte — just consume it without inspecting.
        Fetch();
        IsSleeping = true;
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
        var ie = (InterruptType)_mmu.Read(InterruptEnableAddress);
        var iff = (InterruptType)_mmu.Read(InterruptFlagAddress);
        return ie & iff & InterruptType.All;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInterruptRequested(InterruptType interrupt)
    {
        var iff = (InterruptType)_mmu.Read(InterruptFlagAddress);
        return (iff & interrupt) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearInterruptRequest(InterruptType interrupt)
    {
        var iff = (InterruptType)_mmu.Read(InterruptFlagAddress);
        _mmu.Write(InterruptFlagAddress, (byte)(iff & ~interrupt));
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
