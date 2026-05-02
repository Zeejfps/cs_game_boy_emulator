using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;

namespace GameBoyEmulator.Core.Tests;

public class Mbc1Tests
{
    private sealed class NullBatteryStore : IBatteryStore
    {
        public byte[]? Load(string key) => null;
        public void Save(string key, ReadOnlySpan<byte> data) { }
    }

    private sealed class CapturingBatteryStore : IBatteryStore
    {
        public byte[]? PreloadData { get; set; }
        public byte[]? LastSaved { get; private set; }
        public int SaveCount { get; private set; }
        public string? LastKey { get; private set; }

        public byte[]? Load(string key)
        {
            LastKey = key;
            return PreloadData;
        }

        public void Save(string key, ReadOnlySpan<byte> data)
        {
            LastKey = key;
            LastSaved = data.ToArray();
            SaveCount++;
        }
    }

    private static byte[] BuildRom(int banks)
    {
        var rom = new byte[banks * 0x4000];
        // Each bank starts with its own bank number so reads are easy to verify.
        for (var b = 0; b < banks; b++)
            rom[b * 0x4000] = (byte)b;
        return rom;
    }

    private static Mbc1 NewMbc(
        int banks = 4,
        int ramSize = 0,
        bool hasBattery = false,
        IBatteryStore? store = null)
        => new Mbc1(BuildRom(banks), ramSize, hasBattery, store ?? new NullBatteryStore(), "TEST");

    [Fact]
    public void Bank0_RegionAlwaysReadsBank0_InModeZero()
    {
        var mbc = NewMbc(banks: 4);
        mbc.WriteBank0(0x2000, 0x02); // try to set low bank to 2
        Assert.Equal(0x00, mbc.ReadBank0(0x0000));
    }

    [Fact]
    public void BankN_LowerFiveBitsZero_TranslatedToOne()
    {
        var mbc = NewMbc(banks: 4);
        mbc.WriteBank0(0x2000, 0x00); // requests bank 0 → becomes 1
        Assert.Equal(0x01, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void BankN_SelectsRequestedBank()
    {
        var mbc = NewMbc(banks: 4);
        mbc.WriteBank0(0x2000, 0x03);
        Assert.Equal(0x03, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void BankN_HighBitsCombinedWithLowBits()
    {
        // 128 banks = 7 bits; bankHigh<<5 | bankLow.
        var mbc = NewMbc(banks: 128);
        mbc.WriteBankN(0x4000, 0x02);   // upper2 = 0b10
        mbc.WriteBank0(0x2000, 0x05);   // lower5 = 0b00101
        // effective bank = (0b10 << 5) | 0b00101 = 0x45
        Assert.Equal(0x45, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void BankN_HighBitsApplyEvenWhenLowerFiveZero_StillForcedToOne()
    {
        // Famous quirk: banks 0x20, 0x40, 0x60 are unreachable in BankN region.
        var mbc = NewMbc(banks: 128);
        mbc.WriteBankN(0x4000, 0x01);   // upper2 = 1 → would target 0x20
        mbc.WriteBank0(0x2000, 0x00);   // lower5 = 0 → forced to 1
        // effective bank = (1<<5) | 1 = 0x21, not 0x20
        Assert.Equal(0x21, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void Bank0_InModeOne_LargeRom_UsesUpperBitsForBank0()
    {
        var mbc = NewMbc(banks: 128);
        mbc.WriteBankN(0x4000, 0x02);   // upper2 = 2
        mbc.WriteBankN(0x6000, 0x01);   // mode = 1
        // bank0 region now selects bank (2<<5) = 0x40
        Assert.Equal(0x40, mbc.ReadBank0(0x0000));
    }

    [Fact]
    public void ExternalRam_ReturnsFF_WhenDisabled()
    {
        var mbc = NewMbc(ramSize: 0x2000);
        Assert.Equal(0xFF, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void ExternalRam_WriteIgnored_WhenDisabled()
    {
        var mbc = NewMbc(ramSize: 0x2000);
        mbc.WriteExternalRam(0x0000, 0x42);
        // enable to read back; should still be 0
        mbc.WriteBank0(0x0000, 0x0A);
        Assert.Equal(0x00, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void ExternalRam_ReadWrite_WhenEnabled()
    {
        var mbc = NewMbc(ramSize: 0x2000);
        mbc.WriteBank0(0x0000, 0x0A);
        mbc.WriteExternalRam(0x0123, 0x99);
        Assert.Equal(0x99, mbc.ReadExternalRam(0x0123));
    }

    [Fact]
    public void ExternalRam_BankSwitch_InModeOne_With32KbRam()
    {
        var mbc = NewMbc(ramSize: 0x8000);
        mbc.WriteBankN(0x6000, 0x01);   // mode = 1
        mbc.WriteBank0(0x0000, 0x0A);   // enable RAM

        mbc.WriteBankN(0x4000, 0x00);
        mbc.WriteExternalRam(0x0000, 0x11);

        mbc.WriteBankN(0x4000, 0x01);
        mbc.WriteExternalRam(0x0000, 0x22);

        mbc.WriteBankN(0x4000, 0x00);
        Assert.Equal(0x11, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x01);
        Assert.Equal(0x22, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Persistence_LoadsSaveOnConstruction_WhenBattery()
    {
        var saved = new byte[0x2000];
        saved[0x0010] = 0x77;
        var store = new CapturingBatteryStore { PreloadData = saved };

        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, store: store);
        mbc.WriteBank0(0x0000, 0x0A); // enable RAM

        Assert.Equal(0x77, mbc.ReadExternalRam(0x0010));
        Assert.Equal("TEST", store.LastKey);
    }

    [Fact]
    public void Persistence_DoesNotLoad_WhenNoBattery()
    {
        var saved = new byte[0x2000];
        saved[0x0010] = 0x77;
        var store = new CapturingBatteryStore { PreloadData = saved };

        var mbc = NewMbc(ramSize: 0x2000, hasBattery: false, store: store);
        mbc.WriteBank0(0x0000, 0x0A);

        Assert.Equal(0x00, mbc.ReadExternalRam(0x0010));
    }

    [Fact]
    public void Persistence_SavesOnRamDisableTransition()
    {
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, store: store);

        mbc.WriteBank0(0x0000, 0x0A);              // enable
        mbc.WriteExternalRam(0x0000, 0xAB);        // dirty
        mbc.WriteBank0(0x0000, 0x00);              // disable → should flush

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0xAB, store.LastSaved![0]);
    }

    [Fact]
    public void Persistence_FlushSavesDirtyRam()
    {
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, store: store);

        mbc.WriteBank0(0x0000, 0x0A);
        mbc.WriteExternalRam(0x0005, 0x55);
        mbc.Flush();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0x55, store.LastSaved![0x0005]);
    }

    [Fact]
    public void Persistence_FlushIsNoop_WhenNotDirty()
    {
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, store: store);

        mbc.Flush();
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Persistence_FlushIsNoop_WhenNoBattery()
    {
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0x2000, hasBattery: false, store: store);

        mbc.WriteBank0(0x0000, 0x0A);
        mbc.WriteExternalRam(0x0000, 0xAB);
        mbc.Flush();

        Assert.Equal(0, store.SaveCount);
    }
}
