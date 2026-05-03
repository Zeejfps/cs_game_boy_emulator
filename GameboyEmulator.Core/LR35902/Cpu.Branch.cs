using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RetNz()
    {
        AdvanceClock(4);
        if ((Flags & CpuFlags.Z) != 0)
            return;

        Pc = ReadWordFromBus(Sp);
        Sp += 2;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RetNc()
    {
        AdvanceClock(4);
        if ((Flags & CpuFlags.C) != 0)
            return;

        Pc = ReadWordFromBus(Sp);
        Sp += 2;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RetZ()
    {
        AdvanceClock(4);
        if ((Flags & CpuFlags.Z) == 0)
            return;

        Pc = ReadWordFromBus(Sp);
        Sp += 2;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RetC()
    {
        AdvanceClock(4);
        if ((Flags & CpuFlags.C) == 0)
            return;

        Pc = ReadWordFromBus(Sp);
        Sp += 2;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JpNz()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) != 0)
            return;
        Pc = address;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JpNc()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) != 0)
            return;
        Pc = address;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JpZ()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) == 0)
            return;
        Pc = address;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JpC()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) == 0)
            return;
        Pc = address;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Ret()
    {
        Pc = ReadWordFromBus(Sp);
        Sp += 2;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JpHl()
    {
        Pc = Rhl;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Call()
    {
        var address = FetchWord();
        AdvanceClock(4);
        Sp -= 2;
        PushPcToStack();
        Pc = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Jp()
    {
        Pc = FetchWord();
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CallNz()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) != 0)
            return;
        AdvanceClock(4);
        Sp -= 2;
        PushPcToStack();
        Pc = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CallNc()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) != 0)
            return;
        AdvanceClock(4);
        Sp -= 2;
        PushPcToStack();
        Pc = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CallZ()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) == 0)
            return;
        AdvanceClock(4);
        Sp -= 2;
        PushPcToStack();
        Pc = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CallC()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) == 0)
            return;
        AdvanceClock(4);
        Sp -= 2;
        PushPcToStack();
        Pc = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst(ushort vector)
    {
        AdvanceClock(4);
        Sp -= 2;
        PushPcToStack();
        Pc = vector;
    }

    // Hardware pushes the high byte first (to SP+1) on its first push
    // M-cycle, then the low byte (to SP) on the next. Mooneye's rst_timing
    // observes which of the two writes lands during DMA's last transfer
    // cycle, so the byte order matters — not just the total cycle count.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushPcToStack()
    {
        WriteToBus((ushort)(Sp + 1), (byte)(Pc >> 8));
        WriteToBus(Sp, (byte)(Pc & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst0() => Rst(0x0000);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst1() => Rst(0x0008);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst2() => Rst(0x0010);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst3() => Rst(0x0018);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst4() => Rst(0x0020);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst5() => Rst(0x0028);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst6() => Rst(0x0030);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rst7() => Rst(0x0038);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Jr()
    {
        var offset = (sbyte)Fetch();
        Pc = (ushort)(Pc + offset);
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JrNz()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.Z) != 0)
            return;
        Pc = (ushort)(Pc + offset);
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JrZ()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.Z) == 0)
            return;
        Pc = (ushort)(Pc + offset);
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JrNc()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.C) != 0)
            return;
        Pc = (ushort)(Pc + offset);
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void JrC()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.C) == 0)
            return;
        Pc = (ushort)(Pc + offset);
        AdvanceClock(4);
    }
}
