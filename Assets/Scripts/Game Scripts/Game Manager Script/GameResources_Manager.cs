using UnityEngine;
using System;
using TheLastDrop;

// Saving (added for the maze playtest, R11.8): this script saves the canteen. Clean Storage
// is saved by UpgradeManager, which owns it in the playtest.
public class GameResources_Manager : MonoBehaviour, ISaveable
{
    /// <summary>Restored after Build and Skills.</summary>
    public int RestoreOrder => RestoreOrders.Resources;

    /// <summary>Saves the canteen's litres.</summary>
    public void Capture(SaveData data) => data.canteen = canteen;

    /// <summary>Puts the canteen's litres back (never past its size).</summary>
    public void Restore(SaveData data) => canteen = Mathf.Clamp(data.canteen, 0f, canteen_Max);

    public static GameResources_Manager instance;

    public event Action OnWaterFull;

    [Header("Canteen")]
    public float canteen = 2f;
    public float canteen_Max = 2f;

    // Added for the maze playtest (Playtest_Maze ticks it): pouring into lock basins never empties the canteen.
    [Tooltip("Playtest: the canteen never runs out. Taking water from it gives the water without lowering \"Canteen\". " +
             "Untick to test with the real canteen amount above.")]
    public bool infiniteCanteen = false;

    [Header("Raw Basin")]
    public float rawBasin = 0f;
    public float rawBasin_Max = 15f;

    [Header("Clean Storage")]
    public float cleanStorage = 10f;
    public float cleanStorage_Max = 25f;

    [Header("Unbanked Water")]
    public float unbankedWater = 0f;

    private void Awake()
    {
        instance = this;
    }

    // ADD WATER AND HANDLE OVERFLOW
    private float AddWater(ref float resource, float amount, float max)
    {
        if (amount <= 0)
            return 0;

        float space = Mathf.Max(0, max - resource);
        float added = Mathf.Min(amount, space);

        resource += added;

        if (added < amount)
        {
            Debug.Log("FULL");
            OnWaterFull?.Invoke();
        }

        return added;
    }

    // REMOVE WATER WITHOUT GOING BELOW ZERO
    private float RemoveWater(ref float resource, float amount)
    {
        if (amount <= 0)
            return 0;

        float removed = Mathf.Min(amount, resource);
        resource -= removed;

        return removed;
    }

    public float AddCanteenWater(float amount)
    {
        return AddWater(ref canteen, amount, canteen_Max);
    }

    public float AddRawWater(float amount)
    {
        return AddWater(ref rawBasin, amount, rawBasin_Max);
    }

    public float AddCleanWater(float amount)
    {
        return AddWater(ref cleanStorage, amount, cleanStorage_Max);
    }

    public void AddUnbankedWater(float amount)
    {
        if (amount > 0)
            unbankedWater += amount;
    }

    public float TakeCanteenWater(float amount)
    {
        if (infiniteCanteen)
            return Mathf.Max(0, amount);

        return RemoveWater(ref canteen, amount);
    }

    public float TakeRawWater(float amount)
    {
        return RemoveWater(ref rawBasin, amount);
    }

    public float TakeCleanWater(float amount)
    {
        return RemoveWater(ref cleanStorage, amount);
    }

    // ONLY CLEAN STORAGE CAN PURCHASE UPGRADES
    public bool SpendCleanWater(float cost)
    {
        if (cost < 0 || cleanStorage < cost)
            return false;

        cleanStorage -= cost;
        return true;
    }

    // MOVE UNBANKED WATER INTO CLEAN STORAGE
    public void BankWater()
    {
        AddCleanWater(unbankedWater);
        unbankedWater = 0;
    }

    // REFILL CANTEEN FROM CLEAN STORAGE
    public float RefillCanteen(float amount)
    {
        float space = Mathf.Max(0, canteen_Max - canteen);

        float transferred = Mathf.Min(
            Mathf.Max(0, amount),
            cleanStorage,
            space
        );

        cleanStorage -= transferred;
        canteen += transferred;

        return transferred;
    }

    // UPGRADE CONTAINER CAPACITIES
    public void UpgradeCanteen(float newMax)
    {
        canteen_Max = Mathf.Max(canteen_Max, newMax);
    }

    public void UpgradeRawBasin(float newMax)
    {
        rawBasin_Max = Mathf.Max(rawBasin_Max, newMax);
    }

    public void UpgradeCleanStorage(float newMax)
    {
        cleanStorage_Max = Mathf.Max(cleanStorage_Max, newMax);
    }

    // DISPLAY WATER WITH ONE DECIMAL PLACE
    public string FormatWater(float amount)
    {
        float displayed = Mathf.Floor(
            Mathf.Max(0, amount) * 10f
        ) / 10f;

        return displayed.ToString("F1") + " L";
    }
}