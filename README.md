# Game Boy Emulator

A Game Boy (DMG) emulator written in C# and compiled to WebAssembly so it runs in the browser.

**Play it:** https://gb.builtbyzee.com/

## Background

This is the third emulator I've built, working my way up in complexity:

1. [CHIP-8](https://github.com/Zeejfps/cs_chip8_emulator) — the classic starting point.
2. [Space Invaders (Intel 8080)](https://github.com/Zeejfps/cs_space_invaders_emulator) — a real arcade machine, full CPU implementation.
3. **Game Boy (LR35902)** — this repo. Cycle-accurate(ish) CPU, pixel FIFO PPU, interrupts, timer, and joypad.

## Status

- CPU: full LR35902 instruction set + CB-prefixed ops, interrupts, HALT
- PPU: pixel FIFO with background, window, and sprites; STAT line bug
- Timer (DIV/TIMA) and joypad with interrupts
- Cartridge: ROM-only (MBC0) — MBC1+ not yet implemented
- APU: stub (no audio)
- Mobile: on-screen D-pad / A / B / Start / Select with portrait scaling

## Controls (keyboard)

| Key | Button |
| --- | ------ |
| Arrows | D-pad |
| Z | A |
| X | B |
| Enter | Start |
| Shift | Select |

On touch devices, on-screen controls appear automatically and the screen scales to viewport width.

## Project layout

- `GameboyEmulator.Core` — the emulator: CPU, PPU, MMU, timer, joypad, cartridges
- `GameboyEmulator.Core.Tests` — unit tests
- `GameboyEmulator.Wasm` — .NET → WASM bridge that exposes the emulator to JS
- `GameboyEmulator.Web` — Vite + TypeScript frontend (the site at the link above)
- `docs` — implementation notes

## Build & run

Prerequisites: .NET 9 SDK, Node 18+.

```bash
# tests
dotnet test

# run the web frontend (builds the WASM package first)
cd GameboyEmulator.Web
npm install
npm run dev
```

Then open the URL Vite prints. Drop in a `.gb` ROM and play.
