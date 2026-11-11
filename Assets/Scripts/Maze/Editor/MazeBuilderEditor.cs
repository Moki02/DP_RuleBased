// ============================================================================
// MazeBuilderEditor.cs   (Editor only)
// Adds "Build maze", "Clear" and "Check tile set" buttons to the MazeBuilder Inspector, plus
// a summary of the layout so the designer can check the CSV was read correctly. Ticking the
// Gates / Doors boxes applies straight away, in the editor or in Play mode.
//
// DESIGN RULES: none of its own (MazeBuilder builds the maze from the layout CSV).
//
// HOW TO USE: select the Maze object; the buttons and summary appear under its settings.
// "Build maze" deletes and rebuilds everything under _Generated.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The MazeBuilder Inspector: layout summary and Build / Clear / Check buttons.</summary>
    [CustomEditor(typeof(MazeBuilder))]
    public class MazeBuilderEditor : Editor
    {
        /// <summary>Draws the normal settings, then the summary and buttons.</summary>
        public override void OnInspectorGUI()
        {
            var builder = (MazeBuilder)target;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                builder.ApplyTestToggles();
                MarkDirty(builder);
            }

            EditorGUILayout.Space();
            if (builder.layoutCsv != null && builder.LoadGrid())
            {
                var g = builder.Grid;
                EditorGUILayout.HelpBox(
                    $"{g.Width} x {g.Height} tiles = {g.Width * builder.TileSize:0} x {g.Height * builder.TileSize:0} m (tile {builder.TileSize} m)\n" +
                    $"Walls {g.Count(TileType.Wall)}   Floor {g.Count(TileType.Floor)}   Gates {g.Count(TileType.Gate)}   " +
                    $"Doors {g.Count(TileType.Door) + g.Count(TileType.TimedDoor)}   Valves {g.Count(TileType.Valve)}   " +
                    $"Room tiles {g.Count(TileType.Room)}   Base tiles {g.Count(TileType.Spawn)}\n" +
                    (builder.splitForZone2Rotation
                        ? $"Split mode: {g.Gates.Count} gates, {g.Valves.Count} valves, {g.Rooms.Count} rooms; zone 2 goes under Zone2Pivot.\n"
                        : "") +
                    (builder.tileSet == null
                        ? "No Tile Set: ProBuilder grey-box."
                        : $"Tile Set: {builder.tileSet.name}, {builder.tileSet.wallMode} mode (empty lists use grey-box).") +
                    (builder.gatesEnabled && builder.doorsEnabled ? "" : "\nTESTING: gates or doors are hidden."),
                    builder.gatesEnabled && builder.doorsEnabled ? MessageType.Info : MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Assign the zone's CSV (exported from Google Sheets) to Layout Csv.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox("Build replaces everything under _Generated. Keep props in a separate GameObject.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build maze", GUILayout.Height(28)))
                {
                    builder.Build();
                    MarkDirty(builder);
                }
                if (GUILayout.Button("Clear", GUILayout.Height(28), GUILayout.Width(70)))
                {
                    builder.Clear();
                    MarkDirty(builder);
                }
            }

            using (new EditorGUI.DisabledScope(builder.tileSet == null))
            {
                if (GUILayout.Button("Check tile set (logs each prefab's measured size)"))
                    Debug.Log(builder.DescribeTileSet(), builder.tileSet);
            }
        }

        // Outside Play mode, marks the scene as changed so Unity asks to save it.
        // (In Play mode changes are thrown away anyway.)
        static void MarkDirty(MazeBuilder builder)
        {
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }
    }
}
