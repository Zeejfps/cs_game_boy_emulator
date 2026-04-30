using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public sealed class Mmu : IMemoryBus
{
    private readonly IPpu _ppu;

    public Mmu(IPpu ppu)
    {
        _ppu = ppu;
    }

    public void Write(ushort address, byte value)
    {
        throw new NotImplementedException();
    }

    public void WriteWord(ushort address, ushort value)
    {
        throw new NotImplementedException();
    }

    public byte Read(ushort address)
    {
        throw new NotImplementedException();
    }

    public ushort ReadWord(ushort address)
    {
        throw new NotImplementedException();
    }
}