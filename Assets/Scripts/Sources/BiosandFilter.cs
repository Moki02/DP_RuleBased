// ============================================================================
// BiosandFilter.cs
// The Biosand Filter (Build tab P1). Once bought it replaces the boiling pot as the hub's
// purifier (R6.1: only the best one owned runs) and ripens over two days:
//     50% of its rate the day it's bought, 75% the next day, 100% after that.
// It does this by setting HubPurifier_Manager.RateOverride to
//     filter rate (0.4 L/s) × ripeness
// and returning -1 (= "use the boiling pot") while P1 isn't owned.
//
// It remembers the day P1 was bought and saves it, so ripening carries on after a load.
// It also shows the filter model next to the boiling pot when owned (the model is placed
// in the scene, see HubSceneObjects). LabelText() still gives the rate and state, e.g.
// "Biosand Filter 75%: 0.30 L/s", for debug use; it is no longer shown in the hub.
//
// DESIGN RULES: R3.2, R3.3, R6.1, R6.2, R6.3, section 6 table.
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems) and finds
// the boiling pot (HubPurifier_Manager) by itself.
// HOW TO EDIT: GameBalance > Purifiers (Boil Rate, Filter Rate, Filter Ripening By Day).
// The Build item's rate is "purifierRate" in BuildAndSkills_Effects.csv (it wins when set).
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Biosand Filter: ripening purifier rate, its hub model, the purifier label, and its save.</summary>
    public class BiosandFilter : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<BiosandFilter>();

        /// <summary>The Build item id of the Biosand Filter.</summary>
        public const string FilterId = "P1";

        /// <summary>The day P1 was bought (1-based). 0 = not owned.</summary>
        public int PurchaseDay { get; private set; }

        GameBalance balance;
        UpgradeManager upgrades;
        HubPurifier_Manager purifier;
        Func<float> hook;
        bool wasOwned;


        /// <summary>True while the Biosand Filter is owned.</summary>
        public bool Owned => Skills.Owns(FilterId);

        /// <summary>Whole days since it was bought (0 on the day it's bought).</summary>
        public int DaysSincePurchase => Mathf.Max(0, WorldState.Day - Mathf.Max(1, PurchaseDay));

        /// <summary>Ripeness now: 0.5, 0.75 or 1 (GameBalance > Filter Ripening By Day). 0 if not owned.</summary>
        public float Ripeness => Owned && balance != null ? balance.GetFilterRipeness(DaysSincePurchase) : 0f;

        /// <summary>The filter's rate in L/s now, or -1 when it isn't owned (the boiling pot runs).</summary>
        public float Rate()
        {
            if (!Owned || balance == null) return -1f;
            return Skills.F("purifierRate", balance.filterRate) * Ripeness;
        }

        // ------------------------------------------------------------------ Lifecycle

        void Start()
        {
            balance = BalanceProvider.Current;
            upgrades = UpgradeManager.Instance;
            if (upgrades != null) upgrades.Changed += OnUpgradesChanged;

            // Already owned at Start (e.g. restored before we started): count from today.
            wasOwned = Owned;
            if (wasOwned && PurchaseDay < 1) PurchaseDay = WorldState.Day;

            hook = Rate;
            HubPurifier_Manager.RateOverride = hook; // static hooks are set in Start (team rule)
        }

        void OnDestroy()
        {
            if (upgrades != null) upgrades.Changed -= OnUpgradesChanged;
            if (HubPurifier_Manager.RateOverride == hook) HubPurifier_Manager.RateOverride = null;
            SimpleHud.SetLine("purifier", null);
        }

        // Detect the moment P1 becomes owned (bought) to remember the day.
        // Changed also fires whenever Clean Storage changes, so this runs often; it only acts
        // when "owned" flips.
        void OnUpgradesChanged()
        {
            bool owns = Owned;
            // Loading a save: UpgradeManager.Restore (order 30) fires Changed, which may set
            // "bought today" here; this script's Restore (order 31) runs right after and puts the
            // saved purchase day back. The clock is restored first (order 0), so "today" is the
            // loaded day.
            if (owns && !wasOwned) PurchaseDay = WorldState.Day;
            if (!owns) PurchaseDay = 0;
            wasOwned = owns;
        }

        void Update()
        {
            if (balance == null) balance = BalanceProvider.Current;
            if (purifier == null) purifier = FindAnyObjectByType<HubPurifier_Manager>();

            // The filter model is placed in the scene (HubSceneObjects); show it while owned.
            // No rate label or HUD line any more: the pot's own name text says which purifier runs.
            var hub = HubSceneObjects.Instance;
            if (hub != null && hub.biosandFilter != null && hub.biosandFilter.activeSelf != Owned)
                hub.biosandFilter.SetActive(Owned);
        }

        /// <summary>"Boiling pot: 0.17 L/s" / "Biosand Filter 75%: 0.30 L/s", plus why it's stopped.</summary>
        public string LabelText()
        {
            float rate = purifier != null ? purifier.CurrentRate
                       : Owned ? Rate() : (balance != null ? balance.boilRate : 1f / 6f);
            string head = Owned ? $"Biosand Filter {Mathf.RoundToInt(Ripeness * 100f)}%" : "Boiling pot";
            string text = $"{head}: {rate:0.00} L/s";

            var res = GameResources_Manager.instance;
            if (res != null)
            {
                if (res.cleanStorage >= res.cleanStorage_Max - 0.0001f) text += " (paused: Clean Storage FULL)"; // R3.3, R6.2
                else if (res.rawBasin <= 0.0001f) text += " (waiting for raw water)";                          // R3.2
            }
            return text;
        }

        // ------------------------------------------------------------------ Saving (R11.8)

        /// <summary>Restored just after Build and Skills, so this saved day wins over "bought today".</summary>
        public int RestoreOrder => RestoreOrders.Upgrades + 1;

        /// <summary>Saves the day the filter was bought.</summary>
        public void Capture(SaveData data)
        {
            data.hasSources = true;
            data.biosandPurchaseDay = Owned ? Mathf.Max(1, PurchaseDay) : 0;
        }

        /// <summary>Puts the purchase day back (only if P1 is owned in the save).</summary>
        public void Restore(SaveData data)
        {
            wasOwned = Owned;
            if (!wasOwned) { PurchaseDay = 0; return; }
            if (data.hasSources && data.biosandPurchaseDay > 0) PurchaseDay = data.biosandPurchaseDay;
            else if (PurchaseDay < 1) PurchaseDay = data.day > 0 ? data.day : WorldState.Day; // old save: ripen from the loaded day
        }
    }
}
