using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int CbPrefix()
    {
        var sub = Fetch();
        var op = sub >> 3;
        var operand = sub & 7;

        var value = ReadCbOperand(operand);

        switch (op)
        {
            case < 8:
            {
                var result = op switch
                {
                    0 => CbRlc(value),
                    1 => CbRrc(value),
                    2 => CbRl(value),
                    3 => CbRr(value),
                    4 => CbSla(value),
                    5 => CbSra(value),
                    6 => CbSwap(value),
                    _ => CbSrl(value),
                };
                WriteCbOperand(operand, result);
                return operand == 6 ? 16 : 8;
            }
            case < 16:
                CbBit(value, op - 8);
                return operand == 6 ? 12 : 8;
            case < 24:
                WriteCbOperand(operand, CbRes(value, op - 16));
                return operand == 6 ? 16 : 8;
            default:
                WriteCbOperand(operand, CbSet(value, op - 24));
                return operand == 6 ? 16 : 8;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadCbOperand(int operand) => operand switch
    {
        0 => Rb,
        1 => Rc,
        2 => Rd,
        3 => Re,
        4 => Rh,
        5 => Rl,
        6 => _mmu.Read(Rhl),
        _ => Ra,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteCbOperand(int operand, byte value)
    {
        switch (operand)
        {
            case 0: Rb = value; break;
            case 1: Rc = value; break;
            case 2: Rd = value; break;
            case 3: Re = value; break;
            case 4: Rh = value; break;
            case 5: Rl = value; break;
            case 6: _mmu.Write(Rhl, value); break;
            default: Ra = value; break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbRlc(byte v)
    {
        var c = (v & 0x80) != 0;
        var r = (byte)((v << 1) | (c ? 1 : 0));
        SetFlags(r, n: false, h: false, c: c);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbRrc(byte v)
    {
        var c = (v & 0x01) != 0;
        var r = (byte)((v >> 1) | (c ? 0x80 : 0));
        SetFlags(r, n: false, h: false, c: c);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbRl(byte v)
    {
        var oldC = (_flags & CpuFlags.C) != 0;
        var newC = (v & 0x80) != 0;
        var r = (byte)((v << 1) | (oldC ? 1 : 0));
        SetFlags(r, n: false, h: false, c: newC);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbRr(byte v)
    {
        var oldC = (_flags & CpuFlags.C) != 0;
        var newC = (v & 0x01) != 0;
        var r = (byte)((v >> 1) | (oldC ? 0x80 : 0));
        SetFlags(r, n: false, h: false, c: newC);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbSla(byte v)
    {
        var c = (v & 0x80) != 0;
        var r = (byte)(v << 1);
        SetFlags(r, n: false, h: false, c: c);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbSra(byte v)
    {
        var c = (v & 0x01) != 0;
        var r = (byte)((v >> 1) | (v & 0x80));
        SetFlags(r, n: false, h: false, c: c);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbSwap(byte v)
    {
        var r = (byte)((v >> 4) | (v << 4));
        SetFlags(r, n: false, h: false, c: false);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte CbSrl(byte v)
    {
        var c = (v & 0x01) != 0;
        var r = (byte)(v >> 1);
        SetFlags(r, n: false, h: false, c: c);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CbBit(byte v, int bit)
    {
        var isSet = (v & (1 << bit)) != 0;
        if (isSet) _flags &= ~CpuFlags.Z;
        else _flags |= CpuFlags.Z;
        _flags &= ~CpuFlags.N;
        _flags |= CpuFlags.H;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CbRes(byte v, int bit) => (byte)(v & ~(1 << bit));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CbSet(byte v, int bit) => (byte)(v | (1 << bit));
}
