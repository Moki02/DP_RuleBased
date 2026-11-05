
using UnityEngine;
using TMPro;

public class LockBasin_Manager : Interactable_Manager
{
    [Header("Lock Basin Settings")]
    public float basin_MaxWater = 4f;
    public float basin_Water = 0f;
    public float pourRate = 1f;

    [Header("Lock Basin UI")]
    public UI_StatBar basinBar;
    public TMP_Text basinText;

    [Header("Gate")]
    public GameObject gateObject;

    private GameResources_Manager resources;
    private bool isUnlocked = false;

    private void Start()
    {
        resources = Game_Manager.instance.gameResources_Manager;

        interactionType = InteractionType.Hold;

        basin_Water = Mathf.Clamp(
            basin_Water, 0f, basin_MaxWater
        );

        if (basin_Water >= basin_MaxWater)
            UnlockGate();

        UpdateBasinUI();
    }

    public override bool CanInteract(Player_Manager player)
    {
        if (player == null || isUnlocked)
            return false;

        if (resources == null || resources.canteen <= 0f)
            return false;

        return !player.playerHydration_Manager.IsCollapsed();
    }

    public override void Interact(float deltaTime)
    {
        if (resources == null || isUnlocked)
            return;

        float amount = Mathf.Min(
            pourRate * deltaTime,
            basin_MaxWater - basin_Water
        );

        float transferred = resources.TakeCanteenWater(amount);

        basin_Water += transferred;

        basin_Water = Mathf.Min(
            basin_Water, basin_MaxWater
        );

        UpdateBasinUI();

        if (basin_Water >= basin_MaxWater)
        {
            UnlockGate();
        }
    }

    // Dev note: For now I just make the wall disappear but in the future I'll improve this buy opening the path. (moving up, down, left, or right)
    private void UnlockGate()
    {
        isUnlocked = true;

        if (gateObject != null)
            gateObject.SetActive(false);

        Debug.Log("Lock Basin Full! Gate Unlocked!");
    }

    private void UpdateBasinUI()
    {
        if (basinBar != null)
            basinBar.SetStat(basin_Water, basin_MaxWater);

        if (basinText != null)
        {
            basinText.text =
                Mathf.Floor(basin_Water * 10f) / 10f +
                " / " +
                basin_MaxWater.ToString("0.#") +
                " L";
        }
    }
}
