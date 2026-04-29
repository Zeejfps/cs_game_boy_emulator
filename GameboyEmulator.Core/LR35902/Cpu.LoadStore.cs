using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAb()
    {
        Ra = _mmu.Read(Rbc);
        return 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAd()
    {
        Ra = _mmu.Read(Rde);
        return 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int StAb()
    {
        _mmu.Write(Rbc, Ra);
        return 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int StAd()
    {
        _mmu.Write(Rde, Ra);
        return 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHlIncA()
    {
        var hl = Rhl;
        _mmu.Write(hl, Ra);
        Rhl = (ushort)(hl + 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAHlInc()
    {
        var hl = Rhl;
        Ra = _mmu.Read(hl);
        Rhl = (ushort)(hl + 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHlDecA()
    {
        var hl = Rhl;
        _mmu.Write(hl, Ra);
        Rhl = (ushort)(hl - 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAHlDec()
    {
        var hl = Rhl;
        Ra = _mmu.Read(hl);
        Rhl = (ushort)(hl - 1);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdA16A()
    {
        var address = FetchWord();
        _mmu.Write(address, Ra);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAA16()
    {
        var address = FetchWord();
        Ra = _mmu.Read(address);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdhA8A()
    {
        var offset = Fetch();
        _mmu.Write((ushort)(0xFF00 + offset), Ra);
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdhAA8()
    {
        var offset = Fetch();
        Ra = _mmu.Read((ushort)(0xFF00 + offset));
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCA()
    {
        _mmu.Write((ushort)(0xFF00 + Rc), Ra);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAC()
    {
        Ra = _mmu.Read((ushort)(0xFF00 + Rc));
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdA16Sp()
    {
        var address = FetchWord();
        _mmu.WriteWord(address, Sp);
        return 20;
    }
}
