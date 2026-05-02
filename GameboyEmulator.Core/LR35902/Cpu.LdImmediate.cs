using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBn()
    {
        Rb = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCn()
    {
        Rc = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDn()
    {
        Rd = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEn()
    {
        Re = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHn()
    {
        Rh = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLn()
    {
        Rl = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAn()
    {
        Ra = Fetch();
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMn()
    {
        var value = Fetch();
        Write(Rhl, value);
        return 12;
    }
}
