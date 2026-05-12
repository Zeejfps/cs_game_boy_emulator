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
    // CGB-only registers — wired in Phase 3 as field-backed stubs; the BG
    // attribute fetch and palette-RAM machinery that actually consumes these
    // arrives in Phase 5.
    private const ushort VbkAddress  = 0xFF4F;
    private const ushort BcpsAddress = 0xFF68;
    private const ushort BcpdAddress = 0xFF69;
    private const ushort OcpsAddress = 0xFF6A;
    private const ushort OcpdAddress = 0xFF6B;

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
                    // Re-evaluate the OR-of-sources line without first
                    // forcing it low — forcing a rising edge here would fire
                    // a spurious STAT IRQ on every STAT write and defeats
                    // STAT IRQ blocking (Mooneye stat_irq_blocking).
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
            case BgpAddress:  _bgp = value; RebuildDmgBgPalette(); break;
            case Obp0Address: _obp0 = value; RebuildDmgObjPalette(0); break;
            case Obp1Address: _obp1 = value; RebuildDmgObjPalette(1); break;
            case WyAddress:   _wy = value; break;
            case WxAddress:   _wx = value; break;
            case VbkAddress:  _vramBank = (byte)(value & 0x01); break;
            case BcpsAddress: _bcps = (byte)(value & 0xBF); break; // bit 6 unused/reads 1
            case BcpdAddress: WriteCgbPalette(_bgPaletteRam, ref _bcps, value); break;
            case OcpsAddress: _ocps = (byte)(value & 0xBF); break;
            case OcpdAddress: WriteCgbPalette(_objPaletteRam, ref _ocps, value); break;
        }
    }

    // Writes through the BCPS/OCPS auto-increment cursor into the matching 64-
    // byte CGB palette RAM. Phase 5 will also recompute the RGBA palette table
    // entry — for now we only persist the byte so subsequent reads echo back.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteCgbPalette(byte[] paletteRam, ref byte indexReg, byte value)
    {
        var idx = indexReg & 0x3F;
        paletteRam[idx] = value;
        if ((indexReg & 0x80) != 0)
            indexReg = (byte)(0x80 | ((idx + 1) & 0x3F));
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
            VbkAddress  => (byte)(_vramBank | 0xFE),  // bits 1..7 read 1
            BcpsAddress => (byte)(_bcps | 0x40),       // bit 6 reads 1
            BcpdAddress => _bgPaletteRam[_bcps & 0x3F],
            OcpsAddress => (byte)(_ocps | 0x40),
            OcpdAddress => _objPaletteRam[_ocps & 0x3F],
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
