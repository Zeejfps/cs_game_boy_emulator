namespace GameBoyEmulator.Core.Cartridge;

public sealed class Mbc1 : IMbc
{
    private const int RomBankSize = 0x4000;
    private const int RamBankSize = 0x2000;

    private readonly byte[] _rom;
    private readonly byte[] _ram;
    private readonly int _romBankMask;
    private readonly int _ramBankMask;
    private readonly IBatteryStore _store;
    private readonly string _saveKey;
    private readonly bool _hasBattery;

    private bool _ramEnabled;
    private byte _bankLow = 1;
    private byte _bankHigh;
    private byte _mode;
    private bool _ramDirty;

    public Mbc1(byte[] rom, int ramSize, bool hasBattery, IBatteryStore store, string saveKey)
    {
        if (rom.Length < RomBankSize * 2 || rom.Length % RomBankSize != 0)
            throw new ArgumentException($"MBC1 ROM size must be a multiple of {RomBankSize} and at least 2 banks; got {rom.Length}", nameof(rom));

        _rom = rom;
        _romBankMask = (rom.Length / RomBankSize) - 1;

        _ram = ramSize > 0 ? new byte[ramSize] : Array.Empty<byte>();
        _ramBankMask = ramSize > RamBankSize ? (ramSize / RamBankSize) - 1 : 0;

        _store = store;
        _saveKey = saveKey;
        _hasBattery = hasBattery;

        if (_hasBattery && _ram.Length > 0)
        {
            var loaded = _store.Load(_saveKey);
            if (loaded != null && loaded.Length == _ram.Length)
                Buffer.BlockCopy(loaded, 0, _ram, 0, _ram.Length);
        }
    }

    public void WriteBank0(ushort address, byte value)
    {
        if (address <= 0x1FFF)
        {
            var wasEnabled = _ramEnabled;
            _ramEnabled = (value & 0x0F) == 0x0A;
            if (wasEnabled && !_ramEnabled)
                FlushIfDirty();
        }
        else
        {
            var low = value & 0x1F;
            _bankLow = (byte)(low == 0 ? 1 : low);
        }
    }

    public void WriteBankN(ushort address, byte value)
    {
        if (address <= 0x5FFF)
            _bankHigh = (byte)(value & 0x03);
        else
            _mode = (byte)(value & 0x01);
    }

    public void WriteExternalRam(ushort address, byte value)
    {
        if (!_ramEnabled || _ram.Length == 0)
            return;
        var offset = RamOffset(address);
        if (offset >= _ram.Length)
            return;
        _ram[offset] = value;
        _ramDirty = true;
    }

    public byte ReadBank0(ushort address) => _rom[Bank0Offset() + address];

    public byte ReadBankN(ushort address) => _rom[BankNOffset() + (address - 0x4000)];

    public byte ReadExternalRam(ushort address)
    {
        if (!_ramEnabled || _ram.Length == 0)
            return 0xFF;
        var offset = RamOffset(address);
        return offset < _ram.Length ? _ram[offset] : (byte)0xFF;
    }

    public void Flush() => FlushIfDirty();

    private int Bank0Offset()
    {
        var bank = _mode == 1 ? (_bankHigh << 5) & _romBankMask : 0;
        return bank * RomBankSize;
    }

    private int BankNOffset()
    {
        var bank = ((_bankHigh << 5) | _bankLow) & _romBankMask;
        return bank * RomBankSize;
    }

    private int RamOffset(ushort address)
    {
        var bank = _mode == 1 ? _bankHigh & _ramBankMask : 0;
        return bank * RamBankSize + address;
    }

    private void FlushIfDirty()
    {
        if (!_ramDirty || !_hasBattery || _ram.Length == 0)
            return;
        _store.Save(_saveKey, _ram);
        _ramDirty = false;
    }
}
