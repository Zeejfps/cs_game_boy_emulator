using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBcNn()
    {
        Rbc = FetchWord();
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDeNn()
    {
        Rde = FetchWord();
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHlNn()
    {
        Rhl = FetchWord();
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdSpNn()
    {
        Sp = FetchWord();
        return 12;
    }
}
