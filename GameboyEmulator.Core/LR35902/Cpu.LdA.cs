using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu 
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAb()
    {
        Ra = _mmu.Read(Rbc);
        return 7;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAd()
    {
        Ra = _mmu.Read(Rde);
        return 7;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdA()
    {
        var address = FetchWord();
        Ra = _mmu.Read(address);
        return 13;
    }
}