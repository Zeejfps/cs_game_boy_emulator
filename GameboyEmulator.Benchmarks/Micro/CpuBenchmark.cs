using BenchmarkDotNet.Attributes;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;
using GbTimer = GameBoyEmulator.Core.Timer;

namespace GameBoyEmulator.Benchmarks.Micro;

// CPU benchmark suite.
//
// Why these benchmarks look the way they do:
//
// 1. Cycle-normalised output. Every [Benchmark] declares
//    [Benchmark(OperationsPerInvoke = CycleBudget)]. BDN's Mean column is
//    then nanoseconds per emulated T-cycle directly — lower means faster
//    CPU, no math required to compare runs.
//
// 2. Per-invocation reset. RunBudget zeroes the clock at entry, so the
//    "stop when we hit budget" loop runs the same amount of work on every
//    invocation. (Without this, the clock would carry over from invocation
//    1 into 2 and the loop would exit immediately.)
//
// 3. Drift-invariant kernels. Each kernel is a hand-laid byte sequence at
//    ROM 0x0100 that loops via JR back to its first byte. Register values
//    drift across invocations, but the *opcode flow* does not — every loop
//    pass executes the same dispatch path. That keeps measurement stable
//    across BDN's per-iteration unrolling without paying a per-invocation
//    SkipBoot cost.
//
// 4. Coverage of distinct dispatch paths. A change to the CPU rarely
//    affects every opcode equally. The kernels below pin specific corners:
//
//      AluRegister     register-only ALU       — Execute switch + ALU
//      HlIndirect      LD r,(HL) / LD (HL),r   — Mmu.Read / Mmu.Write path
//      BranchTaken     conditional, taken      — Branch handler taken arm
//      CbBitOps        CB-prefix bit ops       — CB sub-dispatch
//      PushPop         stack ops               — 16-bit push/pop + Mmu
//      DispatchSweep   ~30 distinct opcodes    — switch case spread
//      Mixed           a representative mix    — overall dispatch
//
//    If a change moves only one kernel, you know which path you touched.
//    If it moves all of them by the same amount, the change is in shared
//    dispatch (Step / Fetch / Tick) or the bus clock.
//
// 5. CycleBudget = 1_000_000. Long enough that BDN's per-invocation
//    overhead is dwarfed by emulated work, short enough that an iteration
//    sample contains many invocations for tight statistics.
//
// 6. Realistic bus clock. TickAccumulatingClock ticks the real Timer +
//    OamDmaController on every Cpu.Tick — those run on every cycle of a
//    real GameBoy, so changes that affect how often the CPU calls Tick
//    show up here. A NoOp clock would hide that.
//
// How to use this to validate a CPU change:
//
//   # baseline (before your change)
//   git stash
//   dotnet run -c Release --project GameboyEmulator.Benchmarks -- \
//     --filter '*CpuBenchmark*' --exporters json
//   mv GameboyEmulator.Benchmarks/BenchmarkDotNet.Artifacts \
//      GameboyEmulator.Benchmarks/BenchmarkDotNet.Artifacts.before
//
//   # candidate (your change)
//   git stash pop
//   dotnet run -c Release --project GameboyEmulator.Benchmarks -- \
//     --filter '*CpuBenchmark*' --exporters json
//
//   # compare Mean (ns/T-cycle) kernel-by-kernel.
//
// Expect run-to-run noise of ~0.5-1% on a quiet machine. Differences
// inside that band are not real; differences above it are. Always close
// other apps and disable Turbo Boost / set a fixed CPU governor for the
// tightest numbers.

[MemoryDiagnoser]
public class CpuBenchmark
{
    private const int CycleBudget = 1_000_000;

    private Harness _aluRegister = null!;
    private Harness _hlIndirect = null!;
    private Harness _branchTaken = null!;
    private Harness _cbBitOps = null!;
    private Harness _pushPop = null!;
    private Harness _dispatchSweep = null!;
    private Harness _mixed = null!;

    [GlobalSetup]
    public void Setup()
    {
        _aluRegister = Harness.FromKernel(KernelAluRegister());
        _hlIndirect = Harness.FromKernel(KernelHlIndirect());
        _branchTaken = Harness.FromKernel(KernelBranchTaken());
        _cbBitOps = Harness.FromKernel(KernelCbBitOps());
        _pushPop = Harness.FromKernel(KernelPushPop());
        _dispatchSweep = Harness.FromKernel(KernelDispatchSweep());
        _mixed = Harness.FromKernel(KernelMixed());
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _aluRegister.ResetCpu();
        _hlIndirect.ResetCpu();
        _branchTaken.ResetCpu();
        _cbBitOps.ResetCpu();
        _pushPop.ResetCpu();
        _dispatchSweep.ResetCpu();
        _mixed.ResetCpu();
    }

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long AluRegister() => _aluRegister.RunBudget(CycleBudget);

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long HlIndirect() => _hlIndirect.RunBudget(CycleBudget);

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long BranchTaken() => _branchTaken.RunBudget(CycleBudget);

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long CbBitOps() => _cbBitOps.RunBudget(CycleBudget);

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long PushPop() => _pushPop.RunBudget(CycleBudget);

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long DispatchSweep() => _dispatchSweep.RunBudget(CycleBudget);

    [Benchmark(OperationsPerInvoke = CycleBudget)]
    public long Mixed() => _mixed.RunBudget(CycleBudget);

    // ---- kernels --------------------------------------------------------
    // Each kernel is a byte sequence laid down at ROM 0x0100. LoopBack
    // appends a JR that returns to the first byte. Kernels are designed so
    // the opcode flow does not depend on entry register values.

    private static byte[] KernelAluRegister() => LoopBack(new byte[]
    {
        0x3C,             // INC A       (4T)
        0x04,             // INC B       (4T)
        0x0C,             // INC C       (4T)
        0x14,             // INC D       (4T)
        0x80,             // ADD A,B     (4T)
        0x81,             // ADD A,C     (4T)
        0xA1,             // AND C       (4T)
        0xB0,             // OR  B       (4T)
        0xB9,             // CP  C       (4T)
    });

    private static byte[] KernelHlIndirect() => LoopBack(new byte[]
    {
        // Reload HL each pass so memory traffic always lands on the same
        // WRAM bytes. Read-modify-write pattern exercises both Mmu.Read
        // and Mmu.Write.
        0x21, 0x00, 0xC0, // LD HL, 0xC000  (12T)
        0x7E,             // LD A,(HL)      (8T)
        0x23,             // INC HL         (8T)
        0x77,             // LD (HL),A      (8T)
        0x86,             // ADD A,(HL)     (8T)
        0x23,             // INC HL         (8T)
        0x77,             // LD (HL),A      (8T)
    });

    private static byte[] KernelBranchTaken() => LoopBack(new byte[]
    {
        // XOR A clears Z to 1, so JR Z is always taken. Stresses the
        // taken arm of the conditional-branch handler and the extra
        // Tick(4) that a taken branch performs.
        0xAF,             // XOR A       (4T)
        0x28, 0x00,       // JR Z,+0     (12T taken)
    });

    private static byte[] KernelCbBitOps() => LoopBack(new byte[]
    {
        // Every iteration goes through the CB-prefix sub-dispatch.
        // Regressions to that path show up here and nowhere else.
        0xCB, 0x47,       // BIT 0,A     (8T)
        0xCB, 0x4F,       // BIT 1,A     (8T)
        0xCB, 0x87,       // RES 0,A     (8T)
        0xCB, 0xCF,       // SET 1,A     (8T)
        0xCB, 0x27,       // SLA A       (8T)
        0xCB, 0x1F,       // RR  A       (8T)
    });

    private static byte[] KernelPushPop() => LoopBack(new byte[]
    {
        // SP starts at 0xFFFE. Two pushes drop SP to 0xFFFA, two pops
        // bring it back. Net SP movement per pass is zero, so subsequent
        // passes hit the same stack bytes.
        0xC5,             // PUSH BC     (16T)
        0xD5,             // PUSH DE     (16T)
        0xE1,             // POP HL      (12T)
        0xF1,             // POP AF      (12T)
    });

    private static byte[] KernelDispatchSweep() => LoopBack(new byte[]
    {
        // ~30 distinct register-only opcodes back-to-back. Spreads the
        // work across many cases of the giant Execute() switch, which
        // is where a regression to dispatch shape (case ordering, JIT
        // codegen of the table) is most likely to surface.
        0x3C, 0x04, 0x0C, 0x14, 0x1C, 0x24, 0x2C, // INC A/B/C/D/E/H/L
        0x80, 0x89, 0x92, 0x9B,                   // ADD A,B / ADC A,C / SUB D / SBC A,E
        0xA4, 0xAD, 0xB0, 0xB9,                   // AND H / XOR L / OR B / CP C
        0x07, 0x0F, 0x17, 0x1F,                   // RLCA / RRCA / RLA / RRA
        0x2F, 0x37, 0x3F,                         // CPL / SCF / CCF
        0x47, 0x4F, 0x57, 0x5F, 0x67, 0x6F,       // LD B/C/D/E/H/L,A
        0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D,       // LD A,B/C/D/E/H/L
    });

    private static byte[] KernelMixed() => LoopBack(new byte[]
    {
        // A hand-rolled mix that touches each major opcode class. If a
        // change shifts every other kernel by X% but Mixed by less,
        // you've sped up paths the average game doesn't use much.
        0x3C,             // INC A         (4T)
        0x80,             // ADD A,B       (4T)
        0x21, 0x00, 0xC0, // LD HL, 0xC000 (12T)
        0x7E,             // LD A,(HL)     (8T)
        0x77,             // LD (HL),A     (8T)
        0xC5,             // PUSH BC       (16T)
        0xD1,             // POP DE        (12T)
        0xCB, 0x47,       // BIT 0,A       (8T)
        0xAF,             // XOR A         (4T)
        0x28, 0x00,       // JR Z,+0       (12T, taken)
    });

    // Append `JR -(body.Length + 2)` so PC at the end wraps to body[0].
    // After the JR's two bytes are fetched, PC = start + body.Length + 2;
    // adding a displacement of -(body.Length + 2) returns PC to start.
    private static byte[] LoopBack(byte[] body)
    {
        var len = body.Length;
        if (len + 2 > 128)
            throw new ArgumentException("Kernel body too long for an 8-bit JR.");
        var rom = new byte[len + 2];
        Array.Copy(body, 0, rom, 0, len);
        rom[len] = 0x18;                                 // JR
        rom[len + 1] = unchecked((byte)-(len + 2));      // displacement
        return rom;
    }

    // ---- harness --------------------------------------------------------

    private sealed class Harness
    {
        public Cpu Cpu { get; }
        public TickAccumulatingClock Clock { get; }

        private Harness(Cpu cpu, TickAccumulatingClock clock)
        {
            Cpu = cpu;
            Clock = clock;
        }

        public static Harness FromKernel(byte[] kernel)
        {
            var rom = new byte[0x8000];
            Array.Copy(kernel, 0, rom, 0x0100, kernel.Length);

            var interrupts = new Interrupts();
            var timer = new GbTimer(interrupts);
            var ppu = new NullPpu();
            var mmu = new Mmu(
                new RomOnlyMbc(rom),
                ppu,
                new NullJoypad(),
                timer,
                new NullApu(),
                new Serial(interrupts),
                interrupts);
            var dma = new OamDmaController(mmu, ppu);
            var clock = new TickAccumulatingClock(timer, dma);
            var cpu = new Cpu(mmu, clock, interrupts);

            var h = new Harness(cpu, clock);
            h.ResetCpu();
            return h;
        }

        public void ResetCpu()
        {
            Cpu.SkipBoot();
            Cpu.Pc = 0x0100;
        }

        public long RunBudget(long budget)
        {
            var cpu = Cpu;
            var clock = Clock;
            clock.Reset();
            while (clock.Accumulated < budget)
                cpu.Step();
            return clock.Accumulated;
        }
    }

    private sealed class TickAccumulatingClock : ISystemClock
    {
        private readonly GbTimer _timer;
        private readonly OamDmaController _dma;
        public long Accumulated;

        public TickAccumulatingClock(GbTimer timer, OamDmaController dma)
        {
            _timer = timer;
            _dma = dma;
        }

        public void Advance(int ticks)
        {
            _timer.Tick(ticks);
            _dma.Tick(ticks);
            Accumulated += ticks;
        }

        public void Reset() => Accumulated = 0;
    }
}
