# Huling Patak: playtest build (maze, zone 2 rotation, map, gate minigames, Build and Skills, saving)

Paste everything below into Claude Code, opened in the root folder of our Unity project.

---

You are working in the Unity project for **Huling Patak: The Last Drop**, a third-person survival maze game for PC (Unity **6000.3.6f1**, Starter Assets Third Person Controller, new Input System, Cinemachine, TextMeshPro). I'm the designer. Our developer is away for a few days, so I need you to build a **playtest scene** step by step, in phases, so I can test each piece in the editor before you move on.

The playtest is only about whether these systems **work correctly**:

1. the 2-layer maze built from our CSV,
2. zone 2 turning 90° clockwise every 2 days,
3. the map screen (I call it the "mini map") showing what you've explored, including the turn,
4. gates with the Valve Timing minigame,
5. the Build and Skills screen,
6. autosave.

**Water does nothing to the player in this scene.** No hydration drain, no drinking, no collapse, no water sources. Water exists only as two numbers so locks and the shop can be tested: an infinite canteen (a toggle) and a Clean Storage amount you can top up with a debug key.

## Ground rules

- **Work in phases (below). After each phase, stop.** Tell me what you added or changed, file by file, and give me a short numbered list of test steps for the Unity editor. Then wait until I reply "next".
- **Phase 0 is read-only.** Change nothing until I've seen your scan report.
- **Extend what exists; don't duplicate it.** If a script already does part of a job (a grid loader, a clock, GameBalance), update it rather than writing a second one. Never delete or rename an existing script, scene or asset without asking me first.
- **Keep every tunable number in the `GameBalance` ScriptableObject** (`Assets/_Project/Scripts/Data/GameBalance.cs`; assets `GameBalance` and `GameBalance_Demo` in `Assets/_Project/Data`). Add fields there with sensible headers; no magic numbers in other scripts.
- **Put game logic in plain C# classes** (no MonoBehaviour) wherever you can, with thin MonoBehaviours on top, so the logic can be covered by EditMode tests.
- **Write EditMode tests** (Unity Test Framework) for the logic in each phase. If you can run Unity in batch mode on this machine, run them and report the results. If you can't, tell me to run them from Window > General > Test Runner.
- **New code goes under `Assets/_Project/Scripts/`** in clear subfolders (`Maze/`, `Map/`, `Gates/`, `Upgrades/`, `UI/`, `Save/`, `Debug/`, `Editor/`). Match the namespace the project already uses; if there isn't one, use `HulingPatak`.
- **Don't commit or push.** I'll review and commit myself.
- If something in this prompt conflicts with the existing code, or the rules below are unclear, ask me before guessing.

## Comments: a hard requirement

Our team is a designer, two artists and a developer, and all of them must be able to read and edit this code. So:

- **Every script starts with a header comment** in plain English covering:
  - what the script does;
  - which design rules it follows, using our rule numbers (for example `R8.24`);
  - how to set it up in the Inspector;
  - which values are safe to change, and where.
- **Every public class, method and property gets an XML `///` summary.**
- **Every serialized field gets a `[Tooltip]`** that says what it does and gives its unit (seconds, tiles, liters, degrees).
- **Comment the "why" inside methods** wherever the logic isn't obvious, above all the coordinate math and the rotation.
- **Keep the language simple.** Don't use jargon a first-year student wouldn't know without explaining it.

Example header:

```csharp
// ============================================================================
// Zone2Rotator.cs
// Turns zone 2 of the maze 90 degrees clockwise during the night lockdown of
// every even day (days 2, 4, 6 ...). GDD rules R8.24 to R8.28.
//
// HOW TO USE: put this on the "Maze" object. Drag the Zone2Pivot object into
// the "Pivot" slot. The clock tells it when night starts; you don't call it.
// HOW TO EDIT: how often it turns is "Days Between Turns" in GameBalance.
// Press 5 in Play mode to force a turn while testing.
// ============================================================================
```

## Files I've added to the project

Check these exist first:

| File | What it is |
|---|---|
| `Assets/_Project/Data/Maze/Maze2Layer_0deg.csv` | **The maze.** 95 × 95 tiles: hub, zone 1 and zone 2, zone 2 in its starting orientation. No header row. Row 1 is y = 0 (north), column A is x = 0 (west). |
| `Maze2Layer_90deg.csv`, `_180deg.csv`, `_270deg.csv` | **For tests only.** The same maze with zone 2 turned 1, 2 and 3 times. The game builds from the 0° file and rotates; tests check the rotation against these. |
| `Assets/_Project/Data/BuildAndSkills/BuildAndSkills.csv` | All 34 Build items and skills: Id, Name, Tab, Group, CostLiters, Needs (ids joined by \|), Priority, Tooltip |
| `Assets/_Project/Data/BuildAndSkills/BuildAndSkills_Effects.csv` | One row per effect: Id, Field, Value, Note |

### Tile codes

| Code | Meaning |
|---|---|
| (blank) | Floor |
| `W` | Wall: a full 4 × 4 m block, 10 to 12 m tall |
| `G` | Gate tile; every gate is a lock basin (see Phase 4) |
| `R` | Room floor (rooms are 5 × 5 and open, with no walls or doors of their own) |
| `S` | Base / spawn area (the player spawns at tile 47, 47) |
| `V` | Valve gate: a grate on a corridor tile that opens after its minigame is won |
| `D`, `T` | Not used in this map. Accept them so the loader doesn't break: D is a door, T a timed door. |

## Coordinates and rotation: the core math

- **Grid:** 95 × 95 tiles. Tile (x, y): x = column 0..94 (west to east), y = row 0..94 (north to south). Centre tile is C = (47, 47).
- **Ring** = `max(|x − 47|, |y − 47|)`. Each tile belongs to a part of the maze by its ring:

  | Ring | Part | Turns? |
  |---|---|---|
  | 0–9 | Hub (13 × 13 base inside a 3-tile wall with 4 gates) | No |
  | 10–27 | Zone 1, including its outer wall with 4 exit gates | No |
  | 28–44 | **Zone 2** | **Yes** |
  | 45–47 | Outer wall with zone 2's 8 exit gates | No |

- **Tile to world:** `x × TileSize` on the world X axis and `(94 − y) × TileSize` on the world Z axis, so the CSV reads like a top-down map with north at +Z. TileSize is 4 m, in GameBalance. If the existing loader uses a different convention, tell me in Phase 0 before changing it.
- **Turning zone 2 clockwise once** (seen from above) moves tile (x, y) to **(94 − y, x)**. The reverse, from the current world tile back to its 0° tile, is **(x, y) → (y, 94 − x)**. Apply either one k times for k turns. Tiles outside zone 2 never move.
- **In the world:** all of zone 2's objects sit under one `Zone2Pivot` at the centre tile's world position (188, 0, 188). One clockwise turn is **+90° on Y** with the convention above.
- **Sanity check for the tests:** the valve gate at (60, 15) on the 0° map is at (79, 60) after 1 turn, (34, 79) after 2 and (15, 34) after 3.
- **Turn count on day d:** `turns = floor((d − 1) / 2) mod 4`, plus 1 (mod 4) during the night of an even day, because the turn happens at the **start of the night lockdown on days 2, 4, 6…** (R8.24). So days 1–2 are at 0°, the night of day 2 switches to 90°, days 3–4 are at 90°, and so on, back to 0° on day 9.

## Phase 0: scan (read-only)

Read every script under `Assets/` that we wrote (skip `Packages/`, Starter Assets and third-party folders, but note which ones exist). Then give me a report with:

1. **Every script:** a one-line list of what each one does, grouped by folder.
2. **What already exists for:** GameBalance, a grid or maze loader, a game clock or day counter, player input and the Input Actions asset, UI (uGUI or UI Toolkit, and any Canvas setup), any map or minimap, any minigame, any upgrade or skill code, any save code, scenes, and debug keys.
3. **The existing loader's tile-to-world convention**, and whether it reads the Zone 1 CSV.
4. **Conflicts:** anything in the existing code that conflicts with this prompt.
5. **Your plan:** for each phase, the files you'll add or change.

Then stop and wait.

## Phase 1: maze data and loader

- **`MazeGrid`** (plain C#) loads a CSV TextAsset into a 95 × 95 grid. Give it:
  - code lookup by tile;
  - `Ring` and `PartOf(x, y)` (hub, zone 1, zone 2, outer wall);
  - `WorldToLocal` and `LocalToWorld` for the current turn count;
  - `CodeAt(x, y)` in the **current** orientation;
  - gate groups: connected G tiles form one gate (each is a 3-tile strip). Classify each gate by the ring of its tile nearest the centre: 7 = hub gate, 25 = zone 1 exit, 45 = zone 2 exit;
  - valve list and room list (connected R tiles).
- **Update the existing grid loader** (or add `MazeBuilder` if there isn't one) to build the maze from `Maze2Layer_0deg.csv`:
  - hub and zone 1 under a `Maze_Static` parent;
  - zone 2 under `Zone2Pivot`;
  - the outer wall with its gates under `Maze_Static`.
- **Prefab slots** for wall, floor, room floor, gate and valve. If a slot is empty, use a coloured primitive placeholder, so this works before the artists' prefabs arrive. Keep the "4 wall variants picked by a hash of x and y" idea if the loader already has it.
- **Editor menu `Tools > Huling Patak > Build Playtest Scene`** creates or refreshes `Assets/_Project/Scenes/Playtest_Maze.unity`. It holds:
  - the maze;
  - the Starter Assets third-person player at tile (47, 47), or a simple capsule controller if Starter Assets isn't in the project;
  - a light;
  - the GameBalance reference;
  - a `Systems` object for the managers you add later. Each later phase extends this builder.
- **Tests:**
  - code counts for the 0° file: W 4993, G 48, floor 3632, R 175, S 169, V 8;
  - 16 gate groups of 3 tiles each, listed by their tile nearest the centre:
    - 4 hub gates: (40,47), (47,40), (47,54), (54,47);
    - 4 zone 1 exits: (22,35), (35,72), (59,22), (72,59);
    - 8 zone 2 exits: (2,27), (2,67), (27,2), (27,92), (67,2), (67,92), (92,27), (92,67);
  - 8 valves: 3 in zone 1 at (64,39), (37,46), (35,56) and 5 in zone 2 at (72,3), (60,15), (9,18), (77,50), (12,81);
  - 7 rooms;
  - spawn tile code S.

## Phase 2: clock and zone 2 rotation

- **`GameClock`** (reuse it if one exists):
  - a day is 300 s: 240 s of daylight, then a 60 s night lockdown (R1.5, R8.19);
  - day counter starts at 1;
  - events: `NightStarted(day)`, `NewDay(day)`.
  - Debug key **6** toggles clock speed × 4.
- **Night lockdown, playtest version:** at night every gate closes (collider on, closed look) and reopens at dawn (R8.21, R8.23). Log "caught outside at night" instead of draining health.
- **`Zone2Rotator`**: at `NightStarted` on even days:
  - add 1 turn;
  - set `Zone2Pivot` to `90 × turns` degrees on Y;
  - update `MazeGrid`'s turn count;
  - fire `Zone2Turned(oldTurns, newTurns)`.
  - **If the player stands in zone 2** (any tile with ring 28–44), turn them with the zone: their position about the pivot and their facing +90°. Disable the CharacterController while moving them. Turning maps floor to floor, so this never puts them in a wall (R8.26).
  - **Debug key 5** forces a turn now.
- **Tests:**
  - turn count for days 1–10, day and night;
  - for k = 1, 2, 3: every tile's `CodeAt` after k turns equals the matching 90/180/270 CSV, and tiles outside zone 2 are unchanged;
  - the valve check from the rotation section;
  - with valve gates treated as closed and every G gate as open, all 8 zone 2 exits are reachable from (47, 47) in all 4 orientations. The shortest walks are 71 / 71 / 79 / 75 tiles for 0/90/180/270.

## Phase 3: the map screen ("mini map")

This is the full-screen 2D tile map from the GDD, not an always-on HUD corner map. It opens with **M** and is also a page of the Tab menu (Phase 5). The clock keeps running while it's open.

- **`MapReveal`** (logic) remembers revealed tiles. It stores zone 2 tiles in 0° coordinates (convert with `WorldToLocal`), so revealed tiles, markers and the player's knowledge **turn with zone 2** (R8.28). Everything else is stored as is. Reveal rules:
  - **R8.1a:** entering a tile reveals its 4 neighbours (all 8 if the player owns skill V3 Keen Eyes).
  - **R8.1b:** also reveal up to 2 tiles ahead (4 with V1 Trail Sense) in the camera's facing direction, snapped to the nearest of north/east/south/west. Stop at the first wall and reveal that wall.
  - **R8.1c:** a wall reveals nothing behind it.
  - **R8.1d:** revealed tiles stay revealed. The map starts fully black.
- **`MapScreen`** (UI): draw a 95 × 95 Texture2D, one pixel per tile, point-filtered and scaled to fit. Colours:
  - unrevealed: black;
  - floor: dark tan;
  - wall: olive;
  - gate: blue when closed, light blue when open;
  - room: green;
  - valve: orange;
  - player: a white arrow showing facing;
  - zone 2's border: a thin outline.
  
  Also show:
  - a legend;
  - **map %**: revealed floor tiles out of all floor tiles;
  - **the turn indicator** (new rule, everyone gets it): "Zone 2: 90°" with an arrow. The first time the map opens after a turn, flash the revealed zone 2 tiles for 2 s so the player sees what moved.
- **Markers (R8.4):** F drops a marker at the player's tile or removes one already there; max 3 (8 with V2 Landmarks). Store zone 2 markers in 0° coordinates.
- **Debug key 0** reveals the whole map.
- **Tests:**
  - reveal rules on a small hand-made grid;
  - a revealed zone 2 tile appears at the rotated position after a turn;
  - map % maths.

## Phase 4: gates and the Valve Timing minigame

Every gate has a minigame.

- **Lock basins (every G gate, R8.9):**
  - Litres: **hub gates 4 L, zone 1 exits 10 L, zone 2 exits 18 L** (GameBalance).
  - Hold **E** within 2 m of a gate's basin, on the side nearer the hub, to pour from the canteen at 1 L/s. A float or progress bar rises. Poured water stays between visits.
  - When the basin is full, the prompt changes to "Press E to open the valve" and starts the minigame. Winning opens the gate for good (it still closes at night). Losing keeps the water.
  - Filling any one gate of a ring is enough to pass; the others stay shut until filled.
  - Canteen: a `PlaytestWallet` with "Infinite canteen" ticked by default, plus a canteen amount for when it's off.
- **Valve gates (V tiles, R8.8):** a grate blocks the tile until its minigame is won, then opens for good (closes at night). Log the 2 L reward; it does nothing in this scene.
- **Valve Timing (section 9):** a UI panel over the game.
  - A needle swings back and forth across a half circle (0–180°). A green band sits somewhere on the dial.
  - **Space** while the needle is inside the band = hit. Outside = miss.
  - After each hit the band jumps to a random spot at least 40° away.
  - **R9.1:** the world doesn't pause; player movement is disabled while the panel is open.
  - **R9.2:** **Esc** quits with no penalty.
  - **R9.3:** losing logs "−5 HYD" and locks that gate's minigame for 10 s.
  - Feedback: a clunk sound hook and the wheel turning one notch on a hit; a hiss hook and a red flash on a miss.
- **Difficulty grows smoothly with distance from the hub (R9.6):**
  - `t = clamp01((ring − 9) / 35)`, where ring is that of the gate's tile nearest the centre.
  - green band = `30 − 15 × t` degrees.
  - needle speed = `120 + 80 × t` degrees/second.
  - 3 hits win, 3 misses lose.
  - **Zone 2's 8 exit gates use the exit valve settings instead:** 12° band, 220°/s, 4 hits to win, 3 misses to lose. Winning one is the game's win (R11.6). In the playtest, show a "You escaped on day N" panel with a Continue button.
  - Put all these numbers in GameBalance.
  - **Debug overlay (F1):** while a minigame is open, show t, band width and speed.
- **Tests:** the difficulty function:

  | Gate | t | Band | Speed |
  |---|---|---|---|
  | Hub gates (ring 7) | 0 | 30° | 120 |
  | Zone 1 exits (ring 25) | 0.457 | 23.1° | 156.6 |
  | Valve at (37,46), ring 10 | 0.029 | 29.6° | 122.3 |
  | Valve at (60,15), ring 32 | 0.657 | 20.1° | 172.6 |
  | Zone 2 exits | exit settings | 12° | 220 |

  Also test the lock state machine: empty → filling → full → minigame → open, plus closed at night.

## Phase 5: Build and Skills, and the Tab menu

### Data

- **`UpgradeData`** ScriptableObject: id, display name, tab (Build or Skills), group or branch, cost in litres, prerequisite ids, priority (Must or Nice, team only; players never see it), tooltip, and a list of effects (field name + value).
- **`UpgradeDatabase`** holds them all.
- **Editor menu `Tools > Huling Patak > Import Build and Skills CSV`** creates or updates one asset per row from the two CSVs in `Assets/_Project/Data/BuildAndSkills/`, so I can change prices in the CSV and re-import.

### Logic

- **`UpgradeManager`** keeps the owned set and Clean Storage.
  - Clean Storage starts at **10 L in a 25 L Tapayan**. Its cap follows the owned tier: S2 Plastic Drum 50 L, S3 Ferrocement Tank 150 L, S6 Underground Cistern 300 L.
  - Debug key **7** adds 50 L, up to the cap.
- **Card state, checked in this order:**

  | State | When |
  |---|---|
  | Owned | Already bought |
  | Locked | A prerequisite is missing; the chip names it |
  | Needs bigger storage | Cost is more than the current cap (R7.3) |
  | Short of water | Cost is more than what's stored; the chip says how many litres are missing |
  | Can buy | Otherwise |

- **Buying** subtracts the cost and is permanent, with no refunds (R7.4). There's no confirm dialog. Buying works only at the hub (ring ≤ 9) unless the GameBalance toggle **"Allow buying anywhere (playtest)"** is on; turn it on in the playtest scene.
- **Effects:** expose them through a lookup like `GetFloat("walkSpeed", defaultValue)` / `GetBool(...)`. Wire only the ones this scene can show:
  - M1 Light Sandals: walk speed 4.4 m/s instead of 4 (on the Starter Assets controller);
  - M2 Long Stride: sprint 7.3 m/s;
  - V1 Trail Sense: look ahead 4 tiles;
  - V2 Landmarks: 8 markers;
  - V3 Keen Eyes: reveal 8 neighbours.
  
  Leave the rest stored but unused, with a comment saying which future system reads them.

### The menu (R7.1)

- **One menu, three pages:** Build, Skills, Map.
  - **Tab** opens the menu on **the page last used** (Build the first time).
  - **B** opens it on Build.
  - **M** opens it on Map.
  - The same key again, or **Esc**, closes it.
- **Memory:** also remember the last selected card on Build and on Skills. Save the memory with the game (Phase 6).
- **Navigation:**
  - The Build page has a **"Skills ›" button on its right edge**.
  - The Skills page has a **"‹ Build" button on its left edge**.
  - The header has page tabs: Build | Skills | Map.
- **While the menu is open:** player input is disabled and the cursor is unlocked and visible. The clock pauses only while Build or Skills is open at the hub; the Map page never pauses it.
- **Layout (1920 × 1080 reference; the artists have the same design on a canvas):**
  - **Header:** title "BUILD AND SKILLS"; page tabs; Clean Storage meter ("38.4 / 50 L" plus a bar); day and stage; "Tab Close".
  - **Build page:** four columns: Water, Carry, Hub storage, Purification. Prerequisite chains are vertical stacks of cards joined by 2 px lines, blue once the item above is owned.
    - Water: W1→W4→W7, W2→W3→W8, W5→W6 (W5 needs W2).
    - Carry: S1→S4→S7.
    - Hub storage: S2→S3→S6, S5→S8.
    - Purification: P1→P3, P2.
  - **Skills page:** three branches.
    - Endurance: E1, then E2 and E4, then E3, then E5.
    - Pathfinder: M1, then M2, M3 and M4, then M5 (needs M2 and M3).
    - Scout: V1, then V2, V3 and V4, then V5 (needs V3 and V4).
  - **Cards:** 140 px wide, with:
    - an icon slot (44 px, placeholder showing the id);
    - name;
    - water-drop icon + cost;
    - a state chip.
  - **Selected card:** a 3 px light ring.
  - **Detail panel** (340 px, right side, on both pages):
    - big icon;
    - group;
    - name;
    - effect text (the tooltip);
    - Cost / Needs (✓ next to owned ones) / After buying (litres left);
    - a state message;
    - Buy button ("Buy for 20 L" / "Learn for 20 L", disabled unless Can buy);
    - "Purchases are permanent. No refunds."
  - **Colours:**

    | Element | Hex |
    |---|---|
    | Screen | `#1B1713` |
    | Panel | `#241F1A` |
    | Card | `#2A241E` |
    | Line | `#3A3129` |
    | Text | `#F2E9DC` |
    | Muted text | `#A8998A` |
    | Water blue (owned) | `#3FA9F5` |
    | Can-buy border | `#7CC4F8` |
    | Warning amber | `#E0B565` |
    | Bigger-storage border | `#8A6A2E` |

  - **Type:** use the project's TMP font; headings upper case.
- **Tests:**
  - card state order for each state;
  - prerequisites;
  - the storage cap following S2/S3/S6;
  - menu memory: open Build, switch to Skills, close, Tab reopens Skills; then M opens Map, close, Tab reopens Map.

## Phase 6: saving (R11.8)

- **`SaveManager`** plus an `ISaveable` interface (`Capture()` / `Restore()`), so each system saves its own part. This keeps it easy to extend when the water systems come back.
- **One slot:** JSON in `Application.persistentDataPath/hulingpatak_save.json`, with a `version` number. Write to a temp file, then replace, so a crash can't corrupt the save.
- **Autosave** right after `NewDay`, which comes after the night turn. So a loaded game starts at that dawn, with zone 2 already in the right orientation.
- **What to save:**
  - day number (the turn count comes from the day, but also store it as a check);
  - Clean Storage and the canteen;
  - owned upgrades;
  - per gate: litres poured and open or not;
  - valve gates won;
  - revealed tiles (0° coordinates for zone 2) and markers;
  - menu memory: last page and last selected cards.
- **Debug keys:** F5 saves now, F9 loads.
- **Editor menu `Tools > Huling Patak > Delete Save`.**
- **Continue:** if the project has a main menu, add a Continue button that's enabled only when a save exists. If it doesn't, add `SaveManager.HasSave` and `LoadAndStart()` and tell me where to hook them.
- **Tests:**
  - a save/load round-trip restores every field;
  - loading a day-5 save gives turn count 2 (180°) and the right `CodeAt` values;
  - a bad or old-version file is rejected with a clear log message and doesn't crash.

## Phase 7: playtest pass

- **Debug HUD (F1 toggles it):**
  - day and clock;
  - day or night;
  - zone 2 turn (0/90/180/270°);
  - the player's tile (x, y), ring, part and the code under them;
  - Clean Storage;
  - the nearest gate's state and t.
- **All debug keys go through one `DebugKeys` script**, behind a single "Debug keys on" checkbox in GameBalance (already a team rule):

  | Key | Does |
  |---|---|
  | 0 | Reveal whole map |
  | 5 | Force zone 2 turn |
  | 6 | Clock × 4 |
  | 7 | +50 L Clean Storage |
  | F1 | Debug HUD |
  | F5 | Save |
  | F9 | Load |

  Keep 8 and 9 free; they're already reserved for drought stage and events.
- **Write `Assets/_Project/Docs/PLAYTEST_Maze.md`:** a checklist I can tick while testing. For each system, the steps and the expected result, including:
  - walking from spawn through a hub gate (4 L + minigame);
  - seeing zone 2 turn on the night of day 2 with key 6;
  - the map showing the turned tiles;
  - a zone 1 exit at t ≈ 0.46;
  - buying W1 then W4 at the hub;
  - Tab memory;
  - F5, quitting, F9.
- **Final report:**
  - every file added or changed;
  - what's placeholder and waiting on art;
  - known limits;
  - what our developer should review first.

## Things not to do

- Don't add hydration drain, drinking, collapse, water sources, purifiers, evaporation, events, drought stages, rooms' loot, or survivor notes. Those come later.
- Don't change the CSV files. If one looks wrong, tell me the tile and what you found.
- Don't put text or numbers players see anywhere other than the UI scripts and GameBalance, so the team can find and edit them.
