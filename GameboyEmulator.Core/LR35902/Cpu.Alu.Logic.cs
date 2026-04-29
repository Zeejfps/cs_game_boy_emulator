using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ana(byte value)
    {
        var result = (byte)(Ra & value);
        SetFlags(result, n: false, h: true, c: false);
        Ra = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Xra(byte value)
    {
        var result = (byte)(Ra ^ value);
        SetFlags(result, n: false, h: false, c: false);
        Ra = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaB() => Ana(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaC() => Ana(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaD() => Ana(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaE() => Ana(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaH() => Ana(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaL() => Ana(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaA() => Ana(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AnaM() { Ana(_mmu.Read(Rhl)); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraB() => Xra(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraC() => Xra(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraD() => Xra(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraE() => Xra(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraH() => Xra(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraL() => Xra(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraA() => Xra(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XraM() { Xra(_mmu.Read(Rhl)); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ora(byte value)
    {
        var result = (byte)(Ra | value);
        SetFlags(result, n: false, h: false, c: false);
        Ra = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraB() => Ora(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraC() => Ora(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraD() => Ora(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraE() => Ora(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraH() => Ora(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraL() => Ora(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraA() => Ora(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OraM() { Ora(_mmu.Read(Rhl)); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Cmp(byte value)
    {
        Sub8(Ra, value, false);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpB() => Cmp(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpC() => Cmp(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpD() => Cmp(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpE() => Cmp(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpH() => Cmp(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpL() => Cmp(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpA() => Cmp(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CmpM() { Cmp(_mmu.Read(Rhl)); return 8; }
}
