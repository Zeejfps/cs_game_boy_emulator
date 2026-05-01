namespace GameBoyEmulator.Core;

public interface IJoypad
{
    void Select(byte value);
    byte Read();
    void SetButton(JoypadButton button, bool pressed);
    void Reset();
}
