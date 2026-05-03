// Audio thread worker. Pulls stereo float frames from a SharedArrayBuffer
// ring buffer (filled by the main thread after each emulator tick) and
// writes them to the output. Underrun (main thread late) emits silence.
//
// Index protocol: ctrl[0] = monotonic read index, ctrl[1] = monotonic write
// index, both Uint32. Available frames = (write - read) >>> 0, masked to
// the ring's power-of-two size for indexing.

class GameBoyAudioProcessor extends AudioWorkletProcessor {
  constructor(opts) {
    super();
    const { ringSab, ctrlSab, ringFrames } = opts.processorOptions;
    this.ring = new Float32Array(ringSab);
    this.ctrl = new Uint32Array(ctrlSab);
    this.mask = ringFrames - 1;
  }

  process(_inputs, outputs) {
    const out = outputs[0];
    const left = out[0];
    const right = out[1] ?? left;
    const n = left.length;

    let read = Atomics.load(this.ctrl, 0);
    const write = Atomics.load(this.ctrl, 1);
    let available = (write - read) >>> 0;

    for (let i = 0; i < n; i++) {
      if (available === 0) {
        left[i] = 0;
        if (right !== left) right[i] = 0;
      } else {
        const slot = (read & this.mask) * 2;
        left[i] = this.ring[slot];
        if (right !== left) right[i] = this.ring[slot + 1];
        read = (read + 1) >>> 0;
        available--;
      }
    }

    Atomics.store(this.ctrl, 0, read);
    return true;
  }
}

registerProcessor('gameboy-audio', GameBoyAudioProcessor);
