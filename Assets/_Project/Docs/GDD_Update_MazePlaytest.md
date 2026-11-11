# GDD update: Maze playtest build

**Date:** 2026-11-10 · **Build:** `Assets/Scenes/Playtest_Maze.unity` (Unity 6000.3.6f1) · **Author:** George (with Claude Code)

This update records what the maze playtest build puts into the game, which rules it follows, the new or changed rules it needs, and the questions it raised. Paste each part into the matching GDD section and Rules tab.

**Labels:**
- **CONFIRMED:** approved by the designer brief.
- **ASSUMPTION:** chosen to keep the build moving; needs a yes or no.
- **PROPOSAL:** a suggested new rule.
- **OPEN QUESTION:** undecided.
- **OUT OF SCOPE:** not in this build.

---

## 1. Summary

The playtest scene tests whether six systems **work**, not whether they're balanced or fun yet:

1. The 2-layer maze built from the CSV.
2. Zone 2 turning 90° clockwise every 2 days.
3. The map screen, including the turn.
4. Gates with lock basins and the Valve Timing minigame.
5. The Build and Skills menu.
6. Autosave.

**OUT OF SCOPE (this build):**
- hydration drain, drinking and collapse;
- water sources, purifiers and evaporation;
- events and drought stages;
- room loot and survivor notes.

Water exists only as numbers: an infinite canteen and a Clean Storage amount.

Logic is written as plain C# with EditMode tests. All tunable numbers are in **GameBalance**. Debug keys are behind one checkbox.

---

## 2. What the build does, by system

### 2.1 Maze layout (2-layer map)

**Source:** `Maze2Layer_0deg.csv`, 95 × 95 tiles. Row 1 is y = 0 (north) and column A is x = 0 (west). The centre tile is (47, 47).

**Codes:**
- blank = floor;
- **W** = wall (4 × 4 m, 11 m tall);
- **G** = gate tile, a 3-tile strip per gate, each with a lock basin;
- **R** = room;
- **S** = base / spawn;
- **V** = valve gate (**new code**);
- **D** and **T** are accepted but not used.

**Parts by ring:** ring = max(|x − 47|, |y − 47|).

| Rings | Part | Turns? |
|---|---|---|
| 0–9 | Hub | No |
| 10–27 | Zone 1 | No |
| 28–44 | Zone 2 | **Yes** |
| 45–47 | Outer wall, with zone 2's 8 exits | No |

**Contents, verified by tests:**
- 16 gates: 4 hub gates (ring 7), 4 zone 1 exits (ring 25), 8 zone 2 exits (ring 45);
- 8 valves: 3 in zone 1, 5 in zone 2;
- 7 rooms (5 × 5).

**Tile to world:** tile (x, y) is at (4x, 0, 4(94 − y)), so north is +Z.

**Reachability, verified in all 4 orientations with valves closed:**
- every zone 1 exit reaches every zone 2 exit (128 of 128 routes, as on the reference sheet);
- the shortest walk from the spawn to an exit is 71 / 71 / 79 / 75 tiles at 0° / 90° / 180° / 270°.

### 2.2 Day and night clock (R1.5, R8.19)

- **CONFIRMED:** a day is **240 s of daylight + 60 s of night lockdown = 300 s**, and the day counter starts at 1.
- **CONFIRMED:** GameBalance "Day Length Seconds" now means *daylight only*; "Night Lockdown Seconds" is new.
- **ASSUMPTION:** Demo mode keeps its 75 s daylight and also gets the 60 s night unless that's changed.

### 2.3 Night lockdown (R8.21, R8.23)

- **CONFIRMED:** at night every gate and valve grate closes, even opened ones, and reopens at dawn.
- **CONFIRMED (playtest):** being outside the hub or a room at night only writes "caught outside at night" to the log. There's no damage.
- **PROPOSAL:** a player standing *in* a gate's doorway when night starts is moved to the gate's hub side, so the closing gate can't trap them.
- **ASSUMPTION:** at night you can still pour water into a basin, but you can't start the minigame.

### 2.4 Zone 2 rotation (R8.24 to R8.28)

- **CONFIRMED:** zone 2 turns 90° clockwise at the **start of the night lockdown on days 2, 4, 6 …**.
  - The turn count on day d is floor((d − 1) / 2) mod 4, plus 1 during the night of an even day.
  - So days 1–2 are at 0°, days 3–4 at 90°, days 5–6 at 180°, days 7–8 at 270°, and day 9 is back to 0°.
  - "Days Between Turns" is in GameBalance.
- **CONFIRMED:**
  - one clockwise turn moves tile (x, y) to (94 − y, x);
  - all zone 2 objects sit under one pivot at (188, 0, 188), and one turn is +90° on Y;
  - check: valve (60, 15) → (79, 60) → (34, 79) → (15, 34).
- **CONFIRMED (R8.26):** a player in zone 2 turns with it, both position and facing. The camera turns too, so the view doesn't jump. Turning maps floor onto floor, so the player never ends up in a wall.
- **OPEN QUESTION:** the reference sheet says "a player inside a **zone 2 room** turns with it". The build turns a player standing **anywhere in zone 2** (rings 28–44), as the brief asked. Which is the rule?
- **Implementation note:** the turn is instant, with no animation.

### 2.5 Map screen (R8.1a to R8.1d, R8.4, R8.28)

- **CONFIRMED:** a full-screen tile map, one pixel per tile. **M** opens it, and it's also a page of the Tab menu. The clock keeps running while it's open.
- **CONFIRMED reveal rules**, which replace the old metre-radius reveal:
  - **R8.1a:** stepping on a tile reveals its 4 neighbours, or all 8 with V3 Keen Eyes;
  - **R8.1b:** it also reveals up to 2 tiles ahead (4 with V1 Trail Sense) in the camera's facing direction, snapped to north, east, south or west;
  - **R8.1b and R8.1c:** the look-ahead stops at the first wall and reveals it; nothing behind a wall is revealed;
  - **R8.1d:** revealed tiles stay revealed, and the map starts black.
- **CONFIRMED (R8.28):** what the player knows about zone 2 (revealed tiles and markers) turns with zone 2.
- **CONFIRMED (R8.4):** **F** places or removes a marker, up to 3 (8 with V2 Landmarks).
- **CONFIRMED (new rule, everyone gets it):** a **zone 2 turn indicator** ("Zone 2: 90°" with an arrow). The first time the map opens after a turn, the revealed zone 2 tiles flash for 2 s.
- **ASSUMPTION:** **Map %** = revealed *walkable* tiles (floor, rooms, base, gates, valves) out of all walkable tiles. Walls don't count.
- **ASSUMPTION:** the look-ahead only happens on stepping onto a new tile, as R8.1b is written. A GameBalance option also allows it when turning the camera in place.
- **ASSUMPTION:** the look-ahead sees past gates and valve grates, because only walls block it under R8.1c.

### 2.6 Gates and lock basins (R8.9)

- **CONFIRMED:** every G gate has a lock basin on its hub side.

  | Gate | Litres to fill |
  |---|---|
  | Hub gates | 4 L |
  | Zone 1 exits | 10 L |
  | Zone 2 exits | 18 L |

- **CONFIRMED:**
  - hold **E** within 2 m to pour from the canteen at 1 L/s;
  - poured water stays between visits;
  - when the basin is full, "Press E to open the valve" starts the Valve Timing minigame;
  - winning opens the gate for good (it still closes at night);
  - losing keeps the water;
  - each gate is independent, so filling one gate of a ring is enough to pass.

### 2.7 Valve gates (R8.8)

- **CONFIRMED:** a V tile is a grate that blocks the corridor until its minigame is won. It then opens for good, but still closes at night.
- The 2 L first-open reward (GameBalance "Gate Reward") is only logged in this build.

### 2.8 Valve Timing minigame (section 9)

- **CONFIRMED:**
  - a needle swings across a half circle;
  - **Space** inside the green band is a hit; outside it is a miss;
  - after a hit, the band jumps at least 40° away.
- **CONFIRMED rules:**
  - **R9.1:** the world doesn't pause, but the player can't move;
  - **R9.2:** **Esc** quits with no penalty;
  - **R9.3:** losing logs −5 HYD and locks that gate's minigame for 10 s.
- **CONFIRMED (new rule, replaces the old per-zone table): R9.6 difficulty grows smoothly with distance from the hub.**
  - t = clamp01((ring − 9) / 35)
  - band = 30 − 15t degrees
  - speed = 120 + 80t degrees per second
  - 3 hits win, 3 misses lose

  | Gate | t | Band | Speed |
  |---|---|---|---|
  | Hub gates (ring 7) | 0 | 30° | 120 |
  | Zone 1 exits (ring 25) | 0.457 | 23.1° | 156.6 |
  | Valve at (37, 46), ring 10 | 0.029 | 29.6° | 122.3 |
  | Valve at (60, 15), ring 32 | 0.657 | 20.1° | 172.6 |
  | **Zone 2 exits** | exit valve settings | **12°** | **220** (4 hits / 3 misses) |

- **CONFIRMED (R11.6):** winning a zone 2 exit is the game's win. The playtest shows "You escaped on day N" with a Continue button.
- **Feedback:** a clunk and the wheel turning one notch on a hit; a hiss and a red flash on a miss. The sound hooks exist, but there's no audio yet.

### 2.9 Build and Skills (R7.1 to R7.4)

- **CONFIRMED:** one menu, three pages (Build, Skills, Map).
  - **Tab** opens the menu on the page last used (Build the first time).
  - **B** opens Build and **M** opens Map.
  - The same key again, or **Esc**, closes it.
  - The last page and the last selected card on each page are remembered and saved.
- **CONFIRMED:** while the menu is open the player can't move and the cursor is free. **The clock pauses only on Build or Skills at the hub.** The Map page never pauses it.
- **CONFIRMED data:** 34 items come from `BuildAndSkills.csv` and `BuildAndSkills_Effects.csv`. Re-import with **Tools > The Last Drop > Import Build and Skills CSV** after changing prices.
- **CONFIRMED Clean Storage:** it starts at **10 L in a 25 L Tapayan**. The cap follows the storage owned: S2 = 50 L, S3 = 150 L, S6 = 300 L.
- **CONFIRMED card states**, checked in this order:
  1. **Owned**
  2. **Locked** (the chip names what's missing)
  3. **Needs bigger storage** (R7.3)
  4. **Short of water** (the chip says how many litres)
  5. **Can buy**
- **CONFIRMED (R7.4):** buying is permanent, with no refunds and no confirm dialog. Buying works only at the hub (ring ≤ 9); the playtest has "Allow buying anywhere" on.
- **CONFIRMED wired effects:**
  - M1: walk 4.4 m/s;
  - M2: sprint 7.3 m/s;
  - V1: look 4 tiles ahead;
  - V2: 8 markers;
  - V3: reveal 8 neighbours;
  - S2, S3 and S6: storage cap.

  Every other effect is stored for the systems that come later.
- **ASSUMPTION:** if two owned items set the same value (S1 → S4 → S7 canteen size), the one **bought last** wins.
- **ASSUMPTION:** the Demo mode cost multiplier (0.5, rounded up) applies to **Build items as well as skills**.

### 2.10 Saving (R11.8)

- **CONFIRMED:** one save slot, a JSON file with a version number.
  - It's written to a temporary file first, so a crash can't corrupt the save.
  - **Autosave happens at every dawn**, after that night's turn, so a load starts at that dawn with zone 2 already turned.
  - A bad or old-version file is rejected with a message.
- **CONFIRMED contents:**
  - the day (and zone 2's turns, as a check);
  - Clean Storage and the canteen;
  - owned upgrades;
  - per gate: litres poured and whether it's open;
  - valves won;
  - revealed tiles and markers (zone 2 by its 0° position);
  - menu memory.
- **ASSUMPTION:** the save also stores the player's position and facing.
- **Implementation notes:**
  - zone 2 turns forced with a debug key aren't saved, because the day decides the turn;
  - a minigame lockout isn't saved.
- **Continue:** there's no main menu yet. A Continue button should be enabled when `SaveManager.HasSave` is true and call `SaveManager.LoadAndStart()`.

### 2.11 Controls and debug

**Controls:**
- WASD and Shift to move and sprint;
- **E** to pour or interact;
- **Space** in the minigame;
- **Esc** to quit or close, **and now Esc again resumes from pause** (before, a pause with no visible menu froze the game);
- **M** for the map, **Tab** and **B** for the menu, **F** for a marker.

**Debug keys** (one checkbox in GameBalance, team rule):

| Key | Does |
|---|---|
| 0 | Reveal the map |
| 5 | Force a zone 2 turn |
| 6 | Clock × 4 |
| 7 | +50 L Clean Storage |
| F1 | Debug HUD |
| F5 | Save |
| F9 | Load |

8 and 9 stay reserved for drought stage and events.

---

## 3. Values added to GameBalance

| Header | Value | Default |
|---|---|---|
| Clock | Night Lockdown Seconds | 60 s |
| Clock | Debug Clock Speed | ×4 |
| Maze layout | Wall Height | 11 m |
| Maze layout | Hub / Zone 1 / Zone 2 Max Ring | 9 / 27 / 44 |
| Maze layout | Zone 2 Days Between Turns | 2 |
| Map reveal | Look Ahead Tiles (Trail Sense) | 2 (4) |
| Map reveal | Look Ahead When Turning | off |
| Map reveal | Turn Flash Seconds | 2 s |
| Valve Timing difficulty | Ring Start / Ring Span | 9 / 35 |
| Valve Timing difficulty | Band At Hub / Band Shrink | 30° / 15° |
| Valve Timing difficulty | Speed At Hub / Speed Gain | 120 / 80 °/s |
| Valve Timing difficulty | Hits To Win / Misses To Lose | 3 / 3 |
| Mode | Allow Buying Anywhere (playtest) | off (the playtest turns it on) |
| Mode | Debug Clean Storage Liters | 50 L |

**No longer used:**
- `gridSize` (41) and `revealRadius` (metres), replaced by the CSV size and the tile reveal rules;
- the per-zone Valve Timing rows [0]–[2], replaced by the formula. Row [3] is still the exit valve.

---

## 4. Open questions

1. Does R8.26 turn a player **anywhere in zone 2**, or only **inside a zone 2 room**? (Section 2.4.)
2. Should **Map %** count only blank floor tiles, or every walkable tile as it does now?
3. Should closed **gates and grates block the map look-ahead** the way walls do?
4. Should the look-ahead also work when the player **turns the camera while standing still**?
5. **Pouring at night:** allowed, as now, or blocked like the minigame?
6. Should the save keep the **player's position**, or should every load start the player at the hub?
7. Should the Demo **cost multiplier** apply to Build items, or only to skills?
8. **Zone 1 layout:** the 2-layer sheet's zone 1 is the drawn version, which differs from the `Zone1_Map` sheet in about 1 in 6 tiles. Which one is the source of truth? (Already open in the GDD.)
9. The **win flow** after "You escaped": end screen, credits, or keep playing?
10. Should zone 2's turn be **animated** (and how long), and what happens to a player mid-turn?

---

## 5. Changed / unchanged / next step

**Changed:**
- the clock (night added);
- map reveal (tile rules replace the radius);
- Valve Timing difficulty (formula replaces the table);
- new tile code V;
- the new zone 2 turn indicator rule;
- Esc now toggles pause;
- the save design is now concrete.

**Unchanged:**
- hydration, sources, purifiers, events, drought stages and collapse, which aren't touched by this build;
- the Zone 1 scenes, which still build and play as before.

**Next step:**
1. Run `Assets/_Project/Docs/PLAYTEST_Maze.md` and record results in its notes table.
2. Answer the open questions above.
3. Fold the answers into the Rules tab with rule numbers.
