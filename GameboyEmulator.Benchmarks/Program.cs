using BenchmarkDotNet.Running;
using GameBoyEmulator.Benchmarks.Macro;
using GameBoyEmulator.Benchmarks.Micro;

if (args.Length > 0 && args[0].Equals("macro", StringComparison.OrdinalIgnoreCase))
    return MacroBenchmark.Run(args[1..]);

var switcher = BenchmarkSwitcher.FromTypes(new[]
{
    typeof(PpuBenchmark),
    typeof(CpuBenchmark),
    typeof(MmuBenchmark),
});
switcher.Run(args);
return 0;
