namespace GameBoyEmulator.Core.LR35902;

public interface IBus
{
    void Write(ushort address, byte value);
    byte Read(ushort address);
}