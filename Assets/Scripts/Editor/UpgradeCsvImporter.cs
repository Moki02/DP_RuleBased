// ============================================================================
// UpgradeCsvImporter.cs   (Editor only)
// Adds the menu "Tools > The Last Drop > Import Build and Skills CSV". It reads
//   Assets/_Project/Data/BuildAndSkills/BuildAndSkills.csv
//   Assets/_Project/Data/BuildAndSkills/BuildAndSkills_Effects.csv
// and creates or updates one UpgradeData asset per row (in .../BuildAndSkills/Upgrades/)
// plus the UpgradeDatabase asset that lists them all. Change prices in the CSV and
// re-import; nothing else to do.
//
// DESIGN RULES: R7.1 to R7.4.
//
// HOW TO USE: run the menu after editing either CSV. Problems (bad numbers, unknown ids)
// are listed in a dialog and the Console.
// HOW TO EDIT: file locations are the constants below.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Imports the Build and Skills CSVs into UpgradeData assets.</summary>
    public static class UpgradeCsvImporter
    {
        /// <summary>Folder with the CSVs.</summary>
        public const string Folder = "Assets/_Project/Data/BuildAndSkills";
        /// <summary>The main CSV.</summary>
        public const string MainCsv = Folder + "/BuildAndSkills.csv";
        /// <summary>The effects CSV.</summary>
        public const string EffectsCsv = Folder + "/BuildAndSkills_Effects.csv";
        /// <summary>Where the UpgradeData assets go.</summary>
        public const string AssetFolder = Folder + "/Upgrades";
        /// <summary>The database asset.</summary>
        public const string DatabasePath = Folder + "/UpgradeDatabase.asset";

        /// <summary>Menu entry.</summary>
        [MenuItem("Tools/The Last Drop/Import Build and Skills CSV")]
        public static void ImportFromMenu()
        {
            var problems = new List<string>();
            var db = Import(problems);
            string summary = db == null
                ? "Import failed."
                : $"Imported {db.upgrades.Count} upgrades into {AssetFolder}.";
            if (problems.Count > 0) summary += $"\n\n{problems.Count} problem(s):\n- " + string.Join("\n- ", problems);
            Debug.Log("[UpgradeCsvImporter] " + summary, db);
            EditorUtility.DisplayDialog("Import Build and Skills CSV", summary, "OK");
        }

        /// <summary>Does the import. Returns the database (null if the CSVs are missing).</summary>
        public static UpgradeDatabase Import(List<string> problems)
        {
            if (!File.Exists(MainCsv) || !File.Exists(EffectsCsv))
            {
                problems.Add($"Missing {(File.Exists(MainCsv) ? EffectsCsv : MainCsv)}");
                return null;
            }

            var infos = UpgradeCsv.Parse(File.ReadAllText(MainCsv), File.ReadAllText(EffectsCsv), problems);
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder(Folder, "Upgrades");

            var db = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<UpgradeDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            db.upgrades.Clear();
            foreach (var info in infos)
            {
                string path = $"{AssetFolder}/{info.Id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<UpgradeData>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.CopyFrom(info);
                asset.name = info.Id;
                EditorUtility.SetDirty(asset);
                db.upgrades.Add(asset);
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
