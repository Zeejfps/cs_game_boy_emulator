# Step 6 — DAA

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §3.2.

The 8080 DAA only "fixes" additive BCD results. The LR35902 DAA inspects the
**N** flag and corrects either an add or a subtract. This is why every
ADD/ADC/SUB/SBC/INC/DEC handler must already be setting H and N correctly
(step 1) — DAA reads them as input.

## Tasks

- [ ] Replace the 8080 (additive-only) DAA with the N-aware version
  - [ ] If `N==0` (last op was add):
    - [ ] If `H` or `(A & 0x0F) > 9`: `A += 0x06`
    - [ ] If `C` or `A > 0x9F`: `A += 0x60; C = 1`
  - [ ] If `N==1` (last op was sub):
    - [ ] If `H`: `A = (A - 0x06) & 0xFF`
    - [ ] If `C`: `A -= 0x60`
  - [ ] After: `Z = (A == 0)`; `H = 0`; `C` as set above; `N` preserved
- [ ] Verify every ADD/ADC/SUB/SBC/INC/DEC handler sets H and N correctly (DAA depends on it)

## Exit criteria

- DAA produces correct BCD results for both additive and subtractive prior
  operations — i.e. the Blargg `01-special` and `09-op r,r` tests stop
  flagging it.
- `H` is always 0 after DAA; `Z` reflects the corrected `A`.
