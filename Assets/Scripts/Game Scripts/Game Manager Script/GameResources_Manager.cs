// ============================================================================
// GameResources_Manager.cs
// Every water amount in the game, in one place: the canteen, the Raw Basin, Clean Storage,
// unbanked water and the spare jugs by the hub door.
//
// Clean Storage: when the scene has an UpgradeManager (the maze game), Clean Storage is
// UpgradeManager's number (it pays for Build and Skills). This script reads and writes that
// same number, so the purifier, the tap and the shop can never disagree. Old scenes with no
// UpgradeManager keep their own number here, as before.
//
// DESIGN RULES: R1.1 to R1.4 (units, only Clean Storage buys, overflow), R3.5 (banking,
// Safe Return bonus, spare jugs), R5.1 (an upgrade keeps the water).
//
// HOW TO USE: it's on the Game Manager prefab. Other scripts call AddRawWater, TakeCanteenWater,
// BankWater and so on; don't change the numbers directly unless you have to.
// HOW TO EDIT: start amounts are in GameBalance (Start values); sizes follow the storage
// upgrades (UpgradeManager sets canteen_Max and rawBasin_Max).
// ============================================================================

using UnityEngine;
using System;
using TheLastDrop;

public class GameResources_Manager : MonoBehaviour, ISaveable
{
    public static GameResources_Manager instance;

    /// <summary>Fired when water didn't fit somewhere (the FULL popup, R1.4).</summary>
    public event Action OnWaterFull;

    [Header("Canteen")]
    public float canteen = 2f;
    public float canteen_Max = 2f;

    // Maze playtest only: pouring into lock basins never empties the canteen.
    [Tooltip("Playtest: the canteen never runs out. Taking water from it gives the water without lowering \"Canteen\". " +
             "Turned off automatically when GameBalance > Survival On is ticked.")]
    public bool infiniteCanteen = false;

    [Header("Raw Basin")]
    public float rawBasin = 0f;
    public float rawBasin_Max = 15f;

    [Header("Clean Storage (old scenes only; the maze game uses UpgradeManager's)")]
    [UnityEngine.Serialization.FormerlySerializedAs("cleanStorage")]
    [SerializeField] float cleanStorageLocal = 10f;
    [UnityEngine.Serialization.FormerlySerializedAs("cleanStorage_Max")]
    [SerializeField] float cleanStorageLocal_Max = 25f;

    [Header("Unbanked Water")]
    public float unbankedWater = 0f;

    [Header("Spare jugs (R3.5)")]
    [Tooltip("Banked water that didn't fit anywhere. Pours into Clean Storage as room frees up; can't be spent or drunk.")]
    public float spareJugs = 0f;

    /// <summary>Litres in Clean Storage (UpgradeManager's number when there is one).</summary>
    public float cleanStorage
    {
        get
        {
            var u = UpgradeManager.Instance;
            return u != null && u.Logic != null ? u.Logic.Stored : cleanStorageLocal;
        }
        set
        {
            var u = UpgradeManager.Instance;
            if (u != null && u.Logic != null) u.Logic.SetStoredQuiet(value);
            else cleanStorageLocal = Mathf.Clamp(value, 0f, cleanStorageLocal_Max);
        }
    }

    /// <summary>Clean Storage's size (follows the storage upgrades in the maze game).</summary>
    public float cleanStorage_Max
    {
        get
        {
            var u = UpgradeManager.Instance;
            return u != null && u.Logic != null ? u.Logic.Cap : cleanStorageLocal_Max;
        }
        set { cleanStorageLocal_Max = value; }
    }

    private void Awake()
    {
        instance = this;

        // The real game: start values from GameBalance, and the playtest switches off.
        // (Awake, not Start, so a save loaded in another script's Start isn't overwritten.)
        var b = BalanceProvider.Current;
        if (b != null && b.survivalOn)
        {
            infiniteCanteen = false;
            canteen = Mathf.Min(b.startCanteen, canteen_Max);
            rawBasin = b.startRaw;
        }
    }

    private void Update()
    {
        // R3.5: spare jugs empty into Clean Storage on their own as room frees up.
        if (spareJugs > 0f && Time.timeScale > 0f)
        {
            var b = BalanceProvider.Current;
            float rate = b != null ? b.spareJugPourRate : 1f;
            float room = Mathf.Max(0f, cleanStorage_Max - cleanStorage);
            float moved = Mathf.Min(spareJugs, room, rate * Time.deltaTime);
            if (moved > 0f)
            {
                spareJugs -= moved;
                cleanStorage += moved;
            }
        }
    }

    // ------------------------------------------------------------------ Adding and taking

    public float AddCanteenWater(float amount)
    {
        if (amount <= 0) return 0;
        float added = Mathf.Min(amount, Mathf.Max(0, canteen_Max - canteen));
        canteen += added;
        if (added < amount) WaterFull();
        return added;
    }

    public float AddRawWater(float amount)
    {
        if (amount <= 0) return 0;
        float added = Mathf.Min(amount, Mathf.Max(0, rawBasin_Max - rawBasin));
        rawBasin += added;
        if (added < amount) WaterFull();
        return added;
    }

    public float AddCleanWater(float amount)
    {
        if (amount <= 0) return 0;
        float added = Mathf.Min(amount, Mathf.Max(0, cleanStorage_Max - cleanStorage));
        cleanStorage += added;
        if (added < amount) WaterFull();
        return added;
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
        if (amount <= 0) return 0;
        float removed = Mathf.Min(amount, canteen);
        canteen -= removed;
        return removed;
    }

    public float TakeRawWater(float amount)
    {
        if (amount <= 0) return 0;
        float removed = Mathf.Min(amount, rawBasin);
        rawBasin -= removed;
        return removed;
    }

    public float TakeCleanWater(float amount)
    {
        if (amount <= 0) return 0;
        float removed = Mathf.Min(amount, cleanStorage);
        cleanStorage -= removed;
        return removed;
    }

    // ONLY CLEAN STORAGE CAN PURCHASE UPGRADES (R1.2)
    public bool SpendCleanWater(float cost)
    {
        if (cost < 0 || cleanStorage < cost)
            return false;

        cleanStorage -= cost;
        return true;
    }

    /// <summary>
    /// R3.5: puts <paramref name="amount"/> litres of carried-home water away: Clean Storage first,
    /// then the Raw Basin, then the spare jugs (never lost). Returns how much went to the spare jugs.
    /// </summary>
    public float StoreHomeWater(float amount)
    {
        if (amount <= 0f) return 0f;
        float left = amount;
        float toClean = Mathf.Min(left, Mathf.Max(0f, cleanStorage_Max - cleanStorage));
        cleanStorage += toClean;
        left -= toClean;
        float toRaw = Mathf.Min(left, Mathf.Max(0f, rawBasin_Max - rawBasin));
        rawBasin += toRaw;
        left -= toRaw;
        spareJugs += left;
        return left;
    }

    // MOVE UNBANKED WATER INTO STORAGE (R3.5). Returns the litres banked.
    public float BankWater()
    {
        float amount = unbankedWater;
        unbankedWater = 0;
        StoreHomeWater(amount);
        return amount;
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

    // DISPLAY WATER WITH ONE DECIMAL PLACE (R1.1: rounds down, so 4.96 shows as 4.9)
    public string FormatWater(float amount)
    {
        float displayed = Mathf.Floor(
            Mathf.Max(0, amount) * 10f + 1e-4f
        ) / 10f;

        return displayed.ToString("F1") + " L";
    }

    void WaterFull()
    {
        OnWaterFull?.Invoke();
    }

    // ------------------------------------------------------------------ Saving (R11.8)

    /// <summary>Restored after Build and Skills (so the sizes are right first).</summary>
    public int RestoreOrder => RestoreOrders.Resources;

    /// <summary>Saves the canteen, the Raw Basin, unbanked water and the spare jugs. Clean Storage is saved by UpgradeManager.</summary>
    public void Capture(SaveData data)
    {
        data.canteen = canteen;
        data.hasSurvival = true;
        data.rawBasin = rawBasin;
        data.unbanked = unbankedWater;
        data.spareJugs = spareJugs;
    }

    /// <summary>Puts the water back (never past each container's size).</summary>
    public void Restore(SaveData data)
    {
        canteen = Mathf.Clamp(data.canteen, 0f, canteen_Max);
        if (!data.hasSurvival) return; // a save from before the survival loop
        rawBasin = Mathf.Clamp(data.rawBasin, 0f, rawBasin_Max);
        unbankedWater = Mathf.Max(0f, data.unbanked);
        spareJugs = Mathf.Max(0f, data.spareJugs);
    }
}
