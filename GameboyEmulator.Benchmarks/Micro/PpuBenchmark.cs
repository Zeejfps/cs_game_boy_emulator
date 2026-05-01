using BenchmarkDotNet.Attributes;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Benchmarks.Micro;

[MemoryDiagnoser]
public class PpuBenchmark
{
    private Ppu _ppu = null!;

    [GlobalSetup]
    public void Setup()
    {
        var interrupts = new Interrupts();
        _ppu = new Ppu(interrupts);

        // LCD on, BG on, tile data 0x8000, tile map 0x9800.
        _ppu.WriteRegister(0xFF40, 0x91);
        _ppu.WriteRegister(0xFF47, 0xE4);

        // Non-trivial tile pattern + non-uniform tile map indices so the
        // BG fetcher exercises GetTile / GetTilePixelsLow/High / Push paths.
        var rng = new Random(42);
        for (ushort a = 0; a < 0x1800; a++)
            _ppu.WriteVram(a, (byte)rng.Next(256));
        for (ushort a = 0x1800; a < 0x2000; a++)
            _ppu.WriteVram(a, (byte)(a & 0xFF));
    }

    // 70,224 T-cycles is one full frame.
    [Benchmark]
    public void StepOneFrame() => _ppu.Step(70_224);
}
