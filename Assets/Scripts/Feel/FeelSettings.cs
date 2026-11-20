// ============================================================================
// FeelSettings.cs
// One asset that holds every "game feel" tuning number: screen shake, UI punch, number
// roll-up, floating numbers, the low-HYD vignette, the collapse fade and the minigame hit
// flash / hit-stop. Nothing here changes a game rule; it only changes how things look and feel.
//
// DESIGN RULES: none (feel only, no rule numbers). Does not touch GameBalance.
//
// HOW TO USE: the asset lives at Assets/VFX/Resources/FeelSettings.asset (made by
// Salok > Feel > Build Feel Prefabs). Every Feel script reads FeelSettings.Active, so there is
// nothing to drag in. If the asset is missing the defaults below are used.
// HOW TO EDIT: select the asset and change the numbers. Hover a field for what it does.
// They update while the game runs (no restart needed).
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>All tuning numbers for game feel (shake, punch, floating numbers, overlays, hit feedback).</summary>
    [CreateAssetMenu(menuName = "Salok/Feel Settings", fileName = "FeelSettings")]
    public class FeelSettings : ScriptableObject
    {
        /// <summary>Name of the asset inside a Resources folder.</summary>
        public const string ResourceName = "FeelSettings";

        static FeelSettings active;

        /// <summary>The settings in use: the asset in Resources, or the defaults if there is none.</summary>
        public static FeelSettings Active
        {
            get
            {
                if (active == null)
                {
                    active = Resources.Load<FeelSettings>(ResourceName);
                    if (active == null) active = CreateInstance<FeelSettings>();
                }
                return active;
            }
        }

        [Header("Screen shake")]
        [Tooltip("Is screen shake on when the player has never changed the setting? Players can turn it off (CameraShake.Enabled).")]
        public bool shakeOnByDefault = true;
        [Tooltip("How far the camera moves at full shake (metres). Keep it small: 0.1 to 0.3.")]
        [Range(0f, 1f)] public float shakeMaxOffset = 0.18f;
        [Tooltip("How much the camera tilts at full shake (degrees).")]
        [Range(0f, 10f)] public float shakeMaxAngle = 1.5f;
        [Tooltip("How fast the shake wobbles (higher = buzzier).")]
        [Range(1f, 60f)] public float shakeFrequency = 22f;
        [Tooltip("How much shake fades per second (1 = a full shake lasts one second).")]
        [Range(0.1f, 5f)] public float shakeDecayPerSecond = 1.6f;
        [Tooltip("Small shake (gate slam, door rising).")]
        [Range(0f, 1f)] public float shakeSmall = 0.25f;
        [Tooltip("Medium shake (thunder close by, collapse).")]
        [Range(0f, 1f)] public float shakeMedium = 0.45f;
        [Tooltip("Big shake (zone 2 turning).")]
        [Range(0f, 1f)] public float shakeBig = 0.7f;

        [Header("UI punch (a number or bar bounces when it changes)")]
        [Tooltip("How big it grows at the top of the bounce (1.2 = 20% bigger).")]
        [Range(1f, 2f)] public float punchScale = 1.18f;
        [Tooltip("How long the bounce lasts (seconds).")]
        [Range(0.05f, 1f)] public float punchDuration = 0.28f;

        [Header("Number roll-up (counters)")]
        [Tooltip("How long a counter takes to roll to its new value (seconds).")]
        [Range(0f, 3f)] public float rollDuration = 0.6f;

        [Header("Floating numbers (+1.5 L, -2 HYD, FULL)")]
        [Tooltip("How fast the text rises (metres per second).")]
        [Range(0f, 5f)] public float floatRiseSpeed = 0.9f;
        [Tooltip("How long the text stays (seconds).")]
        [Range(0.2f, 5f)] public float floatLifetime = 1.3f;
        [Tooltip("When it starts fading, as a part of its life (0.5 = fades in the second half).")]
        [Range(0f, 1f)] public float floatFadeStart = 0.55f;
        [Tooltip("Pop-in size at the start (1 = no pop).")]
        [Range(1f, 2f)] public float floatPopScale = 1.35f;
        [Tooltip("Random sideways spread so several numbers don't stack exactly (metres).")]
        [Range(0f, 1f)] public float floatSpread = 0.25f;
        [Tooltip("Colour for good things (+water). The project uses blue only as the water accent.")]
        public Color floatGoodColor = new Color(0.35f, 0.75f, 1f);
        [Tooltip("Colour for bad things (-HYD).")]
        public Color floatBadColor = new Color(1f, 0.45f, 0.4f);
        [Tooltip("Colour for neutral messages (FULL).")]
        public Color floatNeutralColor = Color.white;

        [Header("Low-HYD vignette")]
        [Tooltip("The vignette starts showing below this much HYD (0.3 = 30% of max).")]
        [Range(0f, 1f)] public float vignetteStartsAt = 0.3f;
        [Tooltip("How dark the edges get at 0 HYD (0 to 1).")]
        [Range(0f, 1f)] public float vignetteMaxAlpha = 0.8f;
        [Tooltip("Heartbeat pulse speed when the vignette first appears (beats per minute).")]
        [Range(20f, 200f)] public float vignetteBpmLow = 60f;
        [Tooltip("Heartbeat pulse speed at 0 HYD (beats per minute).")]
        [Range(20f, 200f)] public float vignetteBpmHigh = 120f;
        [Tooltip("How much the pulse adds on top (0 = no pulse).")]
        [Range(0f, 0.5f)] public float vignettePulse = 0.15f;

        [Header("Collapse fade")]
        [Tooltip("Seconds to fade to black.")]
        [Range(0f, 5f)] public float collapseFadeOut = 1.2f;
        [Tooltip("Seconds to stay black.")]
        [Range(0f, 5f)] public float collapseHold = 0.6f;
        [Tooltip("Seconds to fade back in.")]
        [Range(0f, 5f)] public float collapseFadeIn = 0.9f;

        [Header("Minigame hit feedback")]
        [Tooltip("How long the white flash lasts (seconds).")]
        [Range(0f, 0.5f)] public float flashDuration = 0.12f;
        [Tooltip("Flash colour.")]
        public Color flashColor = Color.white;
        [Tooltip("How long the game freezes on a hit (seconds, real time). 0 = no hit-stop.")]
        [Range(0f, 0.3f)] public float hitStopDuration = 0.06f;
        [Tooltip("Game speed during the hit-stop (0 = full freeze, 0.1 = very slow).")]
        [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;
        [Tooltip("How long the UI hit burst (ring) plays (seconds).")]
        [Range(0.05f, 2f)] public float burstDuration = 0.35f;
        [Tooltip("How big the ring grows (times its start size).")]
        [Range(1f, 5f)] public float burstScale = 2.2f;

        /// <summary>Lets the editor and tests use a specific settings object.</summary>
        public static void SetActive(FeelSettings settings) => active = settings;
    }
}
