using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetNz()
    {
        if ((Flags & CpuFlags.Z) != 0)
            return 8;

        Pc = _mmu.ReadWord(Sp);
        Sp += 2;
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetNc()
    {
        if ((Flags & CpuFlags.C) != 0)
            return 8;

        Pc = _mmu.ReadWord(Sp);
        Sp += 2;
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetZ()
    {
        if ((Flags & CpuFlags.Z) == 0)
            return 8;

        Pc = _mmu.ReadWord(Sp);
        Sp += 2;
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RetC()
    {
        if ((Flags & CpuFlags.C) == 0)
            return 8;

        Pc = _mmu.ReadWord(Sp);
        Sp += 2;
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpNz()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) != 0)
            return 12;
        Pc = address;
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpNc()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) != 0)
            return 12;
        Pc = address;
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpZ()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) == 0)
            return 12;
        Pc = address;
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JpC()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) == 0)
            return 12;
        Pc = address;
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ret()
    {
        Pc = _mmu.ReadWord(Sp);
        Sp += 2;
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
        Sp -= 2;
        _mmu.WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Jp()
    {
        Pc = FetchWord();
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallNz()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) != 0)
            return 12;
        Sp -= 2;
        _mmu.WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallNc()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) != 0)
            return 12;
        Sp -= 2;
        _mmu.WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallZ()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.Z) == 0)
            return 12;
        Sp -= 2;
        _mmu.WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CallC()
    {
        var address = FetchWord();
        if ((Flags & CpuFlags.C) == 0)
            return 12;
        Sp -= 2;
        _mmu.WriteWord(Sp, Pc);
        Pc = address;
        return 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rst(ushort vector)
    {
        Sp -= 2;
        _mmu.WriteWord(Sp, Pc);
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
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrNz()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.Z) != 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrZ()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.Z) == 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrNc()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.C) != 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int JrC()
    {
        var offset = (sbyte)Fetch();
        if ((Flags & CpuFlags.C) == 0)
            return 8;
        Pc = (ushort)(Pc + offset);
        return 12;
    }
}
