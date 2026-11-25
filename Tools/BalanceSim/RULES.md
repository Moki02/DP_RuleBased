# Salok Balance Sim: Rules Sheet (Step 1, for designer review)

## Designer decisions (2026-11-25)
1. **Fixed maze only.** The sim uses the real `Maze2Layer_0deg.csv` (+ its zone 2 rotations) for every run.
   No generated mazes. Run-to-run spread comes from events, bot mistakes and minigame rolls,
   so the "maze luck" IQR is really a "run luck" IQR on this one map.
2. **Each bot has its own exit-valve hit rate** (config `bots.<name>.exit_valve_hit_rate`),
   separate from its normal hit rate. Defaults until playtest data: Beginner 35%, Normal 50%,
   Skilled 70% per press. At 50% a Normal bot wins about 34% of exit-valve tries.
   Other valves and doors still use the ring-based timing model.
3. V2/V6 as fewer wrong turns: OK.
4. Tutorial off and 0 s in the Build menu: OK.

**Sanity reference (designer playthrough):** 3 days played, reached zone 2 (the 2nd layer)
on day 3. The designer knows the map, so a Normal bot should reach zone 2 around day 3 to 5.

Every rule and number the sim will use, and where each one comes from in the game.
Paths are relative to `Assets/`. `GB` = `Scripts/Data/GameBalance.cs`, `GB.x` = the partial
file `Scripts/Data/GameBalance.x.cs`. The live values are the `Scripts/Data/GameBalance.asset`
values. That asset matches the code defaults for every field it stores. Newer fields (the
partials: crouch/jump, lockdown drain, tile reward, rooms, endings) are **not stored in the
asset yet**, so the game uses the code defaults for them. Those defaults are what's listed here.

**ASSUMPTION** = not in the code. I had to pick something. Please confirm or fix.
**NOT IN GAME** = the data exists, but no game code uses it. The sim leaves it out too.

> Old sim: `dry_labyrinth_sim.py` isn't in this repo, its git history, or under your user
> folder. If you have it, send me the path and I'll reuse what still applies.
> Otherwise the sim starts fresh.

---

## 0. Time

| Rule | Value | Source |
|---|---|---|
| Daylight | 300 s real | GB:56 `dayLengthSeconds` |
| Night lockdown | 60 s real | GB:60 `nightLockdownSeconds` |
| Whole day | 360 s = **6 real minutes** | `Clock/GameClockLogic.cs` |
| Day counter | Starts at 1. Night fires at t=300, dawn (day+1) at t=360 | GameClockLogic.Advance |
| Clock pauses | While the Build & Skills screen is open (and the world freezes, timeScale 0) | GameClock_Manager.cs:132-148 |
| Real minutes conversion | real_min = (day−1)·6 + time_of_day/60 (+ build-menu time, which the sim sets to 0) | derived |

ASSUMPTION: the bots take 0 s in the Build menu. A real player probably spends 30 to 90 s there,
but the clock is paused, so this only changes real minutes, not days. Config knob: `menu_seconds_per_visit`, default 0.
ASSUMPTION: the guided tutorial (TutorialDirector) is skipped. The clock still runs during
it. The only rule change the tutorial makes is an easier, free-to-fail first hub valve. I'll add
an optional "tutorial tax" (first hub valve auto-won + N seconds lost on day 1). Default off.

## 1. Hydration (HYD)

| Rule | Value | Source |
|---|---|---|
| Max HYD / start HYD | 100 / 100 | GB:83 |
| Drain idle / walk / sprint / pump / minigame | 0.25 / 0.5 / 1.2 / 0.6 / 0.25 per s | GB:86-90 |
| Crouch-walk / crouch-idle | 0.35 / 0.2 per s | GB.Movement:29,32 |
| Jump | 3 HYD per jump × same multipliers; not allowed when Dehydrated | GB.Movement:39; PlayerHydration_Manager.cs:298-332 |
| Formula | base × skills(E1 0.9 all, E2 0.85 moving) × stage heat × event × Sick 1.5 × hub shade 0.5 (× Thirsty-sprint 1.5) | PlayerHydration_Manager.cs:231-285 |
| Hub shade | ×0.5 inside the hub base box | GB:93 |
| Thirsty / Dehydrated | HYD < 60 / < 30. Dehydrated: no sprint, walk ×0.75 | GB:96-98, PlayerHydration_Manager.cs:125-126, GB:72 |
| Drink (Q) | 0.5 L/s from canteen, 25 HYD/L (30 with E3). M4 makes it ×1.5 | GB:102-103, PlayerHydration_Manager.cs:130-132 |
| Tap: refill canteen | 1 L/s from Clean Storage | GB:104 |
| Tap: drink | 0.5 L/s straight from Clean Storage | PlayerHydration_Manager.cs:420 |
| Raw water drink | Same HYD, Sick 30 s (15 with E4), drain ×1.5 | GB:107-108 |
| Night lockdown drain | **+4 HYD/s** extra, outside the hub and every room | GB.Survival:25; Gates/NightLockdown.cs Update |

Crouch and jump in the sim: the bots don't jump (the maze is flat). Crouch is cheaper than idle
or walk but half speed (2 m/s), so a bot only crouches while **waiting**. ASSUMPTION: Normal and
Beginner never crouch-wait. Skilled can learn to (it's a gene). So the crouch/jump drain knobs
mostly show up in the Skilled runs.

## 2. Water containers and start values

| Container | Start | Tiers (cap) | Source |
|---|---|---|---|
| Canteen | 2 L full | 2 → S1 4 → S4 6 → S7 Jerry Can 10 | GB:113, GB:173-179 |
| Clean Storage | 10 L | 25 Tapayan → S2 50 → S3 150 → S6 300 | GB:112, GB:182-188 |
| Raw Basin | 0 L | 15 → S5 40 → S8 100 | GB:114, GB:190-195 |
| Unbanked water | 0 | (carried in your head, no cap) | SurvivalDirector.cs |
| Spare jugs | 0 | overflow: no cap, pour into Clean at 1 L/s when there's room | GB.Survival:37, GameResources_Manager.cs:120-129 |

- Jerry Can: walk ×0.9 while holding more than 6 L. GB:75-76; effects CSV S7.
- **Banking**: walking into the hub banks all unbanked water. Order: Clean first, then Raw, then
  spare jugs. +10% **Safe Return** bonus if it's daytime. SurvivalDirector.Bank; GB.Survival:34.
- **Buying** costs Clean Storage only. You can't buy an item that costs more than Clean Storage's
  cap. Buying is hub-only. No refunds. Upgrades/UpgradeLogic.cs:136-179.
- Evaporation at each dawn uses the ending stage's rate (2% / 3% / 5% / 8%).
  Raw ×1, spare jugs ×1, Clean × cover (1 / 0.5 / 0.5 / 0.1). A heatwave doubles that night's rate.
  Canteen and lock basins never evaporate. Drought/Evaporation.cs:79-91.

## 3. Hub water production

| Source | Rule | Source |
|---|---|---|
| Hand pump | 0.3 L per stroke, 1 stroke / 0.5 s (0.35 with W1) = 0.6 L/s → Raw Basin. Stops when the Raw Basin is full. Drain 0.6 ×0.5 shade | GB:118-120, HubPump_Manager.cs |
| Pump depth factor | Water table by stage: 4 / 5.5 / 6.5 / 7.5 m. Normal pump: 1.0 / 0.8 / 0.5 / **0** (past the 7 m suction limit). W4 Deep-Set: 1 / 1 / 0.8 / 0.6 | GB:123-143, GB:199-209 |
| Boiling pot | Raw → Clean, 0.167 L/s, automatic, runs day and night | GB:159, HubPurifier_Manager.cs |
| Biosand Filter (P1) | Replaces the pot: 0.4 L/s × ripeness (0.5 on buy day, 0.75 next day, then 1) | GB:160-163, Sources/BiosandFilter.cs |
| Rain Catcher (W2/W3/W8) | Only while raining: area 4 / 8 / 16 m² × 0.1 × 0.9 → Raw. **Spills** when Raw is full | GB:146-149, Sources/WaterSources.cs |
| Solar Still (W5, W6 = ×2) | 0.02 L/s × sun (1 / 1.2 / 1.4 / 1.6) × (2 in Heatwave) → Clean directly. Off while raining | GB:152, GB:201-208 |
| Deep Well (W7) | 0.08 L/s × deep-well factor (1 / .95 / .85 / .75) → Raw, always on | GB:155 |
| Water Table Drop event | NOT IN GAME (its weight goes to Calm) | Events/EventManager.cs:13-17 |
| SODIS (P2), Chlorine (P3) | Can be bought, but **NOT IN GAME**: no code makes the water | Upgrades/UpgradeManager.cs:30, UI/StatusIconsUI.cs:5 |

Hub sources keep running while the player is in the maze. Their real limit is the 0.167 L/s pot.
That's about 60 L of Clean water per 6-minute day, but only if the Raw Basin is kept fed.

## 4. Drought stages

| Stage | Start day | Heat × drain | Evap/day | Water table | Storm / Heatwave / Calm* |
|---|---|---|---|---|---|
| 1 El Niño Advisory | 1 | 1.0 | 2% | 4 m | 50 / 10 / 40 |
| 2 Dry Spell | 3 | 1.1 | 3% | 5.5 m | 35 / 20 / 45 |
| 3 Drought | 5 | 1.25 | 5% | 6.5 m | 15 / 25 / 60 |
| 4 State of Calamity | 12 | 1.4 | 8% | 7.5 m (pump dead without W4) | 5 / 30 / 65 |

*Calm includes the weights of the five events that aren't built (EventManager.cs:248-273).
Source: GB:199-209, Drought/StageManager.cs.

## 5. Events (one per day)

| Rule | Value | Source |
|---|---|---|
| Start time | Uniform 40 to 260 s into the day | GB:341-342 |
| Thunderstorm | 20 s warning, then 60 s rain: drain ×0.5, canteen +0.05 L/s anywhere except inside rooms, Rain Catcher on, Solar Still off | GB:345-348; EventManager.cs |
| Heatwave | From its start until dawn: drain ×1.3 (×1.6 in stage 4), Solar Still ×2, that night's evaporation ×2 | GB:351-354 |
| Calm | Nothing | |

## 6. Maze layout (the real one)

Source: `_Project/Data/Maze/Maze2Layer_0deg.csv` (95×95, 4 m tiles), `Maps/2layer maze reference - How to use.csv`.
Ring = max(|x−47|, |y−47|).

| Part | Rings | Contents |
|---|---|---|
| Hub base | 0-6 | 13×13 spawn area (S). Pump, pot, tap, storage, Build menu. Shade ×0.5. Shelter at night |
| Hub wall | 7-9 | 4 hub gates (N/E/S/W), 3 tiles thick. **Lock 4 L** |
| Zone 1 | 10-24 | 1-tile corridors on an odd grid with some loops. **3 rooms** (5×5). 3 V valve grates. Never turns |
| Zone 1 wall | 25-27 | **4 exits, Lock 10 L** (not at the compass points. Inner tiles (59,22), (22,35), (72,59), (35,72)). Hub gates at (47,40), (40,47), (54,47), (47,54). Outer exits at x or y = 27 / 67 on each side |
| Zone 2 | 28-44 | Same corridor style. **4 rooms**. 5 V valves. **Turns 90° clockwise at the start of the night on even days** (2, 4, 6…) | GB:232-236, Maze/Zone2TurnMath.cs:26-30 |
| Outer wall | 45-47 | **8 exits** (2 per side), **Lock 18 L + exit valve**. Getting through one and walking out = WIN |

Tile counts: floor 3632, room 175 (7×25), wall 4993, 16 gates (48 G tiles), 8 V valves.
The reference sheet says every zone 1 exit reaches every zone 2 exit in all 4 rotations, even with the valves closed.

**Walking speed**: 4 m/s walk = **1 tile/s**. Sprint 6.5 m/s = 1.625 tiles/s. M1 4.4, M2 7.3.
Dehydrated ×0.75. GB:70-72.

### Per-seed maze generator: DROPPED (designer decision, fixed maze only)
The section below is kept only for the record. The sim always uses the real CSV.
The game has **one fixed maze** (CSV), and the room roll uses a fixed seed. "A different maze per
seed" isn't how the shipped game works, so here's the plan:
- **seed 0 = the real CSV** (plus its 3 other rotations), used for the day 1-3 sanity check and the final numbers.
- **seed ≥ 1 = generated**: same ring bands, same hub, same wall thicknesses, same 4 + 4 + 8 gate
  positions. Corridors are made as a perfect maze on the odd grid inside each band, then
  loops are added at the CSV's measured loop density. 3 rooms (5×5) in zone 1 and 4 in zone 2 go in
  random spots. 3 + 5 valve grates go on chokepoints. Then a check that every exit is reachable.
- Maze-luck IQR is reported for generated seeds. The real CSV is reported on its own.

## 7. Gates, valves, doors (the Valve Timing minigame)

| Thing | Rule | Source |
|---|---|---|
| Lock basin | Hold E: pour 1 L/s (×1.5 M4) from the canteen. When the canteen is empty, pour from HYD (25 HYD/L) down to 10 HYD. Water stays in the basin between visits | GB:277-283, LockBasin_Manager.cs:220-280 |
| Then | Full → valve minigame. Win = open for good. Lose = −5 HYD, 10 s lockout, water stays. Esc = no cost | Gates/GateLockLogic.cs; GB:288-290 |
| Night | Every gate is shut at night, even opened ones. They reopen at dawn | NightLockdown.cs; GateLockLogic.NightLocked |
| V valve grate | No basin. Minigame only. Lose −5 HYD + lockout. First win pays **+2 L (zone 1) / +2 L (zone 2) unbanked** | GB:269, Minigames/ValveGate.cs:164-171; GateManager.MakeValve |
| Room door (40 doors, 5-7 per room) | Minigame once to unlock. **Lose = no cost, retry at once.** At night an unlocked door opens with E (no minigame). A locked door still needs the minigame at night | Maze/RoomDoor.cs:1-15 |
| Difficulty by ring | t = clamp01((ring−9)/35); band = 30−15t°; speed = 120+80t °/s; 3 hits win, 3 misses lose | GB:294-308, Gates/ValveDifficulty.cs |
| Speed-up | Needle ×1.25 after the 1st and 2nd hit (120 → 150 → 187.5) | GB:311-313 |
| Exit valve (8 outer exits) | 12° band, 220 °/s, **4 hits** win, 3 misses lose (speed-up applies: 220 → 275 → 344) | GB:315-323 |
| Dial | Needle ping-pongs over −90..+90°. Band moves ≥40° after a hit | ValveTimingGame.cs:60, 330-341 |
| Time per press | ASSUMPTION: about 1.2 s between presses, plus a 0.6 s end delay | ValveTimingGame endDelay=0.6 |

Worked difficulties: hub gate ring 7 → t=0 (30°, 120). Zone 1 exit ring 25 → t=0.46 (23°, 157).
Zone 2 valves and doors at ring about 30-40 → t≈0.6-0.9 (21-17°, 168-191). Exit valve: 12°, 220.

**Hit model (ASSUMPTION, important)**: each press has a Gaussian timing error σ_t (seconds).
P(hit) = erf((band/2) / (speed · σ_t · √2)). Each bot's config "hit rate" is its rate **at the
easiest valve (30°, 120 °/s)**, and σ_t is solved from that. Then:

P(win) counts the 1.25× speed-ups (worked out exactly, not sampled):

| Bot (rate at hub valve) | σ_t | P(win) hub gate | P(win) zone 1 exit (23°,157) | P(win) zone 2 door/valve (t≈.8) | Exit valve 1st press | **P(win) exit valve (12°,220,4 hits)** |
|---|---|---|---|---|---|---|
| Beginner 50% | 0.185 s | 33% | 10% | 3.6% | 12% | **0.1%** |
| Normal 70% | 0.121 s | 66% | 27% | 11% | 18% | **0.4%** (~250 tries) |
| Skilled 90% | 0.076 s | 94% | 60% | 30% | 28% | **2.1%** (~50 tries) |

If this model is right, **the exit valve is a hard wall that nothing else in the game comes close
to**. Each try costs −5 HYD and a 10 s lockout, so a Normal bot would almost never escape.
Zone 2 doors (free retries) and valves are also tough. Please tell me your real hit rate on the
exit valve from playtests. I can instead let each bot use a separate "hit rate at exit valve" setting.

## 8. Rooms

| Rule | Value | Source |
|---|---|---|
| Shelter | Standing in a room tile = no lockdown drain | NightLockdown.IsSheltered |
| d | 0 for the room nearest the hub, 1 for the farthest, by ring | Maze/RoomRules.cs:71 |
| Roll | Empty 20%. Cistern 16% (d=0) to 64% (d=1). Otherwise cache. Fixed seed 8 | GB.MazeContent:32-42; RoomRules.Roll |
| Cistern | 2 + 6d L. Refills at dawn × (1, .85, .7, .5) by stage | GB.MazeContent:45-51 |
| Cache | 3 + 7d L, once | GB.MazeContent:54-57 |
| Taking water | E: 1 L/s (×1.5 M4) **into the canteen** (only what fits). Not unbanked | GB.MazeContent:60; RoomContent.cs:15 |
| Note | 1 per room. Payload Reveal / Marker / Clue. No water | Maze/RoomNotes.cs |

Real map with seed 8: 0 Z2-N (ring 38, d=1) cistern 8 L · 1 Z2-NW (32, .75) cistern 6.5 L ·
2 Z1-NE (14, 0) cache 3 L · 3 Z2-E (34, .83) cistern 7 L · 4 Z1-NW (14, 0) cache 3 L ·
5 Z1-W (20, .25) cistern 3.5 L · 6 Z2-SW (38, 1) empty.
On generated mazes the sim rolls rooms per seed with the same odds.

## 9. Maze rewards (all go to unbanked water)

| Reward | Value | Source |
|---|---|---|
| New floor/room tile seen | 0.03 L each | GB.Survival:31 |
| Map reveal | Entering a tile reveals it + 4 neighbours (8 with V3). Plus 2 tiles ahead in the facing direction (4 with V1), stopping at walls | Map/MapReveal.cs R8.1a-d |
| Section mapped (75%, V5 60%) | Zone 1: 6 slices, 2 L each. Zone 2: 8 slices, 3 L each. ×1.5 with V4 | GB:262, GB.MazeContent:24-27 |
| Map milestones | 25 / 50 / 75 / 100% of the whole maze → +10 / 15 / 20 / 30 L | GB:265-266 |
| Valve grate first win | +2 L | GB:269 |

ASSUMPTION: "facing direction" = the direction the bot is walking.

## 10. Collapse and game over

At 0 HYD (CollapseManager.cs):
1. Lose the whole canteen (kept with E5). Lose 50% of unbanked water (rounded down to 0.1).
2. The rest of unbanked water is banked (no bonus).
3. **GAME OVER if Clean Storage < 2 L** (after that banking).
4. Otherwise: wake in the hub, drink 2 L from Clean Storage, HYD = 50 (60 with E3). Clock +30 s.
Source: GB:65, 271, 380-382.
- No day limit and no other way to lose. The sim stops at **day 40** = "still going".
- Game-over cause is tagged by where/when the final collapse happened: `night_lockdown`
  (caught outside at night), `day_maze_zone1/zone2`, `valve_spiral` (HYD lost to valve fails), `hub`.

## 11. Build and Skills (prices and effects)

Source: `_Project/Data/BuildAndSkills/BuildAndSkills.csv` + `_Effects.csv`. Cost × `skillCostMultiplier` (1), rounded up.

| Id | Name | L | Needs | Effect in sim |
|---|---|---|---|---|
| W1 | Greased Handle | 8 | | stroke 0.35 s |
| W2 | Rain Catcher | 15 | | 4 m² |
| W3 | Bigger Roof | 20 | W2 | 8 m² |
| W8 | Wide Roof | 60 | W3 | 16 m² |
| W4 | Deep-Set Cylinder | 20 | W1 | deep pump table |
| W5 | Solar Still | 30 | W2 | still |
| W6 | Second Still | 50 | W5 | ×2 |
| W7 | Deep Well | 120 | W4 or S3 | well |
| S1 / S4 / S7 | Jug / Pack / Jerry Can | 10 / 30 / 70 | chain | canteen 4 / 6 / 10 |
| S2 / S3 / S6 | Drum / Ferrocement / Cistern | 15 / 35 / 150 | chain | Clean 50 / 150 / 300, cover .5/.5/.1 |
| S5 / S8 | Bigger / Concrete Basin | 10 / 45 | chain | Raw 40 / 100 |
| P1 | Biosand Filter | 20 | | 0.4 L/s ripening |
| P2 | SODIS | 25 | | NOT IN GAME (no effect) |
| P3 | Chlorine | 35 | P1 | NOT IN GAME (no effect) |
| E1 | Salakot | 12 | | drain ×0.9 |
| E2 | Steady Breath | 20 | E1 | moving ×0.85 |
| E3 | Water Discipline | 25 | E2 | 30 HYD/L, wake 60 |
| E4 | Iron Stomach | 30 | E1 | Sick 15 s |
| E5 | Second Wind | 50 | E3 | keep canteen |
| M1 | Light Sandals | 15 | | walk 4.4 |
| M2 | Long Stride | 35 | M1 | sprint 7.3 |
| M3 | Pacing | 30 | M1 | no thirsty-sprint ×1.5 |
| M4 | Quick Hands | 20 | M1 | drink/pour ×1.5 |
| M5 | Marathoner | 50 | M2 or M3 | no dehydrated slowdown |
| V1 | Trail Sense | 15 | | look-ahead 4 |
| V2 | Landmarks | 15 | V1 | markers (no sim effect) |
| V3 | Keen Eyes | 30 | V1 | 8-neighbour reveal |
| V4 | Surveyor | 25 | V1 | section reward ×1.5 |
| V5 | Cartographer | 40 | V3 or V4 | section at 60% |
| V6 | Sun Reading | 20 | | compass. ASSUMPTION: cuts the bots' wrong-turn chance by 25% |

Note: V2 (markers) and V6 (compass) only change what the player *knows*. The sim models them as a
small cut in navigation mistakes, so they don't look useless by default. Tell me if you'd rather they had no effect.

## 12. Endings

Win day ≤15 "Water Keeper", 16-22 "Survivor", ≥23 "Barely Made It". GB.Menus:20-30.

## 13. Bot behaviour (ASSUMPTIONS, these are the sim's own rules)

- **Knowledge**: bots know only tiles they've revealed (the same rules as the map). They route with
  BFS over known tiles and explore the frontier. When zone 2 turns, their known zone 2 tiles turn
  too (the map does this, R8.28).
- **Loop**: plan a trip (target = the next gate/basin, a room, or the frontier) → walk → pour /
  minigame / take water → go home when (estimated HYD and water to get home) < a threshold, or
  before lockdown (a home margin in seconds). In the hub: bank, refill canteen, drink at the tap,
  pump until the Raw Basin is full or until leaving time, then buy.
- **Night**: get to the hub or a known room before the lockdown. A bot caught out tries the nearest
  shelter (it can unlock a door with a minigame while draining 4 HYD/s).
- **Beginner**: 15% chance of a wrong turn at junctions. Goes home late (thresholds 30% lower).
  50% hit rate. Buys a random affordable item.
- **Normal**: 5% wrong turns. Sensible thresholds. 70% hit rate. Fixed sensible buy order
  (S1, E1, S2, P1, M1, W1 …). Small noise on decisions.
- **Skilled**: genes = buy order, home threshold, lockdown margin, exploration range, pump
  target, crouch-wait on/off, when to spend HYD-pouring. Evolved with (μ+λ) search on mean escape day.
  90% hit rate.
- Hub actions take their real time (pump at 0.6 L/s, tap at 1 L/s, pour at 1 L/s).
- Added while building the sim (all in `config/bots.json`):
  - **Pump target per visit** (`pump_target_l`): Beginner 8, Normal 15, Skilled evolved. At night the bot fills the Raw Basin anyway. The first version filled the whole basin before every trip, which with a 100 L basin left no time for trips.
  - **Tap reserve** (`tap_keep_l`): the bot won't drink Clean Storage below this.
  - **Pump warning** (`react_pump`): Normal and Skilled buy W1 then W4 two days before the pump dies (the radio says so). Without it, Normal bots died of thirst in the hub from day 12.
  - The hub (base + hub gate basins) is known at start. Everything else must be seen.
  - Bots explore through room doors (unlocking them), since some corridors only connect through rooms.
  - Night: home if it can still be reached in time, otherwise the nearest known room.

## 14. Things I can't model exactly (and how I'll approximate them)

| Game thing | Sim approximation |
|---|---|
| 3D movement, corners, collisions | 1 tile per 4 m of travel, grid steps. No corner cutting |
| Sprinting | Bots walk. Skilled has a gene to sprint on known routes when Hydrated |
| Valve timing skill | Gaussian timing-error model (section 7) |
| Player reading the map / markers / compass | Wrong-turn probability per bot (V2/V6 lower it) |
| Notes Reveal/Marker payloads | Reveal: target room's tiles count as known. Marker: bot learns where that cistern is |
| Tutorial | Off by default (optional day 1 time tax) |
| Build menu time | 0 s (clock paused anyway) |
| Storm canteen refill "in the open" | Anywhere except inside a room |
| Real maze vs generated mazes | Seed 0 = real CSV. Others generated in the same layout (section 6) |
