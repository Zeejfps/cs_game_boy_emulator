using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// 8-pixel BG/window FIFO. MSB end is the next pixel popped; pixels are stored
// as two bitplanes (Low/High) like the underlying tile data. Attribute holds
// the CGB BG attribute byte (palette index in bits 0..2, priority bit in 7);
// all 8 pixels in a FIFO load share the same attribute because the BG fetcher
// only pushes when the FIFO is empty.
internal struct BgFifo
{
    public byte Low;
    public byte High;
    public byte Attribute;
    public byte Count;

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Count == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(byte low, byte high, byte attribute)
    {
        Low = low;
        High = high;
        Attribute = attribute;
        Count = 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Pop()
    {
        var pixel = (((High >> 7) & 1) << 1) | ((Low >> 7) & 1);
        DropOne();
        return pixel;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DropOne()
    {
        Low = (byte)(Low << 1);
        High = (byte)(High << 1);
        Count--;
    }
}
