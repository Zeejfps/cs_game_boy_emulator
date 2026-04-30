namespace GameboyEmulator.Core;

public sealed class Ppu : IPpu
{
    public void WriteVram(ushort address, byte value)
    {
        
    }

    public byte ReadVram(ushort address)
    {
        return 0;
    }

    public byte ReadOam(ushort address)
    {
        return 0;
    }

    public void WriteOam(ushort address, byte value)
    {
        
    }
    
    public void WriteRegister(ushort address, byte value)
    {
        
    }
    
    public byte ReadRegister(ushort address)
    {
        return 0;
    }
}