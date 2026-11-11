# Playtest checklist: Playtest_Maze

Tick each box when the **expected result** happens. If it doesn't, write what you saw next to it and the tile from the debug HUD (F1).

This scene only tests whether these systems **work**: the 2-layer maze, the zone 2 turn, the map, gates and Valve Timing, Build and Skills, and saving. Water does nothing to the player here. There's no drain, drinking or collapse. The canteen is infinite, and Clean Storage is just a number.

## Before you start

- [ ] Open Unity and wait for scripts to compile. **Expected:** the Console shows no red errors.
- [ ] Run **Tools > The Last Drop > Build Playtest Scene**. **Expected:** `Assets/Scenes/Playtest_Maze.unity` opens. The Console says the maze was built ("… 8 valve grates … zone 2 under Zone2Pivot") and the scene was saved.
- [ ] In **Window > General > Test Runner > EditMode**, click **Run All**. **Expected:** every test is green: MazeGrid, GameClock, Zone2Rotation, MapReveal, Gate, Upgrade, MenuMemory and Save tests.
- [ ] Select `Maze` and check that **Gates Enabled** is ticked.
- [ ] Select GameBalance (`Assets/Scripts/Data/GameBalance.asset`) and check that **Enable Debug Keys** is ticked.
- [ ] Optional: run **Tools > The Last Drop > Delete Save** to start fresh.

**Keys:**
- WASD to move, Shift to sprint, mouse to look.
- **E** to pour or interact, **Space** in the minigame, **Esc** to quit or close.
- **M** for the map, **Tab** for the menu, **B** for Build, **F** for a marker.
- Debug keys: **0** reveal map, **5** turn zone 2, **6** clock ×4, **7** +50 L, **F1** debug HUD, **F5** save, **F9** load.

**Finding a tile:** tile (x, y) sits at world position (x × 4, 0, (94 − y) × 4). The **F1** HUD always shows your current tile.

## 1. The maze

- [ ] Press Play. **Expected:** you stand in the blue-tinted base at tile (47, 47), and walls are about 11 m tall.
- [ ] Press **F1**. **Expected:** the HUD reads "Tile (47, 47) ring 0 Hub code Spawn".
- [ ] Look at the hub's wall. **Expected:** 4 blue gates at (40, 47) west, (47, 40) north, (54, 47) east and (47, 54) south, each with a lock basin and a bar on the inside.
- [ ] Walk into the walls. **Expected:** you can't pass through any wall, gate or orange grate.

## 2. A hub gate (4 L and the minigame)

- [ ] Walk to the west hub gate's basin. **Expected:** within about 2 m the prompt reads "Hold [E] to pour water (0.0 / 4 L)".
- [ ] Hold **E**. **Expected:** the bar rises about 1 L per second. After 4 s the prompt changes to "Press [E] to open the valve".
- [ ] Let go halfway on another gate, walk away and come back. **Expected:** the water you poured is still there.
- [ ] Press **E**. **Expected:**
  - the Valve Timing panel opens and you can't move;
  - the HUD clock keeps running;
  - with F1 on, the line reads "t 0.000 band 30.0° speed 120.0°/s, win 3 hits / lose 3 misses".
- [ ] Press **Space** inside the green band. **Expected:** a hit: the wheel turns a notch and the band jumps at least 40° away.
- [ ] Press **Space** outside the band. **Expected:** a miss and a red flash.
- [ ] Make 3 misses. **Expected:**
  - the panel closes;
  - the Console logs "−5 HYD (R9.3) … locked for 10 s";
  - the prompt shows "Valve jammed (10s)" counting down;
  - the basin still holds 4 L.
- [ ] Start it again and press **Esc** mid-game. **Expected:** it closes with no penalty, and the pause menu does **not** open.
- [ ] Win (3 hits). **Expected:** the 3 gate pieces disappear and you can walk through. On the map (M), that gate is light blue.

## 3. A zone 1 exit (t ≈ 0.46)

- [ ] Go to a zone 1 exit: (22, 35), (35, 72), (59, 22) or (72, 59). Ring 25. **Expected:** the basin needs **10 L**.
- [ ] Fill it and start the minigame with F1 on. **Expected:** "t 0.457 band 23.1° speed 156.6°/s". The needle is visibly faster and the band narrower than at the hub.
- [ ] Win it. **Expected:** the gate opens.

## 4. Valve grates

- [ ] Find an orange grate: zone 1 valves are at (64, 39), (37, 46) and (35, 56). Press **E** at it. **Expected:** the minigame starts straight away (no basin). For (37, 46), F1 shows "t 0.029 band 29.6° speed 122.3°/s".
- [ ] Win. **Expected:** the grate disappears, and the Console logs "+2 L water reward (R8.8) … only a log".
- [ ] Optional: the zone 2 valve at (60, 15). **Expected:** "t 0.657 band 20.1° speed 172.6°/s".

## 5. Day, night and the zone 2 turn

- [ ] Press **6**. **Expected:** the HUD clock shows "(x4)". At ×4 a whole 300 s day takes 75 s.
- [ ] Wait for night 1. **Expected:**
  - the clock reads "Day 1 - NIGHT" and counts down;
  - every opened gate and grate comes back and blocks you;
  - the Console logs "Night of day 1 …", plus "Caught outside at night!" unless you're in the base or a room.
- [ ] Dawn of day 2. **Expected:** opened gates open again. Zone 2 has **not** turned yet.
- [ ] Walk through to zone 2 (past a zone 1 exit) and stand still. Wait for the **night of day 2**, about 135 s real time at ×4 from the start of day 1. **Expected:**
  - the Console logs "Zone 2 turned 0° -> 90° (night of day 2); the player was inside and turned with it";
  - your view of zone 2 doesn't jump, but the static walls around zone 2 change;
  - you are not stuck in a wall.
- [ ] Press **5** three more times. **Expected:** 180°, 270°, then back to 0°, each logged.
- [ ] Stand on an open gate's tile when night starts. **Expected:** you're moved to the hub side ("was standing in a gate and was moved").

## 6. The map (M)

- [ ] Press **M**. **Expected:**
  - a full-screen map, black except what you've seen;
  - a white arrow for you, pointing the way you face;
  - Map % in the side panel;
  - "Zone 2: 0°" with an arrow pointing up.
- [ ] Walk a straight corridor, then reopen the map. **Expected:** the tiles you walked are revealed, plus the 4 tiles next to each. 2 tiles ahead are revealed in the camera's direction. Nothing shows behind a wall.
- [ ] Press **F** on 3 tiles, then a 4th. **Expected:** 3 yellow diamonds, and the 4th press logs "full". F on a marker removes it.
- [ ] Reveal some of zone 2, put a marker there, and press **5**. Open the map. **Expected:**
  - "Zone 2: 90°  turned!" with the arrow pointing right;
  - the zone 2 tiles you revealed **flash for 2 s**;
  - your zone 2 tiles and marker now sit a quarter turn clockwise;
  - zone 1 hasn't moved;
  - Map % is unchanged.
- [ ] Press **0**. **Expected:** the whole map is revealed and shows 100.0%.

## 7. Build and Skills (Tab menu)

- [ ] Press **Tab**. **Expected:**
  - the menu opens on **Build**;
  - the header shows "10.0 / 25 L", "DAY n · EL NIÑO ADVISORY" and "Tab Close";
  - at the hub, the HUD clock stops.
- [ ] Check the card chips. **Expected:**
  - W1 is light blue "CAN BUY";
  - W2 is amber "SHORT 5 L";
  - W4 is "NEEDS W1";
  - S6 is "NEEDS S3".
- [ ] **At the hub:** on GameBalance, untick **Allow Buying Anywhere**, stand in the base, select **W1** and click **Buy for 8 L**. **Expected:**
  - Clean Storage drops to 2.0;
  - W1 shows "OWNED" with a blue border;
  - the line from W1 down to W4 turns blue;
  - W4 now says "SHORT 18 L".
- [ ] Press **7** twice. **Expected:** the storage tops out at 25 / 25. Buy **W4** (20 L). **Expected:** it's owned, and 5 L is left.
- [ ] Walk out of the hub and open Build. **Expected:** the panel says "Go back to the hub to buy" and Buy is greyed out. Tick Allow Buying Anywhere again afterwards.
- [ ] Buy **S2** (15 L). **Expected:** the header reads "/ 50 L". Buy S3 and it reads "/ 150 L".
- [ ] Buy **M1**, close the menu and walk. **Expected:** you're a little faster (4.4 m/s). Buy **V1**. **Expected:** the map now reveals 4 tiles ahead.
- [ ] **Tab memory:** press B (Build), click **Skills ›**, select V3, press Tab (close), then Tab again. **Expected:** it reopens on **Skills** with V3 selected.
- [ ] Then press **M**, close, and press Tab. **Expected:** it reopens on **Map**, and the clock keeps running on the Map page.

## 8. Saving

- [ ] Set up a state you'll recognise:
  - buy something;
  - half-fill one basin and open another gate;
  - drop markers;
  - leave the menu on Skills.

  Press **F5**. **Expected:** the Console says "Saved day n to …thelastdrop_save.json".
- [ ] Change things (buy more, press 0, walk away), then press **F9**. **Expected:** everything returns to the F5 state, including your position and the half-filled basin.
- [ ] **Stop Play (quit), press Play, press F9.** **Expected:** the same state loads into the fresh run.
- [ ] Play into the night of day 2 (key 6) and on to the dawn of day 3. **Expected:** "Autosaved at the dawn of day 3". Stop, Play, F9: day 3, "Zone 2: 90°" on the map, and your zone 2 knowledge in its turned place.
- [ ] Use **Tools > The Last Drop > Show Save File**, change `"version": 1` to `0` and save the file. Press F9. **Expected:** a warning that the save was made by another version, and nothing breaks.
- [ ] Run **Tools > The Last Drop > Delete Save**, then press F9. **Expected:** "there is no save file".

## 9. The win

- [ ] Reach a zone 2 exit (for example (27, 92) or (92, 67)). **Expected:** the basin needs **18 L**. The minigame shows "exit valve band 12.0° speed 220.0°/s, win 4 hits".
- [ ] Win it. **Expected:** the **"YOU ESCAPED! You escaped on day N."** panel appears, and **Continue** returns you to play.

## 10. Debug keys off

- [ ] Untick **Enable Debug Keys** on GameBalance and press Play. **Expected:** 0, 5, 6, 7, F1, F5 and F9 do nothing, and the F1 HUD stays hidden. Tick it again afterwards.

## Notes from this playtest

| What | Tile / day | What happened |
|---|---|---|
| | | |
