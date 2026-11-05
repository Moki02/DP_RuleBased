
using UnityEngine;

public class HubPurifier_Manager : MonoBehaviour
{
    [Header("Purifier Settings")]
    public float boilingPotRate = 1f / 6f;

    private GameResources_Manager resources;

    private void Start()
    {
        resources = Game_Manager.instance.gameResources_Manager;
    }

    private void Update()
    {
        if (resources == null)
            return;

        if (Time.timeScale == 0f)
            return;

        PurifyWater();
    }

    private void PurifyWater()
    {
        if (resources.rawBasin <= 0f)
            return;

        if (resources.cleanStorage >= resources.cleanStorage_Max)
            return;

        float amount = boilingPotRate * Time.deltaTime;

        amount = Mathf.Min(
            amount,
            resources.rawBasin,
            resources.cleanStorage_Max - resources.cleanStorage
        );

        if (amount <= 0f)
            return;

        float transferred = resources.TakeRawWater(amount);

        resources.AddCleanWater(transferred);
    }
}
