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
// It also puts a sand-coloured filter model next to the boiling pot when owned, and a
// floating label over the pot that always shows the purifier's rate and state, e.g.
//     "Boiling pot: 0.17 L/s"   or   "Biosand Filter 75%: 0.30 L/s"
// (R4.6 spirit: the player never has to guess why water is slow).
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

        FacingLabel label;
        GameObject filterModel;

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

            string text = LabelText();
            if (purifier != null)
            {
                if (label == null)
                {
                    // An unscaled holder at the pot, so the pot prefab's scale can't stretch the text.
                    var holder = new GameObject("Purifier_Label").transform;
                    holder.position = purifier.transform.position;
                    label = SourceVisuals.Label(holder, balance != null ? balance.sourceLabelHeight : 3f,
                                                balance != null ? balance.sourceLabelFontSize : 4f);
                }
                label.Text = text;

                bool owned = Owned;
                if (owned && filterModel == null) filterModel = BuildModel();
                if (!owned && filterModel != null) { Destroy(filterModel); filterModel = null; }
            }

            // Also a HUD line while in the hub.
            bool atHub = upgrades != null && upgrades.PlayerAtHub;
            SimpleHud.SetLine("purifier", atHub ? text : null);
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

        // A sand-coloured drum beside the boiling pot (no collider).
        GameObject BuildModel()
        {
            var t = purifier.transform;
            float side = balance != null ? balance.biosandSideOffset : 1.5f;
            var holder = new GameObject("BiosandFilter_Model");
            holder.transform.position = t.position + t.right * side;
            SourceVisuals.Part(holder.transform, "Drum", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 0.6f, 0.7f), new Color(0.85f, 0.75f, 0.5f));
            SourceVisuals.Part(holder.transform, "Lid", PrimitiveType.Cylinder, new Vector3(0f, 1.22f, 0f), new Vector3(0.75f, 0.03f, 0.75f), new Color(0.35f, 0.3f, 0.25f));
            return holder;
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
