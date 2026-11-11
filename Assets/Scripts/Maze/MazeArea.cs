// MazeArea.cs
// Marks a safe area the builder made: a room (R tiles) or the base (S tiles). Each one has a
// trigger box over its tiles, so the night lockdown can check "is the player inside a room"
// with a trigger or with Contains().

using UnityEngine;

namespace TheLastDrop
{
    public class MazeArea : MonoBehaviour
    {
        public enum Kind { Room, Base }

        public Kind kind;

        [Tooltip("The tiles this area covers (x = column, y = row from the top).")]
        public RectInt tiles;

        public bool Contains(Vector2Int tile) => tiles.Contains(tile);
    }
}
