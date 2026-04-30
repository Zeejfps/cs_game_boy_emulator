using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public sealed class Mmu : IMemoryBus
{
    private const ushort VRamStartAddress = 0x8000;
    private const ushort VRamEndAddress = 0x9FFF;
    
    private readonly Mbc _mbc;
    private readonly Ppu _ppu;

    public Mmu(Mbc mbc, Ppu ppu)
    {
        _mbc = mbc;
        _ppu = ppu;
    }

    public void Write(ushort address, byte value)
    {
        if (address is >= VRamStartAddress and <= VRamEndAddress)
        {
            _ppu.WriteVram((ushort)(address - VRamStartAddress), value);
            return;
        }

        throw new NotImplementedException();
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