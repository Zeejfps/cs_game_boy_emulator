using System.Buffers;
using System.Runtime.InteropServices.JavaScript;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
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
    // uint per pixel = ARGB packed little-endian as R,G,B,A — drops straight
    // into canvas ImageData with no per-pixel translation on the JS side.
    private static readonly uint[] _frameBufferSnapshot =
        new uint[Ppu.ScreenWidth * Ppu.ScreenHeight];

    // Stereo float scratchpad the host drains audio into between ticks.
    // Sized for ~85 ms of jitter budget at 48 kHz; an 8 kHz host or a wedged
    // main thread that holds Tick for >85 ms will quietly drop oldest samples
    // inside the APU's own ring buffer rather than overflowing this one.
    private const int AudioDrainFrames = 4096;
    private static readonly float[] _audioDrainBuffer = new float[AudioDrainFrames * 2];
    private static MemoryHandle _audioBufferHandle;
    private static bool _isAudioBufferPinned;

    [JSExport]
    public static void Init(int sampleRate)
    {
        if (_gameBoy != null)
            return;

        _clock = new StopwatchClock();
        _battery = new InMemoryBatteryStore();
        // sampleRate <= 0 is interpreted as "no audio yet" — the APU defaults
        // to 48 kHz and produces samples that nothing will drain. Once the
        // host opens an AudioContext, it should re-init or accept the default.
        _gameBoy = new GameBoy(_clock, _battery, new SystemTimeProvider(), sampleRate > 0 ? sampleRate : 48000);
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

    // Audio drain: the host calls DrainAudio() between ticks; samples land in
    // the pinned float buffer at GetAudioBufferPointer(), interleaved L,R.
    // Returns frames written (each frame = 2 floats = 8 bytes). The caller
    // is expected to copy those frames into a SharedArrayBuffer ring buffer
    // that an AudioWorklet reads on the audio thread.
    [JSExport]
    public static int GetAudioBufferFrameCapacity() => AudioDrainFrames;

    [JSExport]
    public static unsafe int GetAudioBufferPointer()
    {
        if (!_isAudioBufferPinned)
        {
            _audioBufferHandle = _audioDrainBuffer.AsMemory().Pin();
            _isAudioBufferPinned = true;
        }
        return (int)_audioBufferHandle.Pointer;
    }

    [JSExport]
    public static int DrainAudio() => Gb().DrainAudio(_audioDrainBuffer);

    private static GameBoy Gb() =>
        _gameBoy ?? throw new InvalidOperationException("Emulator.Init has not been called");

    private static StopwatchClock Clock() =>
        _clock ?? throw new InvalidOperationException("Emulator.Init has not been called");

    private static InMemoryBatteryStore Battery() =>
        _battery ?? throw new InvalidOperationException("Emulator.Init has not been called");
}
