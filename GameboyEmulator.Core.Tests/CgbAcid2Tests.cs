using System.Runtime.InteropServices;
using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

// cgb-acid2 (https://github.com/mattcurrie/cgb-acid2) is the CGB counterpart
// to dmg-acid2: a single-frame pixel-perfect rendering test that exercises
// CGB-specific PPU behavior — BG-to-OAM priority, OAM-index sprite priority,
// LCDC.0 master priority, BG attribute palette/bank/flip bits.
//
// The ROM finishes with `LD B,B; JR -2`, same magic-breakpoint convention as
// Mooneye/dmg-acid2. Once we observe LD B,B at PC, the framebuffer is final
// and we compare RGBA bytes against the reference.
[Trait("Category", "CgbAcid2")]
public class CgbAcid2Tests
{
    private const long CycleBudget = 60_000_000L;
    private const byte LdBBOpcode = 0x40;
    private const int FrameBytes = Ppu.ScreenWidth * Ppu.ScreenHeight * 4;

    private static readonly string RomDir = Path.Combine(
        AppContext.BaseDirectory, "TestRoms", "cgb-acid2");

    [Fact]
    public void CgbAcid2_FramebufferMatchesReference()
    {
        var romPath = Path.Combine(RomDir, "cgb-acid2.gbc");
        var refPath = Path.Combine(RomDir, "reference-cgb.bin");

        Assert.True(
            File.Exists(romPath),
            $"cgb-acid2 ROM not found: {romPath}. " +
            "See TestRoms/cgb-acid2/README.md for fetch instructions.");
        Assert.True(
            File.Exists(refPath),
            $"cgb-acid2 reference not found: {refPath}. " +
            "See TestRoms/cgb-acid2/README.md for how to produce it.");

        var reference = File.ReadAllBytes(refPath);
        Assert.True(
            reference.Length == FrameBytes,
            $"Reference must be exactly {FrameBytes} bytes (160×144×4 RGBA), got {reference.Length}.");

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
            $"cgb-acid2 did not reach LD B,B within {CycleBudget} T-states. " +
            $"PC=0x{system.Cpu.Pc:X4}");

        // RgbFrameBuffer is uint[] stored 0xAA_BB_GG_RR. On a little-endian
        // host that's identical to the R,G,B,A byte layout we keep in the
        // reference file, so a direct byte compare is correct.
        var fbBytes = MemoryMarshal.AsBytes(system.Ppu.RgbFrameBuffer.Span);

        var diffs = new List<(int x, int y, uint expected, uint actual)>();
        for (var i = 0; i < FrameBytes; i += 4)
        {
            if (fbBytes[i] == reference[i] &&
                fbBytes[i + 1] == reference[i + 1] &&
                fbBytes[i + 2] == reference[i + 2] &&
                fbBytes[i + 3] == reference[i + 3])
                continue;

            var pixel = i / 4;
            var x = pixel % Ppu.ScreenWidth;
            var y = pixel / Ppu.ScreenWidth;
            var expected = (uint)reference[i] | ((uint)reference[i + 1] << 8) |
                           ((uint)reference[i + 2] << 16) | ((uint)reference[i + 3] << 24);
            var actual = (uint)fbBytes[i] | ((uint)fbBytes[i + 1] << 8) |
                         ((uint)fbBytes[i + 2] << 16) | ((uint)fbBytes[i + 3] << 24);
            diffs.Add((x, y, expected, actual));
            if (diffs.Count >= 10) break;
        }

        if (diffs.Count > 0)
        {
            var sample = string.Join(
                "\n  ",
                diffs.Select(d => $"({d.x,3},{d.y,3}) expected=0x{d.expected:X8} actual=0x{d.actual:X8}"));
            Assert.Fail(
                $"cgb-acid2 framebuffer differs from reference (showing first {diffs.Count}):\n  {sample}");
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

        // Enable CGB mode across the bus before SkipBoot so the post-boot
        // I/O register state is interpreted in CGB context.
        var isCgb = MbcFactory.IsCgbCartridge(rom);
        Assert.True(isCgb, "cgb-acid2.gbc should report itself as a CGB cart (0x143 bit 7 set).");
        mmu.SetCgbMode(isCgb);
        ppu.SetCgbMode(isCgb);
        // NullApu has no SetCgbMode — APU sound output isn't part of the test.

        var busClock = new SystemClock(ppu, timer, dma, apu);
        var cpu = new Cpu(dma, busClock, interrupts);
        cpu.SetCgbMode(isCgb);
        mmu.SetSpeedController(cpu);

        cpu.SkipBoot();
        // SkipBoot leaves I/O registers cold. The CGB boot ROM normally leaves
        // LCDC=0x91 and clears the DMG palette regs to 0xFC/0xFF; CGB-only
        // palette RAM the ROM initializes itself.
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
