using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Adi() { Add(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Sui() { Sub(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ani() { Ana(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ori() { Ora(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Aci() { Adc(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Sbi() { Sbb(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Xri() { Xra(Fetch()); return 7; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Cpi() { Cmp(Fetch()); return 7; }
}
