interface EmulatorExports {
  Init(sampleRate: number): void;
  LoadRom(rom: Uint8Array, saveData: Uint8Array | null): void;
  GetSaveData(): Uint8Array | null;
  SetBootRom(bootRom: Uint8Array | null): void;
  PowerOn(): void;
  PowerOff(): void;
  IsPoweredOn(): boolean;
  Tick(): void;
  ConsumeFrame(): boolean;
  GetFrameBufferWidth(): number;
  GetFrameBufferHeight(): number;
  GetFrameBufferLength(): number;
  GetFrameBufferPointer(): number;
  GetAudioBufferFrameCapacity(): number;
  GetAudioBufferPointer(): number;
  DrainAudio(): number;
  SetButton(button: number, pressed: boolean): void;
  GetDebugState(): string;
}

/** Mirrors the C# `JoypadButton` enum — values are stable and crossed via WASM. */
export enum JoypadButton {
  A      = 0,
  B      = 1,
  Select = 2,
  Start  = 3,
  Right  = 4,
  Left   = 5,
  Up     = 6,
  Down   = 7,
}

export interface Emulator {
  /** Frame buffer width in pixels (160 on DMG). */
  readonly width: number;
  /** Frame buffer height in pixels (144 on DMG). */
  readonly height: number;

  /**
   * Load a ROM from raw bytes. Must be called before `powerOn`. Cannot be
   * called while the emulator is powered on.
   *
   * `saveData` is the previously-persisted contents of battery-backed
   * cartridge RAM (as returned by a prior `getSave()`), or undefined if
   * there's no save to restore. Ignored for cartridges without battery RAM.
   */
  loadRom(rom: Uint8Array, saveData?: Uint8Array): void;

  /**
   * Install an optional 256-byte DMG boot ROM. When set, `powerOn` starts
   * the CPU at 0x0000 and runs the boot ROM (Nintendo logo scroll, header
   * verification, then jump to 0x0100); when null/unset, the emulator
   * fast-paths past the boot sequence with documented post-boot register
   * values. Must be called when powered off.
   */
  setBootRom(bootRom: Uint8Array | null): void;

  /**
   * Returns the current battery-backed cartridge RAM, or null if the
   * loaded cartridge has no battery RAM. Safe to call at any time —
   * dirty in-memory data is flushed before the bytes are returned.
   *
   * The host owns persistence: write the bytes to localStorage,
   * IndexedDB, a file, a server, etc. Common pattern is to call this
   * after `powerOff()` and on a periodic auto-save timer.
   */
  getSave(): Uint8Array | null;

  powerOn(): void;
  powerOff(): void;
  isPoweredOn(): boolean;

  /**
   * Advance the emulator. Call this once per host frame (typically inside a
   * requestAnimationFrame callback). The emulator measures wall-clock time
   * between ticks and runs the corresponding number of CPU cycles.
   */
  tick(): void;

  /**
   * Subscribe to VBlank — the host should re-render the canvas here.
   * Returns an unsubscribe function.
   */
  onFrame(handler: () => void): () => void;

  /**
   * Returns a live view onto the pinned frame buffer (one byte per pixel,
   * values 0–3 are DMG color IDs after the BG palette has been applied).
   *
   * The returned Uint8Array is a *transient* view into the WASM heap — do not
   * cache it across awaits or other heap-mutating calls. Either consume it
   * immediately (e.g. draw to a canvas) or copy it.
   */
  getFrameBuffer(): Uint8Array;

  /**
   * Set a Game Boy button's pressed state. Edges drive the joypad interrupt;
   * the host is responsible for translating keyboard / gamepad events into
   * `setButton(button, true)` on press and `setButton(button, false)` on release.
   */
  setButton(button: JoypadButton, pressed: boolean): void;

  /**
   * Snapshot of CPU/PPU/interrupt state for debugging in-game freezes.
   * Lines: registers; IME/IF/IE/halted; LCDC/STAT/LY/LYC/SCX/SCY/WX/WY;
   * DIV/TIMA/TAC; bytes at PC; bytes at HL; stack contents.
   */
  getDebugState(): string;

  /**
   * Drain the APU's accumulated stereo float frames since the last call.
   * Returns a transient Float32Array view (interleaved L,R) into the WASM
   * heap — copy it (e.g. into a SharedArrayBuffer ring buffer feeding an
   * AudioWorklet) before the next emulator call, since heap views are
   * invalidated by anything that grows the heap.
   *
   * Frame count = returned array length / 2. Empty array means underrun.
   */
  drainAudio(): Float32Array;
}

export interface InitOptions {
  /** URL where the runtime files are hosted (trailing slash optional). */
  baseUrl: string;
  /**
   * Optional cache-busting token appended as `?v=…` to runtime URLs. The
   * dotnet.js bootstrap and individual resource files (.wasm/.dll/.js) are
   * not content-hashed at this layer, so a stable URL with a stale CDN/browser
   * copy can persist across deployments. Pass the app version (e.g. a git tag)
   * to force a fresh fetch on each release.
   */
  version?: string;
  /**
   * Audio output sample rate in Hz (typically `AudioContext.sampleRate`).
   * The APU mixes and downsamples to this rate. Default 48000.
   */
  sampleRate?: number;
}

export async function init(opts: InitOptions): Promise<Emulator> {
  const baseUrl = opts.baseUrl.endsWith('/') ? opts.baseUrl : opts.baseUrl + '/';
  const query = opts.version ? `?v=${encodeURIComponent(opts.version)}` : '';
  const { dotnet } = await import(baseUrl + 'dotnet.js' + query);

  const runtime = await dotnet
    .withResourceLoader((_type: string, name: string) => baseUrl + name + query)
    .create();

  await runtime.runMain();

  const config = runtime.getConfig();
  const assemblyName: string = config.mainAssemblyName ?? 'GameBoyEmulator.Wasm';
  const exports = await runtime.getAssemblyExports(assemblyName);
  const E: EmulatorExports = exports.GameBoyEmulator.Wasm.Emulator;

  E.Init(opts.sampleRate ?? 48000);

  const width = E.GetFrameBufferWidth();
  const height = E.GetFrameBufferHeight();
  const length = E.GetFrameBufferLength();
  const ptr = E.GetFrameBufferPointer();

  const audioPtr = E.GetAudioBufferPointer();
  // The *view* over the heap can detach if the heap grows, so we re-derive
  // it on each drainAudio() call rather than caching the Float32Array.

  const frameHandlers = new Set<() => void>();

  return {
    width,
    height,
    loadRom: (rom, saveData) => E.LoadRom(rom, saveData ?? null),
    getSave: () => E.GetSaveData(),
    setBootRom: (bootRom) => E.SetBootRom(bootRom),
    powerOn: () => E.PowerOn(),
    powerOff: () => E.PowerOff(),
    isPoweredOn: () => E.IsPoweredOn(),
    tick: () => {
      E.Tick();
      // ConsumeFrame() returns true if the PPU completed at least one frame
      // during this tick; false otherwise. Multiple VBlanks within one tick
      // coalesce into a single handler call — the host only needs to redraw
      // once per RAF anyway.
      if (E.ConsumeFrame()) {
        for (const h of frameHandlers) h();
      }
    },
    onFrame(handler: () => void) {
      frameHandlers.add(handler);
      return () => frameHandlers.delete(handler);
    },
    getFrameBuffer: () => {
      const heap = runtime.localHeapViewU8();
      return heap.subarray(ptr, ptr + length);
    },
    setButton: (button, pressed) => E.SetButton(button, pressed),
    getDebugState: () => E.GetDebugState(),
    drainAudio: () => {
      const frames = E.DrainAudio();
      if (frames === 0) return _emptyF32;
      const heap = runtime.localHeapViewF32();
      const start = audioPtr / 4;
      return heap.subarray(start, start + frames * 2);
    },
  };
}

const _emptyF32 = new Float32Array(0);
