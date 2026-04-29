using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LxiB()
    {
        Rbc = FetchWord();
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LxiD()
    {
        Rde = FetchWord();
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LxiH()
    {
        Rhl = FetchWord();
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LxiSp()
    {
        Sp = FetchWord();
        return 12;
    }
}
