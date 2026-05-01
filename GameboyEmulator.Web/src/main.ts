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

function paint(): void {
  if (!emu) return;
  const fb = emu.getFrameBuffer();
  for (let i = 0; i < fb.length; i++) {
    pixels[i] = PALETTE[fb[i]];
  }
  ctx.putImageData(imageData, 0, 0);
}

function loop(): void {
  if (emu && emu.isPoweredOn()) emu.tick();
  requestAnimationFrame(loop);
}

async function main(): Promise<void> {
  try {
    emu = await init({ baseUrl: '/wasm/' });
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

  const fileInput = document.getElementById('rom') as HTMLInputElement;
  fileInput.addEventListener('change', async () => {
    const file = fileInput.files?.[0];
    if (!file || !emu) return;
    const bytes = new Uint8Array(await file.arrayBuffer());
    if (emu.isPoweredOn()) emu.powerOff();
    emu.loadRom(bytes);
    emu.powerOn();
  });
}

main();
