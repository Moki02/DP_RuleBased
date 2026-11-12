// ============================================================================
// UpgradeAttachments.cs
// Shows things the player wears or carries once the matching skill or Build item is owned,
// by switching scene objects on and off by name. Right now:
//
//   E1 Salakot  ->  any object whose name contains "salakot" (the "Salakot_attachment"
//                   child of PlayerPrefab in Playtest_Maze), switched OFF until E1 is owned.
//
// It checks again whenever Build and Skills change (buying, loading a save, New Game), so
// a loaded save with E1 owned shows the hat straight away.
//
// DESIGN RULES: section 7 (E1 Salakot, a wide farmer's hat), R5.3 spirit (upgrades are visible).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems). Keep the
// attachment switched OFF in the prefab; this script turns it on.
// HOW TO EDIT: to add another one (e.g. S4 Hydration Pack -> "hydrationpack"), add a line to
// the Attachments list below. Names are matched without caring about upper/lower case.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Switches player attachments (like the salakot) on when their upgrade is owned.</summary>
    public class UpgradeAttachments : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<UpgradeAttachments>();

        /// <summary>Upgrade id -> part of the object name to show when it's owned (lower case).</summary>
        static readonly (string id, string namePart)[] Attachments =
        {
            ("E1", "salakot"),
        };

        UpgradeManager upgrades;
        readonly Dictionary<string, List<GameObject>> found = new Dictionary<string, List<GameObject>>();
        bool dirty = true;
        float nextSearch;

        void Start()
        {
            upgrades = UpgradeManager.Instance;
            if (upgrades != null) upgrades.Changed += OnChanged;
        }

        void OnDestroy()
        {
            if (upgrades != null) upgrades.Changed -= OnChanged;
        }

        void OnChanged() => dirty = true;

        void Update()
        {
            if (upgrades == null)
            {
                upgrades = UpgradeManager.Instance;
                if (upgrades != null) { upgrades.Changed += OnChanged; dirty = true; }
            }

            // Look for the objects until they're all found (the player may appear a bit later).
            if (found.Count < Attachments.Length && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                if (Search()) dirty = true;
            }

            if (!dirty) return;
            dirty = false;
            foreach (var (id, namePart) in Attachments)
            {
                if (!found.TryGetValue(namePart, out var list)) continue;
                bool owned = Skills.Owns(id);
                foreach (var go in list)
                    if (go != null && go.activeSelf != owned) go.SetActive(owned);
            }
        }

        // Finds every object (switched off ones too) whose name contains each name part.
        // Returns true if something new was found.
        bool Search()
        {
            bool any = false;
            // Look on the player first (so a menu or a prop with the same word is never touched);
            // only if there's no player yet, look through the whole scene.
            var player = Player_Manager.instance;
            var all = player != null
                ? player.transform.root.GetComponentsInChildren<Transform>(true)
                : FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var (_, namePart) in Attachments)
            {
                if (found.ContainsKey(namePart)) continue;
                var list = new List<GameObject>();
                foreach (var t in all)
                    if (t.name.ToLowerInvariant().Contains(namePart)) list.Add(t.gameObject);
                if (list.Count == 0) continue;
                found[namePart] = list;
                any = true;
            }
            return any;
        }
    }
}
