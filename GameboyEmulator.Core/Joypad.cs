using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Joypad : IJoypad
{
    // Bit layout of _buttons matches the FF00 nibble when both groups are
    // selected: bits 0-3 = A, B, Select, Start; bits 4-7 = Right, Left, Up, Down.
    private const byte ActionMask    = 0x0F;
    private const byte DirectionMask = 0xF0;

    private readonly IInterrupts _interrupts;

    private byte _buttons;
    private bool _selectAction;
    private bool _selectDirection;
    private byte _prevLines = 0x0F;

    public Joypad(IInterrupts interrupts)
    {
        _interrupts = interrupts;
    }

    public void Select(byte value)
    {
        _selectAction    = (value & 0x20) == 0;
        _selectDirection = (value & 0x10) == 0;
        UpdateLinesAndMaybeIrq();
    }

    public byte Read()
    {
        var selectBits = (byte)(
            (_selectAction    ? 0 : 0x20) |
            (_selectDirection ? 0 : 0x10));
        return (byte)(0xC0 | selectBits | ComputeLines());
    }

    public void SetButton(JoypadButton button, bool pressed)
    {
        var bit = (byte)(1 << (int)button);
        if (pressed) _buttons |= bit;
        else         _buttons &= (byte)~bit;
        UpdateLinesAndMaybeIrq();
    }

    public void Reset()
    {
        _buttons = 0;
        _selectAction = false;
        _selectDirection = false;
        _prevLines = 0x0F;
    }

    private byte ComputeLines()
    {
        // Pressed = bit set in _buttons → output line bit 0 (active low).
        // Each group is its own nibble of _buttons; mask + shift directions down.
        if (!_selectAction && !_selectDirection) return 0x0F;

        var lines = (byte)0x0F;
        if (_selectAction)    lines &= (byte)(~_buttons & ActionMask);
        if (_selectDirection) lines &= (byte)(~(_buttons >> 4) & ActionMask);
        return lines;
    }

    private void UpdateLinesAndMaybeIrq()
    {
        var lines = ComputeLines();
        if ((_prevLines & ~lines) != 0)
            _interrupts.Request(InterruptType.Joypad);
        _prevLines = lines;
    }
}
