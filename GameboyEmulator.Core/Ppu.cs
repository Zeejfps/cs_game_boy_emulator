using System.Runtime.CompilerServices;
using GameBoyEmulator.Core.LR35902;

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

    private const int DotsPerLine    = 456;
    private const int LinesPerFrame  = 154;
    private const int VisibleLines   = 144;
    private const int OamScanEndDot  = 80;
    private const int DrawingEndDot  = 80 + 172;

    private const byte LcdEnableMask  = 0x80; // LCDC bit 7
    private const byte StatHBlankIrq  = 0x08; // STAT bit 3
    private const byte StatVBlankIrq  = 0x10; // STAT bit 4
    private const byte StatOamIrq     = 0x20; // STAT bit 5
    private const byte StatLycIrq     = 0x40; // STAT bit 6

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

    private PpuMode _mode;
    private int _dot;
    private bool _statLine; // previous OR-of-sources, for stat-blocking edge detection

    private readonly IInterrupts _interrupts;

    public Ppu(IInterrupts interrupts)
    {
        _interrupts = interrupts;
    }

    public ReadOnlyMemory<byte> FrameBuffer => _frameBuffer;

    public void Tick(int tStates)
    {
        if ((_lcdc & LcdEnableMask) == 0) return;
        for (var i = 0; i < tStates; i++) StepDot();
    }

    private void StepDot()
    {
        _dot++;

        if (_ly < VisibleLines)
        {
            switch (_dot)
            {
                case OamScanEndDot:
                    _mode = PpuMode.Drawing;
                    break;
                case DrawingEndDot:
                    _mode = PpuMode.HBlank;
                    RenderScanline(_ly);
                    break;
            }
        }

        if (_dot == DotsPerLine)
        {
            _dot = 0;
            _ly++;

            switch (_ly)
            {
                case VisibleLines:
                    _mode = PpuMode.VBlank;
                    _interrupts.Request(InterruptType.VBlank);
                    break;
                case LinesPerFrame:
                    _ly = 0;
                    _mode = PpuMode.OamScan;
                    break;
                case < VisibleLines:
                    _mode = PpuMode.OamScan;
                    break;
            }
        }

        UpdateStatLine();
    }

    private void UpdateStatLine()
    {
        var line =
            ((_statSources & StatLycIrq)    != 0 && _ly == _lyc)         ||
            ((_statSources & StatOamIrq)    != 0 && _mode == PpuMode.OamScan) ||
            ((_statSources & StatVBlankIrq) != 0 && _mode == PpuMode.VBlank)  ||
            ((_statSources & StatHBlankIrq) != 0 && _mode == PpuMode.HBlank);

        if (line && !_statLine)
            _interrupts.Request(InterruptType.LcdStat);

        _statLine = line;
    }

    private void RenderScanline(byte line)
    {
        // step 3
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLcdc(byte value)
    {
        var wasOn = (_lcdc & LcdEnableMask) != 0;
        var nowOn = (value & LcdEnableMask) != 0;
        _lcdc = value;
        if (wasOn && !nowOn)
        {
            _ly = 0;
            _dot = 0;
            _mode = PpuMode.HBlank;
            _statLine = false;
        }
    }

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case LcdcAddress: WriteLcdc(value); break;
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
