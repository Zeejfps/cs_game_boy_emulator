namespace GameboyEmulator.Core;

public sealed class Ppu
{
    public void WriteVram(ushort address, byte value)
    {
        
    }

    public byte ReadVram(ushort address)
    {
        return 0;
    }

    public byte ReadOam(byte index)
    {
        return 0;
    }

    public void WriteOam(byte index, byte value)
    {
        
    }
    
    public void WriteRegister(byte index, byte value)
    {
        
    }
    
    public byte ReadRegister(byte index)
    {
        return 0;
    }
}