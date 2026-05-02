using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void And(byte value)
    {
        var result = (byte)(Ra & value);
        SetFlags(result, n: false, h: true, c: false);
        Ra = result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Xor(byte value)
    {
        var result = (byte)(Ra ^ value);
        SetFlags(result, n: false, h: false, c: false);
        Ra = result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndB() => And(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndC() => And(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndD() => And(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndE() => And(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndH() => And(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndL() => And(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndA() => And(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndM() => And(Read(Rhl));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorB() => Xor(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorC() => Xor(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorD() => Xor(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorE() => Xor(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorH() => Xor(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorL() => Xor(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorA() => Xor(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorM() => Xor(Read(Rhl));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Or(byte value)
    {
        var result = (byte)(Ra | value);
        SetFlags(result, n: false, h: false, c: false);
        Ra = result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrB() => Or(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrC() => Or(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrD() => Or(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrE() => Or(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrH() => Or(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrL() => Or(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrA() => Or(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrM() => Or(Read(Rhl));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cp(byte value)
    {
        Sub8(Ra, value, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpB() => Cp(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpC() => Cp(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpD() => Cp(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpE() => Cp(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpH() => Cp(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpL() => Cp(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpA() => Cp(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpM() => Cp(Read(Rhl));
}
