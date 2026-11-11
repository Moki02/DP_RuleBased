# Test: Batch 4 (water sources and the Biosand Filter)

Branch `batch4-sources`. About 15 minutes. Debug keys: **7** = +50 L Clean Storage (to buy things),
**4** = rain on/off (test only, until the Thunderstorm event from batch 3 is merged), **6** = ×4 clock,
**F5 / F9** = save / load. Tab opens Build and Skills.

Buying order that works: 7, buy **S2** (50 L) and **S3** (150 L) first so 7 can fill enough for the big items.
Deep Well (W7) needs **W1 → W4** and **S3**. Solar Still (W5) needs **W2**.

## Setup
- [ ] Pull, open Unity, wait for compiling. **Expected:** no red errors in the Console.
- [ ] **Tools > The Last Drop > Delete Save**, then Play. **Expected:** no source models in the base yet; a label over the boiling pot reads `Boiling pot: 0.17 L/s (waiting for raw water)`.
- [ ] Stand in the base. **Expected:** HUD line `Boiling pot: 0.17 L/s ...` bottom right (hub only).

## Rain Catcher (R4.3, R1.4)
- [ ] Buy **W2 Rain Catcher**. **Expected:** a grey roof over a blue barrel appears 3 tiles west and 3 tiles north of the spawn; label `Rain Catcher 4 m²: 0 L/s (no rain)`. HUD line `Sources: rain 0.000 L/s`.
- [ ] Press **4**. **Expected:** toast `Rain ON`; label `Rain Catcher 4 m²: 0.36 L/s (raining)`; Raw Basin rises 0.36 L/s.
- [ ] Buy **W3 Bigger Roof**. **Expected:** roof gets bigger; label `8 m²: 0.72 L/s`. (**W8**: 16 m², 1.44 L/s.)
- [ ] Keep raining until the Raw Basin is full (or stop the boiling pot by filling Clean Storage with 7). **Expected:** label `FULL, rain lost (... L/s spilling)`, toast `FULL: Raw Basin. Rain is spilling over!` every 4 s, the basin stays at its max.
- [ ] Press **4** again. **Expected:** `Rain OFF`; rain catcher back to 0 L/s.

## Solar Still (R4.4, R3.1, section 10)
- [ ] Buy **W5 Solar Still** (rain off). **Expected:** a dark box with a glass lid 3 tiles east / 3 tiles north of spawn; label `Solar Still: 0.020 L/s (sun ×1)` on day 1; Clean Storage rises without the Raw Basin changing.
- [ ] Press **4** (rain on). **Expected:** `Solar Still: 0 L/s (paused: rain)`. Rain off: it resumes.
- [ ] Buy **W6 Second Still**. **Expected:** two boxes; label `Solar Still ×2: 0.040 L/s`.
- [ ] Fill Clean Storage to the top (7). **Expected:** `paused: Clean Storage FULL`; nothing lost, no FULL toast.
- [ ] Later stages (batch 2 debug key 8, or reach day 3 / 5 / 12). **Expected:** sun factor grows (×1.2, ×1.4, ×1.6), e.g. `0.028 L/s (sun ×1.4)` with one still at Drought.
- [ ] (Needs batch 3) During a Heatwave. **Expected:** `..., heatwave ×2` and double output.

## Deep Well (R4.5)
- [ ] Buy **W1, W4, S2, S3, W7**. **Expected:** a stone ring 3 tiles east / 3 tiles south of spawn; label `Deep Well: 0.080 L/s (El Niño Advisory ×1)`; Raw Basin rises on its own.
- [ ] Later stages. **Expected:** ×0.95 / ×0.85 / ×0.75 (`0.068 L/s` at Drought).
- [ ] Let the Raw Basin fill. **Expected:** `Deep Well: 0 L/s (paused: Raw Basin FULL)`; no water lost, no toast.

## Biosand Filter (R6.1)
- [ ] Note the day, buy **P1 Biosand Filter**. **Expected:** a sand-coloured drum next to the boiling pot; pot label `Biosand Filter 50%: 0.20 L/s`; Raw Basin empties faster than before.
- [ ] Press **6** and wait for the next day. **Expected:** `Biosand Filter 75%: 0.30 L/s`. One more day: `100%: 0.40 L/s`.
- [ ] Fill Clean Storage (7). **Expected:** `(paused: Clean Storage FULL)`; raw water waits in the basin.

## Save and stats
- [ ] Buy P1, wait a day (75%), **F5**, keep playing a day, **F9**. **Expected:** the filter reads 75% again (it remembered the day it was bought); all owned source models are back.
- [ ] Collapse into game over or check `RunStats`. **Expected:** "litres collected" includes water from the rain catcher, still and well.
- [ ] Walk out of the hub. **Expected:** the `Sources` and purifier HUD lines disappear; the world labels stay.
