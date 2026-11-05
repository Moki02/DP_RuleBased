
using UnityEngine;
using TMPro;

public class PlayerUIHud_Manager : MonoBehaviour
{
    Player_Manager player;
    GameResources_Manager resources;

    [Header("Hydration UI")]
    [SerializeField] UI_StatBar hydrationBar;
    [SerializeField] TMP_Text hydrationText;

    [Header("Hydration Colors")]
    [SerializeField] Color hydratedColor = Color.cyan;
    [SerializeField] Color thirstyColor = new Color(1f, 0.5f, 0f);
    [SerializeField] Color dehydratedColor = Color.red;
    [SerializeField] Color collapsedColor = Color.red;

    [Header("Canteen UI")]
    [SerializeField] UI_StatBar canteenBar;
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

    private string FormatLiters(float amount)
    {
        float displayed = Mathf.Floor(amount * 10f) / 10f;

        return displayed.ToString("0.#") + "L";
    }
}
