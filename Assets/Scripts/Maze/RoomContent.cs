// ============================================================================
// RoomContent.cs
// What is inside the maze's rooms: each room gets a fixed-seed outcome (empty, cistern or
// cache), a simple object in its middle that fills the canteen, a survivor note, and an
// icon on the map once the player has walked in.
//
// WHAT IT DOES
//   - At the start of a new game, rolls every room once with GameBalance.roomSeed (R8.12),
//     with odds by the room's distance d from the hub (R8.13), unless the designer has
//     overridden that room. The outcomes are saved.
//   - Puts a primitive in the middle of each room, under the room's own object, so zone 2's
//     rooms turn with zone 2 (MazeBuilder builds each room as "Room_N" with a MazeArea, and
//     zone 2's rooms sit under Zone2Pivot):
//       cistern = blue cylinder, cache = sealed jug (small orange cylinder), empty = grey
//       crate ("remains"). Each has a TextMeshPro label with the litres left.
//     Hold E next to a cistern or cache to fill the canteen (1 L/s, R8.5: only what fits).
//   - Puts a survivor note (a small white box) one tile east of the middle (R8.7). Press E to
//     read it; the first read triggers its payload (R8.17) and adds it to the journal.
//   - Cisterns refill at dawn, scaled by the drought stage (R8.14).
//   - A room counts as found when the player stands on one of its tiles (R8.18); found rooms
//     show on the map as an icon: C = cistern, J = water jug (cache), E = empty.
//   - The journal (J or the Journal button on the map screen) lists every note read.
//
// DESIGN RULES: R8.5, R8.7, R8.11 to R8.18, R1.3 (water counted with RunStats.AddCollected).
//
// HOW TO USE: nothing. It adds itself to the game scene (AutoSystems) and finds the maze,
// the map and the clock. It needs the maze built with "Split For Zone 2 Rotation" (the
// playtest scene) so zone 2's rooms turn; without it everything still works but stays put.
// HOW TO EDIT: numbers are in GameBalance ("Maze content: rooms"). The notes, colours and
// label texts are in this component's Inspector (on the Systems object while playing) or in
// RoomNotes.cs.
// ============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The maze rooms: outcomes, water, notes, journal and map icons.</summary>
    public class RoomContent : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<RoomContent>();

        [Header("Notes (R8.7, R8.17): one per room number")]
        [Tooltip("Survivor notes by room number (see the table in RoomNotes.cs).")]
        public RoomNote[] notes = RoomNotes.Defaults();

        [Header("Look")]
        [Tooltip("Cistern colour (the tank and its map icon).")] public Color cisternColor = new Color32(70, 140, 220, 255);
        [Tooltip("Cache (sealed jug) colour.")] public Color cacheColor = new Color32(230, 150, 60, 255);
        [Tooltip("Empty room's remains and its map icon.")] public Color emptyColor = new Color32(150, 145, 140, 255);
        [Tooltip("The survivor note.")] public Color noteColor = new Color32(240, 235, 220, 255);
        [Tooltip("Label text colour.")] public Color labelColor = Color.white;
        [Tooltip("Label size (TextMeshPro font size in the world; about 0.1 m per point).")]
        public float labelSize = 3.5f;
        [Tooltip("How far from the room's middle the note lies, in tiles (east, before zone 2 turns).")]
        public float noteOffsetTiles = 1f;

        [Header("Text (what the player reads)")]
        [Tooltip("{0} = litres left, {1} = size.")] public string cisternLabel = "Cistern  {0:0.0} / {1:0.0} L\n<size=70%>refills at dawn</size>";
        [Tooltip("{0} = litres left.")] public string cacheLabel = "Sealed jug  {0:0.0} L";
        public string cacheEmptyLabel = "Empty jug";
        public string emptyLabel = "Remains\n<size=70%>no water here</size>";
        public string noteLabel = "Survivor note";
        public string noteReadLabel = "Survivor note (read)";
        public string foundToast = "Found a room. Rooms are safe during the lockdown.";
        public string journalTitle = "JOURNAL";
        public string journalEmpty = "No notes read yet. Every room has one.";

        /// <summary>The one in this scene (null if there is none).</summary>
        public static RoomContent Instance { get; private set; }

        /// <summary>One room's state.</summary>
        class Room
        {
            public RoomInfo info;
            public float d;               // 0 nearest the hub, 1 farthest (R8.13)
            public RoomOutcome outcome;
            public float capacity;        // litres when full
            public float litres;          // litres left
            public bool found;
            public bool noteRead;
            public Transform holder;      // "RoomContent" object under the room
            public WorldLabel waterLabel, noteLabelObj;
        }

        Room[] rooms;
        readonly List<int> readOrder = new List<int>();   // room numbers, in the order their notes were read
        int cisternRefillDay;
        bool ready;
        SaveData pendingRestore;

        GameBalance balance;
        MazeBuilder maze;
        MapRevealTracker tracker;
        MapScreen map;
        GameClock_Manager clock;
        NotePanel panel;
        float labelTimer;

        // ------------------------------------------------------------------ Setup

        void Start()
        {
            Instance = this;
            balance = BalanceProvider.Current;
            maze = FindAnyObjectByType<MazeBuilder>();
            tracker = FindAnyObjectByType<MapRevealTracker>();
            map = FindAnyObjectByType<MapScreen>();
            panel = gameObject.AddComponent<NotePanel>();

            clock = GameClock_Manager.Instance;
            if (clock != null) clock.NewDay += OnNewDay;
            if (map != null)
            {
                map.JournalRequested += ToggleJournal;
                map.extraIcons = RoomIcons;
            }
            TrySetup();
        }

        void OnDestroy()
        {
            if (clock != null) clock.NewDay -= OnNewDay;
            if (map != null)
            {
                map.JournalRequested -= ToggleJournal;
                if (map.extraIcons == (System.Func<IEnumerable<MapIcon>>)RoomIcons) map.extraIcons = null;
            }
            if (Instance == this) Instance = null;
        }

        // Builds the rooms once the maze exists (the builder may build it in its own Start).
        bool TrySetup()
        {
            if (ready) return true;
            if (balance == null || maze == null || maze.Grid == null) return false;
            var grid = maze.Grid;
            var infos = grid.Rooms;
            if (infos.Count > 0 && FindArea(infos[0]) == null && Time.frameCount < 10) return false; // give the builder a moment

            // R8.13: d by ring distance from the hub centre, 0 for the nearest room and 1 for the farthest.
            int nearest = int.MaxValue, farthest = int.MinValue;
            foreach (var r in infos)
            {
                int ring = grid.Ring(CentreTile(r));
                nearest = Mathf.Min(nearest, ring);
                farthest = Mathf.Max(farthest, ring);
            }

            rooms = new Room[infos.Count];
            for (int i = 0; i < infos.Count; i++)
            {
                var room = new Room { info = infos[i] };
                room.d = RoomRules.Distance01(grid.Ring(CentreTile(infos[i])), nearest, farthest);
                rooms[i] = room;
            }
            ready = true;
            NewGameState();
            BuildObjects();

            Debug.Log($"[RoomContent] {rooms.Length} rooms: " + Describe(), this);

            if (pendingRestore != null)
            {
                var data = pendingRestore;
                pendingRestore = null;
                Restore(data);
            }
            return true;
        }

        // A new game: roll every room (R8.12) and fill it.
        void NewGameState()
        {
            readOrder.Clear();
            cisternRefillDay = WorldState.Day;
            float refill = RoomRules.RefillFactor(balance, WorldState.StageIndex);
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                SetOutcome(r, RoomRules.Decide(balance, i, r.d));
                r.litres = r.outcome == RoomOutcome.Cistern ? r.capacity * refill : r.capacity;
                r.found = false;
                r.noteRead = false;
            }
        }

        void SetOutcome(Room r, RoomOutcome outcome)
        {
            r.outcome = outcome;
            r.capacity = RoomRules.Capacity(balance, outcome, r.d);
        }

        static Vector2Int CentreTile(RoomInfo r) =>
            new Vector2Int(r.Tiles.xMin + r.Tiles.width / 2, r.Tiles.yMin + r.Tiles.height / 2);

        string Describe()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rooms.Length; i++)
                sb.Append($"{i} {rooms[i].outcome} (d {rooms[i].d:0.00}, {rooms[i].capacity:0.0} L)  ");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ World objects

        // The MazeArea the builder made for this room (same tile rectangle).
        MazeArea FindArea(RoomInfo info)
        {
            foreach (var a in maze.Areas)
                if (a != null && a.kind == MazeArea.Kind.Room && a.tiles.Equals(info.Tiles)) return a;
            return null;
        }

        void BuildObjects()
        {
            float tile = maze.TileSize;
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                var area = FindArea(r.info);
                var holder = new GameObject("RoomContent").transform;
                if (area != null)
                {
                    // Under the room's own object: zone 2's rooms are under Zone2Pivot, so this turns with them.
                    holder.SetParent(area.transform, false);
                    holder.localPosition = Vector3.zero;   // the area sits on the room's middle, at floor level
                }
                else
                {
                    Debug.LogWarning($"[RoomContent] No room object for room {i} at {r.info.Tiles}; its things won't turn with zone 2.", this);
                    holder.SetParent(maze.transform, true);
                    Vector2Int c = CentreTile(r.info);
                    holder.position = maze.GridToWorld(c.x, c.y);
                }
                holder.name = $"RoomContent_{i}";
                r.holder = holder;

                // The water (or the remains) in the middle.
                GameObject thing;
                switch (r.outcome)
                {
                    case RoomOutcome.Cistern:
                        thing = Primitive(PrimitiveType.Cylinder, "Cistern", holder, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 0.6f, 1.6f), cisternColor);
                        break;
                    case RoomOutcome.Cache:
                        thing = Primitive(PrimitiveType.Cylinder, "SealedJug", holder, new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.4f, 0.6f), cacheColor);
                        break;
                    default:
                        thing = Primitive(PrimitiveType.Cube, "Remains", holder, new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.6f, 0.9f), emptyColor);
                        break;
                }
                if (r.outcome != RoomOutcome.Empty)
                {
                    var src = thing.AddComponent<RoomWaterSource>();
                    src.owner = this;
                    src.roomIndex = i;
                    src.proximityRange = balance.interactRange;
                }
                r.waterLabel = WorldLabel.Create(holder, new Vector3(0f, 2.2f, 0f), labelSize, labelColor);

                // The note, one tile to the side, so its E doesn't fight with the tank's E.
                Vector3 notePos = new Vector3(noteOffsetTiles * tile, 0f, 0f);
                var note = Primitive(PrimitiveType.Cube, "SurvivorNote", holder, notePos + new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.05f, 0.35f), noteColor);
                var stand = Primitive(PrimitiveType.Cube, "NoteCrate", holder, notePos + new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.7f), emptyColor);
                stand.transform.SetAsFirstSibling();
                var reader = note.AddComponent<RoomNoteObject>();
                reader.owner = this;
                reader.roomIndex = i;
                reader.proximityRange = balance.interactRange;
                r.noteLabelObj = WorldLabel.Create(holder, notePos + new Vector3(0f, 1.4f, 0f), labelSize * 0.8f, labelColor);
            }
            RefreshLabels();
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
            return go;
        }

        void RefreshLabels()
        {
            if (!ready) return;
            foreach (var r in rooms)
            {
                if (r.waterLabel != null)
                {
                    switch (r.outcome)
                    {
                        case RoomOutcome.Cistern: r.waterLabel.Set(string.Format(cisternLabel, r.litres, r.capacity)); break;
                        case RoomOutcome.Cache: r.waterLabel.Set(r.litres > 0.0001f ? string.Format(cacheLabel, r.litres) : cacheEmptyLabel); break;
                        default: r.waterLabel.Set(emptyLabel); break;
                    }
                }
                if (r.noteLabelObj != null) r.noteLabelObj.Set(r.noteRead ? noteReadLabel : noteLabel);
            }
        }

        // ------------------------------------------------------------------ Every frame

        void Update()
        {
            if (!ready && !TrySetup()) return;
            CheckFound();

            labelTimer -= Time.unscaledDeltaTime;
            if (labelTimer <= 0f)
            {
                labelTimer = 0.2f;
                RefreshLabels();
            }
        }

        // R8.18: a room is found when the player stands on one of its tiles.
        void CheckFound()
        {
            var player = Player_Manager.instance;
            if (player == null || maze.Grid == null) return;
            Vector2Int world = maze.WorldToGrid(player.transform.position);
            Vector2Int local = maze.Grid.WorldToLocal(world);   // rooms are stored in CSV tiles
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                if (r.found || !r.info.Tiles.Contains(local)) continue;
                r.found = true;
                Debug.Log($"[RoomContent] Room {i} found ({r.outcome}).", this);
                SimpleHud.Toast(foundToast, 3f);
            }
        }

        // ------------------------------------------------------------------ Water (R8.5, R8.14, R8.15)

        /// <summary>Litres left in a room's cistern or cache (0 for an empty room).</summary>
        public float LitresIn(int roomIndex) =>
            ready && roomIndex >= 0 && roomIndex < rooms.Length ? rooms[roomIndex].litres : 0f;

        /// <summary>
        /// Hold E (RoomWaterSource): moves up to roomFillRate × seconds litres into the canteen.
        /// Only what fits goes in; the rest stays in the room (R8.5). Returns the litres moved.
        /// </summary>
        public float FillCanteenFrom(int roomIndex, float seconds)
        {
            var res = GameResources_Manager.instance;
            if (!ready || res == null || roomIndex < 0 || roomIndex >= rooms.Length) return 0f;
            var r = rooms[roomIndex];
            float room = Mathf.Max(0f, res.canteen_Max - res.canteen);
            float wanted = Mathf.Min(balance.roomFillRate * Skills.F("drinkPourRateMult", 1f) * seconds, r.litres, room); // M4 Quick Hands
            if (wanted <= 0f) return 0f;
            float added = res.AddCanteenWater(wanted);
            r.litres = Mathf.Max(0f, r.litres - added);
            RunStats.AddCollected(added);   // R1.3: water entering the game is counted
            if (r.outcome == RoomOutcome.Cache && r.litres <= 0.0001f)
                SimpleHud.Toast("The jug is empty.", 2f);
            return added;
        }

        // R8.14: at dawn every cistern refills to its size × the stage factor.
        void OnNewDay(int day)
        {
            if (!ready) return;
            RefillCisterns(day);
        }

        void RefillCisterns(int day)
        {
            if (day <= cisternRefillDay) return;   // already refilled today (or a save from this dawn)
            cisternRefillDay = day;
            float factor = RoomRules.RefillFactor(balance, WorldState.StageIndex);
            foreach (var r in rooms)
                if (r.outcome == RoomOutcome.Cistern)
                    r.litres = Mathf.Max(r.litres, r.capacity * factor);
            RefreshLabels();
        }

        // ------------------------------------------------------------------ Notes (R8.7, R8.17)

        /// <summary>True if the room's note has been read.</summary>
        public bool IsNoteRead(int roomIndex) =>
            ready && roomIndex >= 0 && roomIndex < rooms.Length && rooms[roomIndex].noteRead;

        RoomNote NoteFor(int roomIndex) =>
            notes != null && roomIndex >= 0 && roomIndex < notes.Length && notes[roomIndex] != null
                ? notes[roomIndex] : RoomNotes.Fallback(roomIndex);

        /// <summary>Press E on a note: shows it. The first read also triggers its payload and adds it to the journal.</summary>
        public void ReadNote(int roomIndex)
        {
            if (!ready || roomIndex < 0 || roomIndex >= rooms.Length) return;
            var r = rooms[roomIndex];
            var note = NoteFor(roomIndex);
            string extra = "";
            if (!r.noteRead)
            {
                r.noteRead = true;
                readOrder.Add(roomIndex);
                extra = ApplyPayload(roomIndex, note);
                RefreshLabels();
            }
            string body = note.text + (string.IsNullOrEmpty(extra) ? "" : "\n\n<i>" + extra + "</i>");
            panel.OpenNote(note.title, body);
        }

        // R8.17: Reveal, Marker or Clue. Returns a line for the player, or "".
        string ApplyPayload(int roomIndex, RoomNote note)
        {
            var reveal = tracker != null ? tracker.Reveal : null;
            switch (note.payload)
            {
                case NotePayload.Reveal:
                {
                    int target = note.targetRoom;
                    if (reveal == null || target < 0 || target >= rooms.Length || target == roomIndex) return "";
                    // Room tiles are CSV (local) tiles, so a zone 2 room is revealed wherever it is now.
                    var newTiles = new List<Vector2Int>();
                    var rect = rooms[target].info.Tiles;
                    for (int y = rect.yMin; y < rect.yMax; y++)
                    for (int x = rect.xMin; x < rect.xMax; x++)
                        if (reveal.RevealLocal(new Vector2Int(x, y))) newTiles.Add(new Vector2Int(x, y));
                    // A note reveal can complete a section or a milestone (R8.2, R8.3). Only new tiles count,
                    // so after debug key 0 nothing pays.
                    if (newTiles.Count > 0 && MapRewards.Instance != null) MapRewards.Instance.CheckTiles(newTiles);
                    return "Another room now shows on your map (M).";
                }
                case NotePayload.Marker:
                {
                    int target = note.targetRoom >= 0 && note.targetRoom < rooms.Length ? note.targetRoom : NearestUnfoundCistern(roomIndex);
                    if (reveal == null || target < 0) return "";
                    Vector2Int tile = CentreTile(rooms[target].info);   // CSV tile: the marker turns with zone 2
                    foreach (var m in reveal.MarkersLocal)
                        if (m == tile) return "That cistern is already marked on your map.";
                    if (reveal.MarkersLocal.Count >= tracker.MaxMarkers)
                        return "Your map has no room for another marker (F removes one), so the cistern wasn't marked.";
                    reveal.AddMarkerLocal(tile);
                    return "A cistern room is now marked on your map (M).";
                }
                default:
                    return "";
            }
        }

        // The cistern room (not this one) the player hasn't found yet, nearest the hub. If every one is found, any other cistern.
        int NearestUnfoundCistern(int exceptRoom)
        {
            int best = -1;
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                for (int i = 0; i < rooms.Length; i++)
                {
                    var r = rooms[i];
                    if (i == exceptRoom || r.outcome != RoomOutcome.Cistern) continue;
                    if (pass == 0 && r.found) continue;
                    if (best < 0 || r.d < rooms[best].d) best = i;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ Journal and map icons

        void ToggleJournal()
        {
            if (!ready) return;
            var sb = new StringBuilder();
            int mapped = MapRewards.Instance != null ? MapRewards.Instance.SectionsMappedCount : 0;
            int total = MapRewards.Instance != null && MapRewards.Instance.Sections != null ? MapRewards.Instance.Sections.Count : 0;
            sb.Append($"<size=80%>Notes read {readOrder.Count} / {rooms.Length}     Sections mapped {mapped} / {total}     " +
                      "Map icons: C cistern, J water jug, E empty room</size>\n\n");
            if (readOrder.Count == 0) sb.Append(journalEmpty);
            foreach (int i in readOrder)
            {
                var note = NoteFor(i);
                sb.Append("<b>").Append(note.title).Append("</b>\n").Append(note.text).Append("\n\n");
            }
            panel.ToggleJournal(journalTitle, sb.ToString());
        }

        // R8.18: an icon for every found room, at its middle tile as it is now (zone 2 rooms turn).
        IEnumerable<MapIcon> RoomIcons()
        {
            if (!ready || maze == null || maze.Grid == null) yield break;
            foreach (var r in rooms)
            {
                if (!r.found) continue;
                Vector2Int world = maze.Grid.LocalToWorld(CentreTile(r.info));
                switch (r.outcome)
                {
                    case RoomOutcome.Cistern: yield return new MapIcon(world, cisternColor, "C"); break;
                    case RoomOutcome.Cache: yield return new MapIcon(world, cacheColor, "J"); break;
                    default: yield return new MapIcon(world, emptyColor, "E"); break;
                }
            }
        }

        // ------------------------------------------------------------------ Saving (R11.8)

        /// <summary>Restored after the map (RestoreOrders.Map = 50) and the clock.</summary>
        public int RestoreOrder => RestoreOrders.Map + 5;

        /// <summary>Saves every room's outcome, litres, found and note-read state, and the last refill day.</summary>
        public void Capture(SaveData data)
        {
            if (!ready) return;
            data.hasRooms = true;
            data.roomOutcomes.Clear();
            data.roomLitres.Clear();
            data.roomsFound.Clear();
            for (int i = 0; i < rooms.Length; i++)
            {
                data.roomOutcomes.Add((int)rooms[i].outcome);
                data.roomLitres.Add(rooms[i].litres);
                if (rooms[i].found) data.roomsFound.Add(i);
            }
            data.notesRead.Clear();
            data.notesRead.AddRange(readOrder);
            data.cisternRefillDay = cisternRefillDay;
        }

        /// <summary>Puts the rooms back. An older save without this part starts the rooms fresh.</summary>
        public void Restore(SaveData data)
        {
            if (!ready) { pendingRestore = data; return; }
            NewGameState();
            if (data.hasRooms)
            {
                if (data.roomOutcomes.Count != rooms.Length)
                    Debug.LogWarning($"[RoomContent] The save has {data.roomOutcomes.Count} rooms but the maze has {rooms.Length}; " +
                                     "rooms that don't match keep their new-game state.", this);
                for (int i = 0; i < rooms.Length && i < data.roomOutcomes.Count; i++)
                {
                    // R8.12: the roll uses a fixed seed, so the saved outcome is the same as the roll; it's
                    // saved as a check. If they differ (the designer changed the seed or an override after
                    // saving), the roll is used, because the room objects were built for it.
                    var saved = (RoomOutcome)data.roomOutcomes[i];
                    if (saved != rooms[i].outcome)
                        Debug.LogWarning($"[RoomContent] Room {i}: saved as {saved} but rolls {rooms[i].outcome} now (seed or override changed). Using the roll.", this);
                    else if (i < data.roomLitres.Count)
                        rooms[i].litres = Mathf.Clamp(data.roomLitres[i], 0f, rooms[i].capacity);
                }
                foreach (int i in data.roomsFound) if (i >= 0 && i < rooms.Length) rooms[i].found = true;
                foreach (int i in data.notesRead)
                {
                    if (i < 0 || i >= rooms.Length || rooms[i].noteRead) continue;
                    rooms[i].noteRead = true;   // no payload on load: it was applied (and saved with the map) back then
                    readOrder.Add(i);
                }
                cisternRefillDay = data.cisternRefillDay;
            }
            // A dawn autosave may have been written before the dawn refill: refill now if today's is missing.
            RefillCisterns(WorldState.Day);
            RefreshLabels();
        }
    }
}
