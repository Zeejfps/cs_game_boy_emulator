using BenchmarkDotNet.Attributes;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.LR35902;
using GbTimer = GameBoyEmulator.Core.Timer;

namespace GameBoyEmulator.Benchmarks.Micro;

[MemoryDiagnoser]
public class MmuBenchmark
{
    private const int Iterations = 10_000;

    private Mmu _mmu = null!;
    private ushort[] _addresses = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rom = new byte[0x8000];
        var interrupts = new Interrupts();
        _mmu = new Mmu(
            new RomOnlyMbc(rom),
            new NullPpu(),
            new NullJoypad(),
            new GbTimer(interrupts),
            new NullApu(),
            new Serial(interrupts),
            interrupts);

        // Stress the address-decode switch by spreading reads across regions:
        // ROM bank0, ROM bankN, VRAM (NullPpu), External RAM, WRAM (low+echo),
        // OAM, IO, HRAM.
        ushort[] regionBases = { 0x0040, 0x4040, 0x8040, 0xA040, 0xC040, 0xE040, 0xFE00, 0xFF40, 0xFF80 };
        var rng = new Random(7);
        _addresses = new ushort[Iterations];
        for (var i = 0; i < Iterations; i++)
            _addresses[i] = (ushort)(regionBases[rng.Next(regionBases.Length)] + rng.Next(64));
    }

    [Benchmark]
    public int Read()
    {
        var mmu = _mmu;
        var addrs = _addresses;
        var sum = 0;
        for (var i = 0; i < addrs.Length; i++)
            sum += mmu.Read(addrs[i]);
        return sum;
    }

    [Benchmark]
    public void Write()
    {
        var mmu = _mmu;
        var addrs = _addresses;
        for (var i = 0; i < addrs.Length; i++)
            mmu.Write(addrs[i], (byte)i);
    }
}
