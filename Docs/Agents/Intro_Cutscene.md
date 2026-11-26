# Intro cutscene and Name Entry

Text-only intro shown before every **New Game** from the main menu. No video: everything is
TextMeshPro, a Canvas and sounds already in `Assets/Audio`.

## Flow

```
MainMenu > New Game > Name Entry panel > Intro scene > Salok_Environment
MainMenu > Continue                      ───────────────> Salok_Environment (no name entry, no intro)
Win / GameOver > Play Again              ───────────────> Salok_Environment (same name, no intro)
```

- `MenuSceneUI.NewGame` opens the Name Entry panel. Begin calls
  `SceneFlow.StartNewGameWithIntro()` (deletes the save like before, then loads the `Intro` scene).
- The Intro scene starts loading `Salok_Environment` in the background straight away
  (`allowSceneActivation = false`, low background priority so typing stays smooth on WebGL) and
  lets it in when the intro ends or is skipped. If it is not ready yet, the normal loading screen
  shows until it is (`LoadingScreen.ShowWhileLoading`). The game scene is only ever loaded once,
  so the tutorial starts once, the normal way (`TutorialDirector.Start`).
- If the Intro scene is missing from the build list, New Game goes straight to the game.

## Files

| What | Where |
|---|---|
| Words, sound cues, timings | `Assets/Resources/Intro/IntroScript.asset` (`IntroScript.cs`) |
| Intro scene (Canvas, text, title card, skip hint, audio) | `Assets/Scenes/Intro.unity` |
| Typing, cues, skip, loading | `Assets/Scripts/Intro/IntroDirector.cs` |
| Sound channels (clicks, one-shots, fading loops) | `Assets/Scripts/Intro/IntroAudio.cs` |
| Name Entry panel | MainMenu scene: `UI > Main Menu > Name Entry` (`NameEntryUI.cs`; **Salok > Intro > Add Name Entry To Main Menu** places it once) |
| The player's name | `PlayerProfile.Name` (`Assets/Scripts/Intro/PlayerProfile.cs`) |
| Scene + asset builder | **Salok > Intro > Build Intro Scene** (`Intro/Editor/IntroSceneBuilder.cs`) |

The builder only creates the asset if it is missing, and asks before rebuilding an existing
scene. After the first build, edit the scene and the asset by hand.

## Editing the text

Select `IntroScript.asset`. Each entry in **Beats** is one screen:

- **Text**: one row = one typed line. Empty rows are ignored.
- Start a row with `[LOUDSPEAKER]` for a loudspeaker line: loudspeaker colour (`#3B7A8C`, set on the
  Intro object), typed fast, no clicks.
- `{name}` becomes the player's name, `{NAME}` the name in capitals.
- **Speed Scale**: 1 = normal, 0.5 = half speed (the letter).
- **Pause Between Lines**: seconds between two lines of this beat (letter: 1.5).
- **Black After**: extra seconds of black after the beat fades out (beat 4: 2 s after the loudspeaker).

Add, remove or reorder beats freely. The look (font, size, colour, position) is on
`Intro Canvas > Beat Text` in the Intro scene.

## Editing the sounds

Each beat has a list of **Cues**:

| Field | Meaning |
|---|---|
| Action | `OneShot` plays once. `StartLoop` loops it, fading in. `StopLoop` fades that clip's loop out. `StopAllLoops` fades every loop out (silence). |
| Clip | Any clip from `Assets/Audio` (not needed for StopAllLoops). |
| Before Line | Fires just before that line starts typing (0 = first line). A number past the last line fires when the beat's text is finished. |
| Delay | Extra seconds after that moment (the text doesn't wait). |
| Volume | 0 to 1, before the Settings slider. |
| Fade Seconds | Fade in (StartLoop) or fade out (StopLoop / StopAllLoops). |

Default cues:

| Beat | Cues |
|---|---|
| 1 | `sfx_ambience_heatwave_01` loop fades in |
| 2 | heatwave fades out (only the typing clicks) |
| 3 | `sfx_ambience_tension_01` loop fades in |
| 4 | `sfx_world_lockdown_06` siren at line 1; `sfx_player_collapse_06` and silence (all loops fade) before the loudspeaker line |
| 5 | `sfx_gates_gateslide_01` at line 1, `sfx_gates_doorrise_01` at line 2 |
| 6 | heatwave (wind) returns; `sfx_ambience_hubday_01` fades in under it after 1 s |
| 7 | none (the letter) |

Typing click: **Click Clip** `sfx_ui_click_03`, **Click Volume** 0.25, one click every
**Chars Per Click** = 2 visible characters (spaces don't click), random pitch 0.9 to 1.1.

Every AudioSource (under `Intro Audio` in the scene) has a `SettingsVolume`: clicks and one-shots
follow the SFX slider, loops follow the Music slider. Three loop channels (`Loop A/B/C`) are
enough for the script; add another child AudioSource and drag it into **Loops** if you need more
loops playing at once.

**Into the game:** when the intro ends, the loops still playing (wind and hub day) carry over
into the game scene and fade out over **Carry Over Fade** (2.5 s) while the game's own
`AmbienceDirector` fades its hub-day layer in over the same time (its crossfade is 2.5 s), so the
hub sound continues without a cut. On skip the fade is 1 s.

## Timings (IntroScript asset)

| Field | Default |
|---|---|
| Chars Per Second | 35 |
| Loudspeaker Chars Per Second | 120 |
| Hold After Beat | 1.5 s |
| Fade Out Seconds | 0.6 s |
| Title Fade In / Title Hold | 1 s / 2 s |
| Carry Over Fade | 2.5 s |

Skip (on the Intro object): hold Enter or Esc for **Skip Hold Seconds** (0.8). The "Hold Enter to
skip" hint fades in after **Hint Delay** (3 s); its thin bar fills while the key is held.
**Pause On Focus Loss**: while the window has no focus, the text, timers and sounds pause.

## Title card

`Intro Canvas > Title Card > Logo` uses `Assets/Art/UI/Title Card/salok-logo E.png`, the wooden sign
the main menu also uses. The sign has no words on it, so `Title Card > Title` writes "SALOK" on it in
Barlow Condensed Bold (if the sprite is missing, only the text is built). Both fade in together.

## Name Entry

- Panel `Name Entry` in the MainMenu Canvas: title "What is your name?", a TMP input field and a
  Begin button. It is a copy of the Confirm Dialog's look (dim backdrop, clipboard-and-cloth box, wood
  button; dark rounded box with JetBrains Mono for the name). Every element is a normal object in the
  Hierarchy, not a prefab.
- Pre-filled with the last name used. Enter or Begin starts; Esc goes back to the main buttons.
- Rules (`PlayerProfile.Clean`): spaces trimmed, 16 characters at most, `Survivor` if empty.
- Saved to PlayerPrefs (`salok.playerName`) and into the save file (`SaveData.playerName`,
  written at each autosave; Continue restores it).
- Use it anywhere: `PlayerProfile.Name`, or `PlayerProfile.Fill("Well done, {name}.")`.

## Web build notes

- The typing loop sets each beat's text once and reveals it with `maxVisibleCharacters`: no
  garbage per frame. Timers use their own clock (no `WaitForSeconds`).
- The game scene loads at `ThreadPriority.Low` during the intro.

## Tests

`Assets/Scripts/Editor/Tests/PlayerProfileTests.cs`: name rules (trim, 16 max, default) and the
`{name}` / `{NAME}` replacement and `[LOUDSPEAKER]` parsing.
