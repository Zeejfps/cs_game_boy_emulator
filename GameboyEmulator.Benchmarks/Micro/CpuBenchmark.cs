using BenchmarkDotNet.Attributes;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
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
    private OamDmaController _dma = null!;
    private TimerBusClock _busClock = null!;

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
        var ppu = new NullPpu();
        _mmu = new Mmu(
            new RomOnlyMbc(rom),
            ppu,
            new NullJoypad(),
            _timer,
            new NullApu(),
            new Serial(interrupts),
            interrupts);
        _dma = new OamDmaController(_mmu, ppu);

        _busClock = new TimerBusClock(_timer, _dma);
        _cpu = new Cpu(_dma, _busClock, interrupts);
        _cpu.SkipBoot();
        _cpu.Pc = 0x0100;
    }

    [Benchmark]
    public long RunDispatchLoop()
    {
        var cpu = _cpu;
        var busClock = _busClock;
        long total = 0;
        while (total < CycleBudget)
        {
            cpu.Step();
            total += busClock.ConsumeAccumulated();
        }
        return total;
    }

    private sealed class TimerBusClock : IBusClock
    {
        private readonly GbTimer _timer;
        private readonly OamDmaController _dma;
        private long _accumulated;

        public TimerBusClock(GbTimer timer, OamDmaController dma)
        {
            _timer = timer;
            _dma = dma;
        }

        public void Tick(int ticks)
        {
            _timer.Tick(ticks);
            _dma.Tick(ticks);
            _accumulated += ticks;
        }

        public long ConsumeAccumulated()
        {
            var c = _accumulated;
            _accumulated = 0;
            return c;
        }
    }
}
