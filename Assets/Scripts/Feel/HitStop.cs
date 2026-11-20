// ============================================================================
// HitStop.cs
// Freezes or slows the whole game for a split second (real time), then puts the speed back.
// Makes a hit feel heavy. Made automatically the first time it is used.
//
// DESIGN RULES: none (feel only). Never runs while the game is paused (timeScale 0) and
// always restores the speed it found, so it can't leave the game slowed down.
//
// HOW TO USE: HitStop.Play(0.06f, 0.05f);  or HitFeedback.DoHitStop() for the FeelSettings values.
// HOW TO EDIT: FeelSettings > Minigame hit feedback.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Short real-time freeze of Time.timeScale.</summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop runner;

        float left = -1f;
        float restoreTo = 1f;

        /// <summary>True while a hit-stop is running.</summary>
        public static bool IsActive => runner != null && runner.left >= 0f;

        /// <summary>Slows the game to <paramref name="slowScale"/> for <paramref name="seconds"/> of real time.</summary>
        public static void Play(float seconds, float slowScale)
        {
            if (seconds <= 0f) return;
            if (!IsActive && Time.timeScale <= 0f) return; // paused: leave it alone
            if (runner == null)
            {
                var go = new GameObject("[HitStop]");
                runner = go.AddComponent<HitStop>();
            }
            runner.Begin(seconds, slowScale);
        }

        void Begin(float seconds, float slowScale)
        {
            if (left < 0f) restoreTo = Time.timeScale; // remember the speed only when a stop starts
            left = seconds;
            Time.timeScale = Mathf.Min(slowScale, restoreTo);
        }

        void Update()
        {
            if (left < 0f) return;
            left -= Time.unscaledDeltaTime;
            if (left < 0f) Time.timeScale = restoreTo;
        }

        void OnDisable()
        {
            if (left >= 0f) Time.timeScale = restoreTo;
            left = -1f;
        }
    }
}
