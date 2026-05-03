using System.Buffers.Binary;

namespace GameBoyEmulator.Core.Cartridge;

public sealed class Mbc3 : IMbc
{
    private const int RomBankSize = 0x4000;
    private const int RamBankSize = 0x2000;
    private const int RtcTrailerSize = 48;

    private readonly byte[] _rom;
    private readonly byte[] _ram;
    private readonly int _romBankMask;
    private readonly int _ramBankMask;
    private readonly IBatteryStore _store;
    private readonly ITimeProvider _timeProvider;
    private readonly string _saveKey;
    private readonly bool _hasBattery;
    private readonly bool _hasRtc;

    private bool _ramAndTimerEnabled;
    private byte _romBank = 1;
    private byte _ramBankOrRtcSelect;
    private bool _saveDirty;

    private byte _rtcSeconds;
    private byte _rtcMinutes;
    private byte _rtcHours;
    private byte _rtcDayLow;
    private byte _rtcDayHigh;

    private byte _latchedSeconds;
    private byte _latchedMinutes;
    private byte _latchedHours;
    private byte _latchedDayLow;
    private byte _latchedDayHigh;

    private byte _lastLatchWrite = 0xFF;
    private DateTime _baseTimeUtc;

    public Mbc3(
        byte[] rom,
        int ramSize,
        bool hasBattery,
        bool hasRtc,
        IBatteryStore store,
        ITimeProvider timeProvider,
        string saveKey)
    {
        if (rom.Length < RomBankSize * 2 || rom.Length % RomBankSize != 0)
            throw new ArgumentException($"MBC3 ROM size must be a multiple of {RomBankSize} and at least 2 banks; got {rom.Length}", nameof(rom));

        _rom = rom;
        _romBankMask = (rom.Length / RomBankSize) - 1;

        _ram = ramSize > 0 ? new byte[ramSize] : Array.Empty<byte>();
        _ramBankMask = ramSize > RamBankSize ? (ramSize / RamBankSize) - 1 : 0;

        _store = store;
        _timeProvider = timeProvider;
        _saveKey = saveKey;
        _hasBattery = hasBattery;
        _hasRtc = hasRtc;
        _baseTimeUtc = timeProvider.UtcNow;

        if (_hasBattery)
            LoadFromStore();
    }

    public void WriteBank0(ushort address, byte value)
    {
        if (address <= 0x1FFF)
        {
            var wasEnabled = _ramAndTimerEnabled;
            _ramAndTimerEnabled = (value & 0x0F) == 0x0A;
            if (wasEnabled && !_ramAndTimerEnabled)
                FlushIfDirty();
        }
        else
        {
            var bank = value & 0x7F;
            _romBank = (byte)(bank == 0 ? 1 : bank);
        }
    }

    public void WriteBankN(ushort address, byte value)
    {
        if (address <= 0x5FFF)
        {
            _ramBankOrRtcSelect = (byte)(value & 0x0F);
        }
        else
        {
            if (_lastLatchWrite == 0x00 && value == 0x01)
            {
                RefreshLiveRtc();
                _latchedSeconds = _rtcSeconds;
                _latchedMinutes = _rtcMinutes;
                _latchedHours = _rtcHours;
                _latchedDayLow = _rtcDayLow;
                _latchedDayHigh = _rtcDayHigh;
            }
            _lastLatchWrite = value;
        }
    }

    public void WriteExternalRam(ushort address, byte value)
    {
        if (!_ramAndTimerEnabled)
            return;

        if (_ramBankOrRtcSelect <= 0x03)
        {
            if (_ram.Length == 0)
                return;
            var offset = RamOffset(address);
            if (offset >= _ram.Length)
                return;
            _ram[offset] = value;
            _saveDirty = true;
        }
        else if (_hasRtc && _ramBankOrRtcSelect >= 0x08 && _ramBankOrRtcSelect <= 0x0C)
        {
            RefreshLiveRtc();
            switch (_ramBankOrRtcSelect)
            {
                case 0x08: _rtcSeconds = (byte)(value & 0x3F); break;
                case 0x09: _rtcMinutes = (byte)(value & 0x3F); break;
                case 0x0A: _rtcHours = (byte)(value & 0x1F); break;
                case 0x0B: _rtcDayLow = value; break;
                case 0x0C: _rtcDayHigh = (byte)(value & 0xC1); break;
            }
            _saveDirty = true;
        }
    }

    public byte ReadBank0(ushort address) => _rom[address];

    public byte ReadBankN(ushort address)
    {
        var bank = _romBank & _romBankMask;
        return _rom[bank * RomBankSize + (address - 0x4000)];
    }

    public byte ReadExternalRam(ushort address)
    {
        if (!_ramAndTimerEnabled)
            return 0xFF;

        if (_ramBankOrRtcSelect <= 0x03)
        {
            if (_ram.Length == 0)
                return 0xFF;
            var offset = RamOffset(address);
            return offset < _ram.Length ? _ram[offset] : (byte)0xFF;
        }

        if (_hasRtc && _ramBankOrRtcSelect >= 0x08 && _ramBankOrRtcSelect <= 0x0C)
        {
            return _ramBankOrRtcSelect switch
            {
                0x08 => _latchedSeconds,
                0x09 => _latchedMinutes,
                0x0A => _latchedHours,
                0x0B => _latchedDayLow,
                _ => _latchedDayHigh,
            };
        }

        return 0xFF;
    }

    public void Flush() => FlushIfDirty();

    private int RamOffset(ushort address)
    {
        var bank = _ramBankOrRtcSelect & _ramBankMask;
        return bank * RamBankSize + address;
    }

    private void RefreshLiveRtc()
    {
        var now = _timeProvider.UtcNow;
        var elapsed = now - _baseTimeUtc;
        if (elapsed.Ticks <= 0)
        {
            _baseTimeUtc = now;
            return;
        }

        var elapsedSeconds = (long)elapsed.TotalSeconds;
        _baseTimeUtc = _baseTimeUtc.AddSeconds(elapsedSeconds);

        if ((_rtcDayHigh & 0x40) != 0)
            return;

        if (elapsedSeconds <= 0)
            return;

        var totalSec = _rtcSeconds + elapsedSeconds;
        var carryMin = totalSec / 60;
        var newSec = totalSec % 60;

        var totalMin = _rtcMinutes + carryMin;
        var carryHour = totalMin / 60;
        var newMin = totalMin % 60;

        var totalHour = _rtcHours + carryHour;
        var carryDay = totalHour / 24;
        var newHour = totalHour % 24;

        var currentDay = ((long)(_rtcDayHigh & 0x01) << 8) | _rtcDayLow;
        var totalDay = currentDay + carryDay;
        var newDay = totalDay & 0x1FF;
        var carryFlag = (byte)((_rtcDayHigh & 0x80) | (totalDay >= 512 ? 0x80 : 0));
        var halt = (byte)(_rtcDayHigh & 0x40);

        _rtcSeconds = (byte)newSec;
        _rtcMinutes = (byte)newMin;
        _rtcHours = (byte)newHour;
        _rtcDayLow = (byte)(newDay & 0xFF);
        _rtcDayHigh = (byte)(halt | carryFlag | (byte)((newDay >> 8) & 0x01));
    }

    private void FlushIfDirty()
    {
        if (!_saveDirty || !_hasBattery)
            return;
        if (_ram.Length == 0 && !_hasRtc)
            return;

        var trailerSize = _hasRtc ? RtcTrailerSize : 0;
        var buffer = new byte[_ram.Length + trailerSize];
        if (_ram.Length > 0)
            Buffer.BlockCopy(_ram, 0, buffer, 0, _ram.Length);
        if (_hasRtc)
            WriteRtcTrailer(buffer.AsSpan(_ram.Length));

        _store.Save(_saveKey, buffer);
        _saveDirty = false;
    }

    private void LoadFromStore()
    {
        var loaded = _store.Load(_saveKey);
        if (loaded == null)
            return;

        var ramBytes = Math.Min(loaded.Length, _ram.Length);
        if (ramBytes > 0)
            Buffer.BlockCopy(loaded, 0, _ram, 0, ramBytes);

        if (_hasRtc && loaded.Length >= _ram.Length + RtcTrailerSize)
        {
            ReadRtcTrailer(loaded.AsSpan(_ram.Length, RtcTrailerSize));
            RefreshLiveRtc();
            _latchedSeconds = _rtcSeconds;
            _latchedMinutes = _rtcMinutes;
            _latchedHours = _rtcHours;
            _latchedDayLow = _rtcDayLow;
            _latchedDayHigh = _rtcDayHigh;
        }
    }

    private void WriteRtcTrailer(Span<byte> dest)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(dest[0..], _rtcSeconds);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[4..], _rtcMinutes);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[8..], _rtcHours);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[12..], _rtcDayLow);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[16..], _rtcDayHigh);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[20..], _latchedSeconds);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[24..], _latchedMinutes);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[28..], _latchedHours);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[32..], _latchedDayLow);
        BinaryPrimitives.WriteUInt32LittleEndian(dest[36..], _latchedDayHigh);
        var unix = new DateTimeOffset(_baseTimeUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        BinaryPrimitives.WriteInt64LittleEndian(dest[40..], unix);
    }

    private void ReadRtcTrailer(ReadOnlySpan<byte> src)
    {
        _rtcSeconds = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[0..]);
        _rtcMinutes = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[4..]);
        _rtcHours = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[8..]);
        _rtcDayLow = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[12..]);
        _rtcDayHigh = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[16..]);
        _latchedSeconds = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[20..]);
        _latchedMinutes = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[24..]);
        _latchedHours = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[28..]);
        _latchedDayLow = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[32..]);
        _latchedDayHigh = (byte)BinaryPrimitives.ReadUInt32LittleEndian(src[36..]);
        var unix = BinaryPrimitives.ReadInt64LittleEndian(src[40..]);
        _baseTimeUtc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
    }
}
