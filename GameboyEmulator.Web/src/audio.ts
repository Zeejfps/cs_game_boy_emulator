import type { Emulator } from 'gameboy-emulator';

// Stereo audio output for the emulator. Sets up:
//   - An AudioContext suspended until the first user gesture (browsers
//     forbid context.resume() before a gesture; calling start() inside a
//     click/keydown handler unblocks it).
//   - An AudioWorklet that consumes stereo float frames from a
//     SharedArrayBuffer ring buffer on the audio thread.
//   - A drain callback the host calls after each emulator tick to copy
//     newly-produced WASM samples into the ring buffer.
//
// Power-of-two ring size lets us mask indices instead of dividing. 4096
// frames at 48 kHz = ~85 ms — enough to ride out main-thread jitter without
// adding noticeable input lag (Game Boy SFX trigger from button press; the
// perceptual threshold is ~100 ms).
const RING_FRAMES = 4096;
const RING_MASK = RING_FRAMES - 1;

export interface AudioBridge {
  /** Sample rate the AudioContext settled on. Pass to the WASM init. */
  readonly sampleRate: number;
  /** Resume the suspended context. MUST be called inside a user-gesture handler. */
  start(): Promise<void>;
  /** Whether start() has succeeded — i.e. samples are flowing. */
  readonly isRunning: boolean;
  /** Copy newly-produced frames from the emulator into the ring buffer. */
  drain(emu: Emulator): void;
}

// Returns null if SharedArrayBuffer is unavailable (cross-origin isolation
// not active). Caller should fall back to silent operation.
export function createAudio(workletUrl: string): AudioBridge | null {
  if (typeof SharedArrayBuffer === 'undefined' || !crossOriginIsolated) {
    console.warn(
      'SharedArrayBuffer / crossOriginIsolated unavailable — audio disabled. ' +
      'Serve with COOP: same-origin and COEP: require-corp headers.',
    );
    return null;
  }

  // AudioContext is constructed eagerly so we know its sampleRate before
  // initializing the WASM emulator (the APU needs it to size its sample
  // counters). Browsers create contexts in 'suspended' state until a user
  // gesture resumes them.
  const ctx = new AudioContext({ latencyHint: 'interactive' });
  const sampleRate = ctx.sampleRate;

  // Two SABs: one for the ring data (Float32 stereo, 4096 frames = 32 KB),
  // one tiny one for the read/write atomic indices (2 × Uint32 = 8 bytes).
  // Indices are monotonic Uint32; (write - read) >>> 0 gives available
  // frames, then & RING_MASK indexes into the ring.
  const ringSab = new SharedArrayBuffer(RING_FRAMES * 2 * Float32Array.BYTES_PER_ELEMENT);
  const ctrlSab = new SharedArrayBuffer(2 * Uint32Array.BYTES_PER_ELEMENT);
  const ring = new Float32Array(ringSab);
  const ctrl = new Uint32Array(ctrlSab);

  let workletNode: AudioWorkletNode | null = null;
  let started = false;
  let starting: Promise<void> | null = null;

  async function start(): Promise<void> {
    if (started) return;
    if (starting) return starting;
    starting = (async () => {
      // addModule is idempotent within a context. Done lazily so the worklet
      // file isn't fetched until audio is actually wanted.
      await ctx.audioWorklet.addModule(workletUrl);
      workletNode = new AudioWorkletNode(ctx, 'gameboy-audio', {
        outputChannelCount: [2],
        processorOptions: {
          ringSab,
          ctrlSab,
          ringFrames: RING_FRAMES,
        },
      });
      workletNode.connect(ctx.destination);
      if (ctx.state === 'suspended') {
        await ctx.resume();
      }
      started = true;
    })();
    try {
      await starting;
    } finally {
      starting = null;
    }
  }

  function drain(emu: Emulator): void {
    if (!started) return;
    const samples = emu.drainAudio();
    if (samples.length === 0) return;

    let write = Atomics.load(ctrl, 1);
    const read = Atomics.load(ctrl, 0);
    // Reserve one slot so write===read unambiguously means "empty"; without
    // the -1, a fully-written ring would also satisfy that and the worklet
    // would read stale data.
    const free = RING_FRAMES - ((write - read) >>> 0) - 1;
    const incoming = samples.length / 2;
    const n = incoming < free ? incoming : free; // drop-newest on overrun

    for (let i = 0; i < n; i++) {
      const slot = (write & RING_MASK) * 2;
      ring[slot] = samples[i * 2];
      ring[slot + 1] = samples[i * 2 + 1];
      write = (write + 1) >>> 0;
    }
    Atomics.store(ctrl, 1, write);
  }

  return {
    sampleRate,
    start,
    get isRunning() { return started; },
    drain,
  };
}
