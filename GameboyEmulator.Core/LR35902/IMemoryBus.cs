namespace GameBoyEmulator.Core.LR35902;

public interface IMemoryBus
{
    void Write(ushort address, byte value);
    void WriteWord(ushort address, ushort value);
    byte Read(ushort address);
}