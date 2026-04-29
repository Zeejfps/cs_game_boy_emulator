using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int And(byte value)
    {
        var result = (byte)(Ra & value);
        SetFlags(result, n: false, h: true, c: false);
        Ra = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Xor(byte value)
    {
        var result = (byte)(Ra ^ value);
        SetFlags(result, n: false, h: false, c: false);
        Ra = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndB() => And(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndC() => And(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndD() => And(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndE() => And(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndH() => And(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndL() => And(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndA() => And(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndM() { And(_mmu.Read(Rhl)); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorB() => Xor(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorC() => Xor(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorD() => Xor(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorE() => Xor(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorH() => Xor(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorL() => Xor(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorA() => Xor(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorM() { Xor(_mmu.Read(Rhl)); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Or(byte value)
    {
        var result = (byte)(Ra | value);
        SetFlags(result, n: false, h: false, c: false);
        Ra = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrB() => Or(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrC() => Or(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrD() => Or(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrE() => Or(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrH() => Or(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrL() => Or(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrA() => Or(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrM() { Or(_mmu.Read(Rhl)); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Cp(byte value)
    {
        Sub8(Ra, value, false);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpB() => Cp(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpC() => Cp(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpD() => Cp(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpE() => Cp(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpH() => Cp(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpL() => Cp(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpA() => Cp(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpM() { Cp(_mmu.Read(Rhl)); return 8; }
}
