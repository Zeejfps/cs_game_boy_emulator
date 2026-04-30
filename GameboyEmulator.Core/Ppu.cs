namespace GameBoyEmulator.Core;

public enum PpuMode : byte
{
    HBlank  = 0,
    VBlank  = 1,
    OamScan = 2,
    Drawing = 3,
}

public sealed class Ppu : IPpu
{
    public const int ScreenWidth = 160;
    public const int ScreenHeight = 144;

    private const int VramSize = 0x2000;
    private const int OamSize = 0xA0;

    private const ushort LcdcAddress = 0xFF40;
    private const ushort StatAddress = 0xFF41;
    private const ushort ScyAddress  = 0xFF42;
    private const ushort ScxAddress  = 0xFF43;
    private const ushort LyAddress   = 0xFF44;
    private const ushort LycAddress  = 0xFF45;
    private const ushort BgpAddress  = 0xFF47;
    private const ushort Obp0Address = 0xFF48;
    private const ushort Obp1Address = 0xFF49;
    private const ushort WyAddress   = 0xFF4A;
    private const ushort WxAddress   = 0xFF4B;

    private readonly byte[] _vram = new byte[VramSize];
    private readonly byte[] _oam = new byte[OamSize];
    private readonly byte[] _frameBuffer = new byte[ScreenWidth * ScreenHeight];

    private byte _lcdc;
    private byte _statSources; // bits 6,5,4,3 — interrupt source enables
    private byte _scy;
    private byte _scx;
    private byte _ly;
    private byte _lyc;
    private byte _bgp;
    private byte _obp0;
    private byte _obp1;
    private byte _wy;
    private byte _wx;

    private PpuMode _mode; // set by the mode state machine (step 2)

    public ReadOnlyMemory<byte> FrameBuffer => _frameBuffer;

    public void WriteVram(ushort address, byte value)
    {
        if (_mode == PpuMode.Drawing) return;
        _vram[address] = value;
    }

    public byte ReadVram(ushort address)
    {
        if (_mode == PpuMode.Drawing) return 0xFF;
        return _vram[address];
    }

    public ReadOnlySpan<byte> ReadVramRange(ushort address, int length) => _vram.AsSpan(address, length);

    public byte ReadOam(ushort address)
    {
        if (_mode is PpuMode.OamScan or PpuMode.Drawing) return 0xFF;
        return _oam[address];
    }

    public void WriteOam(ushort address, byte value)
    {
        if (_mode is PpuMode.OamScan or PpuMode.Drawing) return;
        _oam[address] = value;
    }

    // DMA path: PPU bus restrictions don't apply — DMA itself drives OAM.
    public void WriteOam(ReadOnlySpan<byte> data) => data.CopyTo(_oam);

    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case LcdcAddress: _lcdc = value; break;
            case StatAddress: _statSources = (byte)(value & 0x78); break;
            case ScyAddress:  _scy = value; break;
            case ScxAddress:  _scx = value; break;
            case LyAddress:   /* read-only */ break;
            case LycAddress:  _lyc = value; break;
            case BgpAddress:  _bgp = value; break;
            case Obp0Address: _obp0 = value; break;
            case Obp1Address: _obp1 = value; break;
            case WyAddress:   _wy = value; break;
            case WxAddress:   _wx = value; break;
        }
    }

    public byte ReadRegister(ushort address)
    {
        return address switch
        {
            LcdcAddress => _lcdc,
            StatAddress => (byte)(0x80 | _statSources | (_ly == _lyc ? 0x04 : 0x00) | (byte)_mode),
            ScyAddress  => _scy,
            ScxAddress  => _scx,
            LyAddress   => _ly,
            LycAddress  => _lyc,
            BgpAddress  => _bgp,
            Obp0Address => _obp0,
            Obp1Address => _obp1,
            WyAddress   => _wy,
            WxAddress   => _wx,
            _ => 0xFF
        };
    }
}
