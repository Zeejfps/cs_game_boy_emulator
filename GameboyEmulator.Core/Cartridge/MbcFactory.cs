using System.Text;

namespace GameBoyEmulator.Core.Cartridge;

public sealed class MbcFactory
{
    private const int CartTypeAddress = 0x0147;
    private const int RomSizeAddress = 0x0148;
    private const int RamSizeAddress = 0x0149;
    private const int TitleStart = 0x0134;
    private const int TitleEnd = 0x0143;

    private readonly IBatteryStore _batteryStore;
    private readonly ITimeProvider _timeProvider;

    public MbcFactory(IBatteryStore batteryStore, ITimeProvider timeProvider)
    {
        _batteryStore = batteryStore;
        _timeProvider = timeProvider;
    }

    public IMbc Create(byte[] rom)
    {
        if (rom.Length < 0x0150)
            throw new ArgumentException("ROM is smaller than the cartridge header", nameof(rom));

        var cartType = rom[CartTypeAddress];
        var title = ReadTitle(rom);
        return cartType switch
        {
            0x00 => new RomOnlyMbc(rom),
            0x01 => new Mbc1(rom, 0, hasBattery: false, _batteryStore, title),
            0x02 => new Mbc1(rom, ReadRamSize(rom), hasBattery: false, _batteryStore, title),
            0x03 => new Mbc1(rom, ReadRamSize(rom), hasBattery: true, _batteryStore, title),
            0x0F => new Mbc3(rom, 0, hasBattery: true, hasRtc: true, _batteryStore, _timeProvider, title),
            0x10 => new Mbc3(rom, ReadRamSize(rom), hasBattery: true, hasRtc: true, _batteryStore, _timeProvider, title),
            0x11 => new Mbc3(rom, 0, hasBattery: false, hasRtc: false, _batteryStore, _timeProvider, title),
            0x12 => new Mbc3(rom, ReadRamSize(rom), hasBattery: false, hasRtc: false, _batteryStore, _timeProvider, title),
            0x13 => new Mbc3(rom, ReadRamSize(rom), hasBattery: true, hasRtc: false, _batteryStore, _timeProvider, title),
            0x19 => new Mbc5(rom, 0, hasBattery: false, _batteryStore, title),
            0x1A => new Mbc5(rom, ReadRamSize(rom), hasBattery: false, _batteryStore, title),
            0x1B => new Mbc5(rom, ReadRamSize(rom), hasBattery: true, _batteryStore, title),
            // 0x1C-0x1E add rumble; we don't model rumble but the MBC behaves identically.
            0x1C => new Mbc5(rom, 0, hasBattery: false, _batteryStore, title),
            0x1D => new Mbc5(rom, ReadRamSize(rom), hasBattery: false, _batteryStore, title),
            0x1E => new Mbc5(rom, ReadRamSize(rom), hasBattery: true, _batteryStore, title),
            _ => throw new NotSupportedException($"Cartridge type 0x{cartType:X2} is not supported")
        };
    }

    private static int ReadRamSize(byte[] rom) =>
        rom[RamSizeAddress] switch
        {
            0x00 => 0,
            0x01 => 0x0800,   // 2 KB (unofficial)
            0x02 => 0x2000,   // 8 KB
            0x03 => 0x8000,   // 32 KB
            var v => throw new NotSupportedException($"Unsupported RAM size code 0x{v:X2}")
        };

    private static string ReadTitle(byte[] rom)
    {
        var end = TitleStart;
        for (var i = TitleStart; i <= TitleEnd; i++)
        {
            if (rom[i] == 0)
                break;
            end = i + 1;
        }
        var raw = Encoding.ASCII.GetString(rom, TitleStart, end - TitleStart).Trim();
        return string.IsNullOrEmpty(raw) ? "UNTITLED" : raw;
    }
}
