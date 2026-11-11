// ============================================================================
// GameBalance.Drought.cs
// More GameBalance values: the drought (stage radio messages, the day 5 El Niño line,
// the stage lighting and evaporation). Same GameBalance asset, split over files
// ("partial") so each feature adds its own numbers.
//
// The stage numbers themselves (heat ×, evaporation per day, water table depth) are in
// GameBalance.cs > "Drought stages", and the hand pump's depth tables are "Pump By Depth"
// and "Deep Set Pump By Depth". Those code defaults already match the GDD water table
// table (section 4): Stage 1 4 m 100%, Stage 2 5.5 m 80%, Stage 3 6.5 m 50%,
// Stage 4 7.5 m 0% (past the 7 m suction limit); Deep-Set Cylinder 100/100/80/60%.
//
// DESIGN RULES: R3.4, R5.2 (evaporation), R10.1 (radio, lighting), R10.2 (day 5 line).
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Drought: radio" and
// "Drought: evaporation". Arrays are one entry per stage, in stage order.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Drought: radio (R10.1, R10.2)")]
        [Tooltip("One line under the big stage banner, per stage (index 0 = El Niño Advisory).")]
        public string[] stageBannerLines =
        {
            "Water is still easy to find. Store what you can.",
            "Less rain, hotter days. The pump is getting weaker.",
            "The wells are dropping. Every litre counts now.",
            "No rain is coming. The pump has run dry. Find the exit.",
        };

        [Tooltip("The radio message for each stage, shown as a toast after \"RADIO: \" (R10.1).")]
        public string[] stageRadioLines =
        {
            "PAGASA has issued an El Niño Advisory. Expect below-normal rainfall in the coming weeks.",
            "PAGASA: dry spell declared in your province. Rainfall is well below normal. Save water.",
            "PAGASA: drought conditions now in your area. Water tables are falling; deep wells only.",
            "The province is under a State of Calamity. Water rationing is in force. Stay off the roads at midday.",
        };

        [Tooltip("The day the radio says El Niño has peaked (R10.2).")]
        [Min(1)] public int elNinoPeakDay = 5;

        [Tooltip("What the radio says on that day (R10.2).")]
        public string elNinoPeakLine = "PAGASA: El Niño has peaked in the Pacific.";

        [Tooltip("Seconds the stage banner stays on screen.")]
        [Min(0.5f)] public float stageBannerSeconds = 5f;

        [Tooltip("Seconds each radio toast stays on screen.")]
        [Min(0.5f)] public float radioToastSeconds = 6f;

        [Header("Drought: stage lighting (R10.1, placeholder until the artist's presets)")]
        [Tooltip("ON: the sun gets warmer and brighter each stage. OFF: the drought never touches the lights.")]
        public bool stageLighting = true;

        [Tooltip("Sun colour multiplier per stage (white = the scene's own colour).")]
        public Color[] stageSunTint =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 0.95f, 0.86f),
            new Color(1f, 0.88f, 0.72f),
            new Color(1f, 0.80f, 0.60f),
        };

        [Tooltip("Sun intensity multiplier per stage (1 = the scene's own intensity).")]
        public float[] stageSunIntensity = { 1f, 1.08f, 1.16f, 1.25f };

        [Header("Drought: evaporation (R3.4, R5.2)")]
        [Tooltip("Raw Basin cover factor. 1 = open (GDD: the Raw Basin always evaporates at the full rate).")]
        [Range(0f, 1f)] public float rawBasinEvaporationFactor = 1f;

        [Tooltip("Spare jugs cover factor. 1 = open (R3.5: full stage rate).")]
        [Range(0f, 1f)] public float spareJugEvaporationFactor = 1f;

        [Tooltip("Losses smaller than this (L) don't show a toast (they're still taken).")]
        [Min(0f)] public float evaporationToastMinLiters = 0.05f;
    }
}
