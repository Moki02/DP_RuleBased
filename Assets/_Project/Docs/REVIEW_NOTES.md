# Review notes: what to check first in Unity

Branch `integration-all`. The code was reviewed by four separate reviewers and compile-checked
outside Unity (stand-ins for Unity's code, 0 errors in runtime and Editor scripts). It has still
**never been opened in Unity or played**, so do these checks in order.

## 1. First open (5 min)
- [ ] Unity compiles with no red errors. If one appears, it's most likely a Unity API the stand-ins
      got wrong: read the message, it usually names the exact line.
- [ ] **Tools > The Last Drop > Build Playtest Scene**, then **Delete Save**, then Play.
- [ ] The title screen shows (SALOK: New Game / Continue / Demo / Quit). Press New Game.
- [ ] Console has `[RoomContent] 7 rooms ...` and no "No room object for room" warning
      (if there is one: rebuild the scene; zone 2 rooms won't turn otherwise).
- [ ] The pump, boiling pot and Clean Storage tap stand around the spawn. The team's Hub HUD
      (Raw Basin / Clean Storage) shows in the base.
- [ ] TextMeshPro labels in the world (sources, rooms) show text. If they're blank, import
      TMP Essentials (Window > TextMeshPro > Import TMP Essential Resources).

## 2. Things only Unity can confirm
- [ ] **Drink key is Q.** Hold-to-drink reads the Q key directly. Check the Player Input asset's
      `PlayerDrink` is Q (the animation uses it); a gamepad can't drink yet.
- [ ] **The "Drink" animation** state exists and its end event calls `EndDrinkAnimation`.
- [ ] **Restart and Demo reload the scene.** After pressing Restart (game over) and Demo, the player
      still moves, HYD drains and the HUD shows `DEMO` in the demo.
- [ ] **No overlap:** the bottom-right text (Sick, lockdown, stage, event lines) doesn't sit on
      top of anything at your screen size.
- [ ] **Map can reach 100%** by walking (the 100% milestone needs every non-wall tile to be seeable).
- [ ] **J** isn't bound to anything in the Player Input asset (it opens the journal on the map).
- [ ] The Close / Restart / Quit buttons can be clicked (needs the EventSystem the builder adds).

## 3. Design calls for the designer (the code picked one; change if you disagree)
| Question | What the code does now | Where to change |
|---|---|---|
| Demo day length: GDD says 75 s days, but the demo asset has no night value, so night adds 60 s (135 s) | 75 s day + 60 s night | `GameBalance_Demo` > Night Lockdown Seconds |
| Safe Return +10% "of the water you carried in" | 10% of the unbanked water brought home before night | `SurvivalDirector.Bank` |
| Collapse with water still unbanked | Banked first, then the game-over check (carried water can save you) | `CollapseManager` |
| Sections (the CSV has none) | 6 angle slices in zone 1 (A-F), 8 in zone 2 (G-N) | `MazeSections.cs`, `GameBalance` > zone section counts |
| Tiles revealed by a note | Count for section / milestone rewards, not the 0.03 L per tile | `RoomContent.ApplyPayload` |
| "Read all 8 survivor notes" badge | Not shown (there are 7 rooms / 7 notes) | `WinScreen.cs` |
| Stats "skills bought" | Counts Build items and skills together | `RunStats.Summary` |
| Water Table Drop / Cloud Seeding | Cut to nice to have (their weight goes to Calm); the GDD still says Must | GDD |
| Thunderstorm "in the open" | Everywhere except inside a maze room | `EventManager` |
| Lock basin win reward | Only valve grates pay +2 L (R8.8); lock basins pay nothing | `LockBasin_Manager` |
| Quit after game over | Keeps the last dawn save (Continue resumes it); Restart deletes it | `CollapseManager` |
| Paying a lock basin (changed 2026-11-12) | Canteen first; when it's empty, pours from HYD (1 L = Hyd Per Liter HYD), stopping at 10 HYD with "Too thirsty to pour". GDD R8.9 still says canteen only | `GameBalance` > Lock basins (Pour From Hyd When Canteen Empty, Pour Hyd Floor), `LockBasin_Manager` |
| Tab menu and map look (2026-11-12) | Black and white: black background, white text, grey panels / cards / borders, blue only for water (owned, Can buy, storage bar, gates and valves on the map); map tiles in greys, walls darkest | `GameMenu` / `MapScreen` > Colours (untick Black And White to use the colours in the Inspector) |

## Scenes (added 2026-11-12)
Plain black-and-white placeholders, built in code, for the artists to replace:
- **MainMenu**: SALOK, New Game / Continue (only with a save) / Demo / Settings / Quit.
  Settings: Music volume, SFX volume, Mouse sensitivity (saved between sessions; add a
  `SettingsVolume` component next to any AudioSource so it follows the sliders).
- **Loading screen**: black, "LOADING...", a bar and %; shown on every scene change.
- **Win**: shown when the player walks out through an opened exit gate (zone 2's outer wall,
  or zone 1's in Demo mode). Ending title, day, badges, stats; Play Again / Main Menu / Quit.
- **GameOver**: shown when the player collapses with too little water at home. Stats; Play
  Again / Main Menu / Quit (Main Menu keeps the last dawn save for Continue).
- The game scene no longer shows a title panel. The team's pause menu gets a "Save and quit
  to menu" button at its bottom.

First time: run **Tools > The Last Drop > Build Menu Scenes** once (makes the three scenes and
puts MainMenu first in the Scene List), then press Play in MainMenu. Running it again remakes
the three scenes, so don't once the artists have decorated them.

## UI prefabs for the artists (added 2026-11-12)
Every screen built in code is now a prefab with its own Canvas the artists can edit. Run
**Tools > The Last Drop > Build Menu Scenes** (or **Build UI Prefabs**) once; it never
overwrites a prefab or scene that already exists.

| What | Where to edit | Script with the slots |
|---|---|---|
| Main menu + Settings | `Assets/Scenes/MainMenu.unity` (Canvas) | MenuSceneUI |
| Win / Game Over | `Assets/Scenes/Win.unity`, `GameOver.unity` | EndSceneUI |
| Loading screen | `Assets/Resources/LoadingScreen.prefab` | LoadingScreen |
| Build / Skills / Map menu | `Assets/Resources/UI/GameMenu.prefab` | GameMenuView, UpgradeDetailView |
| Upgrade card (one, copied 33×) | `Assets/Resources/UI/UpgradeCard.prefab`, or drag another into Systems > GameMenu > **Card Prefab** | UpgradeCardUI |
| Card icons | Systems > GameMenu > **Upgrade Icons** (id + sprite) | GameMenu |
| Map screen | `Assets/Resources/UI/Map.prefab` | MapView |
| Map player arrow / markers / room icons | Systems > MapScreen > **Map icons** (sprite, colour, size) | MapScreen |
| Notes and journal | `Assets/Resources/UI/NotePanel.prefab` | NotePanelView |
| Status lines, toasts, banners, fade, pop-up | `Assets/Resources/UI/SimpleHud.prefab` | SimpleHudView |
| Pause "Save and quit to menu" | add a Button named `Quit To Menu Button` to the Player Pause prefab (else one is added) | MainMenu |

Rules for artists: restyle, move, re-font, add art freely; keep the objects that are linked in
the view script's slots (or drag the replacements into the slots). Leave buttons' On Click
lists empty in the in-game prefabs: the code wires them. Card positions, the lines between
cards and the map picture itself stay in code (GameMenu layout lists, the maze).

## 4. Known small issues
Fixed on 2026-11-12:
- `Minigame_Manager.cs` renamed to `MinigameGate_Manager.cs` (in Unity, so the .meta moved too).
- Debug keys only work during play (not on the title, win, game over or pause screens, or the
  collapse fade), so F5 can't overwrite the save from the title screen.
- Tab / B / M do nothing while a note is open (close the note first); they no longer change
  which page Tab opens next time.
- Holding E at the drink tap and Q together no longer drinks at double speed (the tap wins).
- The bottom-left overlap: SimpleHud's status lines moved to the bottom-RIGHT corner, and its
  "Unbanked" line was removed (the team HUD's "+X L" already shows it).
- Raw Basin / Clean Storage had nothing on screen (the team's Player HUD has no hub panel):
  the boiling pot's label now shows the raw water and what the pot is doing, the Clean Storage
  tank's label shows its litres, the pot's water shows only when there is raw water, the lid
  moves only while boiling, and the pump prompt shows the Raw Basin.

- "Some objects were not cleaned up when closing the scene: SimpleHudCanvas": WaterSources and
  BiosandFilter cleared their HUD line in OnDestroy, which made a new canvas while the scene
  closed. Clearing a line no longer makes a canvas.
- The map zooms with the scroll wheel (around the mouse, 1× to 6×) and moves by dragging with
  the left mouse button (MapScreen > Zoom and drag).

Still open:
- A save made before 2026-11-11 doesn't record which map section / milestone rewards were
  already paid, so loading it pays those once more. Use Tools > The Last Drop > Delete Save (or New Game) once.
- The R8.20 siren sound and water-bar flash aren't made (text warnings only). Not planned for now.
