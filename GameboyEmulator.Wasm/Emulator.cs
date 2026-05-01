using System.Buffers;
using System.Runtime.InteropServices.JavaScript;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Wasm;

public static partial class Emulator
{
    private static GameBoy? _gameBoy;
    private static StopwatchClock? _clock;
    private static MemoryHandle _frameBufferHandle;
    private static bool _isFrameBufferPinned;
    private static bool _frameReady;

    [JSExport]
    public static void Init()
    {
        if (_gameBoy != null)
            return;

        _clock = new StopwatchClock();
        _gameBoy = new GameBoy(_clock);
        _gameBoy.FrameCompleted += () => _frameReady = true;
    }

    [JSExport]
    public static bool ConsumeFrame()
    {
        if (!_frameReady) return false;
        _frameReady = false;
        return true;
    }

    [JSExport]
    public static void LoadRom(byte[] rom) => Gb().LoadRom(rom);

    [JSExport]
    public static void PowerOn() => Gb().PowerOn();

    [JSExport]
    public static void PowerOff() => Gb().PowerOff();

    [JSExport]
    public static bool IsPoweredOn() => Gb().IsPoweredOn;

    [JSExport]
    public static void Tick() => Clock().Tick();

    [JSExport]
    public static void SetButton(int button, bool pressed)
        => Gb().SetButton((JoypadButton)button, pressed);

    [JSExport]
    public static int GetFrameBufferWidth() => Ppu.ScreenWidth;

    [JSExport]
    public static int GetFrameBufferHeight() => Ppu.ScreenHeight;

    [JSExport]
    public static int GetFrameBufferLength() => Ppu.ScreenWidth * Ppu.ScreenHeight;

    [JSExport]
    public static unsafe int GetFrameBufferPointer()
    {
        if (!_isFrameBufferPinned)
        {
            _frameBufferHandle = Gb().FrameBuffer.Pin();
            _isFrameBufferPinned = true;
        }
        return (int)_frameBufferHandle.Pointer;
    }

    private static GameBoy Gb() =>
        _gameBoy ?? throw new InvalidOperationException("Emulator.Init has not been called");

    private static StopwatchClock Clock() =>
        _clock ?? throw new InvalidOperationException("Emulator.Init has not been called");
}
