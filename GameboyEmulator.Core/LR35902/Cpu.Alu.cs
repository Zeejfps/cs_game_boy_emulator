using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetZ(byte value)
    {
        if (value == 0) _flags |= CpuFlags.Z;
        else _flags &= ~CpuFlags.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetN(bool on)
    {
        if (on) _flags |= CpuFlags.N;
        else _flags &= ~CpuFlags.N;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetH(bool on)
    {
        if (on) _flags |= CpuFlags.H;
        else _flags &= ~CpuFlags.H;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetC(bool on)
    {
        if (on) _flags |= CpuFlags.C;
        else _flags &= ~CpuFlags.C;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlags(byte result, bool n, bool h, bool c)
    {
        var flags = CpuFlags.None;
        if (result == 0) flags |= CpuFlags.Z;
        if (n) flags |= CpuFlags.N;
        if (h) flags |= CpuFlags.H;
        if (c) flags |= CpuFlags.C;
        _flags = flags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Add8(byte a, byte b, bool carryIn)
    {
        var cin = carryIn ? 1 : 0;
        var full = a + b + cin;
        var result = (byte)full;
        var h = ((a & 0xF) + (b & 0xF) + cin) > 0xF;
        var c = full > 0xFF;
        SetFlags(result, n: false, h: h, c: c);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Sub8(byte a, byte b, bool carryIn)
    {
        var cin = carryIn ? 1 : 0;
        var full = a - b - cin;
        var result = (byte)full;
        var h = ((a & 0xF) - (b & 0xF) - cin) < 0;
        var c = full < 0;
        SetFlags(result, n: true, h: h, c: c);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHL(ushort rr)
    {
        var hl = Rhl;
        var full = hl + rr;
        var h = ((hl & 0x0FFF) + (rr & 0x0FFF)) > 0x0FFF;
        var c = full > 0xFFFF;
        Rhl = (ushort)full;
        SetN(false);
        SetH(h);
        SetC(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddSpSigned(sbyte r8)
    {
        var sp = Sp;
        var rhs = (byte)r8;
        var h = ((sp & 0x0F) + (rhs & 0x0F)) > 0x0F;
        var c = ((sp & 0xFF) + (rhs & 0xFF)) > 0xFF;
        var result = (ushort)(sp + r8);
        _flags = CpuFlags.None;
        if (h) _flags |= CpuFlags.H;
        if (c) _flags |= CpuFlags.C;
        return result;
    }
}
