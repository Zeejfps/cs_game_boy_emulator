using BenchmarkDotNet.Attributes;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.LR35902;
using GbTimer = GameBoyEmulator.Core.Timer;

namespace GameBoyEmulator.Benchmarks.Micro;

[MemoryDiagnoser]
public class CpuBenchmark
{
    private const int CycleBudget = 100_000;

    private Cpu _cpu = null!;
    private Mmu _mmu = null!;
    private GbTimer _timer = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Synthetic 32 KB ROM. Entry at 0x0100:
        //   00         NOP
        //   3C         INC A
        //   04         INC B
        //   80         ADD A, B
        //   18 FA      JR -6  (back to 0x0100)
        var rom = new byte[0x8000];
        rom[0x0100] = 0x00;
        rom[0x0101] = 0x3C;
        rom[0x0102] = 0x04;
        rom[0x0103] = 0x80;
        rom[0x0104] = 0x18;
        rom[0x0105] = 0xFA;

        var interrupts = new Interrupts();
        _timer = new GbTimer(interrupts);
        _mmu = new Mmu(
            new RomOnlyMbc(rom),
            new NullPpu(),
            new NullJoypad(),
            _timer,
            new NullApu(),
            new Serial(interrupts),
            interrupts);

        _cpu = new Cpu(_mmu, interrupts);
        _cpu.SkipBoot();
        _cpu.Pc = 0x0100;
    }

    [Benchmark]
    public int RunDispatchLoop()
    {
        var cpu = _cpu;
        var timer = _timer;
        var total = 0;
        while (total < CycleBudget)
        {
            var t = cpu.Step();
            timer.Tick(t);
            total += t;
        }
        return total;
    }
}
