using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// 8-pixel sprite FIFO. Color stored as two bitplanes (Low/High); palette and
// bg-priority are one bit per slot. Merge logic lives in Ppu.SpriteFetcher.cs.
internal struct SpriteFifo
{
    public byte Low;
    public byte High;
    public byte Palette;
    public byte BgPriority;
    public byte Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int Pixel, int Palette, int BgPriority) Pop()
    {
        var pixel = (((High >> 7) & 1) << 1) | ((Low >> 7) & 1);
        var paletteBit = (Palette >> 7) & 1;
        var bgPrioBit = (BgPriority >> 7) & 1;
        Low = (byte)(Low << 1);
        High = (byte)(High << 1);
        Palette = (byte)(Palette << 1);
        BgPriority = (byte)(BgPriority << 1);
        Count--;
        return (pixel, paletteBit, bgPrioBit);
    }
}
