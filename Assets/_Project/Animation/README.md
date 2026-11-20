# Player animation (Salok)

The player uses **AC_Salok_Player.controller** (this folder), a copy of Synty's
`AC_Polygon_Feminine` from *Animation - Base Locomotion* (`Assets/Art/AnimationBaseLocomotion`).
The pack's own files are never edited. The Animator is on `PlayerPrefab` (root), with
**Apply Root Motion off**: movement is done in code (`PlayerLocomotion_Manager`), and only the
in-place Feminine clips are used (no `*_RootMotion_*` clips).

`Salok > Animation > Build Player Animator` (`Editor/SalokPlayerAnimatorBuilder.cs`) creates
or repairs everything. It is safe to run again: it never overwrites an action state that
already exists, so clips you swap in stay.

## Who sets what

| Script | Job |
|---|---|
| `PlayerLocomotion_Manager` | Moves/turns the player, crouch, jump, gravity. In charge of movement. |
| `PlayerSyntyAnimator` | Reads the movement and sets every Animator parameter below. Never moves the player. |
| `PlayerAnimator_Manager` | `PlayTargetActionAnimation(name, ...)` / `ResetActionAnimation()` for actions. |

## Layers

| # | Layer | What it does |
|---|---|---|
| 0 | Base Layer | Synty locomotion: idle, starts, walk/run/sprint, stops, turn in place, shuffles, crouch, jump, fall, landing |
| 1-3 | Additive_BodyLook / HeadLook / Lean | Synty additive layers: body and head turn, lean into turns |
| 4 | **Actions** (full body) | `Empty`, `Collapse`, `WakeUp`, `Pump`, `Pour` |
| 5 | **UpperBodyActions** (`AM_Salok_UpperBody` mask) | `Empty`, `Drink` (upper body only, so the player can walk while drinking) |

The action layers rest in `Empty` at weight 0; `PlayerSyntyAnimator` fades a layer in while a
state other than `Empty` plays and out when it returns.

## Which clip plays when

| Situation | State / clip | Gait (`CurrentGait`) |
|---|---|---|
| Standing still | `Idle_Standing` (A_Idle_Standing_Femn) | 0 Idle |
| Walk (4 m/s) | Locomotion blend tree at MoveSpeed 4: run clip blended with about a third of sprint | 2 Run |
| Sprint (6.5 m/s) | Locomotion blend tree at MoveSpeed 6.5: almost all sprint clip | 3 Sprint |
| Dehydrated walk (3 m/s) | Tired walk: MoveSpeed 1.9 (mostly the walk clip) played about 1.6× faster, head and body looking down, less lean | 1 Walk |
| Starting to move | `Walk_Start` / `Run_Start` blend by start direction (0/90/180°) | |
| Letting go of the keys | Walk/Run to-idle stop clips | |
| Turning far from the camera while still | `TurnInPlace_Standing` (or `_Crouching`) | |
| Crouched still / moving (2 m/s) | `Idle_Crouching` / crouch blend tree played 1.43× (clip recorded at 1.4 m/s) | 1 Walk |
| C pressed | `Stand_ToCrouch`, `Crouch_ToStand`, `Sprint_ToCrouch` | |
| Space | `Jump_Idle` / `Jump_Walking` / `Jump_Running` / `Jump_Sprinting` by gait, then `Fall`, then a landing by gait and fall time | |
| Q (drink) | UpperBodyActions/`Drink` (old Consume clip, keeps its `EndDrinkAnimation` event) | |
| E held at hand pump | Actions/`Pump` (PLACEHOLDER) | |
| E held at a lock basin | Actions/`Pour` (PLACEHOLDER) | |
| HYD hits 0 | Actions/`Collapse` (old death clip) | |
| Waking at the hub | Actions/`WakeUp` (PLACEHOLDER, 1 s) | |

Feet don't slide because Synty's blend tree has a clip at each recorded speed (walk 1.4, run
2.5, sprint 7 m/s), and MoveSpeed is the player's real speed. Where a single clip has to cover
a faster speed (crouch, tired walk), the `LocomotionPlaybackSpeed` parameter speeds up only the
locomotion and crouch blend trees.

## Parameters

Synty's own, set every frame by `PlayerSyntyAnimator`:
`MoveSpeed`, `CurrentGait` (0 Idle, 1 Walk, 2 Run, 3 Sprint), `IsStopped`, `IsStarting`,
`LocomotionStartDirection`, `MovementInputTapped` / `Pressed` / `Held`, `IsTurningInPlace`,
`CameraRotationOffset`, `IsGrounded`, `FallingDuration`, `IsCrouching`, `IsJumping`,
`IsWalking` (true for the tired walk), `IsStrafing` (always 0), `StrafeDirectionX/Z`,
`ForwardStrafe`, `ShuffleDirectionX/Z`, `InclineAngle` (0), `LeanValue`, `HeadLookX/Y`,
`BodyLookX/Y`.

Added: `LocomotionPlaybackSpeed` (default 1), the speed multiplier of `LocomotionBlendTree`
and `CrouchingBlendTree`.

All tuning (gait mapping, tired walk, lean, head look, action fade) is in the Inspector on
`PlayerSyntyAnimator`; crouch height, jump timing, speed ramp and turn-in-place are on
`PlayerLocomotion_Manager`; speeds, drains and jump cost are in GameBalance.

## Swapping in a real action clip (Pump, Pour, WakeUp)

The placeholders use the idle clip and are tagged `PLACEHOLDER` in the Animator.

1. Import the clip as **Humanoid** (Rig tab: Animation Type Humanoid, Avatar "Create From This
   Model", or copy from `A_Idle_Standing_Femn`). Turn **Loop Time** on for `Pump` and `Pour`
   (they repeat until E is released), off for `WakeUp`.
2. Open `AC_Salok_Player`, go to the **Actions** layer, select the state and drag the clip into
   **Motion**. Keep the state's name exactly (`Pump`, `Pour`, `WakeUp`): the scripts play them
   by name. Clear the `PLACEHOLDER` tag.
3. `WakeUp` only: select its transition to `Empty` and set **Exit Time** to about 0.9 (it is
   set to cut the placeholder after 1 s). The player gets control back when it reaches `Empty`.
4. `Pump` / `Pour` need no transition: the code returns to locomotion when E is released.
5. Play: hold E at the pump / a basin, get collapsed (drink down to 0) to see the wake-up.

`Drink` is on **UpperBodyActions** (arms, head, spine only). To swap it, keep an animation event
calling `EndDrinkAnimation` near the end of the clip, as the current Consume clip does.

## Old controller

`Assets/Art/PlaceHolder/Animator/Player.controller` is no longer used by the game scene
(Salok_Environment / PlayerPrefab). It is still referenced by the old test content:
`Prefabs/Player Prefab/Player.prefab`, `Prefabs/Game Prefab/PlaceHolder/Models/Player.prefab`,
`Scenes/Minigame.unity` and `Scenes/TestingScene.unity`. The scripts still work with it there
(they fall back to its "Speed" value). Once those are gone or switched to AC_Salok_Player it
can be deleted. AC_Salok_Player points at its Drink and Collapse clips directly, not at the
controller.
