import { init, type Emulator } from 'gameboy-emulator';

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
