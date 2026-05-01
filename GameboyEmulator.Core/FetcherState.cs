namespace GameBoyEmulator.Core;

public enum FetcherState : byte
{
    GetTile,
    GetTilePixelsLow,
    GetTilePixelsHigh,
    Push
}