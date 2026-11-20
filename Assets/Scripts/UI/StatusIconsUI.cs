// ============================================================================
// StatusIconsUI.cs
// The row of status icons on the HUD (T43). Each slot shows only while its effect is on:
//   Sick      - after drinking raw water (R2.5); the timer counts the seconds left.
//   Chlorine  - treating water with a chlorine tablet. NOT IN THE GAME CODE YET: the slot is in
//               the prefab but always hidden. When chlorine is added, set ChlorineSecondsLeft.
//   Jerry Can - the S7 Jerry Can slowdown while carrying more than the heavy limit (R5.4).
//               It has no timer; the text shows the litres carried instead.
//
// DESIGN RULES: R2.5 (Sick), R5.4 (Jerry Can slowdown), T43.
//
// HOW TO USE: in the HUD prefab (Assets/Prefabs/UI/HUD.prefab), "Status Icons". Each slot is
// an object with an icon and a text; drag the slot and its text in below.
// HOW TO EDIT: the look (icons, colours, positions) is in the prefab. The slots are in a
// Horizontal Layout Group, so hidden ones leave no gap.
// ============================================================================

using TheLastDrop;
using TMPro;
using UnityEngine;

/// <summary>Shows or hides the Sick / Chlorine / Jerry Can icons and writes their timers.</summary>
public class StatusIconsUI : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        [Tooltip("The whole slot (icon + text). Shown only while the effect is on.")]
        public GameObject root;
        [Tooltip("The timer or value text.")]
        public TMP_Text text;
    }

    public Slot sick = new Slot();
    [Tooltip("Not in the game code yet: always hidden until ChlorineSecondsLeft is set by the chlorine feature.")]
    public Slot chlorine = new Slot();
    public Slot jerryCan = new Slot();

    /// <summary>Seconds of chlorine treatment left (0 = none). Nothing sets it yet: chlorine tablets aren't in the code.</summary>
    public static float ChlorineSecondsLeft;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => ChlorineSecondsLeft = 0f;

    void Update()
    {
        var player = Player_Manager.instance;
        var hyd = player != null ? player.playerHydration_Manager : null;

        float sickLeft = hyd != null ? hyd.SickSecondsLeft : 0f;
        Set(sick, sickLeft > 0f, Mathf.CeilToInt(sickLeft) + "s");

        Set(chlorine, ChlorineSecondsLeft > 0f, Mathf.CeilToInt(ChlorineSecondsLeft) + "s");

        // Same check as PlayerLocomotion_Manager's Jerry Can slowdown (R5.4).
        var res = GameResources_Manager.instance;
        var balance = BalanceProvider.Current;
        bool heavy = res != null && balance != null && Skills.F("heavySpeedMult", 1f) < 1f && res.canteen > balance.heavyCarryThreshold;
        Set(jerryCan, heavy, heavy ? res.FormatWater(res.canteen) : "");
    }

    static void Set(Slot s, bool on, string text)
    {
        if (s == null || s.root == null) return;
        if (s.root.activeSelf != on) s.root.SetActive(on);
        if (on && s.text != null && s.text.text != text) s.text.text = text;
    }
}
