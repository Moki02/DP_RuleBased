// ============================================================================
// GateSlider.cs
// Makes a gate prefab open by sliding its panel sideways into the wall, instead of the whole
// gate vanishing. Put it on the gate prefab's root (the Salok environment kit does this) and
// name the moving part "Leaf".
//
// LockBasin_Manager calls SetShut(true/false) when the gate opens, at night and at dawn.
// The leaf's colliders are off while the gate is open, so the corridor is walkable.
//
// HOW TO EDIT: Slide Distance (metres, usually one tile), Slide Direction (in the gate's own
// space; +X slides to the right as seen from the front) and Slide Seconds.
// Events are there for sounds and dust later.
// ============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace TheLastDrop
{
    /// <summary>A gate whose panel ("Leaf") slides sideways into the wall to open.</summary>
    public class GateSlider : MonoBehaviour
    {
        [Tooltip("The moving panel. Empty = the child named \"Leaf\".")]
        public Transform leaf;
        [Tooltip("How far the panel slides, in metres (one tile = fully out of the corridor).")]
        public float slideDistance = 4f;
        [Tooltip("Which way it slides, in the gate's own space.")]
        public Vector3 slideDirection = Vector3.right;
        [Tooltip("Seconds to open or shut.")]
        [Min(0.01f)] public float slideSeconds = 1.4f;

        [Header("Events (sounds later)")]
        public UnityEvent onStartOpening;
        public UnityEvent onStartClosing;

        /// <summary>True while the gate is (or is moving to) shut.</summary>
        public bool IsShut { get; private set; } = true;

        Vector3 closedPos;
        Collider[] leafColliders;
        float shown;      // 0 = shut, 1 = open
        bool ready;

        void Awake() => Setup();

        void Setup()
        {
            if (ready) return;
            if (leaf == null) leaf = transform.Find("Leaf");
            if (leaf == null) leaf = transform;
            closedPos = leaf.localPosition;
            // Only the colliders that are on (art pieces' own colliders are switched off in the kit).
            leafColliders = System.Array.FindAll(leaf.GetComponentsInChildren<Collider>(true), c => c.enabled && !c.isTrigger);
            ready = true;
        }

        /// <summary>Shut (true) or open (false). Instant skips the slide (loading a save).</summary>
        public void SetShut(bool shut, bool instant = false)
        {
            Setup();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (shut != IsShut)
            {
                if (shut) onStartClosing?.Invoke(); else onStartOpening?.Invoke();
            }
            IsShut = shut;
            if (instant)
            {
                shown = shut ? 0f : 1f;
                Apply();
            }
            SetColliders(shut);
        }

        void Update()
        {
            float target = IsShut ? 0f : 1f;
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime / slideSeconds);
            Apply();
        }

        void Apply()
        {
            float eased = Mathf.SmoothStep(0f, 1f, shown);
            leaf.localPosition = closedPos + slideDirection.normalized * slideDistance * eased;
        }

        void SetColliders(bool on)
        {
            if (leafColliders == null) return;
            foreach (var c in leafColliders)
                if (c != null && !c.isTrigger && c.enabled != on) c.enabled = on;
        }
    }
}
