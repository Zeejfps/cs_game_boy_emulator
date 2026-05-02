using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

// Mooneye Test Suite (https://gekkio.fi/files/mooneye-test-suite/).
//
// Each test ends with the "magic breakpoint" instruction LD B,B (opcode 0x40)
// after writing a Fibonacci pattern into BCDEHL on success or 0x42-fill on
// failure. We run the CPU until PC sits on an LD B,B for two consecutive
// steps (i.e. the test has reached its halt loop), then read the registers.
[Trait("Category", "Mooneye")]
public class MooneyeTests
{
    private const long CycleBudget = 120_000_000L;
    private const byte LdBBOpcode = 0x40;

    private static readonly string RomRoot = Path.Combine(
        AppContext.BaseDirectory, "TestRoms", "mooneye", "acceptance");

    public static IEnumerable<object[]> DmgApplicableRoms()
    {
        if (!Directory.Exists(RomRoot)) yield break;

        foreach (var path in Directory.EnumerateFiles(RomRoot, "*.gb", SearchOption.AllDirectories)
                                      .OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (IsDmgApplicable(name))
                yield return new object[] { Path.GetRelativePath(RomRoot, path) };
        }
    }

    // Mooneye filename convention: `<test>[-<hardware>].gb`. We accept tests
    // with no hardware suffix or a suffix that includes DMG-ABC.
    private static bool IsDmgApplicable(string nameNoExt)
    {
        var dash = nameNoExt.LastIndexOf('-');
        if (dash < 0) return true;
        var suffix = nameNoExt.AsSpan(dash + 1);
        return suffix switch
        {
            "G"           => true,  // all GB models incl. CGB
            "GS"          => true,  // all GB models
            "dmgABC"      => true,
            "dmgABCmgb"   => true,
            // Other suffixes target hardware we don't emulate (SGB, CGB-only,
            // AGB, MGB-only, early DMG0). Skip rather than report as failures.
            _             => false,
        };
    }

    [Theory]
    [MemberData(nameof(DmgApplicableRoms))]
    public void MooneyeRomPasses(string relativePath)
    {
        var path = Path.Combine(RomRoot, relativePath);
        Assert.True(File.Exists(path), $"Mooneye ROM not found: {path}");

        var rom = File.ReadAllBytes(path);
        var system = BuildSystem(rom);

        // Mooneye's halt loop is `LD B,B; JR -2`, so PC oscillates between the
        // LD B,B address and the JR address. Detecting PC at the LD B,B once
        // is enough — the opcode is never used in regular test code.
        long total = 0;
        var done = false;
        while (total < CycleBudget)
        {
            if (system.Mmu.Read(system.Cpu.Pc) == LdBBOpcode) { done = true; break; }

            system.Cpu.Step();
            total += system.BusClock.ConsumeAccumulated();
        }

        var cpu = system.Cpu;
        if (!done)
            Assert.Fail($"Test '{relativePath}' did not reach LD B,B within {CycleBudget} T-states. " +
                        $"PC=0x{cpu.Pc:X4}");

        // 0x42-fill is the explicit fail signal.
        if (cpu.Rb == 0x42 && cpu.Rc == 0x42 && cpu.Rd == 0x42 &&
            cpu.Re == 0x42 && cpu.Rh == 0x42 && cpu.Rl == 0x42)
        {
            Assert.Fail($"Test '{relativePath}' failed (0x42-fill).");
        }

        // Pass = Fibonacci 3,5,8,13,21,34 in BCDEHL.
        Assert.True(
            cpu.Rb == 3 && cpu.Rc == 5 && cpu.Rd == 8 &&
            cpu.Re == 13 && cpu.Rh == 21 && cpu.Rl == 34,
            $"Test '{relativePath}' did not signal pass. Registers: " +
            $"B={cpu.Rb:X2} C={cpu.Rc:X2} D={cpu.Rd:X2} " +
            $"E={cpu.Re:X2} H={cpu.Rh:X2} L={cpu.Rl:X2}");
    }

    private sealed record System(Cpu Cpu, Mmu Mmu, Ppu Ppu, Timer Timer, BusClock BusClock);

    private static System BuildSystem(byte[] rom)
    {
        var interrupts = new Interrupts();
        var ppu = new Ppu(interrupts);
        var timer = new Timer(interrupts);
        var mbcFactory = new MbcFactory(new MooneyeBatteryStore());
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
        // SkipBoot leaves the I/O registers cold; mooneye tests assume the
        // post-boot state the DMG boot ROM normally writes. Mirror what
        // GameBoy.SkipBootIo does.
        mmu.Write(0xFF40, 0x91);
        mmu.Write(0xFF47, 0xFC);
        mmu.Write(0xFF48, 0xFF);
        mmu.Write(0xFF49, 0xFF);
        return new System(cpu, mmu, ppu, timer, busClock);
    }

    private sealed class MooneyeBatteryStore : IBatteryStore
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
