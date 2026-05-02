using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetNz()
    {
        Tick(4);
        if ((Flags & CpuFlags.Z) != 0)
            return 8;

        Pc = ReadWord(Sp);
        Sp += 2;
        Tick(4);
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetNc()
    {
        Tick(4);
        if ((Flags & CpuFlags.C) != 0)
            return 8;

        Pc = ReadWord(Sp);
        Sp += 2;
        Tick(4);
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetZ()
    {
        Tick(4);
        if ((Flags & CpuFlags.Z) == 0)
            return 8;

        Pc = ReadWord(Sp);
        Sp += 2;
        Tick(4);
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetC()
    {
        Tick(4);
        if ((Flags & CpuFlags.C) == 0)
            return 8;

        Pc = ReadWord(Sp);
        Sp += 2;
        Tick(4);
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpNz()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) != 0)
            return 12;
        Pc = address;
        Tick(4);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpNc()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) != 0)
            return 12;
        Pc = address;
        Tick(4);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpZ()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) == 0)
            return 12;
        Pc = address;
        Tick(4);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpC()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) == 0)
            return 12;
        Pc = address;
        Tick(4);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ret()
    {
        Pc = ReadWord(Sp);
        Sp += 2;
        Tick(4);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpHl()
    {
        Pc = Rhl;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Call()
    {
        var address = FetchWord();
        Tick(4);
        Sp -= 2;
        WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Jp()
    {
        Pc = FetchWord();
        Tick(4);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallNz()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) != 0)
            return 12;
        Tick(4);
        Sp -= 2;
        WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallNc()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) != 0)
            return 12;
        Tick(4);
        Sp -= 2;
        WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallZ()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) == 0)
            return 12;
        Tick(4);
        Sp -= 2;
        WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallC()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) == 0)
            return 12;
        Tick(4);
        Sp -= 2;
        WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst(ushort vector)
    {
        Tick(4);
        Sp -= 2;
        WriteWord(Sp, Pc);
        Pc = vector;
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst0() => Rst(0x0000);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst1() => Rst(0x0008);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst2() => Rst(0x0010);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst3() => Rst(0x0018);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst4() => Rst(0x0020);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst5() => Rst(0x0028);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst6() => Rst(0x0030);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst7() => Rst(0x0038);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Jr()
    {
        var offset = (sbyte)Fetch();
        Pc = (ushort)(Pc + offset);
        Tick(4);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrNz()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.Z) != 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        Tick(4);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrZ()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.Z) == 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        Tick(4);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrNc()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.C) != 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        Tick(4);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrC()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.C) == 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        Tick(4);
        return 12;
    }
}
