using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

// dmg-acid2 (https://github.com/mattcurrie/dmg-acid2) is a single-frame
// pixel-perfect rendering test. The ROM finishes with `LD B,B; JR -2`, just
// like Mooneye, so we reuse the magic-breakpoint detection; once the ROM is
// in its halt loop the framebuffer is final and we compare every pixel
// against the reference.
[Trait("Category", "DmgAcid2")]
public class DmgAcid2Tests
{
    private const long CycleBudget = 60_000_000L;
    private const byte LdBBOpcode = 0x40;
    private const int FrameBytes = Ppu.ScreenWidth * Ppu.ScreenHeight;

    private static readonly string RomDir = Path.Combine(
        AppContext.BaseDirectory, "TestRoms", "dmg-acid2");

    [Fact]
    public void DmgAcid2_FramebufferMatchesReference()
    {
        var romPath = Path.Combine(RomDir, "dmg-acid2.gb");
        var refPath = Path.Combine(RomDir, "reference-dmg.bin");

        Assert.True(
            File.Exists(romPath),
            $"dmg-acid2 ROM not found: {romPath}. " +
            "See TestRoms/dmg-acid2/README.md for fetch instructions.");
        Assert.True(
            File.Exists(refPath),
            $"dmg-acid2 reference not found: {refPath}. " +
            "See TestRoms/dmg-acid2/README.md for how to produce it.");

        var reference = File.ReadAllBytes(refPath);
        Assert.True(
            reference.Length == FrameBytes,
            $"Reference must be exactly {FrameBytes} bytes (160×144), got {reference.Length}.");

        var rom = File.ReadAllBytes(romPath);
        var system = BuildSystem(rom);

        long total = 0;
        var done = false;
        while (total < CycleBudget)
        {
            if (system.Mmu.Read(system.Cpu.Pc) == LdBBOpcode) { done = true; break; }
            system.Cpu.Step();
            total += system.BusClock.ConsumeAccumulated();
        }

        Assert.True(
            done,
            $"dmg-acid2 did not reach LD B,B within {CycleBudget} T-states. " +
            $"PC=0x{system.Cpu.Pc:X4}");

        var fb = system.Ppu.FrameBuffer.Span;
        var diffs = new List<(int x, int y, byte expected, byte actual)>();
        for (var i = 0; i < FrameBytes; i++)
        {
            if (fb[i] != reference[i])
            {
                var x = i % Ppu.ScreenWidth;
                var y = i / Ppu.ScreenWidth;
                diffs.Add((x, y, reference[i], fb[i]));
                if (diffs.Count >= 10) break;
            }
        }

        if (diffs.Count > 0)
        {
            var sample = string.Join(
                "\n  ",
                diffs.Select(d => $"({d.x,3},{d.y,3}) expected={d.expected} actual={d.actual}"));
            Assert.Fail(
                $"dmg-acid2 framebuffer differs from reference (showing first {diffs.Count}):\n  {sample}");
        }
    }

    private sealed record System(Cpu Cpu, Mmu Mmu, Ppu Ppu, BusClock BusClock);

    private static System BuildSystem(byte[] rom)
    {
        var interrupts = new Interrupts();
        var ppu = new Ppu(interrupts);
        var timer = new Timer(interrupts);
        var mbcFactory = new MbcFactory(new NullBatteryStore());
        var mbc = mbcFactory.Create(rom);
        var mmu = new Mmu(
            mbc,
            ppu,
            new NullJoypad(),
            timer,
            new NullApu(),
            new NullSerial(),
            interrupts);
        var dma = new OamDmaController(mmu, ppu);
        var busClock = new BusClock(ppu, timer, dma);
        var cpu = new Cpu(dma, busClock, interrupts);
        cpu.SkipBoot();
        // Match what GameBoy.SkipBootIo does — without these, LCDC/BGP stay
        // cold and games that don't initialize them render blank.
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
