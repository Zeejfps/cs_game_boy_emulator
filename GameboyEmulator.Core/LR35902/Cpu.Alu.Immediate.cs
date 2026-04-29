using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddN() { Add(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubN() { Sub(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AndN() { Ana(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int OrN() { Ora(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcN() { Adc(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcN() { Sbb(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int XorN() { Xra(Fetch()); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CpN() { Cmp(Fetch()); return 8; }
}
