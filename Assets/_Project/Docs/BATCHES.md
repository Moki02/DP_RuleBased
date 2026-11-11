# Salok: the batch branches and how they fit together

Written 2026-11-11 for the push to get every must-have mechanic in. Each batch lives on its
own branch. **Batch 1 (`batch1-survival-core`) goes first; every other batch branches from it**
and only plugs into the hooks below, so the branches can be merged in any order with few or
no conflicts.

## Merge order

**Use `integration-all`.** It is all six batches merged, plus the code review fixes and comments
(2026-11-11 evening). Merge only `integration-all` into `main`; the six batch branches are kept
for history and don't contain the review fixes.

After merging, open Unity, wait for it to compile, then run **Tools > The Last Drop > Build
Playtest Scene** once (it places the hub's pump, boiling pot and Clean Storage tap, and links
the demo balance) and press Play. Read `REVIEW_NOTES.md` for what to check first.

## Rules every batch follows

- **No scene edits.** New systems add themselves to the game scene at Play:
  ```csharp
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Register() => AutoSystems.Register<MySystem>();
  ```
  (Only scenes with a BalanceProvider get them, so the old Zone1 scenes are untouched.)
- **New numbers go in a new partial file**: `Assets/Scripts/Data/GameBalance.<Batch>.cs`
  (`public partial class GameBalance { ... }`). Don't edit `GameBalance.cs` or the `.asset` files;
  new fields take their code defaults.
- **New save fields go in a new partial file**: `Assets/Scripts/Save/SaveData.<Batch>.cs`, and the
  system implements `ISaveable`.
- **UI is plain TextMeshPro, no art.** Use `SimpleHud`:
  `SetLine(key, text)`, `Toast(text)`, `Banner(title, line)`, `ShowPanel(title, body, buttons...)`,
  `SetFade(alpha)`. Or `UiBuilder` for anything bigger.
- **Debug keys**: `DebugKeys.Register(Key.Digit8, "label", action)` (no edits to DebugKeys.cs).
- **HYD drain**: `HydrationModifiers.Add("key", ctx => multiplier)` in Start, `Remove("key")` in
  OnDestroy. Multiply, never add (R2.1).
- **Shared world facts** (`WorldState`): `StageIndex`, `Stage`, `IsFinalStage`, `IsRaining`,
  `HeatwaveActive`, `EvaporationMultiplierTonight`, `WaterTableExtraMeters`. The owner sets them
  (in Start or later, never Awake); everyone else reads.
- **Water**: always go through `GameResources_Manager.instance` (`AddRawWater`, `AddCleanWater`,
  `AddUnbankedWater`, `TakeCanteenWater`, `cleanStorage`, `rawBasin`, `spareJugs` ...). Count
  water entering the game with `RunStats.AddCollected(litres)`.
- **Skills**: `Skills.F("field", default)`, `Skills.B("field", default)`, `Skills.Owns("W2")`.
- **Static hooks** (`HubPump_Manager.OutputFactor`, `OutputReason`, `HubPurifier_Manager.RateOverride`):
  set in Start, set back to null in OnDestroy.
- Comments follow the team rule: header block, `///` summaries, `[Tooltip]` on serialized fields,
  rule numbers (R4.1 ...).

## Who owns what

| Batch | Branch | Owns | Hooks it uses |
|---|---|---|---|
| 1 | `batch1-survival-core` | Water ownership, hydration (GameBalance + skills E1-E5, M3-M5), hold-Q drinking, collapse + game over, night drain + warnings, minigame -5 HYD and +2 L, unbanked water (0.03 L/tile, banking, Safe Return, spare jugs), canteen/basin upgrades, hub stations in the scene builder, SimpleHud, AutoSystems, WorldState | - |
| 2 | `batch2-drought` | Stage manager (heat ×, radio text, day 5 line, debug key 8), hand pump by water table (+W4), evaporation each day | `HydrationModifiers`, `HubPump_Manager.OutputFactor`, `WorldState` |
| 3 | `batch3-events` | Daily event roll; Calm, Thunderstorm, Heatwave; scripted Demo events; debug key 9 | `HydrationModifiers`, `WorldState.IsRaining / HeatwaveActive / EvaporationMultiplierTonight` |
| 4 | `batch4-sources` | Rain Catcher, Solar Still (+W6), Deep Well, Biosand Filter (ripening), source labels | `WorldState`, `HubPurifier_Manager.RateOverride` |
| 5 | `batch5-maze-content` | Sections + rewards (+V4, V5), map % milestones, rooms (cache / cistern / empty), survivor notes, journal, room icons on the map | `MapReveal.TileSeen`, `SimpleHud` |
| 6 | `batch6-menus` | Main menu (New Game, Continue, Demo, Quit), win screen with stats and ending title, "Demo complete", Demo mode rules | `CollapseManager.ShowEndScreen`, `RunStats.Summary` |

Nice to have (not in any batch): Water Table Drop, Cloud Seeding, Rationing, Relief Tanker,
Grass Fire, SODIS, Chlorine Tablets, zone 3, Pipe Connect, Leak Patch.
