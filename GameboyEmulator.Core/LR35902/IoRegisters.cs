namespace GameboyEmulator.Core.LR35902;

public static class IoRegisters
{
    public const ushort SerialDataAddress = 0xFF01;
    public const ushort SerialControlAddress = 0xFF02;
    public const ushort DividerAddress = 0xFF04;
    public const ushort TimerCounterAddress = 0xFF05;
    public const ushort TimerModuloAddress = 0xFF06;
    public const ushort TimerControlAddress = 0xFF07;
    public const ushort InterruptFlagAddress = 0xFF0F;
    public const ushort InterruptEnableAddress = 0xFFFF;
}
