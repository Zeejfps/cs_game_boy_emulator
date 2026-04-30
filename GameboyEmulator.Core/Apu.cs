namespace GameBoyEmulator.Core;

public sealed class Apu : IApu
{
    public void WriteRegister(ushort address, byte value)
    {
    }

    public byte ReadRegister(ushort address) => 0xFF;
}
