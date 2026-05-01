namespace GameBoyEmulator.Core.Graphics;

public readonly struct Sprite
{
    public byte Y { get; init; }
    public byte X { get; init; }
    public byte TileId { get; init; }
    public byte Attributes { get; init; }
    public byte OamIndex { get; init; }
}