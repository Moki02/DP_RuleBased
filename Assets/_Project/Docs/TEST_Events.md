# Test: Batch 3 (daily El Niño events)

Branch `batch3-events`. About 15 minutes. Press **6** for the ×4 clock when waiting (event timers speed up too).
Only Calm, Thunderstorm and Heatwave are built; the other events' weights go to Calm (R10.5).

## Setup
- [ ] Pull the branch, open Unity, wait for compiling. **Expected:** no red errors in the Console.
- [ ] **Tools > The Last Drop > Build Playtest Scene**, **Tools > The Last Drop > Delete Save**, then Play.
- [ ] Watch the Console at the start. **Expected:** `[EventManager] Day 1: event at N s into the day (rolled then).` with N between 40 and 260 (R10.4).

## Daily roll (R10.4, R10.7)
- [ ] Wait until N s into the day. **Expected:** Console `Day 1: Thunderstorm/Heatwave/Calm (rolled, stage 1).` and that event starts. Stage 1 odds: 50 storm, 10 heatwave, 40 calm.
- [ ] Wait for the next dawn. **Expected:** a new `Day 2: event at ...` line; only one event per day.
- [ ] Press **9** for a Heatwave just before a day's planned time. **Expected:** the day's own event does not start on top of it (one at a time, R10.7); the heatwave ends at dawn.

## Thunderstorm (debug key 9, first press)
- [ ] Empty your canteen a bit (hold Q), stand in a corridor or the hub, press **9**. **Expected:** banner `THUNDERSTORM COMING` / "Clouds darkening, thunder..."; HUD line `STORM COMING 20 s` counting down.
- [ ] After 20 s. **Expected:** banner `THUNDERSTORM`; HUD `THUNDERSTORM 60 s (thirst ×0.5, canteen refilling)`; the sun gets darker; the canteen rises 0.05 L/s (3 L over the storm, up to its size); F1 debug HUD shows `event ×0.5`.
- [ ] Walk into a maze room during the rain. **Expected:** HUD says `under a roof: no refill` and the canteen stops rising.
- [ ] After 60 s. **Expected:** toast "The rain has stopped.", the light goes back, the HUD line disappears, drain back to normal.
- [ ] (With batch 4) the Rain Catcher only fills while the HUD says THUNDERSTORM.

## Heatwave (debug key 9, second press)
- [ ] Press **9** again. **Expected:** the storm stops at once; banner `HEATWAVE` "Heat index: Danger 46°C. Thirst ×1.3 until dawn; tonight's evaporation ×2."; HUD `HEATWAVE Danger 46°C N s (thirst ×1.3)`, N = seconds to dawn. F1 shows `event ×1.3`.
- [ ] Press **8** (with batch 2) until the last stage, then **9** twice to get a heatwave again. **Expected:** `Extreme Danger 53°C` and ×1.6.
- [ ] Wait for dawn. **Expected:** toast "The heatwave breaks with the new day."; the HUD line goes; (with batch 2) that night's evaporation was doubled.

## Calm (debug key 9, third press)
- [ ] Press **9** again. **Expected:** the heatwave stops, toast "Calm day: no El Niño event today.", no HUD line.

## Demo mode (GDD section 13)
- [ ] Put **GameBalance_Demo** in the BalanceProvider and Play. **Expected:** day 1 no event; Thunderstorm warning 20 s into day 2; Heatwave 20 s into day 3; random rolls from day 4.

## Save and load
- [ ] During a Heatwave press F5, press 9 (storm), then F9. **Expected:** no rain, the Heatwave is back (HUD line), no second event that day.
- [ ] During the rain press F5, then F9. **Expected:** the rain is gone (not saved), `IsRaining` off (the canteen stops refilling), and no new event that day.
- [ ] Load the dawn autosave. **Expected:** the day plans its event normally (Console line), no leftover HUD line.
