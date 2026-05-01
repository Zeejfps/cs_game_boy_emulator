using System.Diagnostics;
using GameBoyEmulator.Core;

namespace GameBoyEmulator.Benchmarks.Macro;

public static class MacroBenchmark
{
    private const long DefaultBudget = 250_000_000L;
    private const double CpuFrequency = 4_194_304.0;
    private const string DefaultRomRel =
        "TestRoms/blargg/cpu_instrs/individual/06-ld r,r.gb";

    public static int Run(string[] args)
    {
        var romPath = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, DefaultRomRel);

        if (!File.Exists(romPath))
        {
            Console.Error.WriteLine($"ROM not found: {romPath}");
            return 1;
        }

        var rom = File.ReadAllBytes(romPath);
        var clock = new BenchmarkClock(ticksPerStep: 70_224);
        var gb = new GameBoy(clock, new NullBatteryStore());
        gb.LoadRom(rom);
        gb.PowerOn();

        for (var i = 0; i < 60; i++) clock.Step();

        long emulatedCycles = 0;
        var sw = Stopwatch.StartNew();
        while (emulatedCycles < DefaultBudget)
            emulatedCycles += clock.Step();
        sw.Stop();

        var realSeconds = sw.Elapsed.TotalSeconds;
        var emulatedSeconds = emulatedCycles / CpuFrequency;
        var xRealtime = emulatedSeconds / realSeconds;
        var cyclesPerSec = emulatedCycles / realSeconds;

        Console.WriteLine($"ROM:              {Path.GetFileName(romPath)}");
        Console.WriteLine($"Emulated cycles:  {emulatedCycles:N0} T-cycles");
        Console.WriteLine($"Emulated time:    {emulatedSeconds:F3} s");
        Console.WriteLine($"Wall time:        {realSeconds:F3} s");
        Console.WriteLine($"Throughput:       {cyclesPerSec:N0} cycles/s");
        Console.WriteLine($"Speed:            {xRealtime:F2}x realtime");

        gb.PowerOff();
        return 0;
    }
}
