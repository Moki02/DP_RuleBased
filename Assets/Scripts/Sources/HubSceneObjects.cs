// ============================================================================
// HubSceneObjects.cs
// Holds the hub objects that live in the scene (not made at Play): Raw Basin tiers, the
// Clean Storage tiers and its level gauge, the spare jugs, the Rain Catcher / Solar Still /
// Deep Well models, the Biosand Filter and their floating labels. Every upgrade tier is
// already placed; the game only turns on the one you own (R5.3), so you can see and move
// each of them in the editor.
//
// The scripts that run these objects (HubPlaceholders, WaterSources, BiosandFilter) read
// the links below. If a game scene has no HubObjects, they make one at Play (with a
// warning) so nothing breaks.
//
// HOW TO USE: Salok > Build > 2. Place hub objects in scene. It makes the "HubObjects"
// object and fills every link. Move or swap any model by hand afterwards; just keep it
// linked in a Tier below.
// HOW TO EDIT: a Tier is a name and the objects shown for it. Names the game looks for:
//   Raw Basin:     Basin, Bigger Basin, Concrete Basin
//   Clean Storage: Tapayan, Plastic Drum, Ferrocement Tank, Underground Cistern
//   Rain Catcher:  Normal, Wider, Bigger
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Links to the hub's scene objects (all tiers placed, only the owned one shown).</summary>
    public class HubSceneObjects : MonoBehaviour
    {
        /// <summary>Name of the scene object that holds this.</summary>
        public const string RootName = "HubObjects";

        /// <summary>One upgrade tier: its name and the objects shown while it is the owned one.</summary>
        [Serializable]
        public class Tier
        {
            public string name;
            public GameObject[] objects = new GameObject[0];
        }

        [Header("Raw Basin")]
        public Tier[] rawBasinTiers = new Tier[0];
        public FacingLabel rawBasinLabel;

        [Header("Clean Storage (tiers sit on the Clean Storage tap object)")]
        public Tier[] cleanStorageTiers = new Tier[0];
        [Tooltip("The blue bar of the level gauge. It is scaled in Y with the fill (pivot in the middle).")]
        public Transform cleanGaugeFill;
        [Tooltip("Height of the gauge in metres when full.")]
        public float cleanGaugeHeight = 1.5f;

        [Header("Spare jugs (one shown per few litres, in this order)")]
        public GameObject[] spareJugs = new GameObject[0];
        public FacingLabel spareJugsLabel;

        [Header("Water sources")]
        public Tier[] rainCatcherTiers = new Tier[0];
        public FacingLabel rainCatcherLabel;
        [Tooltip("First = shown with the Solar Still (W5), second = with the second still (W6).")]
        public GameObject[] solarStills = new GameObject[0];
        public FacingLabel solarStillLabel;
        public GameObject deepWell;
        public FacingLabel deepWellLabel;

        [Header("Purifier")]
        public GameObject biosandFilter;

        static HubSceneObjects instance;

        /// <summary>The scene's hub objects, or null if the scene has none.</summary>
        public static HubSceneObjects Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<HubSceneObjects>(FindObjectsInactive.Include);
                return instance;
            }
        }

        void Awake() => instance = this;
        void OnDestroy() { if (instance == this) instance = null; }

        /// <summary>Shows the objects of the tier called <paramref name="name"/> and hides every other tier (null = hide all).</summary>
        public static void ShowTier(Tier[] tiers, string name)
        {
            if (tiers == null) return;
            // Hide first, then show, so an object listed in two tiers ends up shown.
            foreach (var t in tiers)
                if (t != null && t.name != name) SetActive(t.objects, false);
            foreach (var t in tiers)
                if (t != null && t.name == name) SetActive(t.objects, true);
        }

        /// <summary>Turns a label's object on or off (null-safe).</summary>
        public static void ShowLabel(FacingLabel label, bool on)
        {
            if (label != null && label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
        }

        static void SetActive(GameObject[] objects, bool on)
        {
            if (objects == null) return;
            foreach (var go in objects)
                if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
