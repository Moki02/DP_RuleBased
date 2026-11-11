// ============================================================================
// HubTap_Manager.cs
// The hub tap on the Clean Storage tank. It decides WHETHER the player can drink or refill
// here; the water itself is moved by PlayerHydration_Manager (DrinkFromCleanStorage and
// RefillFromHub), so all the rates live in one place.
//
// DESIGN RULES: R2.4 (at the tap: drink from Clean Storage at the R2.3 rate, or refill the
// canteen at 1 L/s). Not yet: the Rationing event closing the tap (Rationing isn't built).
//
// It also writes how much water is in the tank under the tank's name text, e.g.
// "Clean Storage / 10.0 / 25.0 L" (a floating label is made if the prefab has no name text).
//
// HOW TO USE: on the Clean Storage prefab. Its child "tap points" (HubTapPoint_Manager, one
// set to Drink and one to Refill) find this script on their parent by themselves.
// HOW TO EDIT: nothing to tune here. Rates are in GameBalance > Hydration (drinkRate,
// refillRate). In the maze game Clean Storage is UpgradeManager's number (see
// GameResources_Manager), so the tap and the Build menu always agree.
// ============================================================================

using UnityEngine;
using TMPro;

/// <summary>Says when the hub tap can be used, and passes drink/refill on to the player's hydration.</summary>
public class HubTap_Manager : MonoBehaviour
{
    [Header("Level Label (optional)")]
    [Tooltip("The text over the tank. Empty = the child text that says \"Clean Storage\", or a new floating label.")]
    public TMP_Text levelLabel;

    private Player_Manager player;
    private GameResources_Manager resources;
    private string labelTitle = "Clean Storage";

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance != null ? Game_Manager.instance.gameResources_Manager : null;

        if (levelLabel == null)
            levelLabel = HubPurifier_Manager.FindOrMakeLabel(transform, "Clean Storage");
        if (levelLabel != null)
            labelTitle = HubPurifier_Manager.FirstLine(levelLabel.text, "Clean Storage");

        UpdateLabel();
    }

    private void Update()
    {
        UpdateLabel();
    }

    // "Clean Storage" + "10.0 / 25.0 L" (+ FULL / EMPTY), so the player can see the tank's level.
    private void UpdateLabel()
    {
        if (levelLabel == null || resources == null)
            return;

        float stored = resources.cleanStorage;
        float max = resources.cleanStorage_Max;
        string state = stored >= max - 0.0001f ? "  FULL" : stored <= 0.0001f ? "  EMPTY" : "";

        string text = labelTitle + "\n<size=70%>" + resources.FormatWater(stored) + " / " +
                      resources.FormatWater(max) + state + "</size>";

        if (levelLabel.text != text)
            levelLabel.text = text;
    }

    /// <summary>True if there is clean water, the player isn't at full HYD, and isn't collapsed.</summary>
    public bool CanDrink()
    {
        if (player == null || resources == null)
            return false;

        if (player.playerHydration_Manager.IsCollapsed())
            return false;

        return resources.cleanStorage > 0f &&
               player.playerStats_Manager.player_Hydration <
               player.playerStats_Manager.player_MaxHydration;
    }

    /// <summary>True if there is clean water and the canteen isn't full.</summary>
    public bool CanRefill()
    {
        if (player == null || resources == null)
            return false;

        if (player.playerHydration_Manager.IsCollapsed())
            return false;

        return resources.cleanStorage > 0f &&
               resources.canteen < resources.canteen_Max;
    }

    /// <summary>Drinks from Clean Storage for one frame (<paramref name="deltaTime"/> in seconds).</summary>
    public void Drink(float deltaTime)
    {
        if (!CanDrink())
            return;

        player.playerHydration_Manager.DrinkFromCleanStorage(deltaTime);
    }

    /// <summary>Moves Clean Storage water into the canteen for one frame (<paramref name="deltaTime"/> in seconds).</summary>
    public void Refill(float deltaTime)
    {
        if (!CanRefill())
            return;

        player.playerHydration_Manager.RefillFromHub(deltaTime);
    }
}
