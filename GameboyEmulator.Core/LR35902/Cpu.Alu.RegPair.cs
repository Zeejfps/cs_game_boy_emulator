using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncBc() { Rbc = (ushort)(Rbc + 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncDe() { Rde = (ushort)(Rde + 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncHl() { Rhl = (ushort)(Rhl + 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncSp() { Sp = (ushort)(Sp + 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecBc() { Rbc = (ushort)(Rbc - 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecDe() { Rde = (ushort)(Rde - 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecHl() { Rhl = (ushort)(Rhl - 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecSp() { Sp = (ushort)(Sp - 1); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHl(ushort value) { AddHL(value); Tick(4); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlBc() => AddHl(Rbc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlDe() => AddHl(Rde);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlHl() => AddHl(Rhl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlSp() => AddHl(Sp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddSpR8()
    {
        var r8 = (sbyte)Fetch();
        Sp = AddSpSigned(r8);
        Tick(8);
        return 16;
    }
}
