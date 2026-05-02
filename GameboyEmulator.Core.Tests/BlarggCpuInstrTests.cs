using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

[Trait("Category", "Blargg")]
public class BlarggCpuInstrTests
{
    private const long CycleBudget = 250_000_000L;
    private static readonly string RomRoot = Path.Combine(
        AppContext.BaseDirectory, "TestRoms", "blargg");

    [Theory]
    [InlineData("cpu_instrs/individual/01-special.gb")]
    [InlineData("cpu_instrs/individual/02-interrupts.gb")]
    [InlineData("cpu_instrs/individual/03-op sp,hl.gb")]
    [InlineData("cpu_instrs/individual/04-op r,imm.gb")]
    [InlineData("cpu_instrs/individual/05-op rp.gb")]
    [InlineData("cpu_instrs/individual/06-ld r,r.gb")]
    [InlineData("cpu_instrs/individual/07-jr,jp,call,ret,rst.gb")]
    [InlineData("cpu_instrs/individual/08-misc instrs.gb")]
    [InlineData("cpu_instrs/individual/09-op r,r.gb")]
    [InlineData("cpu_instrs/individual/10-bit ops.gb")]
    [InlineData("cpu_instrs/individual/11-op a,(hl).gb")]
    public void CpuInstrsSubTestPasses(string relativePath) => RunBlarggRom(relativePath);

    // halt_bug.gb writes its results via the LCD/PPU only — it never touches
    // serial (verified: zero writes to 0xFF02 in the ROM image). Validating it
    // therefore requires scraping VRAM tile data, which is out of scope until
    // the PPU lands. Tracked as a follow-up.
    [Fact(Skip = "halt_bug.gb is LCD-output-only; needs PPU/VRAM scraping (deferred).")]
    public void BlarggHaltBug() => RunBlarggRom("halt_bug.gb");

    [Fact]
    public void BlarggInstrTiming() => RunBlarggRom("instr_timing.gb");

    private static void RunBlarggRom(string relativePath)
    {
        var path = Path.Combine(RomRoot, relativePath);
        Assert.True(
            File.Exists(path),
            $"Blargg ROM not found: {path}. " +
            "See GameboyEmulator.Core.Tests/TestRoms/blargg/README.md for fetch instructions.");

        var rom = File.ReadAllBytes(path);
        var interrupts = new Interrupts();
        var timer = new Timer(interrupts);
        var serial = new BlarggSerial(interrupts);
        var mmu = new Mmu(
            new BlarggMbc(rom),
            new NullPpu(),
            new NullJoypad(),
            timer,
            new NullApu(),
            serial,
            interrupts);
        var busClock = new CountingBusClock();
        var cpu = new Cpu(mmu, busClock, interrupts);
        cpu.SkipBoot();

        long total = 0;
        while (total < CycleBudget)
        {
            cpu.Step();
            var t = (int)busClock.ConsumeAccumulated();
            timer.Tick(t);
            total += t;

            var output = serial.Output;
            if (output.Contains("Passed", StringComparison.Ordinal) ||
                output.Contains("Failed", StringComparison.Ordinal))
                break;
        }

        var final = serial.Output;
        Assert.True(
            final.Contains("Passed", StringComparison.Ordinal),
            $"Blargg ROM '{relativePath}' did not pass within {CycleBudget} T-states.\n" +
            $"Captured serial output:\n{final}");
    }

}
