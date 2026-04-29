using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Di()
    {
        InterruptEnabled = false;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ei()
    {
        _enableInterruptsTimer = 2;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Stop()
    {
        throw new NotImplementedException("STOP — wired in step 5");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Reti()
    {
        throw new NotImplementedException("RETI — wired in step 5");
    }
}
