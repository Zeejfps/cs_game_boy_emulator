using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddN() { Add(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubN() { Sub(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndN() { And(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrN() { Or(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcN() { Adc(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcN() { Sbc(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorN() { Xor(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpN() { Cp(Fetch()); return 8; }
}
