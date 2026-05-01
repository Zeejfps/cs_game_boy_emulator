using System.Runtime.CompilerServices;
using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Mmu : IMemoryBus
{
    private const ushort InterruptFlagAddress = 0xFF0F;
    private const ushort InterruptEnableAddress = 0xFFFF;

    private const int BootRomSize = 0x100;

    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _hram = new byte[0x7F];
    private byte _dmaSource;

    private byte[]? _bootRom;
    private bool _bootRomEnabled;

    private IMbc _mbc;
    private readonly IPpu _ppu;
    private readonly IJoypad _joypad;
    private readonly ITimer _timer;
    private readonly IApu _apu;
    private readonly ISerial _serial;
    private readonly IInterruptsView _interrupts;

    public Mmu(
        IMbc mbc,
        IPpu ppu,
        IJoypad joypad,
        ITimer timer,
        IApu apu,
        ISerial serial,
        IInterruptsView interrupts
    ) {
        _mbc = mbc;
        _ppu = ppu;
        _joypad = joypad;
        _timer = timer;
        _apu = apu;
        _serial = serial;
        _interrupts = interrupts;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Write(ushort address, byte value)
    {
        switch (address >> 12)
        {
            case 0x0:
            case 0x1:
            case 0x2:
            case 0x3:
                _mbc.WriteBank0(address, value);
                return;
            case 0x4:
            case 0x5:
            case 0x6:
            case 0x7:
                _mbc.WriteBankN(address, value);
                return;
            case 0x8:
            case 0x9:
                _ppu.WriteVram((ushort)(address - 0x8000), value);
                return;
            case 0xA:
            case 0xB:
                _mbc.WriteExternalRam((ushort)(address - 0xA000), value);
                return;
            case 0xC:
            case 0xD:
                _wram[address - 0xC000] = value;
                return;
            case 0xE:
                _wram[address - 0xE000] = value;
                return;
            default:
                WriteHigh(address, value);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void WriteHigh(ushort address, byte value)
    {
        switch (address)
        {
            case < 0xFE00:
                _wram[address - 0xE000] = value;
                return;
            case < 0xFEA0:
                _ppu.WriteOam((ushort)(address - 0xFE00), value);
                return;
            case < 0xFF00:
                return;
            case < 0xFF80:
                WriteIO(address, value);
                return;
            case < InterruptEnableAddress:
                _hram[address - 0xFF80] = value;
                return;
            case InterruptEnableAddress:
                _interrupts.WriteEnabledInterrupts((InterruptType)value);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void WriteIO(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF00:
                _joypad.Select(value);
                break;
            case 0xFF01:
                _serial.WriteData(value);
                break;
            case 0xFF02:
                _serial.WriteControl(value);
                break;
            case 0xFF04:
                _timer.WriteDiv(value);
                break;
            case 0xFF05:
                _timer.WriteTima(value);
                break;
            case 0xFF06:
                _timer.WriteTma(value);
                break;
            case 0xFF07:
                _timer.WriteTac(value);
                break;
            case Mmu.InterruptFlagAddress:
                _interrupts.WriteRequestedInterrupts((InterruptType)value);
                break;
            case >= 0xFF10 and <= 0xFF3F:
                _apu.WriteRegister(address, value);
                break;
            case 0xFF46:
                WriteDma(value);
                break;
            case >= 0xFF40 and <= 0xFF4B:
                _ppu.WriteRegister(address, value);
                break;
            case 0xFF50:
                // Real hardware locks the boot ROM out permanently on any
                // non-zero write. The boot ROM does this as its final act
                // before jumping to 0x0100.
                if (value != 0) _bootRomEnabled = false;
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public byte Read(ushort address)
    {
        switch (address >> 12)
        {
            case 0x0:
            case 0x1:
            case 0x2:
            case 0x3:
                if (_bootRomEnabled && address < BootRomSize)
                    return _bootRom![address];
                return _mbc.ReadBank0(address);
            case 0x4:
            case 0x5:
            case 0x6:
            case 0x7:
                return _mbc.ReadBankN(address);
            case 0x8:
            case 0x9:
                return _ppu.ReadVram((ushort)(address - 0x8000));
            case 0xA:
            case 0xB:
                return _mbc.ReadExternalRam((ushort)(address - 0xA000));
            case 0xC:
            case 0xD:
                return _wram[address - 0xC000];
            case 0xE:
                return _wram[address - 0xE000];
            default:
                return ReadHigh(address);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private byte ReadHigh(ushort address)
    {
        return address switch
        {
            < 0xFE00 => _wram[address - 0xE000],
            < 0xFEA0 => _ppu.ReadOam((ushort)(address - 0xFE00)),
            < 0xFF00 => 0xFF,
            < 0xFF80 => ReadIO(address),
            < InterruptEnableAddress => _hram[address - 0xFF80],
            InterruptEnableAddress => (byte)_interrupts.ReadEnabledInterrupts()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private byte ReadIO(ushort address)
    {
        return address switch
        {
            0xFF00 => _joypad.Read(),
            0xFF01 => _serial.ReadData(),
            0xFF02 => _serial.ReadControl(),
            0xFF04 => _timer.ReadDiv(),
            0xFF05 => _timer.ReadTima(),
            0xFF06 => _timer.ReadTma(),
            0xFF07 => _timer.ReadTac(),
            InterruptFlagAddress => (byte)((byte)_interrupts.ReadRequestedInterrupts() | 0xE0),
            0xFF46 => _dmaSource,
            >= 0xFF10 and <= 0xFF3F => _apu.ReadRegister(address),
            >= 0xFF40 and <= 0xFF4B => _ppu.ReadRegister(address),
            _ => 0xFF
        };
    }

    private void WriteDma(byte sourcePage)
    {
        _dmaSource = sourcePage;
        var sourceAddress = (ushort)(sourcePage << 8);
        var data = ReadRange(sourceAddress, 0xA0);
        _ppu.WriteOam(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private ReadOnlySpan<byte> ReadRange(ushort address, int length)
    {
        switch (address >> 12)
        {
            case 0x0:
            case 0x1:
            case 0x2:
            case 0x3:
                if (_bootRomEnabled && address + length <= BootRomSize)
                    return _bootRom!.AsSpan(address, length);
                return _mbc.ReadBank0Range(address, length);
            case 0x4:
            case 0x5:
            case 0x6:
            case 0x7:
                return _mbc.ReadBankNRange(address, length);
            case 0x8:
            case 0x9:
                return _ppu.ReadVramRange((ushort)(address - 0x8000), length);
            case 0xA:
            case 0xB:
                return _mbc.ReadExternalRamRange((ushort)(address - 0xA000), length);
            case 0xC:
            case 0xD:
                return _wram.AsSpan(address - 0xC000, length);
            case 0xE:
            case 0xF when address < 0xFE00:
                return _wram.AsSpan(address - 0xE000, length);
            default:
                throw new InvalidOperationException($"ReadRange unsupported source 0x{address:X4}");
        }
    }

    public void Reset()
    {
        Array.Clear(_wram);
        Array.Clear(_hram);
        _dmaSource = 0;
        // Power-cycle re-arms the boot ROM if one is loaded — matches what
        // happens when you turn a real Game Boy off and back on again.
        _bootRomEnabled = _bootRom != null;
    }

    public void SetMbc(IMbc mbc)
    {
        _mbc = mbc;
    }

    public void FlushMbc() => _mbc.Flush();

    public bool IsBootRomEnabled => _bootRomEnabled;

    public void SetBootRom(byte[]? bootRom)
    {
        if (bootRom != null && bootRom.Length != BootRomSize)
            throw new ArgumentException($"DMG boot ROM must be exactly {BootRomSize} bytes; got {bootRom.Length}", nameof(bootRom));
        _bootRom = bootRom;
        _bootRomEnabled = bootRom != null;
    }

    public void WriteWord(ushort address, ushort value)
    {
        var lo = (byte)(value & 0xFF);
        var hi = (byte)(value >> 8);
        Write(address, lo);
        Write((ushort)(address + 1), hi);
    }

    public ushort ReadWord(ushort address)
    {
        var lo = Read(address);
        var hi = Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }
}