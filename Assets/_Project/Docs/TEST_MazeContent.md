# Test: Batch 5 (what's inside the maze)

Branch `batch5-maze-content`. About 15 minutes. Press **6** for the ×4 clock when waiting.
Rooms by number (Maze2Layer_0deg.csv, seed 8): 0 zone 2 north = cistern, 1 zone 2 north-west = cistern,
2 zone 1 north-east = cache, 3 zone 2 east = cistern, 4 zone 1 north-west = cache, 5 zone 1 west = cistern,
6 zone 2 south-west = empty. The Console prints the list at Play: `[RoomContent] 7 rooms: ...`.

## Setup
- [ ] Open Unity, wait for compiling. **Expected:** no red errors in the Console.
- [ ] **Tools > The Last Drop > Delete Save**, then Play. **Expected:** Console shows `[RoomContent] 7 rooms: 0 Cistern (d 1.00, 8.0 L) ...` and the `Systems` object has `MapRewards` and `RoomContent`.

## Sections and milestones (R8.2, R8.3)
- [ ] Explore one slice of zone 1 (sections are 6 cake slices around the centre, A starts at north and goes clockwise). **Expected:** at 75% of its floor, toast `+2.0 L unbanked (section A mapped)` once; Console `[MapRewards] Section A ...`.
- [ ] Map a zone 2 slice. **Expected:** `+3.0 L unbanked (section G mapped)` (or another letter G to N).
- [ ] Reach 25% on the map (M shows Map %). **Expected:** `+10.0 L unbanked (map 25%)` once.
- [ ] Buy **V4 Surveyor**, map another section. **Expected:** 3.0 L (zone 1) or 4.5 L (zone 2).
- [ ] Buy **V5 Cartographer**. **Expected:** sections now pay at 60%.
- [ ] F5, F9, keep walking in a section that already paid. **Expected:** it doesn't pay again.
- [ ] **Debug key 0** (reveal whole map). **Expected:** the map is all shown but NO section or milestone toasts, and unbanked water doesn't jump.

## Rooms: water (R8.5, R8.11 to R8.16)
- [ ] Walk into room 2 or 4 (zone 1, north side). **Expected:** toast `Found a room...`; an orange jug in the middle labelled `Sealed jug 3.0 L` (d = 0).
- [ ] Hold **E** next to it with a 2 L canteen. **Expected:** about 1 L/s goes into the canteen; it stops when the canteen is full, and the label keeps the rest (`1.0 L`). Prompt says `Canteen full`.
- [ ] Empty the jug on a later trip. **Expected:** label `Empty jug`, toast `The jug is empty.`, E prompt gone.
- [ ] Room 5 (zone 1 west): blue tank `Cistern 3.5 / 3.5 L` (d = 0.25). Take some, wait for dawn. **Expected:** it is full again at dawn (×0.85 from Stage 2, ×0.7 Stage 3, ×0.5 Stage 4).
- [ ] Room 6 (zone 2 south-west): grey box `Remains, no water here`, no E on it; the note still works.
- [ ] Debug key 5 (turn zone 2) while looking at a zone 2 room. **Expected:** the tank, note and labels turn with the room.

## Notes and journal (R8.7, R8.17)
- [ ] Press **E** on the small white note on a crate, one tile east of the room's middle. **Expected:** a text panel; you can't move or look; the clock and HYD keep going.
- [ ] Close with E, Esc or the Close button. **Expected:** control and the hidden, locked cursor come back; the note doesn't reopen by itself. The label now says `Survivor note (read)`.
- [ ] Room 2's note (Marker). **Expected:** extra line `A cistern room is now marked on your map`; the map shows a yellow marker on room 5 (the nearest cistern room you haven't found yet). With 3 markers already placed it says there's no room instead.
- [ ] Room 5's note (Reveal). **Expected:** room 1 (zone 2 north-west) appears on the map. Turn zone 2 (key 5): the revealed room turns with it.
- [ ] Room 4's and room 0's notes (Clue). **Expected:** text with a `Clue:` line, no payload.
- [ ] Open the map (M), press **J** (or click `J Journal`). **Expected:** the journal lists every note read, in order, plus `Sections mapped x / 14`. J again or closing the map closes it.

## Map (R8.18, V2)
- [ ] Open the map after finding rooms. **Expected:** a small square with a letter on each found room: C (blue) cistern, J (orange) jug, E (grey) empty. Zone 2 icons move with zone 2.
- [ ] Buy **V2 Landmarks**. **Expected:** every gate shows on the map in blue, even unexplored ones.

## Save
- [ ] Take half a cache, read two notes, find three rooms, F5, then F9. **Expected:** litres, read notes (journal), found icons and paid sections come back; notes don't re-trigger their payloads.

| What | Tile / day | What happened |
|---|---|---|
| | | |
