namespace GameBoyEmulator.Core.Graphics;

public enum BgPixelsFetcherState : byte
{
    GetTile,
    GetTilePixelsLow,
    GetTilePixelsHigh,
    Push
}
