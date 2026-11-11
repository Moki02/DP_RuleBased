// ============================================================================
// UpgradeLogic.cs
// The Build and Skills rules as plain C# (tested):
//   - which upgrades are owned (in the order they were bought) and how much water is in
//     Clean Storage;
//   - Clean Storage's cap follows the best storage owned: 25 L Tapayan to start, S2 Plastic
//     Drum 50 L, S3 Ferrocement Tank 150 L, S6 Underground Cistern 300 L (from the effects
//     CSV, field "cleanStorageCap");
//   - each card's state, checked in this order:
//       Owned               already bought
//       Locked              something it needs isn't owned (the chip names it)
//       NeedsBiggerStorage  it costs more than Clean Storage can hold (R7.3)
//       ShortOfWater        it costs more than what's stored (the chip says how much is missing)
//       CanBuy              otherwise
//   - buying takes the cost from Clean Storage for good: no refunds (R7.4);
//   - buying only works at the hub, unless "Allow buying anywhere (playtest)" is on;
//   - effects: GetFloat("walkSpeed", 4) gives 4.4 once M1 is owned. If two owned upgrades
//     set the same field, the one bought LAST wins (S1, then S4, then S7 for the canteen).
//
// DESIGN RULES: R7.1 to R7.4.
//
// HOW TO USE: UpgradeManager owns one. You don't put this on an object.
// HOW TO EDIT: prices and effects are in the CSVs; start storage and the 25 L Tapayan are
// in GameBalance ("Start Clean", "Clean Storage Tiers" [0]).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheLastDrop
{
    /// <summary>What a card shows, checked in this order.</summary>
    public enum CardState
    {
        /// <summary>Already bought.</summary>
        Owned,
        /// <summary>Something it needs isn't owned yet.</summary>
        Locked,
        /// <summary>Costs more than Clean Storage can hold (R7.3).</summary>
        NeedsBiggerStorage,
        /// <summary>Costs more than what's stored right now.</summary>
        ShortOfWater,
        /// <summary>Can be bought now.</summary>
        CanBuy
    }

    /// <summary>A card's state plus the details its chip shows.</summary>
    public struct CardInfo
    {
        /// <summary>The state.</summary>
        public CardState State;
        /// <summary>Locked: the first missing id (e.g. "W2"). Otherwise null.</summary>
        public string MissingId;
        /// <summary>Short of water: how many litres are missing. Otherwise 0.</summary>
        public float ShortBy;
        /// <summary>The price in litres (after the Demo mode multiplier).</summary>
        public float Cost;
    }

    /// <summary>What happened when the player tried to buy.</summary>
    public enum BuyResult
    {
        /// <summary>Bought: the cost was taken from Clean Storage.</summary>
        Bought,
        /// <summary>Not at the hub (and buying anywhere is off).</summary>
        NotAtHub,
        /// <summary>The card isn't in the Can buy state.</summary>
        NotAvailable,
        /// <summary>No upgrade with that id.</summary>
        UnknownId
    }

    /// <summary>Owned upgrades, Clean Storage and the buying rules.</summary>
    public class UpgradeLogic
    {
        readonly Dictionary<string, UpgradeInfo> byId = new Dictionary<string, UpgradeInfo>(StringComparer.OrdinalIgnoreCase);
        readonly List<UpgradeInfo> all = new List<UpgradeInfo>();
        readonly List<string> owned = new List<string>();
        readonly float baseCap;
        readonly float costMultiplier;

        /// <summary>The effect field that sets Clean Storage's size.</summary>
        public const string CleanStorageCapField = "cleanStorageCap";

        /// <summary>Fired after anything changes (buy, water added, restore).</summary>
        public event Action Changed;

        /// <summary>Sets up the rules.</summary>
        /// <param name="upgrades">Every Build item and skill.</param>
        /// <param name="startStored">Litres in Clean Storage at the start (10).</param>
        /// <param name="baseCap">Clean Storage size before any storage upgrade (the 25 L Tapayan).</param>
        /// <param name="costMultiplier">Multiplies every price (1 in the full game, 0.5 in Demo mode), rounded up.</param>
        public UpgradeLogic(IEnumerable<UpgradeInfo> upgrades, float startStored, float baseCap, float costMultiplier = 1f)
        {
            foreach (var u in upgrades)
            {
                if (u == null || string.IsNullOrEmpty(u.Id) || byId.ContainsKey(u.Id)) continue;
                byId[u.Id] = u;
                all.Add(u);
            }
            this.baseCap = baseCap;
            this.costMultiplier = costMultiplier;
            Stored = Math.Min(startStored, Cap);
        }

        /// <summary>Every upgrade, in CSV order.</summary>
        public IReadOnlyList<UpgradeInfo> All => all;

        /// <summary>Owned ids, in the order they were bought.</summary>
        public IReadOnlyList<string> Owned => owned;

        /// <summary>Litres in Clean Storage.</summary>
        public float Stored { get; private set; }

        /// <summary>Clean Storage's size: the base Tapayan, or the biggest storage upgrade owned.</summary>
        public float Cap
        {
            get
            {
                float cap = baseCap;
                foreach (var id in owned)
                    if (TryGetEffect(byId[id], CleanStorageCapField, out string v) && TryNumber(v, out float n))
                        cap = Math.Max(cap, n);
                return cap;
            }
        }

        /// <summary>The upgrade with this id, or null.</summary>
        public UpgradeInfo Get(string id) => id != null && byId.TryGetValue(id, out var u) ? u : null;

        /// <summary>True if this id is owned.</summary>
        public bool Owns(string id) => owned.Exists(o => string.Equals(o, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Price in litres after the Demo mode multiplier, rounded up to whole litres.</summary>
        public float CostOf(UpgradeInfo u) => (float)Math.Ceiling(u.Cost * costMultiplier - 1e-4);

        // ------------------------------------------------------------------ Card state

        /// <summary>The card state for this id, checked in the order Owned, Locked, Needs bigger storage, Short of water, Can buy.</summary>
        public CardInfo GetCard(string id)
        {
            var u = Get(id);
            if (u == null) return new CardInfo { State = CardState.Locked, MissingId = id };
            var card = new CardInfo { Cost = CostOf(u) };

            if (Owns(u.Id)) { card.State = CardState.Owned; return card; }

            foreach (var need in u.Needs)
                if (!Owns(need)) { card.State = CardState.Locked; card.MissingId = need; return card; }

            if (card.Cost > Cap + 1e-4f) { card.State = CardState.NeedsBiggerStorage; return card; }

            if (card.Cost > Stored + 1e-4f)
            {
                card.State = CardState.ShortOfWater;
                card.ShortBy = card.Cost - Stored;
                return card;
            }

            card.State = CardState.CanBuy;
            return card;
        }

        // ------------------------------------------------------------------ Buying

        /// <summary>
        /// Buys the upgrade if its card says Can buy and the player may buy here.
        /// The cost is taken for good (no refunds, R7.4).
        /// </summary>
        public BuyResult Buy(string id, bool atHub, bool allowAnywhere)
        {
            var u = Get(id);
            if (u == null) return BuyResult.UnknownId;
            if (!atHub && !allowAnywhere) return BuyResult.NotAtHub;
            var card = GetCard(id);
            if (card.State != CardState.CanBuy) return BuyResult.NotAvailable;

            Stored -= card.Cost;
            owned.Add(u.Id);
            Changed?.Invoke();
            return BuyResult.Bought;
        }

        /// <summary>Adds water to Clean Storage, never past the cap. Returns how much went in.</summary>
        public float AddStored(float litres)
        {
            if (litres <= 0f) return 0f;
            float added = Math.Min(litres, Math.Max(0f, Cap - Stored));
            Stored += added;
            if (added > 0f) Changed?.Invoke();
            return added;
        }

        /// <summary>Loading a save: sets the owned ids (in buy order) and the stored water.</summary>
        public void Restore(IEnumerable<string> ownedIds, float stored)
        {
            owned.Clear();
            foreach (var id in ownedIds)
                if (byId.ContainsKey(id) && !Owns(id)) owned.Add(byId[id].Id);
            Stored = Math.Max(0f, Math.Min(stored, Cap));
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ Effects

        /// <summary>The value of an effect field from owned upgrades (last bought wins), or <paramref name="defaultValue"/>.</summary>
        public float GetFloat(string field, float defaultValue)
        {
            return TryGetOwnedValue(field, out string v) && TryNumber(v, out float n) ? n : defaultValue;
        }

        /// <summary>A true/false effect field from owned upgrades (last bought wins), or <paramref name="defaultValue"/>.</summary>
        public bool GetBool(string field, bool defaultValue)
        {
            return TryGetOwnedValue(field, out string v) && bool.TryParse(v, out bool b) ? b : defaultValue;
        }

        /// <summary>A text effect field from owned upgrades (last bought wins), or <paramref name="defaultValue"/>.</summary>
        public string GetString(string field, string defaultValue)
        {
            return TryGetOwnedValue(field, out string v) ? v : defaultValue;
        }

        bool TryGetOwnedValue(string field, out string value)
        {
            // Walk the owned list backwards so the most recently bought upgrade wins.
            for (int i = owned.Count - 1; i >= 0; i--)
                if (TryGetEffect(byId[owned[i]], field, out value)) return true;
            value = null;
            return false;
        }

        static bool TryGetEffect(UpgradeInfo u, string field, out string value)
        {
            foreach (var e in u.Effects)
                if (string.Equals(e.field, field, StringComparison.OrdinalIgnoreCase)) { value = e.value; return true; }
            value = null;
            return false;
        }

        static bool TryNumber(string s, out float n) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
    }
}
