namespace GameBoyEmulator.Core;

public interface IApu
{
    void WriteRegister(ushort address, byte value);
    byte ReadRegister(ushort address);

    // Advance the APU by `tStates` T-cycles. Called from SystemClock alongside
    // the PPU/timer/DMA so audio stays in lockstep with CPU execution.
    void Step(int tStates);

    // Called by Timer when bit-12 of its internal 16-bit counter falls 1->0
    // (or when WriteDiv resets the counter while bit 12 was high). This is the
    // hardware source of the APU's 512 Hz frame sequencer — driving it from
    // here means games that poke DIV to phase-shift envelopes/sweep behave
    // like real hardware. CGB double-speed would use bit 13 instead; this
    // emulator is DMG-only so bit 12 is always correct.
    void OnFrameSequencerTick();

    // Drain available stereo float samples (interleaved L,R) into `dest`.
    // Returns the number of FRAMES written (each frame = 2 floats). Caller
    // sizes `dest` so its length is even. Internal buffer overruns silently
    // drop oldest samples; underruns return 0.
    int DrainAudio(Span<float> dest);
}
