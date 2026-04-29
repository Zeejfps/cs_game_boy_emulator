# Step 2 — Strip 8080-only plumbing

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §5.

The Game Boy has no separate I/O bus, no `XCHG`/`XTHL`, and no
opcode-injection-style interrupts. Remove all of it before reusing the freed
opcode bytes in step 3 — otherwise the old 8080 handlers can run in place of
their LR35902 replacements and silently corrupt state.

## Snapshot of the relevant source after step 1

- `Cpu.Io.cs` contains `Di`, `Ei`, `In`, `Out`. **Only `In`/`Out` go away.**
  `Di`/`Ei` are real LR35902 instructions (`0xF3`/`0xFB`) and move into a new
  `Cpu.Interrupts.cs` partial (which step 5 will grow to hold the IF/IE
  registers, IME handling, and the interrupt service routine). The
  `_enableInterruptsTimer` field they rely on also stays — step 5 rewrites
  EI semantics on top of it.
- `Xchg`/`Xthl` live in `Cpu.Stack.cs` (alongside `PushB/PopB/.../Sphl`), not
  in the old `Cpu.Special.cs` (which step 1 renamed to `Cpu.Alu.Special.cs`).
  `Sphl` (`0xF9`, becomes `LD SP, HL` in step 3) **must not** be removed.
- `Cpu.Lhld.cs`, `Cpu.Shld.cs`, `Cpu.LdA.cs`, `Cpu.StA.cs` still exist as
  per-opcode files for `0x2A`, `0x22`, `0x3A`, `0x32`.
- The 8080 sign/parity branch handlers (`Rpo`, `Rpe`, `Rp`, `Rm`, `Jpo`,
  `Jpe`, `Jp`, `Jm`, `Cpo`, `Cpe`, `Cp`, `Cm`) are already
  `throw new NotImplementedException("…rewired in step 3")` stubs in
  `Cpu.Branch.cs`, but the dispatch arms still call them. Removing the
  dispatch arms in this step lets us delete the stubs too.
- `Cpu.cs` still has `_isInterruptPending`, `_pendingInterruptOpcode`,
  `Interrupt(byte)`, `TryExecuteInterrupt`, and corresponding lines in
  `Reset()` and `Step()`.
- The dispatch switch in `Cpu.cs` is a non-exhaustive switch expression with
  no default arm. Removing a dispatch arm causes that opcode byte to throw
  `SwitchExpressionException` at runtime — which is acceptable "fault on
  illegal opcode" behavior until step 3 adds explicit handlers.

## Tasks

### Production code

- [x] Delete IN/OUT support
  - [x] Remove `IIOBus.cs`
  - [x] Remove the `_io` field and the `IIOBus` constructor parameter on `Cpu`
  - [x] Remove `In()` (`0xDB`) and `Out()` (`0xD3`), and their dispatch arms
  - [x] Create `Cpu.Interrupts.cs` and move `Di()` / `Ei()` into it
    (dispatch arms `0xF3` / `0xFB` keep pointing at the same method names).
    Then delete `Cpu.Io.cs`.
- [x] Remove `Xchg` and `Xthl` from `Cpu.Stack.cs` (keep the file — `PushB`,
  `PopB`, …, `PushPsw`, `PopPsw`, `Sphl` all stay)
- [x] Remove the legacy interrupt-injection plumbing from `Cpu.cs`
  - [x] Field `_isInterruptPending`
  - [x] Field `_pendingInterruptOpcode`
  - [x] Public `Interrupt(byte)` API
  - [x] Method `TryExecuteInterrupt`
  - [x] The corresponding two lines in `Reset()`
  - [x] The `TryExecuteInterrupt` call in `Step()` (leave the `Halted` /
    `UpdateInterruptTimer` / `Fetch` / `Execute` flow intact — step 5
    reintroduces the LR35902 interrupt service routine)
- [x] Remove "undocumented alias" entries from the `Execute` switch
  - [x] `0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38` (currently → `Nop`)
  - [x] `0xCB` (currently → `Jmp`; on LR35902 it is the CB-prefix, wired in
    step 4)
  - [x] `0xD9` (currently → `Ret`; on LR35902 it is `RETI`, wired in step 5)
  - [x] `0xDD, 0xED, 0xFD` (currently → `Call`; permanently illegal on
    LR35902)
- [x] Remove the existing 8080 dispatch arms whose opcode bytes are reused,
      and delete the now-orphaned handlers
  - [x] `0x22 Shld`, `0x2A Lhld`, `0x32 StA`, `0x3A LdA` — also delete
    `Cpu.Lhld.cs`, `Cpu.Shld.cs`, `Cpu.LdA.cs`, `Cpu.StA.cs`. (The
    `LdAb`/`LdAd` / `StAb`/`StAd` handlers in `Cpu.LdA.cs` / `Cpu.StA.cs`
    are valid LR35902 ops `0x0A`/`0x1A`/`0x02`/`0x12`; preserve them by
    moving them into `Cpu.Mov.cs` or a new `Cpu.LoadStore.cs` before
    deleting the original files.) Implemented as `Cpu.LoadStore.cs`.
  - [x] `0xE0 Rpo`, `0xE2 Jpo`, `0xE8 Rpe`, `0xEA Jpe`,
        `0xF0 Rp`,  `0xF2 Jp`,  `0xF8 Rm`,  `0xFA Jm` — delete the dispatch
    arms and the throwing stubs in `Cpu.Branch.cs`.
  - [x] `0xE3 Xthl`, `0xEB Xchg` — dispatch arms removed alongside the
    handlers above.

### Tests

- [x] Delete `GameboyEmulator.Core.Tests/CpuIoTests.cs` (deferred from step 1).
- [x] Remove `NoOpCpuIO` from `CpuTestHelper.cs`.
- [x] Update `CpuTestBase.cs`
  - [x] Drop the `new NoOpCpuIO()` argument from the `Cpu` constructor call
  - [x] Drop `CreateCpu(CpuState, IIOBus)` (no longer needed; was unused
    outside `CpuIoTests`).
- [x] Delete the six tests in `CpuMovTests.cs` that exercise removed
  handlers: `TestXthl`, `TestXchg`, `TestLhld`, `TestShld`, `TestLdA`,
  `TestStA`. (The opcodes 0x2A/0x22/0x3A/0x32 are no longer dispatched, so
  these would now throw `SwitchExpressionException`.)

## Exit criteria

- No references to `IIOBus`, `In`, `Out`, `Xchg`, `Xthl`, `Interrupt(byte)`,
  `_pendingInterruptOpcode`, `_isInterruptPending`, or `TryExecuteInterrupt`
  anywhere in `GameboyEmulator.Core` or `GameboyEmulator.Core.Tests`.
- `Cpu.Io.cs` no longer exists; `Di`/`Ei` are reachable from the dispatch
  switch in their new home.
- `Cpu.Stack.cs` still compiles and still owns `Sphl` plus all push/pop ops;
  `Xchg`/`Xthl` are gone.
- The dispatch switch contains no entry for any of: `0x08, 0x10, 0x18, 0x20,
  0x22, 0x28, 0x2A, 0x30, 0x32, 0x38, 0x3A, 0xCB, 0xD3, 0xD9, 0xDB, 0xDD,
  0xE0, 0xE2, 0xE3, 0xE8, 0xEA, 0xEB, 0xED, 0xF0, 0xF2, 0xF8, 0xFA, 0xFD`.
  Executing any of those bytes throws `SwitchExpressionException` (step 3
  replaces this with explicit handlers / a deliberate illegal-opcode trap).
- The 11 LR35902 permanently-illegal opcodes (`0xD3, 0xDB, 0xDD, 0xE3, 0xE4,
  0xEB, 0xEC, 0xED, 0xF4, 0xFC, 0xFD`) are unhandled — step 3 makes them
  fault explicitly.
- Test suite still compiles and is green (the only deleted tests are the six
  listed above in `CpuMovTests.cs`, plus the entire `CpuIoTests` file).
