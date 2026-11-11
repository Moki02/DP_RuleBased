// ============================================================================
// HubPurifier_Manager.cs
// The purifier: moves Raw Basin water into Clean Storage on its own (no button) while the
// basin has water and storage has room (R3.2, R3.3, R6.2). The boiling pot does 1 L every
// 6 s (0.167 L/s).
//
// A better purifier (the Biosand Filter, R6.1: only the best one owned runs) sets
// RateOverride; until then the boiling pot rate from GameBalance is used.
//
// DESIGN RULES: R3.2, R3.3, R6.1, R6.2.
//
// HOW TO USE: on the Boiling Pot prefab.
// HOW TO EDIT: GameBalance > Purifiers > Boil Rate.
// ============================================================================

using System;
using UnityEngine;
using TheLastDrop;

public class HubPurifier_Manager : MonoBehaviour
{
    /// <summary>
    /// The main purifier's rate in L/s if something better than the pot is owned (Biosand
    /// Filter). Return a value below 0 to use the boiling pot. Null = boiling pot.
    /// </summary>
    public static Func<float> RateOverride;

    [Header("Purifier Settings (old scenes; GameBalance wins when present)")]
    public float boilingPotRate = 1f / 6f;

    private GameResources_Manager resources;
    private GameBalance balance;

    /// <summary>The rate in L/s right now.</summary>
    public float CurrentRate
    {
        get
        {
            float over = RateOverride != null ? RateOverride() : -1f;
            if (over >= 0f) return over;
            return balance != null ? balance.boilRate : boilingPotRate;
        }
    }

    private void Start()
    {
        resources = Game_Manager.instance.gameResources_Manager;
        balance = BalanceProvider.Current;
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

        float room = resources.cleanStorage_Max - resources.cleanStorage;
        if (room <= 0f)
            return;

        float amount = Mathf.Min(CurrentRate * Time.deltaTime, resources.rawBasin, room);
        if (amount <= 0f)
            return;

        float transferred = resources.TakeRawWater(amount);
        resources.AddCleanWater(transferred);
    }
}
