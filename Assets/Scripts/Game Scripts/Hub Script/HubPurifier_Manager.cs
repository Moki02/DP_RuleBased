// ============================================================================
// HubPurifier_Manager.cs
// The purifier: moves Raw Basin water into Clean Storage on its own (no button) while the
// basin has water and storage has room (R3.2, R3.3, R6.2). The boiling pot does 1 L every
// 6 s (0.167 L/s).
//
// A better purifier sets RateOverride: the Biosand Filter (Sources/BiosandFilter.cs, P1)
// does, with its ripening rate (R6.1: only the best purifier owned runs). With no override
// the boiling pot rate from GameBalance is used. It stops while Clean Storage is full, so
// no water is lost (R3.3), and pauses while the game is frozen (time scale 0).
//
// What the player sees (so pumping has a visible result):
//   - the pot's name text ("Boiling Pot") gets two more lines: the Raw Basin water waiting to
//     be boiled ("Raw water 3.5 / 15.0 L") and what the pot is doing ("Boiling" / "Empty:
//     pump water in" / "Clean Storage full"). If the pot has no name text, a floating label
//     is made instead;
//   - the water inside the pot (child "Pot_Water") shows only while the Raw Basin has water;
//   - the lid animation plays only while the pot is actually boiling.
//
// DESIGN RULES: R3.2, R3.3, R6.1, R6.2, R4.6 (labels: the player never guesses).
//
// HOW TO USE: on the Boiling Pot prefab (the Build Playtest Scene menu places one in the hub).
// HOW TO EDIT: GameBalance > Purifiers > Boil Rate. The label's first line is the prefab's
// own name text; the water child's name is "Water Object Name" below.
// ============================================================================

using System;
using UnityEngine;
using TMPro;
using TheLastDrop;

/// <summary>The hub's purifier: turns Raw Basin water into Clean Storage water on its own.</summary>
public class HubPurifier_Manager : MonoBehaviour
{
    /// <summary>
    /// The main purifier's rate in L/s if something better than the pot is owned (Biosand
    /// Filter). Return a value below 0 to use the boiling pot. Null = boiling pot.
    /// </summary>
    public static Func<float> RateOverride;

    // Static fields survive between Play sessions in the editor when "domain reload" is off.
    // Start each Play with no override. (Within a session, BiosandFilter clears its hook in
    // OnDestroy when the scene reloads.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => RateOverride = null;

    [Header("Purifier Settings (old scenes; GameBalance wins when present)")]
    [Tooltip("Boiling pot rate in litres per second (1 L every 6 s = 0.167).")]
    public float boilingPotRate = 1f / 6f;

    [Header("Visuals (all optional)")]
    [Tooltip("The text over the pot. Empty = the child text that says \"Boiling Pot\", or a new floating label.")]
    public TMP_Text levelLabel;
    [Tooltip("Name of the child object that is the water inside the pot. Shown only while the Raw Basin has water.")]
    public string waterObjectName = "Pot_Water";

    private GameResources_Manager resources;
    private GameBalance balance;

    private GameObject waterObject;
    private Animator lidAnimator;
    private string labelTitle = "Boiling Pot";
    private bool boilingThisFrame;

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
        resources = Game_Manager.instance != null ? Game_Manager.instance.gameResources_Manager : null;
        balance = BalanceProvider.Current;

        // The water mesh inside the pot, and the animator that moves the lid.
        Transform water = FindChildByName(transform, waterObjectName);
        if (water != null) waterObject = water.gameObject;
        lidAnimator = GetComponentInChildren<Animator>();

        if (levelLabel == null)
            levelLabel = FindOrMakeLabel(transform, "Boiling Pot");
        if (levelLabel != null)
            labelTitle = FirstLine(levelLabel.text, "Boiling Pot");

        UpdateVisuals();
    }

    private void Update()
    {
        if (resources == null)
            return;

        boilingThisFrame = false;

        if (Time.timeScale != 0f)
            PurifyWater();

        UpdateVisuals();
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
        boilingThisFrame = transferred > 0f;
    }

    // ------------------------------------------------------------------ What the player sees

    private void UpdateVisuals()
    {
        if (resources == null)
            return;

        bool hasWater = resources.rawBasin > 0.0001f;

        if (waterObject != null && waterObject.activeSelf != hasWater)
            waterObject.SetActive(hasWater);

        // The lid rattles only while water is really being boiled.
        if (lidAnimator != null)
            lidAnimator.speed = boilingThisFrame ? 1f : 0f;

        if (levelLabel == null)
            return;

        string status;
        if (!hasWater)
            status = "Empty: pump water in";
        else if (resources.cleanStorage >= resources.cleanStorage_Max - 0.0001f)
            status = "Paused: Clean Storage full";
        else
            status = (RateOverride != null && RateOverride() >= 0f ? "Filtering" : "Boiling") +
                     $" {CurrentRate:0.##} L/s into Clean Storage";

        string text = labelTitle + "\n<size=70%>Raw water " +
                      resources.FormatWater(resources.rawBasin) + " / " +
                      resources.FormatWater(resources.rawBasin_Max) +
                      "\n" + status + "</size>";

        if (levelLabel.text != text)
            levelLabel.text = text;
    }

    // ------------------------------------------------------------------ Helpers (also used by HubTap_Manager)

    /// <summary>
    /// The child text whose first line is <paramref name="title"/> (the prefab's name text, e.g.
    /// "Clean Storage"). If there is none, a floating label is made above <paramref name="root"/>.
    /// </summary>
    public static TMP_Text FindOrMakeLabel(Transform root, string title)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t != null && FirstLine(t.text, "").Equals(title, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        // No name text on this prefab: make a plain floating one (same look as the source labels).
        var b = BalanceProvider.Current;
        FacingLabel label = SourceVisuals.Label(root, b != null ? b.sourceLabelHeight : 3f,
                                                b != null ? b.sourceLabelFontSize : 4f);
        label.Text = title;
        return label.text;
    }

    /// <summary>The first non-empty line of a text, without spaces or rich-text tags around it.</summary>
    public static string FirstLine(string text, string fallback)
    {
        if (string.IsNullOrEmpty(text))
            return fallback;
        foreach (string line in text.Split('\n'))
        {
            string l = line.Trim();
            int tag = l.IndexOf('<');
            if (tag >= 0) l = l.Substring(0, tag).Trim();
            if (l.Length > 0) return l;
        }
        return fallback;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && t.name == childName)
                return t;
        }
        return null;
    }
}
