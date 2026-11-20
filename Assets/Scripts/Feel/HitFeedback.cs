// ============================================================================
// HitFeedback.cs
// The "crunch" for a good minigame hit: a quick white flash on a UI element, a tiny
// freeze of the game (hit-stop) and a ring burst. Each part can be used alone.
//
// DESIGN RULES: none (feel only; the minigame's timing rules are unchanged; the needle
// keeps its own clock if it uses real time).
//
// HOW TO USE: add HitFeedback anywhere (e.g. on the minigame panel) and fill in:
//   Flash Target: the UI image that flashes (the needle, the green zone, the dial).
//   Burst Prefab: Assets/VFX/Resources/Feel/UiHitBurst.prefab, and Burst Parent: where it appears.
// Then call Hit() for a hit and Miss() for a miss (from code or a UnityEvent such as
// ValveTimingGame.onHit / onMiss later; see Docs/Agents/GameFeel_Hookups.md).
// HitFeedback.DoHitStop() also works on its own from any script.
// HOW TO EDIT: flash length / colour, hit-stop length and burst size in FeelSettings >
// Minigame hit feedback.
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Flash, hit-stop and burst for minigame hits.</summary>
    public class HitFeedback : MonoBehaviour
    {
        [Tooltip("UI element that flashes on a hit (optional).")]
        public Graphic flashTarget;
        [Tooltip("UI ring burst prefab (UiHitBurst). Optional.")]
        public GameObject burstPrefab;
        [Tooltip("Where the burst appears (a RectTransform, e.g. the needle tip). Optional.")]
        public RectTransform burstParent;
        [Tooltip("Colour the flash target turns on a miss.")]
        public Color missColor = new Color(1f, 0.35f, 0.3f);
        [Tooltip("Also do a hit-stop on a hit.")]
        public bool hitStopOnHit = true;
        [Tooltip("Bounce this UI element on a hit (optional; needs a UiPunch).")]
        public UiPunch punchOnHit;

        Coroutine flashing;
        Color baseColor;
        bool hasBase;

        /// <summary>A good hit: flash, burst, punch and hit-stop.</summary>
        public void Hit()
        {
            Flash(FeelSettings.Active.flashColor);
            if (burstPrefab != null && burstParent != null)
                FeelPool.Spawn(burstPrefab, burstParent.position, Quaternion.identity, burstParent);
            if (punchOnHit != null) punchOnHit.Punch();
            if (hitStopOnHit) DoHitStop();
        }

        /// <summary>A miss: a red flash, no freeze.</summary>
        public void Miss() => Flash(missColor);

        /// <summary>Flashes the target in <paramref name="color"/> then returns to its own colour.</summary>
        public void Flash(Color color)
        {
            if (flashTarget == null || !isActiveAndEnabled) return;
            if (!hasBase) { baseColor = flashTarget.color; hasBase = true; }
            if (flashing != null) StopCoroutine(flashing);
            flashing = StartCoroutine(FlashRoutine(color));
        }

        IEnumerator FlashRoutine(Color color)
        {
            float length = FeelSettings.Active.flashDuration;
            float t = 0f;
            while (t < length)
            {
                t += Time.unscaledDeltaTime;
                flashTarget.color = Color.Lerp(color, baseColor, t / length);
                yield return null;
            }
            flashTarget.color = baseColor;
            flashing = null;
        }

        void OnDisable()
        {
            if (hasBase && flashTarget != null) flashTarget.color = baseColor;
            flashing = null;
        }

        /// <summary>
        /// Freezes (or slows) the game for FeelSettings.hitStopDuration seconds of real time.
        /// Skipped while the game is paused (timeScale 0). Always puts the speed back.
        /// </summary>
        public static void DoHitStop()
        {
            var s = FeelSettings.Active;
            HitStop.Play(s.hitStopDuration, s.hitStopTimeScale);
        }
    }
}
