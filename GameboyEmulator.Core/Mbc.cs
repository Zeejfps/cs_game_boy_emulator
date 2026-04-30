namespace GameboyEmulator.Core;

public sealed class Mbc : IMbc
{
    public void WriteBank0(ushort address, byte value)
    {
        throw new NotImplementedException();
    }
    
    public void WriteBankN(ushort address, byte value)
    {
        throw new NotImplementedException();
    }
    
    public byte ReadBank0(ushort address)
    {
        throw new NotImplementedException();
    }
    
    public byte ReadBankN(ushort address)
    {
        throw new NotImplementedException();
    }

    public void WriteExternalRam(ushort address, byte value)
    {
        throw new NotImplementedException();
    }

    public byte ReadExternalRam(ushort address)
    {
        throw new NotImplementedException();
    }

    public ReadOnlySpan<byte> ReadBank0Range(ushort address, int length)
    {
        throw new NotImplementedException();
    }

    public ReadOnlySpan<byte> ReadBankNRange(ushort address, int length)
    {
        throw new NotImplementedException();
    }

    public ReadOnlySpan<byte> ReadExternalRamRange(ushort address, int length)
    {
        throw new NotImplementedException();
    }
}