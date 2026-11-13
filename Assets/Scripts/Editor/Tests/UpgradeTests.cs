// ============================================================================
// UpgradeTests.cs   (EditMode tests)
// Checks the Build and Skills rules: reading the CSVs, card state order, prerequisites,
// the Clean Storage cap following S2 / S3 / S6, buying (no refunds, hub only unless the
// playtest toggle is on), effects lookup, and the Demo mode price multiplier.
//
// DESIGN RULES: R7.1 to R7.4.
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: if the CSV prices change on purpose, update the numbers below.
// Start values match GameBalance's defaults: 10 L stored in a 25 L Tapayan.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TheLastDrop
{
    /// <summary>Tests for CsvReader, UpgradeCsv and UpgradeLogic.</summary>
    public class UpgradeTests
    {
        const string Folder = "Assets/_Project/Data/BuildAndSkills/";
        const float StartStored = 10f, Tapayan = 25f;

        /// <summary>Every upgrade from the real CSVs.</summary>
        public static List<UpgradeInfo> LoadReal(List<string> problems = null) =>
            UpgradeCsv.Parse(File.ReadAllText(Folder + "BuildAndSkills.csv"),
                             File.ReadAllText(Folder + "BuildAndSkills_Effects.csv"), problems);

        static UpgradeLogic Real(float mult = 1f) => new UpgradeLogic(LoadReal(), StartStored, Tapayan, mult);

        static UpgradeInfo U(string id, float cost, params string[] needs) =>
            new UpgradeInfo { Id = id, Name = id, Cost = cost, Needs = needs };

        // ------------------------------------------------------------------ CSV

        [Test]
        public void CsvReader_HandlesQuotesAndCommas()
        {
            var rows = CsvReader.Parse("a,\"b, c\",\"say \"\"hi\"\"\"\r\n\r\n,x,\n");
            Assert.AreEqual(2, rows.Count, "blank rows are skipped");
            CollectionAssert.AreEqual(new[] { "a", "b, c", "say \"hi\"" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "", "x", "" }, rows[1]);
        }

        [Test]
        public void RealCsv_35Upgrades_NoProblems()
        {
            var problems = new List<string>();
            var all = LoadReal(problems);
            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(35, all.Count);
            Assert.AreEqual(19, all.Count(u => u.Tab == UpgradeTab.Build));
            Assert.AreEqual(16, all.Count(u => u.Tab == UpgradeTab.Skills));
            Assert.AreEqual(45, all.Sum(u => u.Effects.Count), "one per effects row");

            var w7 = all.First(u => u.Id == "W7");
            CollectionAssert.AreEqual(new[] { "W4", "S3" }, w7.Needs);
            Assert.AreEqual(120f, w7.Cost);
            StringAssert.Contains("Always on", w7.Tooltip, "quoted tooltip with a comma survives");
        }

        // ------------------------------------------------------------------ Card states

        [Test]
        public void CardState_OrderIsOwnedLockedBiggerShortCanBuy()
        {
            var logic = new UpgradeLogic(new[]
            {
                U("A", 5), U("B", 5, "A"), U("Big", 40), U("Mid", 20), U("BigLocked", 40, "A"),
            }, StartStored, Tapayan);

            Assert.AreEqual(CardState.CanBuy, logic.GetCard("A").State);
            var b = logic.GetCard("B");
            Assert.AreEqual(CardState.Locked, b.State);
            Assert.AreEqual("A", b.MissingId, "the chip names what's missing");
            Assert.AreEqual(CardState.NeedsBiggerStorage, logic.GetCard("Big").State, "bigger storage is checked before short of water");
            var mid = logic.GetCard("Mid");
            Assert.AreEqual(CardState.ShortOfWater, mid.State);
            Assert.AreEqual(10f, mid.ShortBy, 1e-4f, "20 L needed, 10 L stored");
            Assert.AreEqual(CardState.Locked, logic.GetCard("BigLocked").State, "locked is checked before bigger storage");

            Assert.AreEqual(BuyResult.Bought, logic.Buy("A", true, false));
            Assert.AreEqual(CardState.Owned, logic.GetCard("A").State);
            Assert.AreEqual(CardState.CanBuy, logic.GetCard("B").State, "its need is owned now");
        }

        [Test]
        public void RealCards_AtTheStart()
        {
            var logic = Real();
            Assert.AreEqual(CardState.CanBuy, logic.GetCard("W1").State, "8 L, 10 stored");
            var w2 = logic.GetCard("W2");
            Assert.AreEqual(CardState.ShortOfWater, w2.State);
            Assert.AreEqual(5f, w2.ShortBy, 1e-4f);
            Assert.AreEqual("W1", logic.GetCard("W4").MissingId);
            Assert.AreEqual("W4", logic.GetCard("W7").MissingId, "first missing need is named");
            Assert.AreEqual("W2", logic.GetCard("W5").MissingId, "W5 needs W2 (checked before its 30 L size)");
            Assert.AreEqual(CardState.CanBuy, logic.GetCard("S1").State, "10 L with exactly 10 stored");
            Assert.AreEqual(CardState.ShortOfWater, logic.GetCard("M1").State, "15 L with 10 stored");
        }

        [Test]
        public void Prerequisites_W1ThenW4()
        {
            var logic = Real();
            logic.AddStored(100f);                         // fills the 25 L Tapayan
            Assert.AreEqual(Tapayan, logic.Stored);
            Assert.AreEqual(BuyResult.NotAvailable, logic.Buy("W4", true, false), "needs W1");
            Assert.AreEqual(BuyResult.Bought, logic.Buy("W1", true, false));
            Assert.AreEqual(17f, logic.Stored, 1e-4f, "25 - 8");
            Assert.AreEqual(CardState.ShortOfWater, logic.GetCard("W4").State, "W1 owned now, but W4 costs 20 and 17 are stored");
            logic.AddStored(100f);
            Assert.AreEqual(BuyResult.Bought, logic.Buy("W4", true, false));
            Assert.AreEqual(5f, logic.Stored, 1e-4f, "25 - 20");
        }

        // ------------------------------------------------------------------ Storage cap

        [Test]
        public void StorageCap_FollowsS2S3S6()
        {
            var logic = Real();
            Assert.AreEqual(25f, logic.Cap, "Tapayan");
            logic.AddStored(1000f);
            Assert.AreEqual(25f, logic.Stored, "never past the cap");

            Assert.AreEqual(BuyResult.Bought, logic.Buy("S2", true, false));
            Assert.AreEqual(50f, logic.Cap, "Plastic Drum");
            Assert.AreEqual(10f, logic.Stored, 1e-4f, "25 - 15");

            logic.AddStored(1000f);
            Assert.AreEqual(CardState.CanBuy, logic.GetCard("S3").State, "35 L fits in 50");
            logic.Buy("S3", true, false);
            Assert.AreEqual(150f, logic.Cap, "Ferrocement Tank");

            Assert.AreEqual(CardState.ShortOfWater, logic.GetCard("S6").State, "150 L fits now, but only 15 stored");
            logic.AddStored(1000f);
            logic.Buy("S6", true, false);
            Assert.AreEqual(300f, logic.Cap, "Underground Cistern");
            Assert.AreEqual(0f, logic.Stored, 1e-4f, "150 - 150");
        }

        [Test]
        public void NeedsBiggerStorage_UntilS2()
        {
            var logic = Real();
            logic.AddStored(1000f);
            logic.Buy("W2", true, false);                  // 15 L: W5 (30 L) now only needs storage
            Assert.AreEqual(CardState.NeedsBiggerStorage, logic.GetCard("W5").State, "30 L > 25 L Tapayan (R7.3)");
            logic.AddStored(1000f);
            logic.Buy("S2", true, false);
            Assert.AreNotEqual(CardState.NeedsBiggerStorage, logic.GetCard("W5").State);
        }

        // ------------------------------------------------------------------ Buying

        [Test]
        public void Buy_OnlyAtHub_UnlessAllowedAnywhere()
        {
            var logic = Real();
            Assert.AreEqual(BuyResult.NotAtHub, logic.Buy("W1", atHub: false, allowAnywhere: false));
            Assert.AreEqual(10f, logic.Stored, "nothing taken");
            Assert.AreEqual(BuyResult.Bought, logic.Buy("W1", atHub: false, allowAnywhere: true));
        }

        [Test]
        public void Buy_IsPermanent_NoSecondBuy()
        {
            var logic = Real();
            logic.Buy("W1", true, false);
            Assert.AreEqual(2f, logic.Stored, 1e-4f);
            Assert.AreEqual(BuyResult.NotAvailable, logic.Buy("W1", true, false), "already owned");
            Assert.AreEqual(2f, logic.Stored, 1e-4f, "nothing taken twice, nothing given back");
            CollectionAssert.AreEqual(new[] { "W1" }, logic.Owned);
        }

        // ------------------------------------------------------------------ Effects

        [Test]
        public void Effects_Lookup()
        {
            var logic = Real();
            logic.AddStored(1000f);
            logic.Buy("S2", true, false);                  // 50 L storage, so V3 (30 L) can fit
            logic.AddStored(1000f);
            Assert.AreEqual(4f, logic.GetFloat("walkSpeed", 4f), "default before M1");
            Assert.AreEqual(BuyResult.Bought, logic.Buy("M1", true, false));
            Assert.AreEqual(4.4f, logic.GetFloat("walkSpeed", 4f), 1e-4f);

            Assert.AreEqual(BuyResult.Bought, logic.Buy("V1", true, false));
            Assert.AreEqual(4f, logic.GetFloat("lookAheadTiles", 2f));
            logic.AddStored(1000f);
            Assert.AreEqual(BuyResult.Bought, logic.Buy("V3", true, false));
            Assert.IsTrue(logic.GetBool("revealDiagonals", false));
            Assert.IsFalse(logic.GetBool("gatesOnMap", false), "V2 not owned");
        }

        [Test]
        public void Effects_LastBoughtWins()
        {
            var logic = Real();
            logic.AddStored(1000f);
            logic.Buy("S1", true, false);
            Assert.AreEqual(4f, logic.GetFloat("canteenCapacity", 2f));
            logic.Buy("S2", true, false);
            logic.AddStored(1000f);
            logic.Buy("S4", true, false);
            Assert.AreEqual(6f, logic.GetFloat("canteenCapacity", 2f));
        }

        [Test]
        public void DemoMultiplier_RoundsUp()
        {
            var logic = Real(0.5f);
            Assert.AreEqual(4f, logic.GetCard("W1").Cost, "8 × 0.5");
            Assert.AreEqual(18f, logic.CostOf(logic.Get("M2")), "35 × 0.5 = 17.5, rounded up");
        }

        [Test]
        public void Restore_PutsBackOwnedAndStored()
        {
            var logic = Real();
            logic.Restore(new[] { "S2", "W1", "NOPE" }, 40f);
            CollectionAssert.AreEqual(new[] { "S2", "W1" }, logic.Owned, "unknown ids are dropped");
            Assert.AreEqual(50f, logic.Cap);
            Assert.AreEqual(40f, logic.Stored);
        }
    }
}
