namespace GameBoyEmulator.Core;

public interface IJoypad
{
    void Select(byte value);
    byte Read();
}