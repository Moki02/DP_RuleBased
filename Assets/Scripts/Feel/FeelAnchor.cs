// ============================================================================
// FeelAnchor.cs
// Marks the exact spot a water effect comes out of: the hand pump's spout, the Clean
// Storage tap, or the spot above a lock basin where the poured water falls in.
// GameFeelDirector uses it if one is found under the object; without one it guesses from
// the object's size and the player's position (good enough, but less exact).
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: in the prefab (HandPump, Clean Storage, the lock basin), make an empty child
// named "Spout", add this component and move it to the mouth of the spout / tap. The blue
// line in the Scene view shows where the water will fall.
// HOW TO EDIT: just move / rotate the object. Water always falls straight down from it.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Where a water effect (pump gush, tap stream, basin pour) starts.</summary>
    public class FeelAnchor : MonoBehaviour
    {
        /// <summary>The anchor under this object (or on it), if there is one.</summary>
        public static FeelAnchor Find(Component owner)
        {
            return owner != null ? owner.GetComponentInChildren<FeelAnchor>(true) : null;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            Gizmos.color = new Color(0.3f, 0.7f, 1f);
            Gizmos.DrawWireSphere(p, 0.05f);
            float drop = Physics.Raycast(p + Vector3.down * 0.02f, Vector3.down, out var hit, 5f,
                                         ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 1.5f;
            Gizmos.DrawLine(p, p + Vector3.down * drop);
        }
#endif
    }
}
