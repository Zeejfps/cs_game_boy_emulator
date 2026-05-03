using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBn() { Rb = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCn() { Rc = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDn() { Rd = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEn() { Re = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHn() { Rh = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLn() { Rl = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAn() { Ra = Fetch(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMn()
    {
        var value = Fetch();
        WriteToBus(Rhl, value);
    }
}
