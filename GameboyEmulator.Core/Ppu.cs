namespace GameBoyEmulator.Core;

public sealed class Ppu : IPpu
{
    public void WriteVram(ushort address, byte value)
    {
        
    }

    public byte ReadVram(ushort address)
    {
        return 0;
    }

    public ReadOnlySpan<byte> ReadVramRange(ushort address, int length)
    {
        return ReadOnlySpan<byte>.Empty;
    }

    public byte ReadOam(ushort address)
    {
        return 0;
    }

    public void WriteOam(ushort address, byte value)
    {

    }

    public void WriteOam(ReadOnlySpan<byte> data)
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