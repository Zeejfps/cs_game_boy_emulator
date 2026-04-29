using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rlc()
    {
        var carry = (Ra & 0x80) != 0;
        Ra = (byte)((Ra << 1) | (carry ? 1 : 0));
        _flags = carry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ral()
    {
        var newCarry = (Ra & 0x80) != 0;
        var oldCarry = (Flags & CpuFlags.C) != 0;
        Ra = (byte)((Ra << 1) | (oldCarry ? 1 : 0));
        _flags = newCarry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rrc()
    {
        var carry = (Ra & 0x01) != 0;
        Ra = (byte)((Ra >> 1) | (carry ? 0x80 : 0));
        _flags = carry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rar()
    {
        var newCarry = (Ra & 0x01) != 0;
        var oldCarry = (Flags & CpuFlags.C) != 0;
        Ra = (byte)((Ra >> 1) | (oldCarry ? 0x80 : 0));
        _flags = newCarry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Cpl()
    {
        Ra = (byte)~Ra;
        SetN(true);
        SetH(true);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Ccf()
    {
        SetN(false);
        SetH(false);
        _flags ^= CpuFlags.C;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Scf()
    {
        SetN(false);
        SetH(false);
        SetC(true);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Daa()
    {
        throw new NotImplementedException("DAA rewrites in step 6");
    }
}
