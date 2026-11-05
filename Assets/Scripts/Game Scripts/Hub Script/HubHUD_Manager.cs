
using UnityEngine;
using TMPro;

public class HubHUD_Manager : MonoBehaviour
{
    [Header("Hub Detection")]
    [SerializeField] BoxCollider hubArea;

    [Header("Hub HUD")]
    [SerializeField] GameObject hubHUD;

    [Header("Raw Basin UI")]
    [SerializeField] UI_StatBar rawBasinBar;
    [SerializeField] TMP_Text rawBasinText;

    [Header("Clean Storage UI")]
    [SerializeField] UI_StatBar cleanStorageBar;
    [SerializeField] TMP_Text cleanStorageText;

    [Header("Unbanked Water UI")]
    [SerializeField] TMP_Text unbankedWaterText;

    private Player_Manager player;
    private GameResources_Manager resources;

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance.gameResources_Manager;

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
