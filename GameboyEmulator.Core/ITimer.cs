namespace GameboyEmulator.Core;

public interface ITimer
{
    void WriteDiv(byte value);
    void WriteTima(byte value);
    void WriteTma(byte value);
    void WriteTac(byte value);
    byte ReadDiv();
    byte ReadTima();
    byte ReadTma();
    byte ReadTac();
}
