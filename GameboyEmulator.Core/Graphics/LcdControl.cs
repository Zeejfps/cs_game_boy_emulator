namespace GameBoyEmulator.Core.Graphics;

[Flags]
public enum LcdControl : byte
{
    None                      = 0,
    BackgroundEnable          = 1 << 0,
    ObjectsEnable             = 1 << 1,
    ObjectsUseLargeSize       = 1 << 2, // 0=8x8, 1=8x16
    BackgroundUsesTileMap1    = 1 << 3, // 0=0x9800, 1=0x9C00
    UseUnsignedTileAddressing = 1 << 4, // 0=signed/0x9000, 1=unsigned/0x8000
    WindowEnable              = 1 << 5,
    WindowUsesTileMap1        = 1 << 6, // 0=0x9800, 1=0x9C00
    LcdEnable                 = 1 << 7,
}
