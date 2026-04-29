using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxB() { Rbc = (ushort)(Rbc + 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxD() { Rde = (ushort)(Rde + 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxH() { Rhl = (ushort)(Rhl + 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxSp() { Sp = (ushort)(Sp + 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxB() { Rbc = (ushort)(Rbc - 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxD() { Rde = (ushort)(Rde - 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxH() { Rhl = (ushort)(Rhl - 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxSp() { Sp = (ushort)(Sp - 1); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHl(ushort value) { AddHL(value); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlB() => AddHl(Rbc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlD() => AddHl(Rde);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlH() => AddHl(Rhl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddHlSp() => AddHl(Sp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddSpR8()
    {
        var r8 = (sbyte)Fetch();
        Sp = AddSpSigned(r8);
        return 16;
    }
}
