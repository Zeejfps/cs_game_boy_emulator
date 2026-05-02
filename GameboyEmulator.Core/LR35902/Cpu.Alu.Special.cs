using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rlca()
    {
        var carry = (Ra & 0x80) != 0;
        Ra = (byte)((Ra << 1) | (carry ? 1 : 0));
        _flags = carry ? CpuFlags.C : CpuFlags.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rla()
    {
        var newCarry = (Ra & 0x80) != 0;
        var oldCarry = (Flags & CpuFlags.C) != 0;
        Ra = (byte)((Ra << 1) | (oldCarry ? 1 : 0));
        _flags = newCarry ? CpuFlags.C : CpuFlags.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rrca()
    {
        var carry = (Ra & 0x01) != 0;
        Ra = (byte)((Ra >> 1) | (carry ? 0x80 : 0));
        _flags = carry ? CpuFlags.C : CpuFlags.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rra()
    {
        var newCarry = (Ra & 0x01) != 0;
        var oldCarry = (Flags & CpuFlags.C) != 0;
        Ra = (byte)((Ra >> 1) | (oldCarry ? 0x80 : 0));
        _flags = newCarry ? CpuFlags.C : CpuFlags.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cpl()
    {
        Ra = (byte)~Ra;
        SetN(true);
        SetH(true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Ccf()
    {
        SetN(false);
        SetH(false);
        _flags ^= CpuFlags.C;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Scf()
    {
        SetN(false);
        SetH(false);
        SetC(true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Daa()
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
    }
}
