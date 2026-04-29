# LR35902 Implementation Plan

A checklist-driven plan for converting the existing Intel 8080 core in
`GameboyEmulator.Core/Intel8080/` into the Game Boy's LR35902 (Sharp SM83) CPU.
Based on [`8080-to-LR35902.md`](8080-to-LR35902.md) §6 ("Suggested order of work").

Each step lives in its own document under [`plan/`](plan/). Work them in
order — later steps assume the earlier ones are done.

## Steps

- [ ] [Step 1 — Flag layout and ALU helpers](plan/step-1-flags-and-alu-helpers.md)
- [ ] [Step 2 — Strip 8080-only plumbing](plan/step-2-strip-8080-plumbing.md)
- [ ] [Step 3 — Implement the 21 "Replace" opcodes](plan/step-3-replace-opcodes.md)
- [ ] [Step 3.1 — Cycle-count audit for Keep/Modify opcodes](plan/step-3.1-cycle-count-audit.md)
- [ ] [Step 4 — CB-prefix path and the 11 CB operations](plan/step-4-cb-prefix.md)
- [ ] [Step 5 — Interrupt model, EI/DI/RETI, HALT, STOP](plan/step-5-interrupts-halt-stop.md)
- [ ] [Step 6 — DAA](plan/step-6-daa.md)
- [ ] [Step 7 — Validate against Blargg's `cpu_instrs`](plan/step-7-blargg-validation.md)

## Reference docs

- [`8080-to-LR35902.md`](8080-to-LR35902.md) — narrative conversion guide
- [`lr35902-opcode-tables.md`](lr35902-opcode-tables.md) — primary + CB opcode tables
