using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rlca()
    {
        var carry = (Ra & 0x80) != 0;
        Ra = (byte)((Ra << 1) | (carry ? 1 : 0));
        _flags = carry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rla()
    {
        var newCarry = (Ra & 0x80) != 0;
        var oldCarry = (Flags & CpuFlags.C) != 0;
        Ra = (byte)((Ra << 1) | (oldCarry ? 1 : 0));
        _flags = newCarry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rrca()
    {
        var carry = (Ra & 0x01) != 0;
        Ra = (byte)((Ra >> 1) | (carry ? 0x80 : 0));
        _flags = carry ? CpuFlags.C : CpuFlags.None;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rra()
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
        var flags = Flags;
        var h = (flags & CpuFlags.H) != 0;
        var c = (flags & CpuFlags.C) != 0;
        byte correction = 0;
        var setC = c;

        if ((flags & CpuFlags.N) == 0)
        {
            if (h || (Ra & 0x0F) > 9) correction |= 0x06;
            if (c || Ra > 0x99) { correction |= 0x60; setC = true; }
            Ra = (byte)(Ra + correction);
        }
        else
        {
            if (h) correction |= 0x06;
            if (c) correction |= 0x60;
            Ra = (byte)(Ra - correction);
        }

        SetZ(Ra);
        SetH(false);
        SetC(setC);
        return 4;
    }
}
