// ============================================================================
// HubHUD_Manager.cs
// The hub part of the team's HUD:
//   - the "+X L" unbanked water counter (always shown);
//   - the Raw Basin and Clean Storage bars and "litres / size" text, shown only while the
//     player stands inside the hub box.
//
// DESIGN RULES: R1.1 (litres, one decimal, rounded down: uses GameResources_Manager.FormatWater),
// R3.5 (unbanked water), the Resources table (Raw Basin and Clean Storage on the hub screen).
//
// HOW TO USE: on the Player HUD. Drag in:
//   Hub Area     - the hub's BoxCollider (the same box as PlayerHydration_Manager's "Hub Area").
//                  If it's empty the hub panel never shows.
//   Hub HUD      - the panel to show/hide.
//   bars / texts - any can be left empty and are then skipped.
// HOW TO EDIT: nothing to tune. In the maze game Clean Storage and its size come from
// UpgradeManager (through GameResources_Manager), so the bar follows the storage upgrades.
// ============================================================================

using UnityEngine;
using TMPro;

/// <summary>Shows unbanked water, and the Raw Basin / Clean Storage bars while the player is at the hub.</summary>
public class HubHUD_Manager : MonoBehaviour
{
    [Header("Hub Detection")]
    [Tooltip("The hub's BoxCollider. The hub panel shows while the player is inside it. Empty = the panel never shows.")]
    [SerializeField] BoxCollider hubArea;

    [Header("Hub HUD")]
    [Tooltip("The panel with the Raw Basin and Clean Storage bars (turned on only inside the hub).")]
    [SerializeField] GameObject hubHUD;

    [Header("Raw Basin UI")]
    [Tooltip("Bar showing Raw Basin litres / size.")]
    [SerializeField] UI_StatBar rawBasinBar;
    [Tooltip("Text like \"3.5 L / 15.0 L\".")]
    [SerializeField] TMP_Text rawBasinText;

    [Header("Clean Storage UI")]
    [Tooltip("Bar showing Clean Storage litres / size.")]
    [SerializeField] UI_StatBar cleanStorageBar;
    [Tooltip("Text like \"10.0 L / 25.0 L\".")]
    [SerializeField] TMP_Text cleanStorageText;

    [Header("Unbanked Water UI")]
    [Tooltip("Text like \"+3.0 L\": water earned in the maze, not yet carried home (R3.5).")]
    [SerializeField] TMP_Text unbankedWaterText;

    private Player_Manager player;
    private GameResources_Manager resources;

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance != null ? Game_Manager.instance.gameResources_Manager : null;

        // Fix: these used resources without checking it, so a scene without a Game Manager
        // threw an error here. Update already had this check.
        if (player == null || resources == null)
            return;

        UpdateUnbankedWaterUI();
        UpdateHubUI();
    }

    private void Update()
    {
        if (player == null || resources == null)
            return;

        UpdateUnbankedWaterUI();
        UpdateHubUI();
    }

    private void UpdateUnbankedWaterUI()
    {
        if (unbankedWaterText == null)
            return;

        unbankedWaterText.text =
            "+" + resources.FormatWater(resources.unbankedWater);
    }

    private void UpdateHubUI()
    {
        bool isInsideHub = IsInsideHub();

        if (hubHUD != null)
            hubHUD.SetActive(isInsideHub);

        if (!isInsideHub)
            return;

        UpdateRawBasinUI();
        UpdateCleanStorageUI();
    }

    private void UpdateRawBasinUI()
    {
        if (rawBasinBar != null)
            rawBasinBar.SetStat(
                resources.rawBasin,
                resources.rawBasin_Max
            );

        if (rawBasinText != null)
        {
            rawBasinText.text =
                resources.FormatWater(resources.rawBasin) +
                " / " +
                resources.FormatWater(resources.rawBasin_Max);
        }
    }

    private void UpdateCleanStorageUI()
    {
        if (cleanStorageBar != null)
            cleanStorageBar.SetStat(
                resources.cleanStorage,
                resources.cleanStorage_Max
            );

        if (cleanStorageText != null)
        {
            cleanStorageText.text =
                resources.FormatWater(resources.cleanStorage) +
                " / " +
                resources.FormatWater(resources.cleanStorage_Max);
        }
    }

    // True if the player is inside the hub box. Works for a rotated or scaled box: the
    // player's position is turned into the box's own local space and compared with its size.
    private bool IsInsideHub()
    {
        if (hubArea == null || player == null)
            return false;

        Vector3 localPosition =
            hubArea.transform.InverseTransformPoint(
                player.transform.position
            );

        localPosition -= hubArea.center;

        Vector3 halfSize = hubArea.size * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfSize.x &&
               Mathf.Abs(localPosition.y) <= halfSize.y &&
               Mathf.Abs(localPosition.z) <= halfSize.z;
    }
}
