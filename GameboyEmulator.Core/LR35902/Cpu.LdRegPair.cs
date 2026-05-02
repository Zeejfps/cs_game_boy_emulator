using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBcNn() { Rbc = FetchWord(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDeNn() { Rde = FetchWord(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlNn() { Rhl = FetchWord(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdSpNn() { Sp = FetchWord(); }
}
