namespace GameBoyEmulator.Core.LR35902;

public interface IMemoryBus
{
    void Write(ushort address, byte value);
    byte Read(ushort address);
}