# Game feel hook-up list

> **WIRED (2026-11-24, branch `game-feel`):** everything below is now connected by the Feel scripts
> (`GameFeelDirector`, `PlayerFeel`, `AmbienceDirector`, `UiButtonSounds`, `SceneFeel`), using the
> `Assets/VFX/GameFeelLibrary.asset` (swap a sound or change a volume there). Install / re-install:
> **Salok > Feel > Install Game Feel**. See "How it was wired" at the end for the hooks that were
> added to gameplay scripts.

> **Final sound picks (2026-11-24):** every moment below names its final file in `Assets/Audio`
> (details, sources and licences in `Assets/Audio/CREDITS.md`). VFX are all approved. Screen shake:
> the team chose the **big** shake (`shakeBig`) for every shake moment. Floating numbers are half
> their first size. Music (BGM) is handled by another team member.

For the team to wire up later. **Nothing here is wired into the game yet** (George's call:
"don't hook anything in the hooking up list"). Sound files are in `Assets/Audio`; listen to any of
them in FeelLab (`Assets/Scenes/FeelLab.unity`, Salok > Feel > Refresh Sound Board (everything)).

How to read the "Trigger" column:
- ✅ **exists**: the event or method is already there; just subscribe / add to the UnityEvent.
- ➕ **needs a hook**: the moment happens inside a private method; the smallest change is
  given (usually one `public event Action` line plus one `?.Invoke()` call). That edit belongs
  to the script's owner (mostly Jose's scripts in `Game Scripts/`).

Feel components (all in `Assets/Scripts/Feel`, tuning in `Assets/VFX/Resources/FeelSettings.asset`):
`FloatingText.Show / ShowAmount`, `UiPunch.Punch`, `NumberRoll.SetValue`, `CameraShake.Add`,
`LowHydVignette.SetHydration`, `CollapseFade.Play`, `HitFeedback.Hit / Miss`, `HitStop.Play`,
`FeelPool.Spawn(effectPrefab, position, rotation)` for every VFX prefab in `Assets/VFX/Effects`.

---

## One-time setup (no gameplay script edits)

| What | How |
|---|---|
| Screen shake | Add `CameraShake` to the **Camera Object** (the child with the Camera component under Player_Camera's pivot) in the player prefab / scene. `Player_Camera` needs **no** code change: the shake is added just before the frame is drawn and removed right after. |
| Shake on/off in Settings | Add a Toggle to the Settings screen prefab; on change: `CameraShake.Enabled = isOn` (saved in PlayerPrefs). Show it with `toggle.isOn = CameraShake.Enabled`. |
| Low-HYD vignette | Drop `Assets/VFX/Resources/Feel/LowHydVignette.prefab` into the game scene. Each frame something calls `vignette.SetHydration(stats.player_Hydration, stats.player_MaxHydration)` (from `PlayerStats_Manager`). Could be a tiny Feel-side script that reads the stats, so no developer script changes. |
| Collapse fade | Drop `CollapseFade.prefab` into the scene (or keep using `SimpleHud.SetFade`; see Collapse below). |
| Audio mixer volume | Every AudioSource that plays these sounds gets `SettingsVolume` (Kind = Sfx, or Music for ambience) so the Settings sliders work. |

## Player

| Moment | Sound (final pick) | Effect / feel | Trigger |
|---|---|---|---|
| Footstep on sand | `sfx_player_footstep_sand_02` (one long recording; loop while walking on sand, or cut into steps) | `VFX_FootstepDust` at the foot | ➕ No footstep event. Best: **Animation Events** on the walk/run clips calling a small `Footstep()` relay on the player. No-code-edit option: a Feel-side script that plays a step every X metres the player's CharacterController moves. |
| Footstep on stone (maze) | `sfx_player_footstep_stone_07` (one step; vary pitch ±5% per step) | none | Same as above; pick sand or stone by what's under the player (raycast down, check the ground layer/tag). |
| Sprint breath | `sfx_player_sprintbreath_03` (20 s; loop while sprinting) | none | ➕ `PlayerLocomotion_Manager.HandleGroundedMovement` computes `isSprinting` locally. Smallest change: expose `public bool IsSprinting { get; private set; }`, then a loop fades in while true. |
| Drinking gulp | `sfx_player_drink_01` | `FloatingText.ShowAmount(+hyd, " HYD", head)` | ✅ `PlayerHydration_Manager.IsDrinking` (poll: play gulps while true). ➕ Cleaner: `public event Action Drank` invoked in `HandleDrinkAction` / `DrinkFromCanteen`. |
| Canteen fill (at tap) | `sfx_player_canteenfill_06` | `VFX_PourStream` at the tap | ✅ `HubTap_Manager.Refill(deltaTime)` is called every frame while filling. ➕ Add `public bool IsRefilling` (true while Refill ran this frame) to start/stop the loop. |
| Collapse | `sfx_player_collapse_06` | `CameraShake.Add(shakeBig)` + `CollapseFade.Play(...)` | ✅ `PlayerHydration_Manager.OnCollapsed` (event). `CollapseManager.CollapseRoutine` already fades with `SimpleHud.SetFade`; swapping it for `CollapseFade` is optional. |
| Low HYD heartbeat / breathing | `sfx_player_heartbeat_03` (loop) + `sfx_player_lowhyd_breath_02` | `LowHydVignette` | ✅ Read `PlayerStats_Manager.player_Hydration / player_MaxHydration`; start the loop below the vignette's start point (`FeelSettings.vignetteStartsAt`, default 30%). Volume = `vignette.Strength`. |
| Cough when sick | `sfx_player_cough_03` | `VFX_SickFlies` parented to the player while sick | ✅ `PlayerHydration_Manager.SickSecondsLeft > 0` (poll). Cough at random every 4 to 8 s while sick. |
| HYD lost (e.g. drinking dirty water, pouring HYD) | none | `FloatingText.ShowAmount(-x, " HYD", head)` | ✅ `LockBasin_Manager` pours from HYD (`TakeFromHyd`, private). ➕ `public event Action<float> PouredHyd`. |

## Hub water

| Moment | Sound (final pick) | Effect / feel | Trigger |
|---|---|---|---|
| Pump stroke / handle creak | `sfx_hub_pumploop_01` (loop while pumping; 6 strokes, 5.5 s) | `VFX_WaterSplash` at the spout; `UiPunch` on the Raw Basin bar | ➕ `HubPump_Manager.PumpStroke()` is private. Add `public event Action Stroked;` and `Stroked?.Invoke();` at its end. |
| Water gush into the Raw Basin | `sfx_hub_gush_05` | `VFX_PourStream` at the spout during the stroke | Same `Stroked` event. |
| Basin full | none | `FloatingText.Show("FULL", basin, Neutral)` | ✅ `GameResources_Manager.OnWaterFull` (event). `HubPump_Manager.ShowFull` is the moment it's shown. |
| Boiling bubbles (loop) | `sfx_hub_boil_04` (loop) | `VFX_Steam` above the pot (loop) | ➕ `HubPurifier_Manager.PurifyWater` runs each frame. Add `public bool IsPurifying { get; private set; }`; loop sound + steam on while true. |
| Pouring into Clean Storage | `sfx_hub_pour_02` (loop) | `VFX_PourStream`; `NumberRoll` on the Clean Storage counter | Same `IsPurifying` (that's when clean water is added). Counter: call `NumberRoll.SetValue(clean)` wherever the HUD writes the litres. |
| Tap running | `sfx_hub_tap_06` (valve twist when the tap opens), then `sfx_hub_pour_02` (loop) while it runs | `VFX_PourStream` at the tap | ✅/➕ `HubTap_Manager.Drink / Refill` (per frame). Add `IsRunning`. |
| Water banked ("+L") | `sfx_ui_waterbanked_02` | `VFX_WaterSparkle` at the hub + `FloatingText.ShowAmount(+L, " L", hub)` + `UiPunch` on the counter | ✅ `SurvivalDirector.Bank(...)` and `SurvivalDirector.Reward(litres, why)` (public). ➕ Cleanest: `public static event Action<float, string> Rewarded` in `Reward`. |

## Gates, doors and the minigame

| Moment | Sound (final pick) | Effect / feel | Trigger |
|---|---|---|---|
| Pouring into a lock basin (loop) | `sfx_hub_pour_02` (loop; shared with Clean Storage) | `VFX_PourStream` into the basin | ➕ `LockBasin_Manager.Update` pours while held. Add `public bool IsPouring`. |
| Lock basin full / gate unlocks | `sfx_gates_success_01` | `VFX_DustPuff` + `CameraShake.Add(shakeBig)` | ✅ `LockBasin_Manager.Opened` (event). |
| Valve wheel turning | `sfx_gates_valve_03` | none | ➕ `ValveGate.StartMinigame` (private) / `ValveTimingGame.Begin` (public). Play on Begin. |
| Minigame needle tick | `sfx_gates_tick_02` (loop) | none | ➕ `ValveTimingGame.Update` moves the needle. Add `public event Action NeedleTick` when it passes the centre / each sweep. |
| Green hit | `sfx_gates_hit_01` | `HitFeedback.Hit()` (flash, hit-stop, `UiHitBurst`) | ✅ `ValveTimingGame.onHit` (UnityEvent): drag `HitFeedback.Hit` in. Set HitFeedback's Flash Target / Burst Parent to the minigame needle. |
| Miss | `sfx_ui_notenough_05` (shared with "not enough water") | `HitFeedback.Miss()` | ✅ `ValveTimingGame.onMiss` (UnityEvent). |
| Minigame success | `sfx_gates_success_01` | `UiPunch` on the panel | ✅ `ValveGate.onOpened` (UnityEvent) / `ValveGate.Opened` (event). |
| Minigame fail | `sfx_gates_fail_03` | `CameraShake.Add(shakeBig)` | ✅ `ValveGate.onFailed` (UnityEvent); also `SurvivalDirector.MinigameLost()`. |
| Heavy metal gate sliding | `sfx_gates_doorrise_01` (swapped on purpose: this file is the gate sound) | `VFX_DustPuff` at the gate base + `CameraShake.Add(shakeBig)` | ✅ `GateSlider.onStartOpening` / `onStartClosing` (UnityEvents). |
| Room door rising | `sfx_gates_gateslide_01` (swapped on purpose: this file is the door sound) | `VFX_DustPuff` + small shake | ✅ `RoomDoor.onOpened` (UnityEvent) / `RoomDoor.onUnlocked`, `RoomDoor.Unlocked` (event). |
| Gate slam at night | `sfx_gates_gateslam_04` (already layered for weight) | `VFX_DustPuff` + `CameraShake.Add(shakeBig)` | ✅ `GateSlider.onStartClosing`, or `GameClock_Manager.NightStarted` (event) for one slam for all gates. |

## Maze

| Moment | Sound (final pick) | Effect / feel | Trigger |
|---|---|---|---|
| Zone 2 turning | `sfx_maze_zone2turnmix_01` (7.5 s, already layered; once per turn) | `VFX_Zone2Dust` around the zone + `CameraShake.Add(shakeBig)` | ✅ `Zone2Rotator.Zone2Turned(oldTurns, newTurns)` (event). |
| Cistern filling | `sfx_maze_cistern_05` (splash + bubbling, 4.5 s) | `VFX_WaterSplash` | ➕ `RoomWaterSource` / `WaterSources.Update` (no event). Add `public event Action Filled` where the cistern fills. |
| Reading a note | `sfx_maze_note_01` | none | ✅ `NotePanel.OpenNote(title, body)` (public): play inside, or ➕ `public event Action Opened`. |
| Map open / close | `sfx_maze_mapopen_02` (open and close) | none | ✅ `MapScreen.Open()` / `MapScreen.Close()` (public). ➕ events `Opened` / `Closed` to avoid editing the methods. |
| Marker placed | `sfx_maze_marker_01` | `UiPunch` on the marker icon | ✅ `MapRevealTracker.MarkerToggled` (event). |
| Section revealed / water reward | `sfx_maze_reveal_04` | `VFX_RewardPop` at the player + `FloatingText.ShowAmount(+L, " L", ...)` | ✅ `MapReveal.TileSeen` (event) is per tile. ➕ For the section: `MapRewards.CheckSection` (private) gives the reward through `SurvivalDirector.Reward`; add `public event Action<int> SectionRewarded`. |

## Night and world

| Moment | Sound (final pick) | Effect / feel | Trigger |
|---|---|---|---|
| Dusk warning (bell / siren) | `sfx_world_duskwarning_08` (one 2 s siren) | `UiPunch` on the clock | ➕ `NightLockdown.UpdateWarnings` shows `SimpleHud.Toast("Lockdown in ...")` at each warning time. Add `public event Action<float> Warning` (seconds left) there. |
| Tension near dusk (ambience) | `sfx_ambience_tension_01` (loop) | none | Same warning window (fade in during the last minute). |
| Lockdown | `sfx_world_lockdown_06` (horn) | `CameraShake.Add(shakeBig)` | ✅ `GameClock_Manager.NightStarted` (event). |
| Dawn | `sfx_world_dawn_06` (birds, loop) | none | ✅ `GameClock_Manager.NewDay` (event). |
| Thunderstorm (loop) + thunder | `sfx_ambience_storm_03` (loop), `sfx_ambience_rain_01` (light rain loop), `sfx_world_thunder_03` (one-shot) | `VFX_Rain` following the camera; random thunder + `CameraShake.Add(shakeBig)` | ➕ `EventManager.StartStorm / StartRain / EndStorm` are private. Add `public event Action<GameEventType> EventStarted, EventEnded`. |
| Heatwave | `sfx_ambience_heatwave_01` (loop) | `VFX_DustBlowing` + `VFX_SandStreaks` following the camera | ➕ `EventManager.StartHeatwave` (private), same events as above. |
| Drought stage change sting | `sfx_stinger_droughtstage_04` | `CameraShake.Add(shakeBig)` | ➕ `StageManager.ApplyStage(index, announce)` (private; `announce` = a real change). Add `public event Action<int> StageChanged` when `announce` is true. |

## UI

| Moment | Sound (final pick) | Effect / feel | Trigger |
|---|---|---|---|
| Click | `sfx_ui_click_03` | none | No code: add an `EventTrigger` or a tiny `UiSound` component on the button prefabs in `Assets/Resources/UI` (UI prefabs belong to the UI owner). |
| Hover | `sfx_ui_hover_03` | none | Same (PointerEnter). |
| Open / close menu | `sfx_ui_menuopen_02` (open and close) | `UiPunch` on the panel | ✅ `GameMenu.Press(MenuKey)` / `SwitchTo(page)` (public). ➕ `public event Action<bool> OpenChanged`. |
| Buy upgrade | `sfx_ui_buy_05` | `UiPunch` on the card + `NumberRoll` on the water counter | ✅ `UpgradeManager.Changed` (event) fires after a purchase (also on load, so check it was a buy). |
| Unlock skill | `sfx_ui_unlock_05` | `UiPunch` on the skill card | ✅ `UpgradeManager.Changed` (same; tell skills from upgrades by the card's kind). |
| Not enough water | `sfx_ui_notenough_05` | `UiPunch` on the price (red flash with `HitFeedback.Miss`) | ➕ Where the buy button refuses (in `GameMenu` / `UpgradeLogic`). Add `public event Action CantAfford`. |
| Win sting | `sfx_stinger_win_12` | none | ✅ `WinScreen.Show(day)` (public static). |
| Game over sting | `sfx_stinger_gameover_10` | `CollapseFade` | ➕ `CollapseManager.GameOver` (private). Add `public static event Action GameOverShown`. |

## Ambience loops

| Where / when | Sound (final pick) | Trigger |
|---|---|---|
| Hub by day | `sfx_ambience_hubday_01` | Player inside the hub (`PlayerHydration_Manager.IsInsideHub()`) and not night. |
| Maze by day (dry wind) | `sfx_ambience_mazeday_02` | Player outside the hub, day. |
| Night | `sfx_ambience_night_01` | `GameClock_Manager.NightStarted` → `NewDay`. |
| Heatwave / thunderstorm | see Night and world | `EventManager` events (➕ above). |
| Main menu | `sfx_ambience_menu_02` | An AudioSource (loop, Play On Awake, `SettingsVolume` = Music) in `MainMenu.unity`. No code. |
| Tension near dusk | `sfx_ambience_tension_01` | Dusk warning window (➕ above). |

Crossfade between loops with two AudioSources (fade one down while the other comes up over
1 to 2 s). A small `AmbienceDirector` in `Assets/Scripts/Feel` can do this once the triggers exist.

## Summary of the small hooks to add (owner's call)

| Script | Add |
|---|---|
| `HubPump_Manager` | `event Action Stroked` (in `PumpStroke`) |
| `HubPurifier_Manager` | `bool IsPurifying` |
| `HubTap_Manager` | `bool IsRunning` / `IsRefilling` |
| `LockBasin_Manager` | `bool IsPouring`, `event Action<float> PouredHyd` |
| `PlayerLocomotion_Manager` | `bool IsSprinting` |
| `PlayerHydration_Manager` | `event Action Drank` |
| Player animation clips | Animation Events for footsteps |
| `ValveTimingGame` | `event Action NeedleTick` |
| `NightLockdown` | `event Action<float> Warning` |
| `EventManager` | `event Action<GameEventType> EventStarted / EventEnded` |
| `StageManager` | `event Action<int> StageChanged` |
| `MapRewards` | `event Action<int> SectionRewarded` |
| `SurvivalDirector` | `static event Action<float, string> Rewarded` |
| `CollapseManager` | `static event Action GameOverShown` |
| `MapScreen`, `NotePanel`, `GameMenu` | `Opened` / `Closed` events (optional; public methods exist) |
| Buy flow | `event Action CantAfford` |

## How it was wired (2026-11-24)

**Feel scripts (all listen-only; no rule or value changes):**

| Script | Where | Does |
|---|---|---|
| `GameFeelDirector` | `GameFeel` object, game scene | hub, lock basins, valve minigame, gates / doors, zone 2, cistern, map / note / menu, rewards, dusk / lockdown / drought, buy |
| `PlayerFeel` | the player | footsteps by distance (sand loop + dust, stone steps), sprint breath, gulps, low-HYD vignette + heartbeat + breath, cough + flies when sick, collapse, "-HYD" when pouring |
| `AmbienceDirector` | `GameFeel` object | crossfaded hub / maze / night ambience, tension, heatwave, storm + rain, dawn birds, thunder, rain / dust effects |
| `UiButtonSounds` | `GameFeel` object, every scene | click / hover on every Button (incl. ones made at runtime) |
| `SceneFeel` | `GameFeel` object in MainMenu / Win / GameOver | menu ambience loop, win sting, game over sting |

**Small hooks added to gameplay scripts (one property or event each, with a comment):**

| Script | Added |
|---|---|
| `HubPump_Manager` | `IsPumping`, `static event Stroked(pump, litres)` |
| `HubPurifier_Manager` | `IsBoiling` |
| `HubTap_Manager` | `LastUsedTime`, `LastUseWasRefill` |
| `LockBasin_Manager` | `LastPourTime`, `static event PouredFromHyd(basin, hyd)` |
| `ValveTimingGame` | `IsRoundRunning` (the existing static `IsPlaying` also counts the end delay) |
| `RoomWaterSource` | `LastUsedTime` |
| `NightLockdown` | `static event LockdownWarning(secondsLeft)` |
| `StageManager` | `static event StageChanged(stage)` |
| `SurvivalRules` (SurvivalDirector.cs) | `static event Rewarded(litres, why)` |
| `UpgradeManager` | `static event Purchased(id, result)` |

Not needed after all (already public): sprinting (input + `CanSprint` + `IsCrouching`), storms / heatwave
(`EventManager.Current`, `WorldState`), map / note / menu open (`IsOpen`, `NoteOpen`), gates and doors (their
UnityEvents are subscribed at runtime), the valve minigame's own sound slots and `onHit` / `onMiss`.

**Choices made while wiring (change in the scripts' Inspector or the library):**
- Footsteps are by distance walked (0.6 m walking, 0.85 m sprinting). Sand vs stone is the ground's object /
  material name (sand, dirt, desert, terrain, dune) or a Terrain; anything else is stone (`PlayerFeel > Sand Words`).
- Gate / door / zone 2 sounds are silent for the first 2 s of the scene (loading a save sets every gate).
- Gates and doors shake the screen only within 14 m of the player (`GameFeelDirector > Shake Range`).
- Collapse keeps the game's own fade (`SimpleHud.SetFade`); `CollapseFade` was not swapped in.
- The minigame lose has the fail sound but no shake (the result isn't exposed outside the lock basin).
- Thunder: random every 7-18 s during the storm; claps louder than 85% also shake.
- The Settings screen does not have a shake on/off toggle yet (UI prefab, owner's call): `CameraShake.Enabled`.
