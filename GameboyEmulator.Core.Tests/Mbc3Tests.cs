using System.Buffers.Binary;
using GameBoyEmulator.Core.Cartridge;

namespace GameBoyEmulator.Core.Tests;

public class Mbc3Tests
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

    private sealed class FakeTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public void Advance(TimeSpan ts) => UtcNow = UtcNow + ts;
    }

    private static byte[] BuildRom(int banks)
    {
        var rom = new byte[banks * 0x4000];
        for (var b = 0; b < banks; b++)
            rom[b * 0x4000] = (byte)b;
        return rom;
    }

    private static Mbc3 NewMbc(
        int banks = 4,
        int ramSize = 0,
        bool hasBattery = false,
        bool hasRtc = false,
        IBatteryStore? store = null,
        FakeTimeProvider? clock = null)
        => new Mbc3(
            BuildRom(banks),
            ramSize,
            hasBattery,
            hasRtc,
            store ?? new NullBatteryStore(),
            clock ?? new FakeTimeProvider(),
            "TEST");

    private static void EnableRamAndRtc(Mbc3 mbc) => mbc.WriteBank0(0x0000, 0x0A);
    private static void DisableRamAndRtc(Mbc3 mbc) => mbc.WriteBank0(0x0000, 0x00);

    private static void Latch(Mbc3 mbc)
    {
        mbc.WriteBankN(0x6000, 0x00);
        mbc.WriteBankN(0x6000, 0x01);
    }

    [Fact]
    public void Bank0_AlwaysReadsFixedBankZero()
    {
        var mbc = NewMbc(banks: 4);
        mbc.WriteBank0(0x2000, 0x02);
        Assert.Equal(0x00, mbc.ReadBank0(0x0000));
    }

    [Fact]
    public void BankN_BankZeroForcedToOne()
    {
        var mbc = NewMbc(banks: 4);
        mbc.WriteBank0(0x2000, 0x00);
        Assert.Equal(0x01, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void BankN_SelectsRequestedBank()
    {
        var mbc = NewMbc(banks: 8);
        mbc.WriteBank0(0x2000, 0x05);
        Assert.Equal(0x05, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void BankN_SevenBitsRespected_HighBitTruncated()
    {
        // 128 banks. Writing 0xFF should truncate to 0x7F.
        var mbc = NewMbc(banks: 128);
        mbc.WriteBank0(0x2000, 0xFF);
        Assert.Equal(0x7F, mbc.ReadBankN(0x4000));
    }

    [Fact]
    public void BankN_NoModeRegisterEffect()
    {
        // Unlike MBC1, writing to 0x6000-0x7FFF (other than the 0->1 latch sequence)
        // must not change the ROM bank visible at 0x4000-0x7FFF.
        var mbc = NewMbc(banks: 8);
        mbc.WriteBank0(0x2000, 0x03);
        mbc.WriteBankN(0x6000, 0x01); // would have been mode toggle in MBC1
        Assert.Equal(0x03, mbc.ReadBankN(0x4000));
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
        EnableRamAndRtc(mbc);
        Assert.Equal(0x00, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void ExternalRam_ReadWrite_WhenEnabled()
    {
        var mbc = NewMbc(ramSize: 0x2000);
        EnableRamAndRtc(mbc);
        mbc.WriteExternalRam(0x0123, 0x99);
        Assert.Equal(0x99, mbc.ReadExternalRam(0x0123));
    }

    [Fact]
    public void ExternalRam_BankSwitch_With32KbRam()
    {
        var mbc = NewMbc(ramSize: 0x8000);
        EnableRamAndRtc(mbc);

        mbc.WriteBankN(0x4000, 0x00);
        mbc.WriteExternalRam(0x0000, 0x11);

        mbc.WriteBankN(0x4000, 0x01);
        mbc.WriteExternalRam(0x0000, 0x22);

        mbc.WriteBankN(0x4000, 0x02);
        mbc.WriteExternalRam(0x0000, 0x33);

        mbc.WriteBankN(0x4000, 0x03);
        mbc.WriteExternalRam(0x0000, 0x44);

        mbc.WriteBankN(0x4000, 0x00);
        Assert.Equal(0x11, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x01);
        Assert.Equal(0x22, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x02);
        Assert.Equal(0x33, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x03);
        Assert.Equal(0x44, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Rtc_LatchedRegistersStartAtZero()
    {
        var mbc = NewMbc(hasRtc: true);
        EnableRamAndRtc(mbc);
        Latch(mbc);

        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(0x00, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x09);
        Assert.Equal(0x00, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Rtc_LiveRegistersAdvanceWithClock()
    {
        var clock = new FakeTimeProvider();
        var mbc = NewMbc(hasRtc: true, clock: clock);
        EnableRamAndRtc(mbc);

        clock.Advance(TimeSpan.FromSeconds(65));
        Latch(mbc);

        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(5, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x09);
        Assert.Equal(1, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Rtc_LatchSequenceRequiresZeroThenOne()
    {
        var clock = new FakeTimeProvider();
        var mbc = NewMbc(hasRtc: true, clock: clock);
        EnableRamAndRtc(mbc);

        clock.Advance(TimeSpan.FromSeconds(30));
        // Wrong sequence: 0x01 then 0x01 should not latch
        mbc.WriteBankN(0x6000, 0x01);
        mbc.WriteBankN(0x6000, 0x01);

        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(0, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Rtc_HaltBitFreezesTime()
    {
        var clock = new FakeTimeProvider();
        var mbc = NewMbc(hasRtc: true, clock: clock);
        EnableRamAndRtc(mbc);

        // Set halt bit (bit 6 of dayHigh register)
        mbc.WriteBankN(0x4000, 0x0C);
        mbc.WriteExternalRam(0x0000, 0x40);

        clock.Advance(TimeSpan.FromHours(1));
        Latch(mbc);

        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(0, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x0A);
        Assert.Equal(0, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Rtc_DayCarryOnOverflow()
    {
        var clock = new FakeTimeProvider();
        var mbc = NewMbc(hasRtc: true, clock: clock);
        EnableRamAndRtc(mbc);

        // Set day register to 511 (0x1FF). dayLow = 0xFF, dayHigh bit0 = 1.
        mbc.WriteBankN(0x4000, 0x0B);
        mbc.WriteExternalRam(0x0000, 0xFF);
        mbc.WriteBankN(0x4000, 0x0C);
        mbc.WriteExternalRam(0x0000, 0x01); // halt cleared, day MSB = 1

        clock.Advance(TimeSpan.FromDays(1));
        Latch(mbc);

        mbc.WriteBankN(0x4000, 0x0B);
        Assert.Equal(0x00, mbc.ReadExternalRam(0x0000));
        mbc.WriteBankN(0x4000, 0x0C);
        var dayHigh = mbc.ReadExternalRam(0x0000);
        Assert.Equal(0x80, dayHigh & 0x80); // carry set
        Assert.Equal(0x00, dayHigh & 0x01); // day MSB cleared after wrap
    }

    [Fact]
    public void Rtc_WriteToRegistersUpdatesLive()
    {
        var clock = new FakeTimeProvider();
        var mbc = NewMbc(hasRtc: true, clock: clock);
        EnableRamAndRtc(mbc);

        // Halt the clock so we can set seconds deterministically before letting time advance.
        mbc.WriteBankN(0x4000, 0x0C);
        mbc.WriteExternalRam(0x0000, 0x40);

        mbc.WriteBankN(0x4000, 0x08);
        mbc.WriteExternalRam(0x0000, 30);

        // Unhalt and let 5 seconds pass.
        mbc.WriteBankN(0x4000, 0x0C);
        mbc.WriteExternalRam(0x0000, 0x00);
        clock.Advance(TimeSpan.FromSeconds(5));
        Latch(mbc);

        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(35, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Rtc_ReadsLatchedNotLive()
    {
        var clock = new FakeTimeProvider();
        var mbc = NewMbc(hasRtc: true, clock: clock);
        EnableRamAndRtc(mbc);

        clock.Advance(TimeSpan.FromSeconds(10));
        Latch(mbc);

        // Time advances, but reads should still return the latched snapshot.
        clock.Advance(TimeSpan.FromSeconds(20));

        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(10, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Persistence_LoadsRamOnConstruction_WhenBattery()
    {
        var saved = new byte[0x2000];
        saved[0x0010] = 0x77;
        var store = new CapturingBatteryStore { PreloadData = saved };

        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, store: store);
        EnableRamAndRtc(mbc);

        Assert.Equal(0x77, mbc.ReadExternalRam(0x0010));
        Assert.Equal("TEST", store.LastKey);
    }

    [Fact]
    public void Persistence_RestoresElapsedRealTime()
    {
        // Persisted state: latched 12:00:00 day 0, base time = clock.UtcNow - 1h
        var clock = new FakeTimeProvider();
        var savedAt = clock.UtcNow - TimeSpan.FromHours(1);

        var trailer = new byte[48];
        // live registers represent state at savedAt: 12:00:00 day 0
        WriteUInt32(trailer, 0, 0);   // sec
        WriteUInt32(trailer, 4, 0);   // min
        WriteUInt32(trailer, 8, 12);  // hour
        WriteUInt32(trailer, 12, 0);  // dayLow
        WriteUInt32(trailer, 16, 0);  // dayHigh
        WriteUInt32(trailer, 20, 0);
        WriteUInt32(trailer, 24, 0);
        WriteUInt32(trailer, 28, 12);
        WriteUInt32(trailer, 32, 0);
        WriteUInt32(trailer, 36, 0);
        WriteInt64(trailer, 40, new DateTimeOffset(savedAt, TimeSpan.Zero).ToUnixTimeSeconds());

        var store = new CapturingBatteryStore { PreloadData = trailer };
        var mbc = NewMbc(ramSize: 0, hasBattery: true, hasRtc: true, store: store, clock: clock);

        EnableRamAndRtc(mbc);
        Latch(mbc);

        mbc.WriteBankN(0x4000, 0x0A);
        Assert.Equal(13, mbc.ReadExternalRam(0x0000)); // 12 + 1 hour elapsed
    }

    [Fact]
    public void Persistence_LegacySaveWithoutRtcTrailer()
    {
        var saved = new byte[0x2000];
        saved[0x0042] = 0xAB;
        var store = new CapturingBatteryStore { PreloadData = saved };

        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, hasRtc: true, store: store);
        EnableRamAndRtc(mbc);

        Assert.Equal(0xAB, mbc.ReadExternalRam(0x0042));
        Latch(mbc);
        mbc.WriteBankN(0x4000, 0x08);
        Assert.Equal(0, mbc.ReadExternalRam(0x0000));
    }

    [Fact]
    public void Persistence_FlushSavesRamAndRtc()
    {
        var clock = new FakeTimeProvider();
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, hasRtc: true, store: store, clock: clock);

        EnableRamAndRtc(mbc);
        mbc.WriteExternalRam(0x0005, 0x55);
        mbc.Flush();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0x2000 + 48, store.LastSaved!.Length);
        Assert.Equal(0x55, store.LastSaved[0x0005]);
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

        EnableRamAndRtc(mbc);
        mbc.WriteExternalRam(0x0000, 0xAB);
        mbc.Flush();

        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Persistence_SavesOnRamDisableTransition()
    {
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0x2000, hasBattery: true, store: store);

        EnableRamAndRtc(mbc);
        mbc.WriteExternalRam(0x0000, 0xAB);
        DisableRamAndRtc(mbc);

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0xAB, store.LastSaved![0]);
    }

    [Fact]
    public void Persistence_RtcOnlyCart_FlushesJustTrailer()
    {
        var clock = new FakeTimeProvider();
        var store = new CapturingBatteryStore();
        var mbc = NewMbc(ramSize: 0, hasBattery: true, hasRtc: true, store: store, clock: clock);

        EnableRamAndRtc(mbc);
        // Writing to RTC marks dirty.
        mbc.WriteBankN(0x4000, 0x08);
        mbc.WriteExternalRam(0x0000, 5);
        mbc.Flush();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(48, store.LastSaved!.Length);
    }

    private static void WriteUInt32(byte[] dest, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(offset), value);

    private static void WriteInt64(byte[] dest, int offset, long value)
        => BinaryPrimitives.WriteInt64LittleEndian(dest.AsSpan(offset), value);
}
