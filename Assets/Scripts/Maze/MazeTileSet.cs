// MazeTileSet.cs
// The art for one zone. Make one per zone (Create > Dry Labyrinth > Maze Tile Set) and drop
// it on the MazeBuilder. Any list left empty falls back to a ProBuilder grey-box piece, so
// the artists can swap pieces in one at a time.
//
// Two ways to build walls (Wall Mode):
//   Blocks - one prefab fills each wall tile (a solid 4 x 4 m block).
//   Faces  - thin wall panels are placed only on the sides of a wall tile that face a corridor.
//            This is how Synty modular walls work, and it's much lighter (no hidden insides).
//            Optional pillars hide the seams at corners.
//
// Kitbashing rules for artists (Synty or anything else):
//   - Build each kitbash under one empty parent and save that parent as a prefab.
//   - Faces mode: a panel's FRONT (the side players see) must face +Z (blue arrow forward).
//   - Exact size and pivot don't matter if "Align To Bounds" is on and "Fit" is set: the
//     builder measures the mesh, scales it to the tile, and lines it up itself.
//   - Use the "Check tile set" button on the MazeBuilder to see each prefab's measured size.
//   - Colliders: the builder makes its own clean wall collision, so kitbash colliders are
//     switched off by default (MazeBuilder > Collision). Gates and doors keep theirs.

using System;
using UnityEngine;

namespace DryLabyrinth.Maze
{
    public enum WallMode { Blocks, Faces }

    public enum FitMode
    {
        None,    // use the prefab at its own size
        Stretch, // stretch the width (and depth for blocks and floors) to exactly one tile
        Uniform  // scale evenly so the width is exactly one tile, keeping proportions
    }

    [Serializable]
    public class MazePiece
    {
        public GameObject prefab;

        [Tooltip("How often this one is picked compared with the others in the same list.")]
        [Min(0.01f)] public float weight = 1f;

        [Tooltip("Scale the piece to fit one tile.")]
        public FitMode fit = FitMode.Stretch;

        [Tooltip("Ignore the prefab's pivot: place it by its measured mesh bounds (centred, sitting on the floor).")]
        public bool alignToBounds = true;

        [Tooltip("Extra nudge after placing, in the piece's own space (Z = forward).")]
        public Vector3 positionOffset;

        [Tooltip("Extra turn around Y in degrees, if the model faces the wrong way.")]
        public float yRotation;

        [Tooltip("Extra scale on top of Fit. (1, 1, 1) = none.")]
        public Vector3 scale = Vector3.one;

        public bool IsValid => prefab != null;
        public float Weight => weight > 0f ? weight : 1f;
        public Vector3 Scale => scale == Vector3.zero ? Vector3.one : scale; // new list entries can start at zero
    }

    [CreateAssetMenu(fileName = "MazeTileSet", menuName = "Dry Labyrinth/Maze Tile Set")]
    public class MazeTileSet : ScriptableObject
    {
        [Header("Walls")]
        public WallMode wallMode = WallMode.Faces;

        [Tooltip("Blocks mode: pieces that fill a whole wall tile.")]
        public MazePiece[] wallBlocks = new MazePiece[0];

        [Tooltip("Blocks mode: turn each block by 0/90/180/270 degrees (picked per tile) for variety.")]
        public bool rotateBlocks = false;

        [Tooltip("Faces mode: wall panels, front facing +Z. One is placed on each side of a wall tile that faces a corridor.")]
        public MazePiece[] wallFaces = new MazePiece[0];

        [Tooltip("Faces mode, optional: placed on the centre of every wall tile (a top cap or filler). Use Position Offset Y to lift it.")]
        public MazePiece[] wallCaps = new MazePiece[0];

        [Header("Pillars (Faces mode, optional)")]
        [Tooltip("Pillars or trims placed on wall corners to hide seams between panels.")]
        public MazePiece[] pillars = new MazePiece[0];
        [Tooltip("Corners that stick out into a corridor.")]
        public bool pillarsOnOuterCorners = true;
        [Tooltip("Corners where a corridor turns (inside corners).")]
        public bool pillarsOnInnerCorners = false;

        [Header("Gates and doors")]
        [Tooltip("G tiles: zone exits, hub gates, room gates. Modelled for a corridor running along Z.")]
        public MazePiece[] gates = new MazePiece[0];
        [Tooltip("D tiles. Room and hallway doors use the same pieces. Modelled for a corridor running along Z.")]
        public MazePiece[] doors = new MazePiece[0];

        [Header("Floors (optional)")]
        [Tooltip("Placed on every walkable tile. Empty = one big grey-box floor.")]
        public MazePiece[] floors = new MazePiece[0];
        [Tooltip("Room tiles (R). Empty = the normal floor.")]
        public MazePiece[] roomFloors = new MazePiece[0];
        [Tooltip("Base tiles (S). Empty = the normal floor.")]
        public MazePiece[] spawnFloors = new MazePiece[0];

        public static bool HasAny(MazePiece[] list)
        {
            if (list == null) return false;
            foreach (var p in list) if (p != null && p.IsValid) return true;
            return false;
        }

        /// <summary>Stable hash of a tile position, so every build gives the same result.</summary>
        public static int Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & int.MaxValue;
            }
        }

        /// <summary>Weighted pick that always gives the same piece for the same hash.</summary>
        public static MazePiece Pick(MazePiece[] list, int hash)
        {
            if (list == null) return null;
            float total = 0f;
            foreach (var p in list) if (p != null && p.IsValid) total += p.Weight;
            if (total <= 0f) return null;

            float r = (hash % 10007) / 10007f * total;
            MazePiece last = null;
            foreach (var p in list)
            {
                if (p == null || !p.IsValid) continue;
                last = p;
                r -= p.Weight;
                if (r < 0f) return p;
            }
            return last;
        }
    }
}
