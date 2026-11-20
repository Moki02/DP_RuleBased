// ============================================================================
// FeelLabDemo.cs
// The "Feel" panel in FeelLab: one method per demo button, so George and the artists can
// try every feel component (floating numbers, punch, roll-up, shake, vignette, collapse
// fade, hit flash / hit-stop) without the game.
//
// DESIGN RULES: none (FeelLab tool only; not used in the game).
//
// HOW TO USE: open FeelLab, press Play, click the buttons on the right. The HYD slider
// drives the low-HYD vignette.
// HOW TO EDIT: the buttons are plain UI in the scene; each calls one method below.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Demo actions for the FeelLab feel panel.</summary>
    public class FeelLabDemo : MonoBehaviour
    {
        [Tooltip("Where floating numbers appear (e.g. above the dummy player).")]
        public Transform floatAnchor;
        [Tooltip("The bar or number that bounces.")]
        public UiPunch punchTarget;
        [Tooltip("The counter that rolls up.")]
        public NumberRoll counter;
        [Tooltip("The low-HYD vignette in the scene.")]
        public LowHydVignette vignette;
        [Tooltip("The HYD slider (0..1). Its On Value Changed calls SetHyd.")]
        public Slider hydSlider;
        [Tooltip("The collapse fade in the scene.")]
        public CollapseFade collapseFade;
        [Tooltip("The minigame hit feedback.")]
        public HitFeedback hitFeedback;
        [Tooltip("Text showing whether shake is on.")]
        public TMP_Text shakeLabel;

        void Start()
        {
            if (hydSlider != null)
            {
                SetHyd(hydSlider.value);
            }
            if (counter != null) counter.SetInstant(12f);
            UpdateShakeLabel();
        }

        Vector3 FloatPos => floatAnchor != null ? floatAnchor.position : Vector3.up * 2f;

        /// <summary>Shows "+1.5 L" in the water colour.</summary>
        public void FloatWater() => FloatingText.ShowAmount(1.5f, " L", FloatPos);
        /// <summary>Shows "-2 HYD" in the bad colour.</summary>
        public void FloatHyd() => FloatingText.ShowAmount(-2f, " HYD", FloatPos);
        /// <summary>Shows "FULL".</summary>
        public void FloatFull() => FloatingText.Show("FULL", FloatPos, FloatingText.Mood.Neutral);

        /// <summary>Bounces the punch target.</summary>
        public void Punch() { if (punchTarget != null) punchTarget.Punch(); }

        /// <summary>Adds 2.5 L to the counter (rolls up).</summary>
        public void AddToCounter() { if (counter != null) counter.SetValue(counter.Target + 2.5f); }
        /// <summary>Takes 1 L from the counter (rolls down).</summary>
        public void TakeFromCounter() { if (counter != null) counter.SetValue(Mathf.Max(0f, counter.Target - 1f)); }

        /// <summary>Small screen shake.</summary>
        public void ShakeSmall() => CameraShake.Add(FeelSettings.Active.shakeSmall);
        /// <summary>Medium screen shake.</summary>
        public void ShakeMedium() => CameraShake.Add(FeelSettings.Active.shakeMedium);
        /// <summary>Big screen shake.</summary>
        public void ShakeBig() => CameraShake.Add(FeelSettings.Active.shakeBig);

        /// <summary>Turns screen shake on / off (the same switch the Settings screen will use).</summary>
        public void ToggleShake()
        {
            CameraShake.Enabled = !CameraShake.Enabled;
            UpdateShakeLabel();
        }

        void UpdateShakeLabel()
        {
            if (shakeLabel != null) shakeLabel.text = CameraShake.Enabled ? "Shake: ON" : "Shake: OFF";
        }

        /// <summary>Sets the vignette's HYD (0..1).</summary>
        public void SetHyd(float fraction) { if (vignette != null) vignette.SetHydrationFraction(fraction); }

        /// <summary>Plays the collapse fade with a medium shake.</summary>
        public void Collapse()
        {
            CameraShake.Add(FeelSettings.Active.shakeMedium);
            if (collapseFade != null) collapseFade.Play();
        }

        /// <summary>Minigame green hit.</summary>
        public void Hit() { if (hitFeedback != null) hitFeedback.Hit(); }
        /// <summary>Minigame miss.</summary>
        public void Miss() { if (hitFeedback != null) hitFeedback.Miss(); }
    }
}
