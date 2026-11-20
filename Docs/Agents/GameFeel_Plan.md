# Game feel plan (Agent 2)

## Status (2026-11-20)
- Approved by George ("go ahead but don't hook anything in the hooking up list").
- Sounds: 129 Mixkit WAV candidates picked and licence-checked, listed in `Assets/Audio/CREDITS.md`
  (status "waiting for OK"). Download waits for George's OK (234 MB). Freesound picks are
  under "George to download".
- Code: `Assets/Scripts/Feel/` (components) and `Assets/Scripts/Feel/Editor/` (builders, audio
  import rules, tests). Compile-checked outside Unity with Unity's own compiler; **not yet
  compiled or tested inside the Editor**.
- VFX prefabs, feel prefabs and the FeelLab scene are made by **Salok > Feel > Build FeelLab**
  (needs the Editor; not run yet).
- Hook-up list: `Docs/Agents/GameFeel_Hookups.md`. Nothing wired into the game.

Date: 2026-11-20. Scope from `Agent2_GameFeel.md`. I only touch `Assets/Audio/`, `Assets/VFX/`,
`Assets/Scripts/Feel/`, `Assets/Scenes/FeelLab.unity` and `Docs/Agents/GameFeel_*.md`.

## 1. VFX: what I reuse from Synty
All Synty FX are plain Particle Systems (no VFX Graph), so they work on WebGL. Several have very
high Max Particles (rain: 10,000), so every copy gets cut down. Copies go in `Assets/VFX/`,
named `VFX_<what>`. Synty originals are never edited.

| Effect we need | Synty source to copy | Change |
|---|---|---|
| Rain (thunderstorm) | Generic `FX_Rain_01` | 10,000 -> about 800 particles, follows the camera |
| Blowing dust / sand (heatwave) | Egypt `FX_Sand_Streaks_01`, Wasteland `FX_Dust_Blowing_01` | already light (50); warmer tint |
| Gate slide / door rise dust puff | Dungeon `FX_Dust_Large_01_Soft` | one burst, not a loop; Max 40 |
| Zone 2 turning dust | Wasteland `FX_Dust_Storm_01` | ring shape around the zone, 1,000 -> 150 |
| Footstep dust | Wasteland `FX_Wheel_Dust_02` | tiny burst of 4 to 6, pooled |
| Water splash (pump, tap, basin) | Generic `FX_Waterfall_Foam_01` | short burst, blue-white |
| Boiling steam | Wasteland `FX_Smoke_Small_01` | white, slow, low count |
| "Water banked" sparkle / reward pop | Dungeon `FX_Sparkles_01` + `FX_Ring_01` | blue water accent |
| Sick particles near player | Wasteland `FX_Flies_01` or `FX_Goop_Bubbles_01` | sickly green, few |

## 2. What I build new (Shuriken, cheap)
- Pour stream (pump spout, tap, lock basin): a thin stretched-billboard stream + the splash above.
- Minigame hit burst (UI): a Canvas prefab (ring + flash image) that scales and fades, pooled.
  It's UI, so it stays editable by the artists instead of being a particle effect.
- All effects: Max Particles set, unlit particle material, no lights, no soft particles,
  played through a small pool (`FeelPool`), never Instantiate / Destroy per play.

## 3. The "pop" components (`Assets/Scripts/Feel/`)
- `FeelSettings` ScriptableObject: every tuning number (shake strength, punch scale, float speed,
  vignette start HYD, hit-stop length...). One asset in `Assets/VFX/Settings/`.
- `FloatingText`: world-space TextMeshPro prefab, pooled, rises and fades ("+1.5 L", "-2 HYD", "FULL").
- `UiPunch` (scale bounce when a value changes) and `NumberRoll` (counter rolls up to the new value).
- `CameraShake`: goes on the Camera object. It adds its offset just before the camera renders and
  removes it right after (URP render callbacks), so **`Player_Camera` needs no edit**. Has an
  on/off switch saved in PlayerPrefs; adding the toggle to the Settings screen goes in the hook-up list.
- `ScreenOverlays`: Canvas prefabs for the low-HYD vignette (pulses harder as HYD drops) and the
  collapse fade to black. Images and colours editable in the prefab.
- `HitFeedback`: white flash on a UI element plus a short hit-stop (timeScale dip, unscaled timer).
- Every script with the project header block and tooltips, plus a few EditMode tests for the maths
  (vignette curve, number roll, shake falloff).

## 4. Sounds
- **WAV only; CC0 / public domain / "free, no attribution required" only.** Licence checked per file.
- Where I'll search, in this order:
  1. **Mixkit** sound effects: WAV downloads, Mixkit Free License (no attribution). Good for UI,
     water, doors, ambience.
  2. **OpenGameArt** with the CC0 filter: only files offered as WAV (many are OGG; those go in
     CREDITS as candidates only).
  3. **Freesound** with the CC0 filter: best range (sand footsteps, pump creak, stone grind), but
     downloading needs a login, so these go under "George to download" with exact links.
  4. **Sonniss GDC bundles**: royalty-free, no attribution, all WAV, but the bundles are many GB.
     I'll list the exact files worth pulling; George downloads the bundle.
  5. **Pixabay**: mostly MP3, so likely candidates only.
- I'll show you each batch (file names, source, size) before I download it.
- 2 to 3 options per sound where I can, sorted into `Assets/Audio/SFX/<Category>/`,
  `Ambience/`, `Stingers/`, named `sfx_<category>_<what>_01.wav`, with `CREDITS.md` rows.
- Import settings set by an editor script (so they're consistent): SFX Decompress On Load, mono;
  ambience Vorbis, Compressed In Memory (Streaming for long tracks), loop points checked.
- Nothing is wired into the game.

## 5. FeelLab scene
One scene, `Assets/Scenes/FeelLab.unity`, built by a menu item `Salok > Feel > Build FeelLab`
(I'll run it in batch mode when the Editor is closed, so the scene is a real saved scene you can edit).
- Left: **Sound board**, a Canvas with a scroll list grouped by folder (Player, Hub, Gates,
  Maze, Night, UI, Ambience). One real button per sound; ambience buttons toggle loops.
  `Salok > Feel > Refresh Sound Board` adds buttons for new files.
- Middle: **VFX row**, each effect on a labelled pedestal with a Play button; a sand strip and a
  stone strip for footsteps.
- Right: **Feel panel**, buttons for floating text, UI punch, number roll, shake, a HYD slider that
  drives the vignette, collapse fade, minigame hit flash / hit-stop.

## 6. Hook-up list
`Docs/Agents/GameFeel_Hookups.md`: for each sound, effect and component, the moment and the
script/event that triggers it. Some moments already have events (`GateSlider.onStartOpening`,
`RoomDoor.onOpened`, `ValveTimingGame.onHit/onMiss`, `Zone2Rotator.Zone2Turned`,
`GameClock_Manager.NightStarted/NewDay`, `PlayerHydration_Manager.OnCollapsed`,
`GameResources_Manager.OnWaterFull`, `MapReveal.TileSeen`). Others don't yet (pump stroke,
drinking, boiling, storm/heatwave start, stage change); for those I'll write the smallest event to add.

## Order of work after your OK
1. Sound search (Mixkit + OpenGameArt first), CREDITS.md, "George to download" list.
2. VFX copies and new effects.
3. Feel components + tests.
4. FeelLab scene.
5. Hook-up list.
