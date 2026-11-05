
using UnityEngine;
using UnityEngine.UI;

public class UI_StatBar : MonoBehaviour
{
    [Header("Stat Bar")]
    [SerializeField] Image statBar;

    [Header("Bar Settings")]
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

        if (Mathf.Abs(statBar.fillAmount - targetFill) < 0.001f)
        {
            statBar.fillAmount = targetFill;
        }
    }

    public void SetStat(float current, float max)
    {
        float newFill = max <= 0f ? 0f : Mathf.Clamp01(current / max);

        if (!isInitialized)
        {
            statBar.fillAmount = newFill;
            targetFill = newFill;
            isInitialized = true;
            return;
        }

        targetFill = newFill;
    }

    public void SetBarColor(Color newColor)
    {
        statBar.color = newColor;
    }
}
