using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviB()
    {
        Rb = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviC()
    {
        Rc = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviD()
    {
        Rd = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviE()
    {
        Re = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviH()
    {
        Rh = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviL()
    {
        Rl = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviA()
    {
        Ra = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MviM()
    {
        var value = Fetch();
        _mmu.Write(Rhl, value);
        return 12;
    }
}
