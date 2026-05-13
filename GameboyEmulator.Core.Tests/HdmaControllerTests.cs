using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class HdmaControllerTests
{
    // 64 KB byte-array-backed bus so source reads and VRAM writes can be
    // verified directly. Real MMU mode-blocking and MBC dispatch aren't
    // relevant to HDMA's contract — it just reads from src and writes to dst.
    private sealed class FlatBus : IBus
    {
        public readonly byte[] Memory = new byte[0x10000];
        public byte Read(ushort address) => Memory[address];
        public void Write(ushort address, byte value) => Memory[address] = value;
    }

    private readonly FlatBus _bus = new();
    private readonly HdmaController _hdma;

    public HdmaControllerTests()
    {
        _hdma = new HdmaController(_bus);
    }

    [Fact]
    public void GeneralDma_CopiesRequestedBlocksImmediately()
    {
        // 32 bytes of sentinel data at 0x4000 — copy to VRAM at 0x8100.
        for (var i = 0; i < 32; i++) _bus.Memory[0x4000 + i] = (byte)(0x10 + i);

        _hdma.WriteRegister(0xFF51, 0x40); // src high
        _hdma.WriteRegister(0xFF52, 0x00); // src low
        _hdma.WriteRegister(0xFF53, 0x01); // dst high (within VRAM page)
        _hdma.WriteRegister(0xFF54, 0x00); // dst low
        // length = (1+1)*16 = 32 bytes, bit 7 = 0 → general DMA
        _hdma.WriteRegister(0xFF55, 0x01);

        for (var i = 0; i < 32; i++)
            Assert.Equal((byte)(0x10 + i), _bus.Memory[0x8100 + i]);

        // After completion HDMA5 reads 0xFF.
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF55));
        Assert.False(_hdma.IsHBlankActive);
    }

    [Fact]
    public void HBlankDma_TransfersOneBlockPerHBlank()
    {
        // 64 bytes (4 blocks) at 0x5000 → 0x8200
        for (var i = 0; i < 64; i++) _bus.Memory[0x5000 + i] = (byte)(0x80 + i);

        _hdma.WriteRegister(0xFF51, 0x50);
        _hdma.WriteRegister(0xFF52, 0x00);
        _hdma.WriteRegister(0xFF53, 0x02);
        _hdma.WriteRegister(0xFF54, 0x00);
        // length = (3+1)*16 = 64 bytes, bit 7 = 1 → H-Blank DMA
        _hdma.WriteRegister(0xFF55, 0x83);

        // Before the first HBlank, nothing in VRAM yet.
        Assert.Equal(0x00, _bus.Memory[0x8200]);
        Assert.True(_hdma.IsHBlankActive);
        // HDMA5 read: bit 7 = 0 (active), 3 blocks remaining (length-1).
        Assert.Equal(0x03, _hdma.ReadRegister(0xFF55));

        _hdma.OnHBlank();
        for (var i = 0; i < 16; i++) Assert.Equal((byte)(0x80 + i), _bus.Memory[0x8200 + i]);
        Assert.Equal(0x02, _hdma.ReadRegister(0xFF55)); // 2 blocks left, active

        _hdma.OnHBlank();
        _hdma.OnHBlank();
        _hdma.OnHBlank(); // final block

        for (var i = 0; i < 64; i++) Assert.Equal((byte)(0x80 + i), _bus.Memory[0x8200 + i]);
        Assert.False(_hdma.IsHBlankActive);
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF55));
    }

    [Fact]
    public void HBlankDma_CancellationStopsTransferAndPreservesRemaining()
    {
        // 4 blocks armed
        _hdma.WriteRegister(0xFF51, 0x60);
        _hdma.WriteRegister(0xFF52, 0x00);
        _hdma.WriteRegister(0xFF53, 0x03);
        _hdma.WriteRegister(0xFF54, 0x00);
        _hdma.WriteRegister(0xFF55, 0x83);

        // Transfer one block, then cancel.
        _hdma.OnHBlank();
        _hdma.WriteRegister(0xFF55, 0x00); // bit 7 = 0 while active → cancel

        Assert.False(_hdma.IsHBlankActive);
        // Cancelled with 48 bytes (3 blocks) remaining: bit 7 = 1, lower = 2.
        Assert.Equal(0x82, _hdma.ReadRegister(0xFF55));

        // Further HBlanks should not copy anything.
        var snapshot = _bus.Memory[0x8310]; // 16 bytes into where the *2nd* block would have landed
        _hdma.OnHBlank();
        Assert.Equal(snapshot, _bus.Memory[0x8310]);
    }

    [Fact]
    public void HDMA5_ReadIsFFWhenIdle()
    {
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF55));
    }

    [Fact]
    public void HDMA1_to_4_AlwaysReadFF()
    {
        _hdma.WriteRegister(0xFF51, 0x12);
        _hdma.WriteRegister(0xFF52, 0x34);
        _hdma.WriteRegister(0xFF53, 0x56);
        _hdma.WriteRegister(0xFF54, 0x78);
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF51));
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF52));
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF53));
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF54));
    }
}
