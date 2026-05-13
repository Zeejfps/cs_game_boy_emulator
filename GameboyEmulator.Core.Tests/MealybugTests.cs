using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

// Mealybug Tearoom Tests (https://github.com/mattcurrie/mealybug-tearoom-tests)
// are PPU-timing torture tests: each ROM finishes with `LD B,B; JR -2` once
// rendering is done, then sits in the halt loop. We compare the final
// framebuffer against the per-test reference under expected-dmg/.
//
// Mealybug ROMs are DMG cartridges. Reference set used:
//   expected/DMG-blob/   — DMG-mode output, 4 grayscale shades $00/$55/$AA/$FF.
// The .bin files contain one byte per pixel = shade index 0..3 (0=white,
// 3=black), which is exactly what the PPU's `_frameBuffer` stores after BGP/
// OBP resolution. Comparing shade indices makes the test palette-agnostic
// (our DMG palette tints green; Mealybug's expected is true grayscale).
//
// 7 ROMs (the "_change2" variants) have no DMG reference — they test CGB-
// only behavior. They're omitted here; running them against CGB references
// would also require emulating the CGB boot ROM's DMG-compat auto-palette,
// which we don't model yet.
[Trait("Category", "Mealybug")]
public class MealybugTests
{
    private const long CycleBudget = 120_000_000L;
    private const byte LdBBOpcode = 0x40;
    private const int FramePixels = Ppu.ScreenWidth * Ppu.ScreenHeight;

    private static readonly string RomDir = Path.Combine(
        AppContext.BaseDirectory, "TestRoms", "mealybug", "roms");
    private static readonly string RefDir = Path.Combine(
        AppContext.BaseDirectory, "TestRoms", "mealybug", "expected-dmg");

    public static IEnumerable<object[]> RomsWithDmgReference()
    {
        if (!Directory.Exists(RomDir) || !Directory.Exists(RefDir)) yield break;

        foreach (var path in Directory.EnumerateFiles(RomDir, "*.gb", SearchOption.TopDirectoryOnly)
                                      .OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var refPath = Path.Combine(RefDir, name + ".bin");
            if (File.Exists(refPath))
                yield return new object[] { Path.GetFileName(path) };
        }
    }

    [Theory]
    [MemberData(nameof(RomsWithDmgReference))]
    public void MealybugRomMatchesReference(string romFile)
    {
        var romPath = Path.Combine(RomDir, romFile);
        var refPath = Path.Combine(RefDir, Path.GetFileNameWithoutExtension(romFile) + ".bin");

        var reference = File.ReadAllBytes(refPath);
        Assert.True(
            reference.Length == FramePixels,
            $"Reference must be exactly {FramePixels} bytes (shade-index, 160×144), got {reference.Length}.");

        var rom = File.ReadAllBytes(romPath);
        var system = BuildSystem(rom);

        long total = 0;
        var done = false;
        while (total < CycleBudget)
        {
            if (system.Mmu.Read(system.Cpu.Pc) == LdBBOpcode) { done = true; break; }
            system.Cpu.Step();
            total += system.SystemClock.ConsumeAccumulated();
        }

        Assert.True(
            done,
            $"{romFile} did not reach LD B,B within {CycleBudget} T-states. " +
            $"PC=0x{system.Cpu.Pc:X4}");

        var fb = system.Ppu.FrameBuffer.Span;
        var diffCount = 0;
        var diffs = new List<(int x, int y, byte expected, byte actual)>();
        for (var i = 0; i < FramePixels; i++)
        {
            if (fb[i] == reference[i]) continue;
            diffCount++;
            if (diffs.Count < 10)
            {
                var x = i % Ppu.ScreenWidth;
                var y = i / Ppu.ScreenWidth;
                diffs.Add((x, y, reference[i], fb[i]));
            }
        }

        if (diffCount > 0)
        {
            var sample = string.Join(
                "\n  ",
                diffs.Select(d => $"({d.x,3},{d.y,3}) expected=shade{d.expected} actual=shade{d.actual}"));
            Assert.Fail(
                $"{romFile}: {diffCount} of {FramePixels} pixels differ " +
                $"(showing first {diffs.Count}):\n  {sample}");
        }
    }

    private sealed record System(Cpu Cpu, Mmu Mmu, Ppu Ppu, SystemClock SystemClock);

    private static System BuildSystem(byte[] rom)
    {
        var interrupts = new Interrupts();
        var ppu = new Ppu(interrupts);
        var timer = new Timer(interrupts);
        var mbcFactory = new MbcFactory(new NullBatteryStore(), new SystemTimeProvider());
        var mbc = mbcFactory.Create(rom);
        var apu = new NullApu();
        var mmu = new Mmu(
            mbc,
            ppu,
            new NullJoypad(),
            timer,
            apu,
            new NullSerial(),
            interrupts);
        var dma = new OamDmaController(mmu, ppu);
        var hdma = new HdmaController(mmu, ppu);
        mmu.SetHdmaController(hdma);
        ppu.OnHBlankEntry = hdma.OnHBlank;

        // Mealybug DMG-blob references are captured running as DMG carts —
        // leave the system in DMG mode (cart's CGB flag is 0).
        var isCgb = MbcFactory.IsCgbCartridge(rom);
        mmu.SetCgbMode(isCgb);
        ppu.SetCgbMode(isCgb);

        var busClock = new SystemClock(ppu, timer, dma, apu);
        var cpu = new Cpu(dma, busClock, interrupts);
        cpu.SetCgbMode(isCgb);
        mmu.SetSpeedController(cpu);

        cpu.SkipBoot();
        mmu.Write(0xFF40, 0x91);
        mmu.Write(0xFF47, 0xFC);
        mmu.Write(0xFF48, 0xFF);
        mmu.Write(0xFF49, 0xFF);
        return new System(cpu, mmu, ppu, busClock);
    }

    private sealed class NullBatteryStore : IBatteryStore
    {
        public byte[]? Load(string key) => null;
        public void Save(string key, ReadOnlySpan<byte> data) { }
    }

    private sealed class NullSerial : ISerial
    {
        public byte ReadData() => 0xFF;
        public byte ReadControl() => 0xFF;
        public void WriteData(byte value) { }
        public void WriteControl(byte value) { }
    }
}
