using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Lhld()
    {
        var address = FetchWord();
        Rhl = _mmu.ReadWord(address);
        return 16;
    }
}
