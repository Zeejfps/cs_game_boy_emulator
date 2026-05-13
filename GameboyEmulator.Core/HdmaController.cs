using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

// CGB VRAM DMA. HDMA1/2 form the 16-bit source pointer (low 4 bits of HDMA2
// ignored — transfers align to 16 bytes); HDMA3/4 form the dest pointer
// (bits 15..13 are ignored / forced to 0x8000-0x9FFF — VRAM only). HDMA5
// is the trigger:
//   - bit 7 = 0 on write: general DMA — transfer (length+1)*16 bytes
//     immediately. Real hardware stalls the CPU for the duration; this
//     impl runs it synchronously without consuming cycles, which is
//     timing-inaccurate but functionally correct for the games that use
//     it (Crystal palette streaming, Zelda Oracle title screen).
//   - bit 7 = 1 on write: H-Blank DMA — arm a transfer that copies 16
//     bytes on each entry into PPU mode 0 until length is exhausted.
//     Writing bit 7 = 0 to HDMA5 while a H-Blank transfer is active
//     cancels it (length is preserved in the readable value).
//
// HDMA is the bus master, so destination writes go directly to the PPU's
// VRAM rather than through the MMU — the MMU enforces CPU-side mode-3
// VRAM lockout, which would otherwise drop the bytes from any general DMA
// that fires while PPU is in Drawing. Source reads stay on the MMU because
// the canonical sources (ROM/SRAM/WRAM) are never mode-gated and HDMA
// sourcing from VRAM is undefined hardware behavior we don't try to model.
public sealed class HdmaController
{
    private readonly IBus _bus;
    private readonly IPpu _ppu;

    private byte _srcHigh;
    private byte _srcLow;   // already masked to 0xF0 on write
    private byte _dstHigh;  // already masked to 0x1F on write
    private byte _dstLow;   // already masked to 0xF0 on write
    private int _remaining; // bytes left in current transfer (0 when none)
    private bool _hblankActive;

    public HdmaController(IBus bus, IPpu ppu)
    {
        _bus = bus;
        _ppu = ppu;
    }

    public bool IsHBlankActive => _hblankActive;

    public void Reset()
    {
        _srcHigh = 0;
        _srcLow = 0;
        _dstHigh = 0;
        _dstLow = 0;
        _remaining = 0;
        _hblankActive = false;
    }

    // HDMA1-4 are write-only — real hardware reads them as 0xFF. HDMA5 reports
    // remaining length and active state.
    public byte ReadRegister(ushort address) => address switch
    {
        0xFF55 => ReadHdma5(),
        _ => 0xFF,
    };

    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF51: _srcHigh = value; break;
            case 0xFF52: _srcLow  = (byte)(value & 0xF0); break;
            case 0xFF53: _dstHigh = (byte)(value & 0x1F); break;
            case 0xFF54: _dstLow  = (byte)(value & 0xF0); break;
            case 0xFF55: WriteHdma5(value); break;
        }
    }

    private byte ReadHdma5()
    {
        if (_remaining == 0) return 0xFF;
        var blocks = (byte)(((_remaining >> 4) - 1) & 0x7F);
        return _hblankActive
            ? blocks                  // active: bit 7 = 0
            : (byte)(0x80 | blocks);  // cancelled / pending start: bit 7 = 1
    }

    private void WriteHdma5(byte value)
    {
        if (_hblankActive && (value & 0x80) == 0)
        {
            // Cancel: keep _remaining so the next HDMA5 read reports it.
            _hblankActive = false;
            return;
        }

        _remaining = (((value & 0x7F) + 1) << 4);

        if ((value & 0x80) == 0)
        {
            // General DMA — copy everything now.
            while (_remaining > 0)
            {
                StepOneByte();
            }
        }
        else
        {
            _hblankActive = true;
        }
    }

    // Called by PPU on Drawing → HBlank transition. Quietly returns when no
    // H-Blank transfer is armed.
    public void OnHBlank()
    {
        if (!_hblankActive) return;

        // One 16-byte block per HBlank. Even if _remaining > 16, only one
        // chunk transfers per scanline — the game polls HDMA5 to know when
        // the full payload has landed.
        for (var i = 0; i < 16 && _remaining > 0; i++)
        {
            StepOneByte();
        }

        if (_remaining == 0) _hblankActive = false;
    }

    private void StepOneByte()
    {
        var src = (ushort)((_srcHigh << 8) | _srcLow);
        // _dstHigh is masked to 0x1F on write so this is already a VRAM-relative
        // offset in 0x0000-0x1FFF — exactly what Ppu.WriteVram expects.
        var dstOffset = (ushort)((_dstHigh << 8) | _dstLow);
        _ppu.WriteVram(dstOffset, _bus.Read(src));
        AdvancePointers();
        _remaining--;
    }

    private void AdvancePointers()
    {
        // Increment the 16-bit src/dst as if they were ushorts. Dest stays in
        // VRAM (0x8000-0x9FFF) by construction; if the game programs an
        // overlong transfer the high byte will wrap past 0x1F, which real
        // hardware mirrors and we simply allow.
        if (++_srcLow == 0) _srcHigh++;
        if (++_dstLow == 0) _dstHigh++;
    }
}
