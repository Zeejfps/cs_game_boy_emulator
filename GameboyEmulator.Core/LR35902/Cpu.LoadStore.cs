using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdABc()
    {
        Ra = Read(Rbc);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdADe()
    {
        Ra = Read(Rde);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBcA()
    {
        Write(Rbc, Ra);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDeA()
    {
        Write(Rde, Ra);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHlIncA()
    {
        var hl = Rhl;
        Write(hl, Ra);
        Rhl = (ushort)(hl + 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAHlInc()
    {
        var hl = Rhl;
        Ra = Read(hl);
        Rhl = (ushort)(hl + 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHlDecA()
    {
        var hl = Rhl;
        Write(hl, Ra);
        Rhl = (ushort)(hl - 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAHlDec()
    {
        var hl = Rhl;
        Ra = Read(hl);
        Rhl = (ushort)(hl - 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdA16A()
    {
        var address = FetchWord();
        Write(address, Ra);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAA16()
    {
        var address = FetchWord();
        Ra = Read(address);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdhA8A()
    {
        var offset = Fetch();
        Write((ushort)(0xFF00 + offset), Ra);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdhAA8()
    {
        var offset = Fetch();
        Ra = Read((ushort)(0xFF00 + offset));
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCA()
    {
        Write((ushort)(0xFF00 + Rc), Ra);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAC()
    {
        Ra = Read((ushort)(0xFF00 + Rc));
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdA16Sp()
    {
        var address = FetchWord();
        WriteWord(address, Sp);
        return 20;
    }
}
