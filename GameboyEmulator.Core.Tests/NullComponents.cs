using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Core.Tests;

internal sealed class NullPpu : IPpu
{
    public void WriteVram(ushort address, byte value) { }
    public byte ReadVram(ushort address) => 0xFF;
    public ReadOnlySpan<byte> ReadVramRange(ushort address, int length) => ReadOnlySpan<byte>.Empty;
    public void WriteOam(ushort address, byte value) { }
    public void WriteOam(ReadOnlySpan<byte> data) { }
    public byte ReadOam(ushort address) => 0xFF;
    public void WriteRegister(ushort address, byte value) { }
    public byte ReadRegister(ushort address) => 0xFF;
}

internal sealed class NullJoypad : IJoypad
{
    public void Select(byte value) { }
    public byte Read() => 0xFF;
    public void SetButton(JoypadButton button, bool pressed) { }
    public void Reset() { }
}

internal sealed class NullApu : IApu
{
    public void WriteRegister(ushort address, byte value) { }
    public byte ReadRegister(ushort address) => 0xFF;
}
