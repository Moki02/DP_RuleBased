# Test: Batch 2 (drought stages, water table, evaporation)

Branch `batch2-drought`. About 10 minutes. Press **6** for the ×4 clock when waiting, **8** to jump a stage.

## Setup
- [ ] Pull the branch, open Unity, wait for compiling. **Expected:** no red errors in the Console.
- [ ] **Tools > The Last Drop > Delete Save**, then Play. **Expected:** a `StageManager` and an `Evaporation` component appear on the `Systems` object.

## Stage 1 at the start (R10.1)
- [ ] Look at the bottom-left text. **Expected:** `Stage 1: El Niño Advisory  (heat ×1)`, and a toast `RADIO: PAGASA has issued an El Niño Advisory...`.
- [ ] Look at the hand pump. **Expected:** label says `Hand Pump: 100%, water table 4 m`.
- [ ] Stand still in the base for 40 s. **Expected:** HYD drops by about 5 (0.25/s × 0.5 shade × 1 heat). Repeat after pressing **8** three times (Stage 4): about 7 (× 1.4).

## Debug key 8 and the stage changes (R10.1, section 4, R2.1)
- [ ] Press **8**. **Expected:** big banner `DRY SPELL` with one line under it, toast `RADIO: PAGASA: dry spell declared...`, left text `Stage 2: Dry Spell  (heat ×1.1)`, the sun gets slightly warmer/brighter. Pump label `80%, water table 5.5 m`; holding E fills the Raw Basin slower (about 4.8 L in 10 s instead of 6).
- [ ] Press **8**. **Expected:** `DROUGHT`, `Stage 3 ... (heat ×1.25)`, pump `50%, water table 6.5 m`. HYD drains visibly faster than at stage 1.
- [ ] Press **8**. **Expected:** `STATE OF CALAMITY`, `(heat ×1.4)`, pump `DRY, water table 7.5 m`; holding E gives no water.
- [ ] Press **8** again. **Expected:** toast `Already the last stage (State of Calamity)`; nothing else changes (R10.3).
- [ ] Buy **W4 Deep-Set Cylinder** (press 7 for water first if needed). **Expected:** pump label now `60%, water table 7.5 m, deep-set` and it pumps again. (Stage 3 with W4 = 80%, stage 2 = 100%.)

## Stages by day (R10.1, R10.2)
- [ ] New game, clock on ×4 (key 6), wait until day 3. **Expected:** at dawn the `DRY SPELL` banner and radio line appear on their own.
- [ ] Wait until day 5. **Expected:** `DROUGHT` banner, its radio line, then the toast `RADIO: PAGASA: El Niño has peaked in the Pacific.`

## Evaporation (R3.4, R5.2)
- [ ] New game. Pump the Raw Basin to 15 L, note Clean Storage (e.g. 10 L) and the canteen. Wait for dawn of day 2. **Expected:** toast `Evaporation overnight: -0.5 L` (15 × 2% + 10 × 2% = 0.5); Raw Basin 14.7, Clean Storage 9.8; the canteen is unchanged; lock basins unchanged.
- [ ] Press **8** twice during the day (Stage 3, 5%), fill up, wait for dawn. **Expected:** losses use 5% (the stage of the day that just ended, forced stage counts).
- [ ] Buy **S2 Plastic Drum**, wait for dawn. **Expected:** Clean Storage loses half the rate (2.5% at stage 3); Raw Basin still the full rate. (Console: `cover ×0.5`.)
- [ ] With spare jugs holding water (bank when everything is full), wait for dawn. **Expected:** spare jugs lose the full stage rate too.
- [ ] Heatwave (batch 3, or set `WorldState.EvaporationMultiplierTonight = 2` from a test script): **Expected:** that dawn's loss is doubled and the toast adds `(heatwave ×2)`; the next dawn is back to normal.

## Save
- [ ] Press **8** once (Stage 2 on day 1), **F5**, stop, Play, **F9**. **Expected:** still `Stage 2: Dry Spell`, no banner on load, pump at 80%.
- [ ] Load a save from an earlier stage. **Expected:** the stage line goes back without a banner.

| What | Stage / day | What happened |
|---|---|---|
| | | |
