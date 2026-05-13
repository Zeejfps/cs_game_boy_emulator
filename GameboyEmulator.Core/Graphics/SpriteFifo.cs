using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// 8-pixel sprite FIFO. Color stored as two bitplanes (Low/High); bg-priority
// is one bit per slot. Palette is 3 bits per slot stored as three bitplanes
// (Palette0/1/2) so CGB OBJ palettes 0..7 round-trip through the same shift
// pattern — DMG slots in bit 0 only (OBP0/OBP1). Merge logic in Ppu.SpriteFetcher.cs.
internal struct SpriteFifo
{
    public byte Low;
    public byte High;
    public byte Palette0;
    public byte Palette1;
    public byte Palette2;
    public byte BgPriority;
    public byte Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int Pixel, int Palette, int BgPriority) Pop()
    {
        var pixel = (((High >> 7) & 1) << 1) | ((Low >> 7) & 1);
        var palette = (((Palette2 >> 7) & 1) << 2)
                    | (((Palette1 >> 7) & 1) << 1)
                    |  ((Palette0 >> 7) & 1);
        var bgPrioBit = (BgPriority >> 7) & 1;
        Low = (byte)(Low << 1);
        High = (byte)(High << 1);
        Palette0 = (byte)(Palette0 << 1);
        Palette1 = (byte)(Palette1 << 1);
        Palette2 = (byte)(Palette2 << 1);
        BgPriority = (byte)(BgPriority << 1);
        Count--;
        return (pixel, palette, bgPrioBit);
    }
}
