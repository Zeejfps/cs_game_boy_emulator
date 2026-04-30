namespace GameBoyEmulator.Core;

public sealed class Joypad : IJoypad
{
    public void Select(byte value)
    {
    }

    public byte Read() => 0xFF;
}
