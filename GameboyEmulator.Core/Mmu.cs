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
    
    private const ushort OamStartAddress = 0xFE00;
    private const ushort OamEndAddress = 0xFE9F;
    
    private readonly Mbc _mbc;
    private readonly Ppu _ppu;

    public Mmu(Mbc mbc, Ppu ppu)
    {
        _mbc = mbc;
        _ppu = ppu;
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
            case >= OamStartAddress and <= OamEndAddress:
                _ppu.WriteOam((ushort)(address - OamStartAddress), value);
                return;
            default:
                throw new NotImplementedException();
        }
    }

    public void WriteWord(ushort address, ushort value)
    {
        var lo = (byte)(value & 0xFF);
        var hi = (byte)(value >> 8);
        Write(address, lo);
        Write((ushort)(address + 1), hi);
    }

    public byte Read(ushort address)
    {
        return 0;
    }

    public ushort ReadWord(ushort address)
    {
        var lo = Read(address);
        var hi = Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }
}