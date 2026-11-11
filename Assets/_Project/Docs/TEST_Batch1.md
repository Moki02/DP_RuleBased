# Test: Batch 1 (foundation + core survival loop)

Branch `batch1-survival-core`. About 15 minutes. Press **6** for the ×4 clock when waiting.

## Setup
- [ ] Pull the branch, open Unity, wait for compiling. **Expected:** no red errors in the Console.
- [ ] **Tools > The Last Drop > Build Playtest Scene.** **Expected:** a `HubStations` object with the hand pump, boiling pot and Clean Storage appears around the spawn.
- [ ] **Tools > The Last Drop > Delete Save**, then Play.

## Hydration (R2)
- [ ] Stand still in the base for 40 s. **Expected:** HYD drops by about 5 (0.25/s × 0.5 hub shade). The team HUD shows HYD and the canteen; at the base the Hub HUD shows Raw Basin and Clean Storage.
- [ ] Walk out of the base and sprint. **Expected:** HYD falls faster; at 59 the text says Thirsty, at 29 Dehydrated and Shift stops working.
- [ ] Hold **Q**. **Expected:** the canteen drops 0.5 L/s and HYD rises 12.5/s; it stops at 100 HYD or an empty canteen.

## Hub
- [ ] Hold **E** on the pump for 10 s. **Expected:** Raw Basin +6.0 L (0.3 L per stroke). At 15 L the prompt says FULL.
- [ ] Watch the boiling pot. **Expected:** Raw Basin goes down and Clean Storage goes up about 1 L every 6 s.
- [ ] Press Tab, buy something, close. **Expected:** Clean Storage keeps the pot's water (it was wiped before).
- [ ] Use the Clean Storage tap (drink / refill). **Expected:** works, 1 L/s refill.
- [ ] Buy **S1 Water Jug**. **Expected:** canteen reads `/ 4 L`. **S5** makes the Raw Basin 40 L.

## Unbanked water and banking (R3.5, R8.2a)
- [ ] Walk into new corridors. **Expected:** `Unbanked +x.x L` grows by 0.03 L per new tile.
- [ ] Walk back into the hub before night. **Expected:** toast `+x.x L banked (+y Safe Return)`, Clean Storage goes up.
- [ ] Bank when Clean Storage is full. **Expected:** the rest goes to the Raw Basin, then `Spare jugs`, which pour back in as room frees.

## Minigames (R9.3, R8.8)
- [ ] Lose a valve minigame. **Expected:** toast `-5 HYD`, HYD drops 5.
- [ ] Win an orange valve grate. **Expected:** `+2.0 L unbanked (valve opened)`.

## Night (R8.20, R8.21)
- [ ] Watch the last minute of day. **Expected:** `Lockdown in N s` bottom left; toasts at 60, 40, 20, 10 and 5 s.
- [ ] Stay outside at night. **Expected:** HYD drops 4 per second; you collapse in ~25 s.

## Collapse and game over (R11)
- [ ] Collapse (from the night, or let HYD run out). **Expected:** 2 s fade, then you wake in the base at 50 HYD with the banner "YOU COLLAPSED"; the canteen is empty, unbanked is halved, 2 L came out of Clean Storage, the clock jumped 30 s.
- [ ] Collapse with less than 2 L in Clean Storage. **Expected:** "THE DROUGHT WINS" with day, map %, litres collected, items bought and collapses, plus Restart and Quit.

## Save
- [ ] F5, change things, F9. **Expected:** HYD, canteen, Raw Basin, unbanked water and spare jugs come back too.

| What | Tile / day | What happened |
|---|---|---|
| | | |
