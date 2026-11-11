// ============================================================================
// MazeArea.cs
// Marks a safe area the builder made: a room (R tiles) or the base (S tiles). Each one has a
// trigger box over its tiles, so the night lockdown can check "is the player inside a room"
// with a trigger or with Contains(). RoomContent also puts each room's tank, note and labels
// under its room's MazeArea, so zone 2's rooms carry them along when zone 2 turns.
//
// DESIGN RULES: R8.11 (rooms are 5 × 5 safe areas), R8.21 (rooms and the hub are safe
// during the lockdown).
//
// HOW TO USE: the MazeBuilder adds it ("Room_N" and "Base" objects). Don't add it by hand.
// HOW TO EDIT: nothing to tune; the tiles come from the CSV layout.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>A room or the hub base, with the tiles it covers (CSV / 0 degree tiles).</summary>
    public class MazeArea : MonoBehaviour
    {
        /// <summary>Room (R tiles) or Base (S tiles).</summary>
        public enum Kind { Room, Base }

        [Tooltip("Room (R tiles) or Base (S tiles). Set by the MazeBuilder.")]
        public Kind kind;

        [Tooltip("The tiles this area covers (x = column, y = row from the top).")]
        public RectInt tiles;

        /// <summary>True if this CSV (0 degree) tile is inside the area. For zone 2, turn a world tile back with MazeGrid.WorldToLocal first.</summary>
        public bool Contains(Vector2Int tile) => tiles.Contains(tile);
    }
}
