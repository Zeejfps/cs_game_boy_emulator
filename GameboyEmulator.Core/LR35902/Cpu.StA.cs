using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int StA()
    {
        var address = FetchWord();
        _mmu.Write(address, Ra);
        return 13;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int StAb()
    {
        _mmu.Write(Rbc, Ra);
        return 7;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int StAd()
    {
        _mmu.Write(Rde, Ra);
        return 7;
    }
}