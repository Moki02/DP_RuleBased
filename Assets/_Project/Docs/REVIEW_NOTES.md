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
- [ ] **No overlap:** the bottom-left text (unbanked, Sick, lockdown, stage, event lines) doesn't sit on
      top of the team's HUD at your screen size.
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

## 4. Known small issues (not fixed)
- `Minigame_Manager.cs` contains a class named `MinigameGate_Manager` (file and class names differ,
  from before today). Unity can't add it from Add Component; old Zone1 scenes may show "Missing script".
  Rename the file OR the class to match.
- Debug keys still work behind the title and end screens (F5 there overwrites the save).
- Pressing M/B/Tab while a note is open changes which Tab page opens next time (cosmetic).
- A save made before today pays any already-earned section/milestone rewards once. Delete old saves.
- Hold E at the drink tap and Q at the same time drinks from both (double speed).
- The R8.20 siren sound and water-bar flash aren't made (text warnings only).
