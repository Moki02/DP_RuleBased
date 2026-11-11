// ============================================================================
// RoomNotes.cs
// The survivor notes (R8.7): one per room, each with a payload (R8.17). This file holds the
// note format and the 7 placeholder notes. The writers can replace the text in the
// Inspector (Systems > RoomContent > Notes) without touching code.
//
// DESIGN RULES:
//   R8.7  One note per room. Reading it shows a text panel (the clock keeps running) and
//         triggers its payload. Read notes go into the journal (J on the map screen).
//   R8.17 Payloads: Reveal (opens a room's tiles on the map), Marker (pins a cistern room on
//         the map, if there's room for one more marker), Clue (text only).
//         Payload targets are room numbers, and rooms are stored in CSV (zone-local)
//         tiles, so zone 2 payloads turn with the zone.
//
// ROOM NUMBERS (Maze2Layer_0deg.csv, sorted top to bottom, then left to right):
//   0 zone 2 north (69, 9)      1 zone 2 north-west (15, 25)   2 zone 1 north-east (59, 33)
//   3 zone 2 east (81, 33)      4 zone 1 north-west (33, 37)   5 zone 1 west (27, 53)
//   6 zone 2 south-west (9, 65)
// With roomSeed 8: 0 cistern, 1 cistern, 2 cache, 3 cistern, 4 cache, 5 cistern, 6 empty.
// If the layout changes, check the room numbers and the targets below.
//
// HOW TO USE: RoomContent uses Defaults() and shows the note for each room.
// HOW TO EDIT: change the text, payload or target room in the Inspector or in Defaults().
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>What reading a note does (R8.17).</summary>
    public enum NotePayload
    {
        /// <summary>Text only: a hint about a mechanic.</summary>
        Clue,
        /// <summary>Reveals another room's tiles on the map.</summary>
        Reveal,
        /// <summary>Pins a map marker on a cistern room.</summary>
        Marker
    }

    /// <summary>One survivor note.</summary>
    [Serializable]
    public class RoomNote
    {
        [Tooltip("Who wrote it (the panel's title).")]
        public string title = "A note";
        [Tooltip("The note itself. Keep it short (about 60 words) so the journal fits on screen.")]
        [TextArea(3, 8)] public string text = "";
        [Tooltip("What reading it does (R8.17).")]
        public NotePayload payload = NotePayload.Clue;
        [Tooltip("Reveal: the room number to show on the map. Marker: the cistern room to mark, or -1 = the nearest cistern room not found yet.")]
        public int targetRoom = -1;

        /// <summary>An empty note (Unity uses this when a note is added in the Inspector).</summary>
        public RoomNote() { }

        /// <summary>Makes a note.</summary>
        public RoomNote(string title, string text, NotePayload payload, int targetRoom = -1)
        {
            this.title = title;
            this.text = text;
            this.payload = payload;
            this.targetRoom = targetRoom;
        }
    }

    /// <summary>The placeholder notes.</summary>
    public static class RoomNotes
    {
        /// <summary>The 7 placeholder notes, one per room number (see the table at the top).</summary>
        public static RoomNote[] Defaults() => new[]
        {
            // 0: zone 2 north (cistern)
            new RoomNote("Pedring, the well digger",
                "Forty years I dug wells for this town. Last month I dug one more and found warm sand. " +
                "When the siren goes, get inside a room like this one. The gates shut on everyone else.\n\n" +
                "Clue: during the night lockdown only the rooms and the hub are safe.",
                NotePayload.Clue),

            // 1: zone 2 north-west (cistern) -> reveals room 0
            new RoomNote("Lita, on the back of a rice sack",
                "I climbed the rubble at noon and saw another room to the north-east, with a tank under the floor. " +
                "My legs gave out before I got there. I drew the way for whoever comes next.",
                NotePayload.Reveal, 0),

            // 2: zone 1 north-east (cache) -> marks a cistern
            new RoomNote("Mang Ruben",
                "The council said the rains would come in June. Then July. I stopped listening and went looking for cisterns. " +
                "There is one to the west that still fills every dawn. I marked it for you. Don't drink it all, kid. " +
                "Ha. Drink it all.",
                NotePayload.Marker),

            // 3: zone 2 east (cistern) -> marks another cistern
            new RoomNote("Unsigned, shaky handwriting",
                "The tanks in the far rooms fill again every morning, a little less each week. " +
                "I put a mark on the next one I know. If you find my sister, tell her I tried.",
                NotePayload.Marker),

            // 4: zone 1 north-west (cache)
            new RoomNote("Tomas, the schoolteacher",
                "I counted the nights. Every second night the outer ring of the maze turns a quarter turn clockwise " +
                "while the gates are shut. The inner ring never moves. Learn the inner ring by heart. Trust the outer one less.\n\n" +
                "Clue: zone 2 turns 90° clockwise on the nights of days 2, 4, 6 ...",
                NotePayload.Clue),

            // 5: zone 1 west (cistern) -> reveals room 1
            new RoomNote("Aling Nena",
                "My grandson went into the outer ring looking for the room with the cool tank, north-west of here." +
                "He told me the way and I wrote it down. If you see a red slipper, that's his. Bring him home.",
                NotePayload.Reveal, 1),

            // 6: zone 2 south-west (empty, the wry one)
            new RoomNote("Benjo, mapmaker (self-appointed)",
                "I mapped every corridor of this maze. Every single one. A beautiful map. " +
                "Turns out you can't drink a map. At least the village pays for one: the more you chart, the more water you're owed.\n\n" +
                "Clue: mapping 25, 50, 75 and 100% of the maze pays water.",
                NotePayload.Clue),
        };

        /// <summary>The note used for a room with no note written (more rooms than notes).</summary>
        public static RoomNote Fallback(int roomIndex) => new RoomNote(
            "A torn page",
            "The ink has run. You can only read one word: \"water\".",
            NotePayload.Clue);
    }
}
