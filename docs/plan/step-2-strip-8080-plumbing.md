# Step 2 — Strip 8080-only plumbing

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §5.

The Game Boy has no separate I/O bus, no `XCHG`/`XTHL`, and no
opcode-injection-style interrupts. Remove all of it before reusing the freed
opcode bytes in step 3 — otherwise the old 8080 handlers can run in place of
their LR35902 replacements and silently corrupt state.

## Tasks

- [ ] Delete IN/OUT support
  - [ ] Remove `IIOBus.cs`
  - [ ] Remove `Cpu.Io.cs`
  - [ ] Remove the `IIOBus` field/parameter from `Cpu`
- [ ] Remove XCHG / XTHL from `Cpu.Special.cs` (keep the file)
- [ ] Remove the legacy interrupt-injection plumbing
  - [ ] `_isInterruptPending`
  - [ ] `_pendingInterruptOpcode`
  - [ ] Public `Interrupt(byte)` API
- [ ] Remove "undocumented alias" entries from the `Execute` switch
  - [ ] `0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0xCB, 0xD9, 0xDD, 0xED, 0xFD`
- [ ] Remove or rewrite the existing 8080 dispatch arms whose opcode bytes are reused
  - [ ] `0x22 Shld`, `0x2A Lhld`, `0x32 StA`, `0x3A LdA`
  - [ ] `0xE0 Rpo`, `0xE2 Jpo`, `0xE8 Rpe`, `0xEA Jpe`
  - [ ] `0xF0 Rp`, `0xF2 Jp`, `0xF8 Rm`, `0xFA Jm`
- [ ] Delete (or rename + rewrite) the per-opcode files now reused
  - [ ] `Cpu.Lhld.cs`, `Cpu.Shld.cs`, `Cpu.LdA.cs`, `Cpu.StA.cs`

## Exit criteria

- No references to `IIOBus`, `In`, `Out`, `Xchg`, `Xthl`, `Interrupt(byte)`,
  `_pendingInterruptOpcode`, or `_isInterruptPending` anywhere in the project.
- Every reused opcode byte either has no handler at all or throws
  "not implemented" — step 3 will fill them in.
- All 11 illegal opcodes can stay unhandled for now; step 3 makes them fault.
