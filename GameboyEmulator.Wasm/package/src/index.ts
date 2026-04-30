interface EmulatorExports {
  Init(): void;
  LoadRom(rom: Uint8Array): void;
  PowerOn(): void;
  PowerOff(): void;
  IsPoweredOn(): boolean;
  Tick(): void;
  ConsumeFrame(): boolean;
  GetFrameBufferWidth(): number;
  GetFrameBufferHeight(): number;
  GetFrameBufferLength(): number;
  GetFrameBufferPointer(): number;
}

export interface Emulator {
  /** Frame buffer width in pixels (160 on DMG). */
  readonly width: number;
  /** Frame buffer height in pixels (144 on DMG). */
  readonly height: number;

  /**
   * Load a ROM from raw bytes. Must be called before `powerOn`. Cannot be
   * called while the emulator is powered on.
   */
  loadRom(rom: Uint8Array): void;

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
}

export interface InitOptions {
  /** URL where the runtime files are hosted (trailing slash optional). */
  baseUrl: string;
}

export async function init(opts: InitOptions): Promise<Emulator> {
  const baseUrl = opts.baseUrl.endsWith('/') ? opts.baseUrl : opts.baseUrl + '/';
  const { dotnet } = await import(baseUrl + 'dotnet.js');

  const runtime = await dotnet
    .withResourceLoader((_type: string, name: string) => baseUrl + name)
    .create();

  await runtime.runMain();

  const config = runtime.getConfig();
  const assemblyName: string = config.mainAssemblyName ?? 'GameBoyEmulator.Wasm';
  const exports = await runtime.getAssemblyExports(assemblyName);
  const E: EmulatorExports = exports.GameBoyEmulator.Wasm.Emulator;

  E.Init();

  const width = E.GetFrameBufferWidth();
  const height = E.GetFrameBufferHeight();
  const length = E.GetFrameBufferLength();
  const ptr = E.GetFrameBufferPointer();

  const frameHandlers = new Set<() => void>();

  return {
    width,
    height,
    loadRom: (rom: Uint8Array) => E.LoadRom(rom),
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
  };
}
