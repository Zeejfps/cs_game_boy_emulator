using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxB() { Rbc = (ushort)(Rbc + 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxD() { Rde = (ushort)(Rde + 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxH() { Rhl = (ushort)(Rhl + 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InxSp() { Sp = (ushort)(Sp + 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxB() { Rbc = (ushort)(Rbc - 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxD() { Rde = (ushort)(Rde - 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxH() { Rhl = (ushort)(Rhl - 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcxSp() { Sp = (ushort)(Sp - 1); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Dad(ushort value) { AddHL(value); return 10; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DadB() => Dad(Rbc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DadD() => Dad(Rde);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DadH() => Dad(Rhl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DadSp() => Dad(Sp);
}
