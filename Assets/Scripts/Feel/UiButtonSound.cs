// ============================================================================
// UiButtonSound.cs
// The click / hover sound for one button. UiButtonSounds adds one to every Button while the
// game runs; add one yourself and tick "Silent" to keep a button quiet.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: nothing to do (added automatically). Silent: tick it on the button.
// HOW TO EDIT: the clips and volume are in GameFeelLibrary (UI Click, UI Hover).
// ============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Plays the click / hover sound for one button (added by UiButtonSounds).</summary>
    public class UiButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Tooltip("Tick to keep this button silent.")]
        public bool silent;

        Selectable selectable;
        static float lastHover;

        void Awake() => selectable = GetComponent<Selectable>();

        bool CanSound => !silent && UiButtonSounds.Current != null && (selectable == null || selectable.IsInteractable());

        /// <summary>Mouse moved onto the button.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanSound || Time.unscaledTime - lastHover < 0.05f) return;
            lastHover = Time.unscaledTime;
            FeelAudio.Play(UiButtonSounds.Current.uiHover);
        }

        /// <summary>Button clicked.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CanSound) return;
            FeelAudio.Play(UiButtonSounds.Current.uiClick);
        }
    }
}
