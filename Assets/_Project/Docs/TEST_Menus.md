# Test: Batch 6 (menus, end screens, Demo mode)

Branch `batch6-menus`. About 15 minutes. Debug keys must be on (GameBalance > Enable Debug Keys).
**F8** wins at once (shows the win screen for today's day). **6** speeds the clock.

## Setup
- [ ] Pull the branch, open Unity, wait for compiling. **Expected:** no red errors in the Console.
- [ ] **Tools > The Last Drop > Build Playtest Scene.** **Expected:** `Systems > BalanceProvider` has `GameBalance` in Balance and `GameBalance_Demo` in Demo Balance.
- [ ] **Tools > The Last Drop > Delete Save**, then Play.

## Main menu (R11.8, R13.1)
- [ ] Press Play with no save. **Expected:** full-screen panel "SALOK" with New Game, Demo, Quit (no Continue). The clock and HYD don't move, the cursor is free.
- [ ] With the title up, press Esc, Tab, M and B. **Expected:** nothing opens behind it.
- [ ] Click **New Game**. **Expected:** the panel closes, the cursor locks, you can walk, the clock runs.
- [ ] Play to the next dawn (or press F5), stop Play, press Play again. **Expected:** the title now also shows **Continue** and "A saved game was found."
- [ ] Click **Continue**. **Expected:** you are back where the save was: day, water, gates, map (Console: `[SaveManager] Loaded day N`).
- [ ] Stop, Play, click **New Game**. **Expected:** a fresh day 1 run; stop and Play again: no Continue (the save was deleted).
- [ ] Click **Quit**. **Expected:** Play mode stops.

## Demo mode (section 13)
- [ ] Play, click **Demo**. **Expected:** the scene reloads straight into play (no title), the left HUD shows `DEMO`, days last 75 s.
- [ ] Look at a zone 1 exit lock basin. **Expected:** it needs 8 L (not 10) and opens with the exit valve settings.
- [ ] Fill it and win the valve (or press F8). **Expected:** end screen titled **DEMO COMPLETE**, "You escaped on day N.", then the stats; the game is frozen behind it.
- [ ] Click **Restart**. **Expected:** a new Demo run starts at once (no title), still `DEMO`. A full-game save made earlier is still there (stop, Play: Continue is shown).
- [ ] In Demo mode, wait past a dawn. **Expected:** no "Autosaved" line in the Console (the demo never overwrites the full game's save).
- [ ] Remove Demo Balance from the BalanceProvider and click Demo. **Expected:** a yellow warning in the Console and the full game plays (no `DEMO` line).

## Win screen (R11.6, R11.7)
- [ ] In the full game, press F8 on day 1. **Expected:** **WATER KEEPER**, "You escaped on day 1.", day, map %, litres collected, items bought, collapses, and Restart / Quit. Time is frozen, Esc/Tab do nothing.
- [ ] Use key 6 to reach day 16, press F8. **Expected:** **SURVIVOR**. Day 23 or later: **BARELY MADE IT**.
- [ ] Press 0 (reveal map) then F8. **Expected:** "Badges: Mapped 100% of the maze" (only if the map really reads 100%).
- [ ] Fill a zone 2 exit lock (18 L) and win its valve. **Expected:** the same win screen (the old "YOU ESCAPED!" panel no longer appears).
- [ ] Click **Restart**. **Expected:** a new run at day 1 straight away (no title), the save is deleted.

## Game over
- [ ] Collapse with less than 2 L in Clean Storage, click **Restart**. **Expected:** a new run straight away, no title screen.

## Pause fallback
- [ ] Press Esc while playing. **Expected:** if the team's pause menu shows, use it as before. If it can't be seen, a "PAUSED" panel with **Resume** and **Save and quit to menu** shows instead; Esc or Resume closes it.
- [ ] Click **Save and quit to menu**. **Expected:** the game saves, the scene reloads to the SALOK title with Continue.
