// ============================================================================
// UiBurst.cs
// A quick ring that grows and fades on the UI (the minigame "hit" burst). It is plain UI
// (Images), so artists can change the ring, add sparks, recolour it in the prefab.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: prefab Assets/VFX/Resources/Feel/UiHitBurst.prefab. Spawn it with
// FeelPool.Spawn(prefab, position, rotation, parentRect) or let HitFeedback do it.
// It hides itself and goes back to the pool when done.
// HOW TO EDIT: length and size in FeelSettings > Minigame hit feedback. Every Image under it
// fades together (CanvasGroup).
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Pooled UI burst: scales up and fades out, then returns to FeelPool.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiBurst : MonoBehaviour
    {
        CanvasGroup group;
        Vector3 baseScale;
        float time;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            baseScale = transform.localScale;
        }

        void OnEnable()
        {
            time = 0f;
            if (group != null) group.alpha = 1f;
            transform.localScale = baseScale * 0.5f;
        }

        void Update()
        {
            var s = FeelSettings.Active;
            time += Time.unscaledDeltaTime;
            float t = s.burstDuration > 0f ? time / s.burstDuration : 1f;
            if (t >= 1f)
            {
                FeelPool.Release(gameObject);
                return;
            }
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = baseScale * Mathf.Lerp(0.5f, s.burstScale, eased);
            group.alpha = 1f - t;
        }
    }
}
