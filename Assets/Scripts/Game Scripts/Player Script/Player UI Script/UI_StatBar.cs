// ============================================================================
// UI_StatBar.cs
// A bar (HYD, canteen, basins, minigame progress) that slides smoothly to its new value.
// The first value it gets is shown straight away, so bars don't "fill up" when a scene starts.
//
// DESIGN RULES: none (a helper used by the HUDs).
//
// HOW TO USE: on a UI Image whose Image Type is "Filled". Other scripts call
// SetStat(current, max) and SetBarColor(colour).
// HOW TO EDIT: "Fill Speed" sets how fast the bar slides.
// Note: it uses game time, so while the game is paused the bar stops sliding.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>A filled Image that slides towards current / max.</summary>
public class UI_StatBar : MonoBehaviour
{
    [Header("Stat Bar")]
    [Tooltip("The Image to fill (Image Type must be Filled). Empty = the Image on this object.")]
    [SerializeField] Image statBar;

    [Header("Bar Settings")]
    [Tooltip("How fast the bar slides to its new value (higher = faster; about 1/speed seconds to get most of the way).")]
    [SerializeField] float fillSpeed = 5f;

    private bool isInitialized = false;
    private float targetFill;

    private void Awake()
    {
        if (statBar == null)
        {
            statBar = GetComponent<Image>();
        }

        targetFill = statBar.fillAmount;
    }

    private void Update()
    {
        statBar.fillAmount = Mathf.Lerp(
            statBar.fillAmount,
            targetFill,
            fillSpeed * Time.deltaTime
        );

        // Snap when very close, or the Lerp would creep towards the value forever.
        if (Mathf.Abs(statBar.fillAmount - targetFill) < 0.001f)
        {
            statBar.fillAmount = targetFill;
        }
    }

    /// <summary>Sets the bar to current / max (0 to 1). A max of 0 shows an empty bar.</summary>
    public void SetStat(float current, float max)
    {
        float newFill = max <= 0f ? 0f : Mathf.Clamp01(current / max);

        // First value: jump there instead of sliding from whatever the Image started at.
        if (!isInitialized)
        {
            statBar.fillAmount = newFill;
            targetFill = newFill;
            isInitialized = true;
            return;
        }

        targetFill = newFill;
    }

    /// <summary>Changes the bar's colour (e.g. by hydration state).</summary>
    public void SetBarColor(Color newColor)
    {
        statBar.color = newColor;
    }
}
