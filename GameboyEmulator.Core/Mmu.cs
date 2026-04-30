using System.Runtime.CompilerServices;
using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public sealed class Mmu : IMemoryBus
{
    private const ushort Bank0StartAddress = 0x0000;
    private const ushort Bank0EndAddress = 0x3FFF;
    
    private const ushort BankNStartAddress = 0x4000;
    private const ushort BankNEndAddress = 0x7FFF;
    
    private const ushort VRamStartAddress = 0x8000;
    private const ushort VRamEndAddress = 0x9FFF;
    
    private const ushort ExternalRamStartAddress = 0xA000;
    private const ushort ExternalRamEndAddress = 0xBFFF;

    private const ushort WRamStartAddress = 0xC000;
    private const ushort WRamEndAddress = 0xDFFF;

    private const ushort EchoRamStartAddress = 0xE000;
    private const ushort EchoRamEndAddress = 0xFDFF;

    private const ushort OamStartAddress = 0xFE00;
    private const ushort OamEndAddress = 0xFE9F;

    private const ushort UnusableStartAddress = 0xFEA0;
    private const ushort UnusableEndAddress = 0xFEFF;

    private const ushort IoRegistersStartAddress = 0xFF00;
    private const ushort IoRegistersEndAddress = 0xFF7F;

    private const ushort HRamStartAddress = 0xFF80;
    private const ushort HRamEndAddress = 0xFFFE;

    private const ushort InterruptEnableAddress = 0xFFFF;

    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _hram = new byte[0x7F];
    private byte _interruptEnable;

    private readonly IMbc _mbc;
    private readonly IPpu _ppu;
    private readonly IJoypad _joypad;
    private readonly ITimer _timer;

    public Mmu(IMbc mbc, IPpu ppu, IJoypad joypad, ITimer timer)
    {
        _mbc = mbc;
        _ppu = ppu;
        _joypad = joypad;
        _timer = timer;
    }

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case >= Bank0StartAddress and <= Bank0EndAddress:
                _mbc.WriteBank0(address, value);
                return;
            case >= BankNStartAddress and <= BankNEndAddress:
                _mbc.WriteBankN(address, value);
                return;
            case >= VRamStartAddress and <= VRamEndAddress:
                _ppu.WriteVram((ushort)(address - VRamStartAddress), value);
                return;
            case >= ExternalRamStartAddress and <= ExternalRamEndAddress:
                _mbc.WriteExternalRam((ushort)(address - ExternalRamStartAddress), value);
                return;
            case >= WRamStartAddress and <= WRamEndAddress:
                _wram[address - WRamStartAddress] = value;
                return;
            case >= EchoRamStartAddress and <= EchoRamEndAddress:
                _wram[address - EchoRamStartAddress] = value;
                return;
            case >= OamStartAddress and <= OamEndAddress:
                _ppu.WriteOam((ushort)(address - OamStartAddress), value);
                return;
            case >= UnusableStartAddress and <= UnusableEndAddress:
                return;
            case >= IoRegistersStartAddress and <= IoRegistersEndAddress:
                WriteIO(address, value);
                return;
            case >= HRamStartAddress and <= HRamEndAddress:
                _hram[address - HRamStartAddress] = value;
                return;
            case InterruptEnableAddress:
                _interruptEnable = value;
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
            case 0xFF02:
                // TODO: serial
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
        }
    }
    
    public byte Read(ushort address)
    {
        return 0;
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