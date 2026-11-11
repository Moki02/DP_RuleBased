// ============================================================================
// PlayerUIHud_Manager.cs
// The team's main HUD: the HYD bar (coloured by hydration state) with "HYD / max" text,
// and the canteen bar with "litres / size" text. Refreshed every frame.
//
// DESIGN RULES: Resources table (HYD bar always visible, canteen under it), the hydration
// states table (Hydrated / Thirsty / Dehydrated / Collapsed), R1.1 (litres rounded down to
// one decimal).
//
// HOW TO USE: on the Player HUD. Drag in the two UI_StatBars and two texts; all four are
// needed. Pick the four state colours.
// HOW TO EDIT: colours in the Inspector. The thresholds for each state are in GameBalance >
// Hydration (read by PlayerHydration_Manager), not here.
// ============================================================================

using UnityEngine;
using TMPro;

/// <summary>Shows the player's HYD and canteen.</summary>
public class PlayerUIHud_Manager : MonoBehaviour
{
    Player_Manager player;
    GameResources_Manager resources;

    [Header("Hydration UI")]
    [Tooltip("The HYD bar (required).")]
    [SerializeField] UI_StatBar hydrationBar;
    [Tooltip("Text like \"75/100\" (HYD points, rounded down). Required.")]
    [SerializeField] TMP_Text hydrationText;

    [Header("Hydration Colors")]
    [Tooltip("Bar colour at 60 HYD and above.")]
    [SerializeField] Color hydratedColor = Color.cyan;
    [Tooltip("Bar colour while Thirsty (30 to 59 HYD).")]
    [SerializeField] Color thirstyColor = new Color(1f, 0.5f, 0f);
    [Tooltip("Bar colour while Dehydrated (1 to 29 HYD; can't sprint).")]
    [SerializeField] Color dehydratedColor = Color.red;
    [Tooltip("Bar colour at 0 HYD (collapsed).")]
    [SerializeField] Color collapsedColor = Color.red;

    [Header("Canteen UI")]
    [Tooltip("The canteen bar (required).")]
    [SerializeField] UI_StatBar canteenBar;
    [Tooltip("Text like \"1.5L/2L\" (litres). Required.")]
    [SerializeField] TMP_Text canteenText;

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance.gameResources_Manager;

        RefreshHUD();
    }

    private void Update()
    {
        RefreshHUD();
    }

    /// <summary>Redraws both bars and texts now.</summary>
    public void RefreshHUD()
    {
        if (player == null || resources == null)
            return;

        UpdateHydrationUI();
        UpdateCanteenUI();
    }

    private void UpdateHydrationUI()
    {
        float hydration = player.playerStats_Manager.player_Hydration;
        float maxHydration = player.playerStats_Manager.player_MaxHydration;

        hydrationBar.SetStat(hydration, maxHydration);

        hydrationText.text =
            Mathf.FloorToInt(hydration) + "/" +
            Mathf.FloorToInt(maxHydration);

        switch (player.playerHydration_Manager.currentHydrationState)
        {
            case PlayerHydration_Manager.HydrationState.Hydrated:
                hydrationBar.SetBarColor(hydratedColor);
                break;

            case PlayerHydration_Manager.HydrationState.Thirsty:
                hydrationBar.SetBarColor(thirstyColor);
                break;

            case PlayerHydration_Manager.HydrationState.Dehydrated:
                hydrationBar.SetBarColor(dehydratedColor);
                break;

            case PlayerHydration_Manager.HydrationState.Collapsed:
                hydrationBar.SetBarColor(collapsedColor);
                break;
        }
    }

    private void UpdateCanteenUI()
    {
        float canteen = resources.canteen;
        float maxCanteen = resources.canteen_Max;

        canteenBar.SetStat(canteen, maxCanteen);

        canteenText.text =
            FormatLiters(canteen) + "/" +
            FormatLiters(maxCanteen);
    }

    // R1.1: one decimal, rounded down (1.96 shows 1.9). The tiny 0.0001 stops float rounding
    // turning 1.7 (stored as 1.6999999) into 1.6; it's the same as GameResources_Manager.FormatWater,
    // so this HUD and the other water texts show the same number.
    private string FormatLiters(float amount)
    {
        float displayed = Mathf.Floor(Mathf.Max(0f, amount) * 10f + 1e-4f) / 10f;

        return displayed.ToString("0.#") + "L";
    }
}
