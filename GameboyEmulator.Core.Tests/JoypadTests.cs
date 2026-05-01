using GameBoyEmulator.Core;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class JoypadTests
{
    private sealed class FakeInterruptBus : IInterrupts
    {
        private InterruptType _requested;
        private InterruptType _enabled;

        public int JoypadCount { get; private set; }

        public void Request(InterruptType kind)
        {
            _requested |= kind;
            if (kind == InterruptType.Joypad)
                JoypadCount++;
        }

        public void Clear(InterruptType kind) => _requested &= ~kind;
        public bool IsRequested(InterruptType kind) => (_requested & kind) != 0;
        public InterruptType GetPending() => _requested & _enabled;
        public InterruptType ReadRequestedInterrupts() => _requested;
        public void WriteRequestedInterrupts(InterruptType requestedInterrupts) => _requested = requestedInterrupts;
        public InterruptType ReadEnabledInterrupts() => _enabled;
        public void WriteEnabledInterrupts(InterruptType enabledInterrupts) => _enabled = enabledInterrupts;
    }

    private const byte SelectAction    = 0xDF; // P15 low (bit 5 = 0), P14 high
    private const byte SelectDirection = 0xEF; // P14 low (bit 4 = 0), P15 high
    private const byte SelectBoth      = 0xCF; // both low
    private const byte SelectNeither   = 0xFF; // both high

    private readonly FakeInterruptBus _interrupts = new();
    private readonly Joypad _joypad;

    public JoypadTests()
    {
        _joypad = new Joypad(_interrupts);
    }

    [Fact]
    public void Read_AfterReset_ReturnsAllOnes()
    {
        Assert.Equal(0xFF, _joypad.Read());
    }

    [Fact]
    public void Read_NeitherSelected_ReturnsAllOnesEvenWhenButtonsHeld()
    {
        _joypad.SetButton(JoypadButton.A, true);
        _joypad.SetButton(JoypadButton.Down, true);

        _joypad.Select(SelectNeither);

        Assert.Equal(0xFF, _joypad.Read());
    }

    [Fact]
    public void Read_ActionSelected_ReturnsActionNibbleOnly()
    {
        _joypad.SetButton(JoypadButton.A, true);     // bit 0
        _joypad.SetButton(JoypadButton.Start, true); // bit 3
        _joypad.SetButton(JoypadButton.Down, true);  // direction — should not show

        _joypad.Select(SelectAction);

        // bits 7-6 = 1, bit 5 = 0 (P15 selected), bit 4 = 1, low nibble: A and Start pressed → 0110
        Assert.Equal(0xD6, _joypad.Read());
    }

    [Fact]
    public void Read_DirectionSelected_ReturnsDirectionNibbleOnly()
    {
        _joypad.SetButton(JoypadButton.Right, true); // bit 4
        _joypad.SetButton(JoypadButton.Up, true);    // bit 6
        _joypad.SetButton(JoypadButton.B, true);     // action — should not show

        _joypad.Select(SelectDirection);

        // bits 7-6 = 1, bit 5 = 1, bit 4 = 0 (P14 selected), low nibble: Right and Up pressed → 1010
        Assert.Equal(0xEA, _joypad.Read());
    }

    [Fact]
    public void Read_BothSelected_AndsTheTwoGroups()
    {
        // A pressed (action bit 0) but Right pressed (direction bit 0).
        // Both groups contribute: action group bit 0 = 0, direction group bit 0 = 0 → AND = 0.
        _joypad.SetButton(JoypadButton.A, true);
        _joypad.SetButton(JoypadButton.Right, true);

        _joypad.Select(SelectBoth);

        // Both selects low → bits 5,4 = 0. Low nibble: bit 0 from both groups = 0, others = 1.
        Assert.Equal(0xCE, _joypad.Read());
    }

    [Fact]
    public void Press_OnSelectedGroup_RequestsJoypadInterrupt()
    {
        _joypad.Select(SelectAction);

        _joypad.SetButton(JoypadButton.A, true);

        Assert.Equal(1, _interrupts.JoypadCount);
    }

    [Fact]
    public void Press_OnUnselectedGroup_DoesNotRequestInterrupt()
    {
        _joypad.Select(SelectAction);

        _joypad.SetButton(JoypadButton.Down, true);

        Assert.Equal(0, _interrupts.JoypadCount);
    }

    [Fact]
    public void Release_DoesNotRequestInterrupt()
    {
        _joypad.Select(SelectAction);
        _joypad.SetButton(JoypadButton.A, true);
        var before = _interrupts.JoypadCount;

        _joypad.SetButton(JoypadButton.A, false);

        Assert.Equal(before, _interrupts.JoypadCount);
    }

    [Fact]
    public void Select_ThatExposesAlreadyHeldButton_RequestsInterrupt()
    {
        // Hold the button before any group is selected — no IRQ yet.
        _joypad.SetButton(JoypadButton.A, true);
        Assert.Equal(0, _interrupts.JoypadCount);

        // Now selecting the action group pulls bit 0 low → falling edge → IRQ.
        _joypad.Select(SelectAction);

        Assert.Equal(1, _interrupts.JoypadCount);
    }

    [Fact]
    public void Reset_ClearsButtonsAndSelects()
    {
        _joypad.SetButton(JoypadButton.A, true);
        _joypad.Select(SelectAction);

        _joypad.Reset();

        Assert.Equal(0xFF, _joypad.Read());
    }
}
