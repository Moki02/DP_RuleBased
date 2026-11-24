// ============================================================================
// UiButtonSounds.cs
// Gives every Button in the scene a click and a hover sound, including buttons made later
// from prefabs (the Build menu cards, the map, pop-ups). Disabled buttons stay silent.
// Nothing on the UI prefabs is changed: a tiny UiButtonSound helper is added to each
// button while the game runs.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: on the "GameFeel" object in each scene (Salok > Feel > Install Game Feel).
// To keep one button silent, add a UiButtonSound to it yourself and tick "Silent".
// HOW TO EDIT: which clips and how loud: GameFeelLibrary (UI Click, UI Hover).
// ============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Adds click / hover sounds to every Button in the scene.</summary>
    public class UiButtonSounds : MonoBehaviour
    {
        [Tooltip("Which sounds to use.")]
        public GameFeelLibrary library;
        [Tooltip("Seconds between looks for new buttons (real time).")]
        [Min(0.1f)] public float scanEvery = 0.5f;

        /// <summary>The library the button helpers read.</summary>
        public static GameFeelLibrary Current { get; private set; }

        float timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void OnEnable()
        {
            if (library != null) Current = library;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = scanEvery;
            if (library != null) Current = library;
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.GetComponent<UiButtonSound>() == null) b.gameObject.AddComponent<UiButtonSound>();
        }
    }
}
