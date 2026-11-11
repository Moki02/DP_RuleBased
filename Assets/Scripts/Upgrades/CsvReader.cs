// ============================================================================
// CsvReader.cs
// Reads CSV text (as exported by Google Sheets) into rows of cells. Handles cells in
// "double quotes" that contain commas, and "" inside quotes meaning one quote mark.
//
// DESIGN RULES: none (a tool).
//
// HOW TO USE: var rows = CsvReader.Parse(textAsset.text);   rows[0] is the header row.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using System.Collections.Generic;
using System.Text;

namespace TheLastDrop
{
    /// <summary>Turns CSV text into rows of cells.</summary>
    public static class CsvReader
    {
        /// <summary>All non-empty rows of the CSV, each split into cells (quotes removed).</summary>
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return rows;

            var cells = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // "" inside quotes is a quote mark; a single " ends the quoted part.
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                    continue;
                }

                if (c == '"') inQuotes = true;
                else if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; // Windows line end
                    EndRow();
                }
                else cell.Append(c);
            }
            EndRow();
            return rows;

            void EndRow()
            {
                cells.Add(cell.ToString());
                cell.Clear();
                bool empty = true;
                foreach (var s in cells) if (s.Trim().Length > 0) { empty = false; break; }
                if (!empty) rows.Add(cells.ToArray());
                cells.Clear();
            }
        }
    }
}
