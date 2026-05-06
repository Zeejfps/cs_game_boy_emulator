import { init, JoypadButton, type Emulator } from 'gameboy-emulator';
import { createAudio, type AudioBridge } from './audio';

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
let audio: AudioBridge | null = null;
let currentSaveKey: string | null = null;
// Retained after LoadRom so save-import can soft-reset the cart with the
// imported RAM, and save-export can name the download after the ROM file.
let currentRomBytes: Uint8Array | null = null;
let currentRomFileName: string | null = null;

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
const MUTE_KEY = 'gb-muted';

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

// Cart header introspection — mirrors MbcFactory.cs. Used to gate the
// save-export/import UI and validate imported file sizes.
const RTC_TRAILER_SIZE = 48;
function cartHasBattery(rom: Uint8Array): boolean {
  const t = rom[0x0147];
  return t === 0x03 || t === 0x0F || t === 0x10 || t === 0x13 || t === 0x1B || t === 0x1E;
}
function cartHasRtc(rom: Uint8Array): boolean {
  const t = rom[0x0147];
  return t === 0x0F || t === 0x10;
}
function cartRamSize(rom: Uint8Array): number {
  switch (rom[0x0149]) {
    case 0x00: return 0;
    case 0x01: return 0x0800;
    case 0x02: return 0x2000;
    case 0x03: return 0x8000;
    default: return 0;
  }
}
// Sizes we accept for an imported .sav: canonical (matches what GetSaveData
// produces) plus, for RTC carts, the RAM-only size emulators that don't write
// the RTC trailer use.
function expectedSaveSizes(rom: Uint8Array): number[] {
  const ram = cartRamSize(rom);
  const rtc = cartHasRtc(rom);
  const canonical = ram + (rtc ? RTC_TRAILER_SIZE : 0);
  if (rtc && ram > 0) return [canonical, ram];
  return [canonical];
}

type DialogOpts = {
  title: string;
  message: string;
  okLabel?: string;
  cancelLabel?: string | null; // null = no cancel button (alert mode)
  destructive?: boolean;
};

let dialogClose: ((result: boolean) => void) | null = null;

function openDialog(opts: DialogOpts): Promise<boolean> {
  // Resolve any in-flight dialog as cancelled before opening a new one.
  if (dialogClose) dialogClose(false);

  const root = document.getElementById('dialog-root') as HTMLElement;
  const titleEl = document.getElementById('dialog-title') as HTMLElement;
  const messageEl = document.getElementById('dialog-message') as HTMLElement;
  const okBtn = root.querySelector('.dialog-ok') as HTMLButtonElement;
  const cancelBtn = root.querySelector('.dialog-cancel') as HTMLButtonElement;
  const backdrop = root.querySelector('.dialog-backdrop') as HTMLElement;

  titleEl.textContent = opts.title;
  messageEl.textContent = opts.message;
  okBtn.textContent = opts.okLabel ?? 'OK';
  okBtn.classList.toggle('dialog-destructive', !!opts.destructive);

  const hasCancel = opts.cancelLabel !== null;
  cancelBtn.hidden = !hasCancel;
  if (hasCancel) cancelBtn.textContent = opts.cancelLabel ?? 'Cancel';

  root.hidden = false;
  // Default focus: cancel for destructive (so a stray Enter doesn't confirm),
  // ok otherwise.
  (opts.destructive && hasCancel ? cancelBtn : okBtn).focus();

  return new Promise<boolean>((resolve) => {
    const finish = (result: boolean) => {
      okBtn.removeEventListener('click', onOk);
      cancelBtn.removeEventListener('click', onCancel);
      document.removeEventListener('keydown', onKey);
      backdrop.removeEventListener('click', onBackdrop);
      root.hidden = true;
      dialogClose = null;
      resolve(result);
    };
    const onOk = () => finish(true);
    const onCancel = () => finish(false);
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); finish(false); }
    };
    const onBackdrop = (e: MouseEvent) => {
      if (e.target === backdrop) finish(false);
    };

    okBtn.addEventListener('click', onOk);
    cancelBtn.addEventListener('click', onCancel);
    document.addEventListener('keydown', onKey);
    backdrop.addEventListener('click', onBackdrop);
    dialogClose = finish;
  });
}

function showConfirm(
  title: string,
  message: string,
  opts?: { okLabel?: string; cancelLabel?: string; destructive?: boolean },
): Promise<boolean> {
  return openDialog({
    title,
    message,
    okLabel: opts?.okLabel,
    cancelLabel: opts?.cancelLabel ?? 'Cancel',
    destructive: opts?.destructive,
  });
}

function showAlert(title: string, message: string, opts?: { okLabel?: string }): Promise<void> {
  return openDialog({
    title,
    message,
    okLabel: opts?.okLabel,
    cancelLabel: null,
  }).then(() => undefined);
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
    if (emu && emu.isPoweredOn()) {
      emu.tick();
      // Audio is drained AFTER tick so the freshly-produced samples are
      // available; the worklet on the audio thread reads from the same SAB
      // ring without further coordination.
      audio?.drain(emu);
    }
  } catch (err) {
    console.error('Emulator tick failed — halting CPU loop:', err);
    if (emu?.isPoweredOn()) {
      try { emu.powerOff(); } catch (offErr) { console.error('powerOff during recovery failed:', offErr); }
    }
    return; // stop scheduling further frames; the user can load another ROM.
  }
  requestAnimationFrame(loop);
}

function setupSaveMenu(): void {
  const toggleBtn = document.getElementById('save-menu-toggle') as HTMLButtonElement | null;
  const popover = document.getElementById('save-popover') as HTMLElement | null;
  const importInput = document.getElementById('save-import') as HTMLInputElement | null;
  if (!toggleBtn || !popover || !importInput) return;

  const exportBtn = popover.querySelector('[data-action="export"]') as HTMLButtonElement;
  const importBtn = popover.querySelector('[data-action="import"]') as HTMLButtonElement;

  let isOpen = false;

  const open = () => {
    if (isOpen) return;
    const enabled = !!currentRomBytes && cartHasBattery(currentRomBytes);
    exportBtn.disabled = !enabled;
    importBtn.disabled = !enabled;
    popover.hidden = false;
    toggleBtn.setAttribute('aria-expanded', 'true');
    document.addEventListener('pointerdown', onOutside, true);
    document.addEventListener('keydown', onKey);
    isOpen = true;
  };
  const close = () => {
    if (!isOpen) return;
    popover.hidden = true;
    toggleBtn.setAttribute('aria-expanded', 'false');
    document.removeEventListener('pointerdown', onOutside, true);
    document.removeEventListener('keydown', onKey);
    isOpen = false;
  };
  const onOutside = (e: PointerEvent) => {
    const t = e.target as Node;
    if (popover.contains(t) || toggleBtn.contains(t)) return;
    close();
  };
  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'Escape') close();
  };

  toggleBtn.addEventListener('click', () => {
    if (isOpen) close(); else open();
  });

  exportBtn.addEventListener('click', () => {
    close();
    handleExport().catch((err) => console.error('Export failed:', err));
  });
  importBtn.addEventListener('click', () => {
    close();
    importInput.click();
  });
  importInput.addEventListener('change', () => {
    const file = importInput.files?.[0];
    // Reset so picking the same file again still fires 'change'.
    importInput.value = '';
    if (!file) return;
    handleImport(file).catch((err) => console.error('Import failed:', err));
  });
}

async function handleExport(): Promise<void> {
  if (!emu || !currentRomBytes) return;
  if (!cartHasBattery(currentRomBytes)) {
    await showAlert('Export unavailable', 'This cart has no battery-backed save.');
    return;
  }
  const bytes = emu.getSave();
  if (!bytes || bytes.length === 0) {
    await showAlert('Nothing to export', 'No save data has been written yet.');
    return;
  }
  // Strip the .gb extension so the .sav sits next to the ROM in file managers.
  const base = currentRomFileName
    ? currentRomFileName.replace(/\.gbc?$/i, '')
    : readCartTitle(currentRomBytes);
  const blob = new Blob([new Uint8Array(bytes)], { type: 'application/octet-stream' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${base}.sav`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

async function handleImport(file: File): Promise<void> {
  if (!emu || !currentRomBytes) return;
  if (!cartHasBattery(currentRomBytes)) {
    await showAlert('Import unavailable', 'This cart has no battery-backed save.');
    return;
  }

  let bytes: Uint8Array;
  try {
    bytes = new Uint8Array(await file.arrayBuffer());
  } catch (err) {
    console.error('Failed to read import file:', err);
    await showAlert('Import failed', 'Could not read the selected file.');
    return;
  }

  const valid = expectedSaveSizes(currentRomBytes);
  if (!valid.includes(bytes.length)) {
    await showAlert(
      'Import failed',
      `Save file size doesn't match this cart.\nGot ${bytes.length} bytes, expected ${valid.join(' or ')}.`,
    );
    return;
  }

  const ok = await showConfirm(
    'Import save',
    'Replace current save with imported file? Game will reset.',
    { okLabel: 'Import', destructive: true },
  );
  if (!ok) return;

  if (emu.isPoweredOn()) emu.powerOff();
  emu.loadRom(currentRomBytes, bytes);
  // Commit the imported save to localStorage immediately so closing the tab
  // before any other persist trigger fires can't lose it.
  persistCurrentSave();
  emu.powerOn();
}

async function main(): Promise<void> {
  document.querySelectorAll<HTMLElement>('.version-text').forEach((el) => {
    el.textContent = __APP_VERSION__;
  });

  // The AudioContext's sampleRate is fixed at construction and the APU
  // needs it to compute its host-rate sample period — so audio is set up
  // first, sample rate is read off it, then the WASM init is parameterized
  // to match. If SAB/cross-origin-isolation is unavailable, audio stays null
  // and the emulator runs silently.
  audio = createAudio('/audio-worklet.js');

  try {
    emu = await init({
      baseUrl: '/wasm/',
      version: __APP_VERSION__,
      sampleRate: audio?.sampleRate ?? 48000,
    });
  } catch (err) {
    console.error('Failed to initialize emulator:', err);
    return;
  }

  emu.onFrame(paint);
  requestAnimationFrame(loop);

  window.addEventListener('keydown', (e) => {
    if (e.key === '`' && emu) {
      e.preventDefault();
      console.log(emu.getDebugState());
      return;
    }
    if ((e.key === 'm' || e.key === 'M') && !e.repeat) {
      e.preventDefault();
      applyMute(!muted);
      return;
    }
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

  const bindTouchButton = (el: HTMLButtonElement, buttons: JoypadButton[]) => {
    const press = (e: Event) => {
      e.preventDefault();
      el.classList.add('pressed');
      if (!emu) return;
      for (const b of buttons) emu.setButton(b, true);
    };
    const release = (e: Event) => {
      e.preventDefault();
      el.classList.remove('pressed');
      if (!emu) return;
      for (const b of buttons) emu.setButton(b, false);
    };
    el.addEventListener('touchstart', press, { passive: false });
    el.addEventListener('touchend', release, { passive: false });
    el.addEventListener('touchcancel', release, { passive: false });
    el.addEventListener('mousedown', press);
    el.addEventListener('mouseup', release);
    el.addEventListener('mouseleave', release);
    el.addEventListener('contextmenu', (e) => e.preventDefault());
  };

  document.querySelectorAll<HTMLButtonElement>('#page-game button[data-btn]').forEach((el) => {
    const name = el.dataset.btn as keyof typeof JoypadButton | undefined;
    if (!name) return;
    bindTouchButton(el, [JoypadButton[name]]);
  });

  document.querySelectorAll<HTMLButtonElement>('#page-game button[data-combo]').forEach((el) => {
    const combo = el.dataset.combo;
    if (combo === 'AB') bindTouchButton(el, [JoypadButton.A, JoypadButton.B]);
  });

  const ssPill = document.getElementById('ss-pill');
  if (ssPill) {
    const leftEl = ssPill.querySelector<HTMLElement>('.ss-left')!;
    const rightEl = ssPill.querySelector<HTMLElement>('.ss-right')!;
    let activePointer: number | null = null;

    // Center seam fires both — fat-finger zone for soft-reset / Pokemon menu cheats.
    const SEAM = 0.2; // 20% of the pill width is "both"

    const apply = (clientX: number) => {
      const rect = ssPill.getBoundingClientRect();
      const t = (clientX - rect.left) / rect.width;
      const sel = t < 0.5 + SEAM / 2;
      const start = t > 0.5 - SEAM / 2;
      if (!emu) return;
      emu.setButton(JoypadButton.Select, sel);
      emu.setButton(JoypadButton.Start, start);
      leftEl.classList.toggle('pressed', sel);
      rightEl.classList.toggle('pressed', start);
    };
    const clear = () => {
      if (emu) {
        emu.setButton(JoypadButton.Select, false);
        emu.setButton(JoypadButton.Start, false);
      }
      leftEl.classList.remove('pressed');
      rightEl.classList.remove('pressed');
    };

    ssPill.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      ssPill.setPointerCapture(e.pointerId);
      activePointer = e.pointerId;
      apply(e.clientX);
    });
    ssPill.addEventListener('pointermove', (e) => {
      if (e.pointerId !== activePointer) return;
      apply(e.clientX);
    });
    const end = (e: PointerEvent) => {
      if (e.pointerId !== activePointer) return;
      activePointer = null;
      clear();
    };
    ssPill.addEventListener('pointerup', end);
    ssPill.addEventListener('pointercancel', end);
    ssPill.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  const showPage = (p: 'picker' | 'game') => {
    document.getElementById('page-picker')!.classList.toggle('hidden', p !== 'picker');
    document.getElementById('page-game')!.classList.toggle('hidden', p !== 'game');
  };

  const muteBtn = document.getElementById('mute-toggle') as HTMLButtonElement | null;
  const muteIconOn = muteBtn?.querySelector<SVGElement>('.icon-mute-on') ?? null;
  const muteIconOff = muteBtn?.querySelector<SVGElement>('.icon-mute-off') ?? null;
  // Tracked locally so the toggle works even when audio is null (no SAB /
  // cross-origin isolation) — the user's preference still persists for later.
  let muted = localStorage.getItem(MUTE_KEY) === '1';
  const applyMute = (next: boolean) => {
    muted = next;
    audio?.setMuted(next);
    if (muteBtn) {
      muteBtn.setAttribute('aria-pressed', next ? 'true' : 'false');
      muteBtn.setAttribute('aria-label', next ? 'Unmute audio' : 'Mute audio');
    }
    // SVG elements don't reliably honor the `hidden` attribute across browsers
    // (display defaults differ from HTML), so flip display directly.
    if (muteIconOn) muteIconOn.style.display = next ? 'none' : '';
    if (muteIconOff) muteIconOff.style.display = next ? '' : 'none';
    try { localStorage.setItem(MUTE_KEY, next ? '1' : '0'); } catch { /* storage full / blocked — ignore */ }
  };
  applyMute(muted);
  muteBtn?.addEventListener('click', () => applyMute(!muted));

  const fsBtn = document.getElementById('fullscreen-toggle');
  fsBtn?.addEventListener('click', async () => {
    try {
      if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
      } else {
        await document.exitFullscreen();
      }
    } catch (err) {
      console.warn('Fullscreen request failed:', err);
    }
  });
  document.addEventListener('fullscreenchange', () => {
    if (!fsBtn) return;
    const on = !!document.fullscreenElement;
    fsBtn.setAttribute('aria-label', on ? 'Exit fullscreen' : 'Toggle fullscreen');
    fsBtn.classList.toggle('active', on);
  });

  document.getElementById('power-off')?.addEventListener('click', async () => {
    const ok = await showConfirm(
      'Power off',
      'Power off and return to ROM picker? Your progress will be saved.',
      { okLabel: 'Power off' },
    );
    if (!ok) return;
    if (emu?.isPoweredOn()) {
      persistCurrentSave();
      emu.powerOff();
    }
    // Reset so picking the same file again still fires 'change'.
    (document.getElementById('rom') as HTMLInputElement).value = '';
    showPage('picker');
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
      currentRomBytes = bytes;
      currentRomFileName = file.name;
      emu.powerOn();
      // Browsers block AudioContext.resume() outside a user gesture; the
      // file-picker change event is one. Failures here aren't fatal — silent
      // operation is still useful.
      audio?.start().catch((err) => console.warn('Audio start failed:', err));
      showPage('game');
    } catch (err) {
      console.error(`Failed to load ROM "${file.name}":`, err);
    }
  });

  setupSaveMenu();

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
