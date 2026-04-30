using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public sealed class Mmu : IMemoryBus
{
    private const ushort VRamStartAddress = 0x8000;
    private const ushort VRamEndAddress = 0x9FFF;
    
    private readonly Ppu _ppu;

    public Mmu(Ppu ppu)
    {
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
        throw new NotImplementedException();
    }

    public byte Read(ushort address)
    {
        return 0;
    }

    public ushort ReadWord(ushort address)
    {
        var lo = Read(address);
        var hi = Read((ushort)(address + 1));
        return (byte)((hi << 8) | lo);
    }
}