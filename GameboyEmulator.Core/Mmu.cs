using System.Runtime.CompilerServices;
using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public sealed class Mmu : IMemoryBus
{
    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _hram = new byte[0x7F];
    private byte _dmaSource;

    private readonly IMbc _mbc;
    private readonly IPpu _ppu;
    private readonly IJoypad _joypad;
    private readonly ITimer _timer;
    private readonly IApu _apu;
    private readonly ISerial _serial;
    private readonly IInterruptRegisters _interrupts;

    public Mmu(
        IMbc mbc,
        IPpu ppu,
        IJoypad joypad,
        ITimer timer,
        IApu apu,
        ISerial serial,
        IInterruptRegisters interrupts
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
        switch (address)
        {
            case <= 0x3FFF:
                _mbc.WriteBank0(address, value);
                return;
            case <= 0x7FFF:
                _mbc.WriteBankN(address, value);
                return;
            case <= 0x9FFF:
                _ppu.WriteVram((ushort)(address - 0x8000), value);
                return;
            case <= 0xBFFF:
                _mbc.WriteExternalRam((ushort)(address - 0xA000), value);
                return;
            case <= 0xDFFF:
                _wram[address - 0xC000] = value;
                return;
            case <= 0xFDFF:
                _wram[address - 0xE000] = value;
                return;
            case <= 0xFE9F:
                _ppu.WriteOam((ushort)(address - 0xFE00), value);
                return;
            case <= 0xFEFF:
                return;
            case <= 0xFF7F:
                WriteIO(address, value);
                return;
            case <= 0xFFFE:
                _hram[address - 0xFF80] = value;
                return;
            case 0xFFFF:
                _interrupts.InterruptEnable = value;
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
            case 0xFF0F:
                _interrupts.InterruptFlag = value;
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
                // TODO: boot ROM disable
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public byte Read(ushort address)
    {
        return address switch
        {
            <= 0x3FFF => _mbc.ReadBank0(address),
            <= 0x7FFF => _mbc.ReadBankN(address),
            <= 0x9FFF => _ppu.ReadVram((ushort)(address - 0x8000)),
            <= 0xBFFF => _mbc.ReadExternalRam((ushort)(address - 0xA000)),
            <= 0xDFFF => _wram[address - 0xC000],
            <= 0xFDFF => _wram[address - 0xE000],
            <= 0xFE9F => _ppu.ReadOam((ushort)(address - 0xFE00)),
            <= 0xFEFF => 0xFF,
            <= 0xFF7F => ReadIO(address),
            <= 0xFFFE => _hram[address - 0xFF80],
            0xFFFF => _interrupts.InterruptEnable
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
            0xFF0F => _interrupts.InterruptFlag,
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

    private ReadOnlySpan<byte> ReadRange(ushort address, int length)
    {
        return address switch
        {
            <= 0x3FFF => _mbc.ReadBank0Range(address, length),
            <= 0x7FFF => _mbc.ReadBankNRange(address, length),
            <= 0x9FFF => _ppu.ReadVramRange((ushort)(address - 0x8000), length),
            <= 0xBFFF => _mbc.ReadExternalRamRange((ushort)(address - 0xA000), length),
            <= 0xDFFF => _wram.AsSpan(address - 0xC000, length),
            <= 0xFDFF => _wram.AsSpan(address - 0xE000, length),
            _ => throw new InvalidOperationException($"ReadRange unsupported source 0x{address:X4}")
        };
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