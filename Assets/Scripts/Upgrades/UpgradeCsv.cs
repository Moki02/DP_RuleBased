// ============================================================================
// UpgradeCsv.cs
// Reads the two Build and Skills CSVs into UpgradeInfo objects:
//   BuildAndSkills.csv          Id, Name, Tab, Group, CostLiters, Needs, Priority, Tooltip
//   BuildAndSkills_Effects.csv  Id, Field, Value, Note   (one row per effect)
// Columns are found by their header name, so their order doesn't matter. "Needs" lists
// ids joined by | (for example W4|S3).
//
// DESIGN RULES: R7.1 to R7.4.
//
// HOW TO USE: the importer (Tools > The Last Drop > Import Build and Skills CSV) and the
// tests call UpgradeCsv.Parse(mainCsvText, effectsCsvText, problems).
// HOW TO EDIT: edit the CSVs, not this file.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheLastDrop
{
    /// <summary>Reads the Build and Skills CSVs.</summary>
    public static class UpgradeCsv
    {
        /// <summary>
        /// Every upgrade in the main CSV, in file order, with its effects attached.
        /// Anything odd (unknown tab, bad number, effect for an unknown id) is added to
        /// <paramref name="problems"/> so the importer can show it.
        /// </summary>
        public static List<UpgradeInfo> Parse(string mainCsv, string effectsCsv, List<string> problems = null)
        {
            problems ??= new List<string>();
            var list = new List<UpgradeInfo>();
            var byId = new Dictionary<string, UpgradeInfo>(StringComparer.OrdinalIgnoreCase);

            var rows = CsvReader.Parse(mainCsv);
            if (rows.Count == 0) { problems.Add("Main CSV is empty."); return list; }
            var col = Columns(rows[0]);

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Cell(row, col, "Id");
                if (id.Length == 0) continue;

                var info = new UpgradeInfo
                {
                    Id = id,
                    Name = Cell(row, col, "Name"),
                    Group = Cell(row, col, "Group"),
                    Tooltip = Cell(row, col, "Tooltip"),
                };

                string tab = Cell(row, col, "Tab");
                if (!Enum.TryParse(tab, true, out info.Tab))
                    problems.Add($"{id}: unknown Tab '{tab}' (use Build or Skills).");

                string cost = Cell(row, col, "CostLiters");
                if (!float.TryParse(cost, NumberStyles.Float, CultureInfo.InvariantCulture, out info.Cost))
                    problems.Add($"{id}: CostLiters '{cost}' is not a number.");

                string priority = Cell(row, col, "Priority");
                if (priority.Length > 0 && !Enum.TryParse(priority, true, out info.Priority))
                    problems.Add($"{id}: unknown Priority '{priority}' (use Must or Nice).");

                var needs = new List<string>();
                foreach (var n in Cell(row, col, "Needs").Split('|'))
                    if (n.Trim().Length > 0) needs.Add(n.Trim());
                info.Needs = needs.ToArray();

                if (byId.ContainsKey(id)) { problems.Add($"{id}: listed twice; the second row is ignored."); continue; }
                byId[id] = info;
                list.Add(info);
            }

            var effectRows = CsvReader.Parse(effectsCsv);
            if (effectRows.Count > 0)
            {
                var ecol = Columns(effectRows[0]);
                for (int r = 1; r < effectRows.Count; r++)
                {
                    var row = effectRows[r];
                    string id = Cell(row, ecol, "Id");
                    if (id.Length == 0) continue;
                    if (!byId.TryGetValue(id, out var info)) { problems.Add($"Effects: unknown id '{id}'."); continue; }
                    info.Effects.Add(new UpgradeEffect(Cell(row, ecol, "Field"), Cell(row, ecol, "Value"), Cell(row, ecol, "Note")));
                }
            }

            foreach (var info in list)
                foreach (var need in info.Needs)
                    if (!byId.ContainsKey(need)) problems.Add($"{info.Id}: needs unknown id '{need}'.");

            return list;
        }

        static Dictionary<string, int> Columns(string[] header)
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++) d[header[i].Trim()] = i;
            return d;
        }

        static string Cell(string[] row, Dictionary<string, int> col, string name) =>
            col.TryGetValue(name, out int i) && i < row.Length ? row[i].Trim() : "";
    }
}
