// One-off script to generate PWA icon PNGs from public/favicon.svg.
// Run via: npx --no-install node scripts/generate-pwa-icons.mjs
// (sharp is installed transiently with `npm install --no-save sharp`)
import sharp from 'sharp';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');
const svgPath = path.join(root, 'public', 'favicon.svg');
const outDir = path.join(root, 'public');

const svg = await fs.readFile(svgPath);

// "Any" purpose: full-bleed render of the SVG.
for (const size of [192, 512]) {
  await sharp(svg)
    .resize(size, size)
    .png()
    .toFile(path.join(outDir, `pwa-${size}.png`));
}

// "Maskable" purpose: SVG fits inside the inner 80% safe area, padded with
// the app theme color so OSes can crop/round the outer frame.
const maskableSize = 512;
const innerSize = Math.round(maskableSize * 0.8);
const pad = Math.round((maskableSize - innerSize) / 2);
const inner = await sharp(svg).resize(innerSize, innerSize).png().toBuffer();
await sharp({
  create: {
    width: maskableSize,
    height: maskableSize,
    channels: 4,
    background: { r: 0x08, g: 0x18, b: 0x20, alpha: 1 },
  },
})
  .composite([{ input: inner, top: pad, left: pad }])
  .png()
  .toFile(path.join(outDir, 'pwa-maskable-512.png'));

console.log('Generated pwa-192.png, pwa-512.png, pwa-maskable-512.png');
