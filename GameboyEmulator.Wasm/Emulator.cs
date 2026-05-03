using System.Buffers;
using System.Runtime.InteropServices.JavaScript;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Wasm;

public static partial class Emulator
{
    private static GameBoy? _gameBoy;
    private static StopwatchClock? _clock;
    private static InMemoryBatteryStore? _battery;
    private static MemoryHandle _frameBufferHandle;
    private static bool _isFrameBufferPinned;
    private static bool _frameReady;

    // The PPU writes pixels into its internal buffer continuously. A single host
    // tick can run more than one DMG frame's worth of cycles (RAF jitter, 120Hz
    // displays, brief stalls), so by the time the host paints, the live buffer
    // may already contain the top scanlines of frame N+1 — visible as a tear.
    // We snapshot the buffer at VBlank so the host always reads a complete frame.
    private static readonly byte[] _frameBufferSnapshot =
        new byte[Ppu.ScreenWidth * Ppu.ScreenHeight];

    [JSExport]
    public static void Init()
    {
        if (_gameBoy != null)
            return;

        _clock = new StopwatchClock();
        _battery = new InMemoryBatteryStore();
        _gameBoy = new GameBoy(_clock, _battery);
        _gameBoy.FrameCompleted += OnFrameCompleted;
    }

    private static void OnFrameCompleted()
    {
        Gb().FrameBuffer.Span.CopyTo(_frameBufferSnapshot);
        _frameReady = true;
    }

    [JSExport]
    public static bool ConsumeFrame()
    {
        if (!_frameReady) return false;
        _frameReady = false;
        return true;
    }

    [JSExport]
    public static void LoadRom(byte[] rom, byte[]? saveData)
    {
        Battery().Bytes = saveData;
        Gb().LoadRom(rom);
    }

    [JSExport]
    public static void SetBootRom(byte[]? bootRom) => Gb().SetBootRom(bootRom);

    [JSExport]
    public static byte[]? GetSaveData()
    {
        Gb().FlushBatteryRam();
        return Battery().Bytes;
    }

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
    public static string GetDebugState() => Gb().GetDebugState();

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
            _frameBufferHandle = _frameBufferSnapshot.AsMemory().Pin();
            _isFrameBufferPinned = true;
        }
        return (int)_frameBufferHandle.Pointer;
    }

    private static GameBoy Gb() =>
        _gameBoy ?? throw new InvalidOperationException("Emulator.Init has not been called");

    private static StopwatchClock Clock() =>
        _clock ?? throw new InvalidOperationException("Emulator.Init has not been called");

    private static InMemoryBatteryStore Battery() =>
        _battery ?? throw new InvalidOperationException("Emulator.Init has not been called");
}
