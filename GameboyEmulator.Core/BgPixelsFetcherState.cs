namespace GameBoyEmulator.Core;

public enum BgPixelsFetcherState : byte
{
    GetTile,
    GetTilePixelsLow,
    GetTilePixelsHigh,
    Push
}