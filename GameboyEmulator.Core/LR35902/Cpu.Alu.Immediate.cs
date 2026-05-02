using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddN() => Add(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubN() => Sub(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AndN() => And(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrN() => Or(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcN() => Adc(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcN() => Sbc(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void XorN() => Xor(Fetch());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CpN() => Cp(Fetch());
}
