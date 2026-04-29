# LR35902 Opcode Tables (companion to `8080-to-LR35902.md`)

These tables map every Intel 8080 opcode in `GameboyEmulator.Core/Intel8080/`
to the LR35902 (Sharp SM83 / DMG CPU) instruction at the same byte. Cycle
counts are in **T-states** (1 M-cycle = 4 T). Section references (e.g. §3, §4)
point at the main conversion guide.

Legend: **Keep** / **Modify** / **Replace** / **Delete** / **Add** — see the
main guide for definitions.

---

## 1. Primary opcode table (0x00–0xFF)

### 0x00–0x3F (NOP, INX/DCX, INR/DCR, MVI, rotates, immediate loads, accumulator helpers)

| Opcode | 8080 mnemonic | Current handler | Action | LR35902 mnemonic | Notes |
|--------|---------------|-----------------|--------|------------------|-------|
| 0x00 | NOP | `Nop` | **Keep** | NOP | 4 T. |
| 0x01 | LXI B,d16 | `LxiB` | **Keep** | LD BC,d16 | 12 T. |
| 0x02 | STAX B | `StAb` | **Keep** | LD (BC),A | 8 T. |
| 0x03 | INX B | `InxB` | **Keep** | INC BC | 8 T, no flags. |
| 0x04 | INR B | `InrB` | **Modify** | INC B | Flags: Z, N=0, H. C is **preserved** (8080 also preserves C, so semantically same — but verify N is now set to 0 instead of being a P flag). |
| 0x05 | DCR B | `DcrB` | **Modify** | DEC B | Flags: Z, N=1, H. |
| 0x06 | MVI B,d8 | `MviB` | **Keep** | LD B,d8 | 8 T. |
| 0x07 | RLC | `Rlc` | **Modify** | RLCA | 4 T. Flags become Z=0, N=0, H=0, C=bit7. (8080 only touches C; LR35902 forces Z, N, H to 0.) |
| 0x08 | NOP (alias) | `Nop` | **Replace** | LD (a16),SP | Stores SP at the 16-bit immediate address. 20 T. |
| 0x09 | DAD B | `DadB` | **Modify** | ADD HL,BC | 8 T. Flags: Z **preserved**, N=0, H from bit 11, C from bit 15. |
| 0x0A | LDAX B | `LdAb` | **Keep** | LD A,(BC) | 8 T. |
| 0x0B | DCX B | `DcxB` | **Keep** | DEC BC | 8 T, no flags. |
| 0x0C | INR C | `InrC` | **Modify** | INC C | See 0x04. |
| 0x0D | DCR C | `DcrC` | **Modify** | DEC C | See 0x05. |
| 0x0E | MVI C,d8 | `MviC` | **Keep** | LD C,d8 | 8 T. |
| 0x0F | RRC | `Rrc` | **Modify** | RRCA | 4 T. Z=0, N=0, H=0, C=bit0. |
| 0x10 | NOP (alias) | `Nop` | **Replace** | STOP | 2-byte instruction (0x10 0x00). Halts CPU + LCD until a button is pressed. Implement as a state flag; the second byte must be consumed. |
| 0x11 | LXI D,d16 | `LxiD` | **Keep** | LD DE,d16 | 12 T. |
| 0x12 | STAX D | `StAd` | **Keep** | LD (DE),A | 8 T. |
| 0x13 | INX D | `InxD` | **Keep** | INC DE | 8 T. |
| 0x14 | INR D | `InrD` | **Modify** | INC D | See 0x04. |
| 0x15 | DCR D | `DcrD` | **Modify** | DEC D | See 0x05. |
| 0x16 | MVI D,d8 | `MviD` | **Keep** | LD D,d8 | 8 T. |
| 0x17 | RAL | `Ral` | **Modify** | RLA | 4 T. Z=0, N=0, H=0, C=bit7. |
| 0x18 | NOP (alias) | `Nop` | **Replace** | JR r8 | Unconditional 8-bit signed relative jump. 12 T. |
| 0x19 | DAD D | `DadD` | **Modify** | ADD HL,DE | 8 T. See 0x09. |
| 0x1A | LDAX D | `LdAd` | **Keep** | LD A,(DE) | 8 T. |
| 0x1B | DCX D | `DcxD` | **Keep** | DEC DE | 8 T. |
| 0x1C | INR E | `InrE` | **Modify** | INC E | See 0x04. |
| 0x1D | DCR E | `DcrE` | **Modify** | DEC E | See 0x05. |
| 0x1E | MVI E,d8 | `MviE` | **Keep** | LD E,d8 | 8 T. |
| 0x1F | RAR | `Rar` | **Modify** | RRA | 4 T. Z=0, N=0, H=0, C=bit0. |
| 0x20 | NOP (alias) | `Nop` | **Replace** | JR NZ,r8 | 12 T taken, 8 T not taken. |
| 0x21 | LXI H,d16 | `LxiH` | **Keep** | LD HL,d16 | 12 T. |
| 0x22 | SHLD a16 | `Shld` | **Replace** | LD (HL+),A | Store A at (HL), then HL++. 8 T. (Old 8080 SHLD with absolute addr is gone.) |
| 0x23 | INX H | `InxH` | **Keep** | INC HL | 8 T. |
| 0x24 | INR H | `InrH` | **Modify** | INC H | See 0x04. |
| 0x25 | DCR H | `DcrH` | **Modify** | DEC H | See 0x05. |
| 0x26 | MVI H,d8 | `MviH` | **Keep** | LD H,d8 | 8 T. |
| 0x27 | DAA | `Daa` | **Modify** | DAA | 4 T. LR35902 DAA depends on the **N** flag (it must subtract-correct after SUB/SBC, add-correct after ADD/ADC). 8080 DAA only does add-correct. Algorithm differs — see §3.2. |
| 0x28 | NOP (alias) | `Nop` | **Replace** | JR Z,r8 | 12 / 8 T. |
| 0x29 | DAD H | `DadH` | **Modify** | ADD HL,HL | 8 T. See 0x09. |
| 0x2A | LHLD a16 | `Lhld` | **Replace** | LD A,(HL+) | 8 T. |
| 0x2B | DCX H | `DcxH` | **Keep** | DEC HL | 8 T. |
| 0x2C | INR L | `InrL` | **Modify** | INC L | See 0x04. |
| 0x2D | DCR L | `DcrL` | **Modify** | DEC L | See 0x05. |
| 0x2E | MVI L,d8 | `MviL` | **Keep** | LD L,d8 | 8 T. |
| 0x2F | CMA | `Cma` | **Modify** | CPL | 4 T. Flags: N=1, H=1 set explicitly (8080 CMA touched no flags). |
| 0x30 | NOP (alias) | `Nop` | **Replace** | JR NC,r8 | 12 / 8 T. |
| 0x31 | LXI SP,d16 | `LxiSp` | **Keep** | LD SP,d16 | 12 T. |
| 0x32 | STA a16 | `StA` | **Replace** | LD (HL-),A | Store A at (HL), then HL--. 8 T. |
| 0x33 | INX SP | `InxSp` | **Keep** | INC SP | 8 T. |
| 0x34 | INR M | `InrM` | **Modify** | INC (HL) | Z, N=0, H. 12 T. |
| 0x35 | DCR M | `DcrM` | **Modify** | DEC (HL) | Z, N=1, H. 12 T. |
| 0x36 | MVI M,d8 | `MviM` | **Keep** | LD (HL),d8 | 12 T. |
| 0x37 | STC | `Stc` | **Modify** | SCF | 4 T. C=1, but **N=0, H=0** (8080 STC only touched C). |
| 0x38 | NOP (alias) | `Nop` | **Replace** | JR C,r8 | 12 / 8 T. |
| 0x39 | DAD SP | `DadSp` | **Modify** | ADD HL,SP | 8 T. See 0x09. |
| 0x3A | LDA a16 | `LdA` | **Replace** | LD A,(HL-) | 8 T. |
| 0x3B | DCX SP | `DcxSp` | **Keep** | DEC SP | 8 T. |
| 0x3C | INR A | `InrA` | **Modify** | INC A | See 0x04. |
| 0x3D | DCR A | `DcrA` | **Modify** | DEC A | See 0x05. |
| 0x3E | MVI A,d8 | `MviA` | **Keep** | LD A,d8 | 8 T. |
| 0x3F | CMC | `Cmc` | **Modify** | CCF | 4 T. C ^= 1, **N=0, H=0**. |

### 0x40–0x7F (8×8 register-to-register moves + HALT)

All of `MOV r,r'` (0x40–0x7F except 0x76) become `LD r,r'` and are functionally
unchanged. Cycles: 4 T for register/register, 8 T for any source or destination of
`(HL)`. **Keep** all existing `MoveXX` handlers as-is.

| Opcode | 8080 mnemonic | Action | LR35902 mnemonic |
|--------|---------------|--------|------------------|
| 0x40–0x45, 0x47 | MOV B,r | **Keep** | LD B,r |
| 0x46 | MOV B,M | **Keep** | LD B,(HL) |
| 0x48–0x4D, 0x4F | MOV C,r | **Keep** | LD C,r |
| 0x4E | MOV C,M | **Keep** | LD C,(HL) |
| 0x50–0x55, 0x57 | MOV D,r | **Keep** | LD D,r |
| 0x56 | MOV D,M | **Keep** | LD D,(HL) |
| 0x58–0x5D, 0x5F | MOV E,r | **Keep** | LD E,r |
| 0x5E | MOV E,M | **Keep** | LD E,(HL) |
| 0x60–0x65, 0x67 | MOV H,r | **Keep** | LD H,r |
| 0x66 | MOV H,M | **Keep** | LD H,(HL) |
| 0x68–0x6D, 0x6F | MOV L,r | **Keep** | LD L,r |
| 0x6E | MOV L,M | **Keep** | LD L,(HL) |
| 0x70–0x75, 0x77 | MOV M,r | **Keep** | LD (HL),r |
| 0x76 | HLT (`Hlt`) | **Modify** | HALT | LR35902 HALT has the famous "halt bug": if `IME=0` and an interrupt is pending, the next instruction is fetched but PC is **not** incremented (byte is read twice). See §4. Cycles: 4 T. |
| 0x78–0x7D, 0x7F | MOV A,r | **Keep** | LD A,r |
| 0x7E | MOV A,M | **Keep** | LD A,(HL) |

### 0x80–0xBF (ALU on register)

All eight ALU groups are functionally retained, but every handler must be reviewed
for new flag semantics (especially **N** and **H**, see §3).

| Opcode range | 8080 | Action | LR35902 |
|--------------|------|--------|---------|
| 0x80–0x87 | ADD r | **Modify** | ADD A,r — Z, N=0, H, C |
| 0x88–0x8F | ADC r | **Modify** | ADC A,r — Z, N=0, H, C |
| 0x90–0x97 | SUB r | **Modify** | SUB r — Z, N=1, H, C |
| 0x98–0x9F | SBB r | **Modify** | SBC A,r — Z, N=1, H, C |
| 0xA0–0xA7 | ANA r | **Modify** | AND r — Z, N=0, **H=1**, C=0 (8080 sets H from bit 3 of ORed inputs; LR35902 forces H=1) |
| 0xA8–0xAF | XRA r | **Modify** | XOR r — Z, N=0, H=0, C=0 |
| 0xB0–0xB7 | ORA r | **Modify** | OR r — Z, N=0, H=0, C=0 |
| 0xB8–0xBF | CMP r | **Modify** | CP r — Z, N=1, H, C (same as SUB but discards result) |

### 0xC0–0xFF (Stack, jumps, calls, returns, immediates, restarts, misc)

| Opcode | 8080 mnemonic | Current handler | Action | LR35902 mnemonic | Notes |
|--------|---------------|-----------------|--------|------------------|-------|
| 0xC0 | RNZ | `Rnz` | **Keep** | RET NZ | 20 / 8 T. |
| 0xC1 | POP B | `PopB` | **Keep** | POP BC | 12 T. |
| 0xC2 | JNZ a16 | `Jnz` | **Keep** | JP NZ,a16 | 16 / 12 T. |
| 0xC3 | JMP a16 | `Jmp` | **Keep** | JP a16 | 16 T. |
| 0xC4 | CNZ a16 | `Cnz` | **Keep** | CALL NZ,a16 | 24 / 12 T. |
| 0xC5 | PUSH B | `PushB` | **Keep** | PUSH BC | 16 T. |
| 0xC6 | ADI d8 | `Adi` | **Modify** | ADD A,d8 | See ADD r. 8 T. |
| 0xC7 | RST 0 | `Rst0` | **Keep** | RST 00H | 16 T. |
| 0xC8 | RZ | `Rz` | **Keep** | RET Z | 20 / 8 T. |
| 0xC9 | RET | `Ret` | **Keep** | RET | 16 T. |
| 0xCA | JZ a16 | `Jz` | **Keep** | JP Z,a16 | 16 / 12 T. |
| 0xCB | JMP (alias) | `Jmp` | **Replace** | **PREFIX CB** | Switches decoder to the CB table (§2). The 4 T prefix fetch is already included in the CB instruction totals in §2 — do not double-count. |
| 0xCC | CZ a16 | `Cz` | **Keep** | CALL Z,a16 | 24 / 12 T. |
| 0xCD | CALL a16 | `Call` | **Keep** | CALL a16 | 24 T. |
| 0xCE | ACI d8 | `Aci` | **Modify** | ADC A,d8 | 8 T. |
| 0xCF | RST 1 | `Rst1` | **Keep** | RST 08H | 16 T. |
| 0xD0 | RNC | `Rnc` | **Keep** | RET NC | 20 / 8 T. |
| 0xD1 | POP D | `PopD` | **Keep** | POP DE | 12 T. |
| 0xD2 | JNC a16 | `Jnc` | **Keep** | JP NC,a16 | 16 / 12 T. |
| 0xD3 | OUT d8 | `Out` | **Delete** | *(illegal)* | Game Boy has no IN/OUT bus. Treat as illegal opcode → fault. |
| 0xD4 | CNC a16 | `Cnc` | **Keep** | CALL NC,a16 | 24 / 12 T. |
| 0xD5 | PUSH D | `PushD` | **Keep** | PUSH DE | 16 T. |
| 0xD6 | SUI d8 | `Sui` | **Modify** | SUB d8 | 8 T. |
| 0xD7 | RST 2 | `Rst2` | **Keep** | RST 10H | 16 T. |
| 0xD8 | RC | `Rcy` | **Keep** | RET C | 20 / 8 T. |
| 0xD9 | RET (alias) | `Ret` | **Replace** | RETI | Pop PC and re-enable interrupts (IME=1, *immediately*, not after one instruction like EI). 16 T. |
| 0xDA | JC a16 | `Jc` | **Keep** | JP C,a16 | 16 / 12 T. |
| 0xDB | IN d8 | `In` | **Delete** | *(illegal)* | |
| 0xDC | CC a16 | `Cc` | **Keep** | CALL C,a16 | 24 / 12 T. |
| 0xDD | CALL (alias) | `Call` | **Delete** | *(illegal)* | |
| 0xDE | SBI d8 | `Sbi` | **Modify** | SBC A,d8 | 8 T. |
| 0xDF | RST 3 | `Rst3` | **Keep** | RST 18H | 16 T. |
| 0xE0 | RPO | `Rpo` | **Replace** | LDH (a8),A | Store A at `0xFF00 + a8`. 12 T. |
| 0xE1 | POP H | `PopH` | **Keep** | POP HL | 12 T. |
| 0xE2 | JPO a16 | `Jpo` | **Replace** | LD (C),A | Store A at `0xFF00 + C`. 8 T. (No 16-bit immediate; opcode is 1 byte.) |
| 0xE3 | XTHL | `Xthl` | **Delete** | *(illegal)* | |
| 0xE4 | CPO a16 | `Cpo` | **Delete** | *(illegal)* | |
| 0xE5 | PUSH H | `PushH` | **Keep** | PUSH HL | 16 T. |
| 0xE6 | ANI d8 | `Ani` | **Modify** | AND d8 | See AND. 8 T. |
| 0xE7 | RST 4 | `Rst4` | **Keep** | RST 20H | 16 T. |
| 0xE8 | RPE | `Rpe` | **Replace** | ADD SP,r8 | Add signed 8-bit to SP. Flags: Z=0, N=0, H from bit 3 of `SP_low + r8`, C from bit 7 of `SP_low + r8` (low-byte arithmetic). 16 T. |
| 0xE9 | PCHL | `Pchl` | **Keep** | JP HL | 4 T (LR35902 is faster than 8080's 5 T). Note: despite some references writing this as `JP (HL)`, the destination is the *value* of HL, not memory at HL — there is no indirection. |
| 0xEA | JPE a16 | `Jpe` | **Replace** | LD (a16),A | 16 T. |
| 0xEB | XCHG | `Xchg` | **Delete** | *(illegal)* | |
| 0xEC | CPE a16 | `Cpe` | **Delete** | *(illegal)* | |
| 0xED | CALL (alias) | `Call` | **Delete** | *(illegal)* | (Note: this is Z80's prefix byte, but on LR35902 it is illegal.) |
| 0xEE | XRI d8 | `Xri` | **Modify** | XOR d8 | 8 T. |
| 0xEF | RST 5 | `Rst5` | **Keep** | RST 28H | 16 T. |
| 0xF0 | RP | `Rp` | **Replace** | LDH A,(a8) | Load A from `0xFF00 + a8`. 12 T. |
| 0xF1 | POP PSW | `PopPsw` | **Modify** | POP AF | The popped F register's low 4 bits **must be forced to 0** on LR35902 (only Z, N, H, C exist). |
| 0xF2 | JP a16 | `Jp` | **Replace** | LD A,(C) | Load A from `0xFF00 + C`. 8 T. |
| 0xF3 | DI | `Di` | **Modify** | DI | LR35902: clears IME *immediately* (8080 also immediate, behavior matches). Verify your `InterruptEnabled` semantics map onto IME — see §4. 4 T. |
| 0xF4 | CP a16 | `Cp` | **Delete** | *(illegal)* | |
| 0xF5 | PUSH PSW | `PushPsw` | **Modify** | PUSH AF | F now has low 4 bits = 0 (already true if popped correctly). |
| 0xF6 | ORI d8 | `Ori` | **Modify** | OR d8 | 8 T. |
| 0xF7 | RST 6 | `Rst6` | **Keep** | RST 30H | 16 T. |
| 0xF8 | RM | `Rm` | **Replace** | LD HL,SP+r8 | HL = SP + signed r8. Flags: Z=0, N=0, H/C as in 0xE8. 12 T. |
| 0xF9 | SPHL | `Sphl` | **Modify** | LD SP,HL | Behavior identical, cycle count is now 8 T (8080 is 5). |
| 0xFA | JM a16 | `Jm` | **Replace** | LD A,(a16) | 16 T. |
| 0xFB | EI | `Ei` | **Modify** | EI | One-instruction delay before IME is set — semantics already match the existing `_enableInterruptsTimer`. Verify the value is correct (set to 1, set IME after the *next* instruction completes). 4 T. |
| 0xFC | CM a16 | `Cm` | **Delete** | *(illegal)* | |
| 0xFD | CALL (alias) | `Call` | **Delete** | *(illegal)* | |
| 0xFE | CPI d8 | `Cpi` | **Modify** | CP d8 | See CP r. 8 T. |
| 0xFF | RST 7 | `Rst7` | **Keep** | RST 38H | 16 T. |

### 1.1 Summary counts

| Action     | Count |
|------------|------:|
| Keep       |   ~95 |
| Modify     |   ~70 |
| Replace    |    21 |
| Delete     |    11 |
| Add (CB)   |   256 |

The 21 **Replace** opcodes are:
`0x08, 0x10, 0x18, 0x20, 0x22, 0x28, 0x2A, 0x30, 0x32, 0x38, 0x3A, 0xCB, 0xD9, 0xE0, 0xE2, 0xE8, 0xEA, 0xF0, 0xF2, 0xF8, 0xFA`.

(Counts are approximate; "Modify" and "Keep" overlap depending on whether a handler
needs flag-setting changes. The flag rewrite in §3 will touch most ALU handlers.)

### 1.2 Illegal opcodes (must fault)

```
0xD3, 0xDB, 0xDD, 0xE3, 0xE4, 0xEB, 0xEC, 0xED, 0xF4, 0xFC, 0xFD
```

On real hardware these lock up the CPU. Suggested implementation: throw / log /
break in the decoder rather than silently NOP, so test ROMs can flag them.

---

## 2. The 0xCB-prefixed instruction table (256 new opcodes)

When the decoder sees 0xCB, fetch the next byte and dispatch from this 256-entry
table. Every CB instruction is 2 bytes total. The low 3 bits select the operand:

```
0 → B    1 → C    2 → D    3 → E
4 → H    5 → L    6 → (HL) 7 → A
```

All `(HL)` variants take **16 T** (read-modify-write); `BIT n,(HL)` is **12 T**
(read only). All register variants take **8 T**.

### 2.1 Operations (groups of 8)

| Range       | Operation | Description | Flag effects (Z N H C) |
|-------------|-----------|-------------|-------------------------|
| 0x00–0x07   | RLC r     | Rotate left, bit 7 → C and bit 0 | Z=result, N=0, H=0, C=bit7 |
| 0x08–0x0F   | RRC r     | Rotate right, bit 0 → C and bit 7 | Z=result, N=0, H=0, C=bit0 |
| 0x10–0x17   | RL r      | Rotate left through C | Z=result, N=0, H=0, C=old bit7 |
| 0x18–0x1F   | RR r      | Rotate right through C | Z=result, N=0, H=0, C=old bit0 |
| 0x20–0x27   | SLA r     | Shift left arithmetic (bit 0 = 0) | Z=result, N=0, H=0, C=bit7 |
| 0x28–0x2F   | SRA r     | Shift right arithmetic (bit 7 preserved) | Z=result, N=0, H=0, C=bit0 |
| 0x30–0x37   | **SWAP r**| Swap upper/lower nibbles | Z=result, N=0, H=0, C=0 |
| 0x38–0x3F   | SRL r     | Shift right logical (bit 7 = 0) | Z=result, N=0, H=0, C=bit0 |
| 0x40–0x7F   | BIT n,r   | Test bit n; n = (op-0x40) >> 3 | Z=!bit, N=0, H=1, C unchanged |
| 0x80–0xBF   | RES n,r   | Reset bit n | none |
| 0xC0–0xFF   | SET n,r   | Set bit n | none |

### 2.2 Full opcode index

For each opcode `op` in `0x00..0xFF`:

```
operation = op >> 3        // 0..31, looked up via §2.1 ranges
operand   = op & 0x07      // B,C,D,E,H,L,(HL),A
bit_index = (op >> 3) & 7  // only meaningful for BIT/RES/SET
```

| op>>3 | Mnemonic    | op>>3 | Mnemonic    | op>>3 | Mnemonic    | op>>3 | Mnemonic    |
|-------|-------------|-------|-------------|-------|-------------|-------|-------------|
| 0     | RLC r       | 8     | BIT 0,r     | 16    | RES 0,r     | 24    | SET 0,r     |
| 1     | RRC r       | 9     | BIT 1,r     | 17    | RES 1,r     | 25    | SET 1,r     |
| 2     | RL r        | 10    | BIT 2,r     | 18    | RES 2,r     | 26    | SET 2,r     |
| 3     | RR r        | 11    | BIT 3,r     | 19    | RES 3,r     | 27    | SET 3,r     |
| 4     | SLA r       | 12    | BIT 4,r     | 20    | RES 4,r     | 28    | SET 4,r     |
| 5     | SRA r       | 13    | BIT 5,r     | 21    | RES 5,r     | 29    | SET 5,r     |
| 6     | SWAP r      | 14    | BIT 6,r     | 22    | RES 6,r     | 30    | SET 6,r     |
| 7     | SRL r       | 15    | BIT 7,r     | 23    | RES 7,r     | 31    | SET 7,r     |

Implementation suggestion: rather than 256 case arms, decode `op` into
`(operation, operand)` and dispatch through a small operation-level switch with
a register-getter/setter helper. Keep `(HL)` on a separate path so the cycle
count is correct.
