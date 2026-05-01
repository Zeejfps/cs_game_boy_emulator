namespace GameBoyEmulator.Core;

[Flags]
public enum LcdControl : byte
{
    None       = 0,
    BgEnable   = 1 << 0,
    ObjEnable  = 1 << 1,
    ObjSize    = 1 << 2, // 0=8x8, 1=8x16
    BgTileMap  = 1 << 3, // 0=0x9800, 1=0x9C00
    TileData   = 1 << 4, // 0=signed/0x9000, 1=unsigned/0x8000
    WinEnable  = 1 << 5,
    WinTileMap = 1 << 6, // 0=0x9800, 1=0x9C00
    LcdEnable  = 1 << 7,
}
