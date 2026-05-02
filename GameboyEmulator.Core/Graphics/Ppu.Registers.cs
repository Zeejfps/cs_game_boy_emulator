using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// Memory-mapped register I/O (0xFF40-0xFF4B) and the LCDC-write side effects.
public sealed partial class Ppu
{
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case LcdcAddress: WriteLcdc((LcdControl)value); break;
            case StatAddress:
                _statSources = (StatFlags)value & StatFlags.Sources;
                if (_isLcdEnabled)
                {
                    _statLine = false;
                    UpdateStatLine();
                }
                break;
            case ScyAddress:  _scy = value; break;
            case ScxAddress:  _scx = value; break;
            case LyAddress:   /* read-only */ break;
            case LycAddress:
                _lyc = value;
                if (_isLcdEnabled)
                {
                    _lycMatch = _ly == _lyc;
                    UpdateStatLine();
                }
                break;
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
            LcdcAddress => (byte)_lcdc,
            StatAddress => (byte)(StatFlags.Unused | _statSources | (_lycMatch ? StatFlags.LycEqualLy : 0) | (StatFlags)EffectiveStatMode()),
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLcdc(LcdControl value)
    {
        var wasLcdEnabled = _isLcdEnabled;

        _isLcdEnabled = value.HasFlag(LcdControl.LcdEnable);
        _isBackgroundDrawingEnabled = value.HasFlag(LcdControl.BackgroundEnable);
        _isObjectDrawingEnabled = value.HasFlag(LcdControl.ObjectsEnable);
        _isWindowDrawingEnabled = value.HasFlag(LcdControl.WindowEnable);
        _spriteHeight = value.HasFlag(LcdControl.ObjectsUseLargeSize) ? (byte)16 : (byte)8;
        _lcdc = value;
        // BG fetcher fields (_bgTileMap, _windowTileMap, _bgTilePixels,
        // _bgTileFlipBit) are latched at EnterDrawingMode, not here.

        if (wasLcdEnabled && !_isLcdEnabled)
        {
            OnLcdDisabled();
        }
        else if (!wasLcdEnabled && _isLcdEnabled)
        {
            OnLcdEnabled();
        }
    }

    private void OnLcdDisabled()
    {
        _ly = 0;
        _dot = 0;
        _mode = PpuMode.HBlank;
        _wyTriggered = false;
        _windowLineCounter = 0;
        _inWindow = false;
        _windowRenderedThisLine = false;
        // _statLine and _lycMatch are preserved: the comparison clock and the
        // STAT-IRQ line are frozen, not reset, while the LCD is off.
    }

    private void OnLcdEnabled()
    {
        _ly = 0;
        _dot = 0;
        _wyTriggered = false;
        _windowLineCounter = 0;
        _lycMatch = _ly == _lyc;
        _firstScanlineAfterEnable = true;
        EnterOamScanMode();
    }

    // While _firstScanlineAfterEnable is set, STAT reports mode 0 instead of
    // mode 2 during the first OAM scan after the LCD turns on. The PPU is
    // still internally scanning OAM; only the externally visible mode bits
    // are masked.
    private byte EffectiveStatMode()
        => _firstScanlineAfterEnable && _mode == PpuMode.OamScan ? (byte)0 : (byte)_mode;
}
