import { init, JoypadButton, type Emulator } from 'gameboy-emulator';

const KEY_MAP: Record<string, JoypadButton> = {
  ArrowUp:    JoypadButton.Up,
  ArrowDown:  JoypadButton.Down,
  ArrowLeft:  JoypadButton.Left,
  ArrowRight: JoypadButton.Right,
  z: JoypadButton.A,
  Z: JoypadButton.A,
  x: JoypadButton.B,
  X: JoypadButton.B,
  Enter: JoypadButton.Start,
  Shift: JoypadButton.Select,
};

// Classic DMG green palette (color IDs 0..3), packed little-endian RGBA so
// each entry can be written as a single Uint32 into ImageData.
const PALETTE = new Uint32Array([
  0xffd0f8e0, // 0xE0F8D0 lightest
  0xff70c088, // 0x88C070
  0xff566834, // 0x346856
  0xff201808, // 0x081820 darkest
]);

const canvas = document.getElementById('screen') as HTMLCanvasElement;
const ctx = canvas.getContext('2d')!;
const imageData = ctx.createImageData(canvas.width, canvas.height);
const pixels = new Uint32Array(imageData.data.buffer);

let emu: Emulator | null = null;
let currentSaveKey: string | null = null;

// Backstop for anything that escapes the per-callsite try/catches below
// (event handlers, microtasks, etc.). Without these listeners, async errors
// in event callbacks silently disappear into the void.
window.addEventListener('error', (e) => {
  console.error('Uncaught error:', e.error ?? e.message);
});
window.addEventListener('unhandledrejection', (e) => {
  console.error('Unhandled promise rejection:', e.reason);
});

const SAVE_PREFIX = 'gb-save:';

function readCartTitle(rom: Uint8Array): string {
  // DMG cart title lives at 0x0134-0x0143, ASCII, null-terminated.
  let end = 0x0134;
  for (let i = 0x0134; i <= 0x0143; i++) {
    if (rom[i] === 0) break;
    end = i + 1;
  }
  let title = '';
  for (let i = 0x0134; i < end; i++) title += String.fromCharCode(rom[i]);
  return title.trim() || 'UNTITLED';
}

function bytesToBase64(bytes: Uint8Array): string {
  let bin = '';
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

function base64ToBytes(b64: string): Uint8Array {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

function persistCurrentSave(): void {
  if (!emu || !currentSaveKey) return;
  const bytes = emu.getSave();
  if (bytes && bytes.length > 0) {
    try {
      localStorage.setItem(currentSaveKey, bytesToBase64(bytes));
    } catch (err) {
      console.warn('Failed to persist save:', err);
    }
  }
}

function paint(): void {
  if (!emu) return;
  const fb = emu.getFrameBuffer();
  for (let i = 0; i < fb.length; i++) {
    pixels[i] = PALETTE[fb[i]];
  }
  ctx.putImageData(imageData, 0, 0);
}

function loop(): void {
  try {
    if (emu && emu.isPoweredOn()) emu.tick();
  } catch (err) {
    console.error('Emulator tick failed — halting CPU loop:', err);
    if (emu?.isPoweredOn()) {
      try { emu.powerOff(); } catch (offErr) { console.error('powerOff during recovery failed:', offErr); }
    }
    return; // stop scheduling further frames; the user can load another ROM.
  }
  requestAnimationFrame(loop);
}

async function main(): Promise<void> {
  const versionEl = document.getElementById('version');
  if (versionEl) versionEl.textContent = __APP_VERSION__;

  try {
    emu = await init({ baseUrl: '/wasm/', version: __APP_VERSION__ });
  } catch (err) {
    console.error('Failed to initialize emulator:', err);
    return;
  }

  emu.onFrame(paint);
  requestAnimationFrame(loop);

  window.addEventListener('keydown', (e) => {
    const button = KEY_MAP[e.key];
    if (button === undefined || !emu) return;
    e.preventDefault();
    if (e.repeat) return;
    emu.setButton(button, true);
  });
  window.addEventListener('keyup', (e) => {
    const button = KEY_MAP[e.key];
    if (button === undefined || !emu) return;
    e.preventDefault();
    emu.setButton(button, false);
  });

  const touchButtons = document.querySelectorAll<HTMLButtonElement>('#touch-controls button[data-btn]');
  touchButtons.forEach((el) => {
    const name = el.dataset.btn as keyof typeof JoypadButton | undefined;
    if (!name) return;
    const button = JoypadButton[name];
    const press = (e: Event) => {
      e.preventDefault();
      if (!emu) return;
      emu.setButton(button, true);
    };
    const release = (e: Event) => {
      e.preventDefault();
      if (!emu) return;
      emu.setButton(button, false);
    };
    el.addEventListener('touchstart', press, { passive: false });
    el.addEventListener('touchend', release, { passive: false });
    el.addEventListener('touchcancel', release, { passive: false });
    el.addEventListener('mousedown', press);
    el.addEventListener('mouseup', release);
    el.addEventListener('mouseleave', release);
    el.addEventListener('contextmenu', (e) => e.preventDefault());
  });

  const fileInput = document.getElementById('rom') as HTMLInputElement;
  fileInput.addEventListener('change', async () => {
    const file = fileInput.files?.[0];
    if (!file || !emu) return;
    try {
      const bytes = new Uint8Array(await file.arrayBuffer());

      // PowerOff flushes the previous cart's dirty RAM into our store; persist
      // it to localStorage before we let LoadRom overwrite the buffer.
      if (emu.isPoweredOn()) emu.powerOff();
      persistCurrentSave();

      const title = readCartTitle(bytes);
      currentSaveKey = SAVE_PREFIX + title;
      const savedB64 = localStorage.getItem(currentSaveKey);
      const restored = savedB64 ? base64ToBytes(savedB64) : undefined;

      console.info(`Loading ROM "${title}" (${bytes.length} bytes, cart type 0x${bytes[0x0147].toString(16).padStart(2, '0')})`);
      emu.loadRom(bytes, restored);
      emu.powerOn();
    } catch (err) {
      console.error(`Failed to load ROM "${file.name}":`, err);
    }
  });

  // Save on tab close / mobile background. pagehide fires reliably on iOS;
  // visibilitychange covers tab-switching desktop browsers.
  window.addEventListener('pagehide', persistCurrentSave);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') persistCurrentSave();
  });

  // Periodic safety net — protects against tab crashes between explicit saves.
  setInterval(() => {
    if (emu?.isPoweredOn()) persistCurrentSave();
  }, 10_000);
}

main();
