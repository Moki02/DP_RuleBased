// ============================================================================
// HoldProgressUI.cs
// The fill next to the interaction prompt while a "hold" action runs (T43). It shows how
// full the thing being filled is, so the player sees each second of holding pay off:
//   Hold E at the hand pump        -> Raw Basin level
//   Hold E at the hub tap (Drink)  -> HYD
//   Hold E at the hub tap (Refill) -> canteen
//   Hold E at a cistern / jug      -> canteen
//   Hold E at a lock basin         -> the basin's water
//   Hold Q (drinking the canteen)  -> HYD
// It is hidden when nothing is being held.
//
// DESIGN RULES: Controls table (hold E / hold Q), R8.9 (hold E to pour), T43.
//
// HOW TO USE: in the HUD prefab (Assets/Prefabs/UI/HUD.prefab) under "Interaction Text", next
// to the "E" key box. Drag in "Root" (shown / hidden) and "Fill" (an Image set to Filled).
// HOW TO EDIT: the look is in the prefab: the ring/bar sprite, its colour, size and place.
// ============================================================================

using TheLastDrop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a fill for the hold action that is running (pumping, pouring, drinking, refilling).</summary>
public class HoldProgressUI : MonoBehaviour
{
    [Tooltip("Shown while a hold action runs, hidden otherwise. Empty = this object's first child.")]
    public GameObject root;
    [Tooltip("An Image with Image Type = Filled. Its Fill Amount goes from 0 (empty) to 1 (full).")]
    public Image fill;
    [Tooltip("Optional text, e.g. \"62%\".")]
    public TMP_Text percentText;

    void Update()
    {
        float p = Progress();
        bool show = p >= 0f;
        var r = root != null ? root : (transform.childCount > 0 ? transform.GetChild(0).gameObject : null);
        if (r != null && r.activeSelf != show) r.SetActive(show);
        if (!show) return;
        if (fill != null) fill.fillAmount = p;
        if (percentText != null) percentText.text = Mathf.FloorToInt(p * 100f) + "%";
    }

    /// <summary>0-1 for the hold action running now, or -1 when none is.</summary>
    public static float Progress()
    {
        var gm = Game_Manager.instance;
        if (gm != null && !gm.CanControlPlayer()) return -1f;

        var res = GameResources_Manager.instance;
        var player = Player_Manager.instance;
        var held = PlayerInteraction_Manager.instance != null ? PlayerInteraction_Manager.instance.ActiveHold : null;

        if (held != null)
        {
            if (held is HubPump_Manager && res != null) return Ratio(res.rawBasin, res.rawBasin_Max);
            if (held is LockBasin_Manager basin && basin.Lock != null) return Mathf.Clamp01(basin.Lock.Fill);
            if (held is HubTapPoint_Manager tap)
                return tap.tapAction == HubTapPoint_Manager.TapAction.Drink ? Hyd(player) : Canteen(res);
            if (held is RoomWaterSource) return Canteen(res);
            return -1f;
        }

        // Hold Q: drinking from the canteen.
        if (player != null && player.playerHydration_Manager != null && player.playerHydration_Manager.IsDrinking)
            return Hyd(player);
        return -1f;
    }

    static float Ratio(float v, float max) => max <= 0f ? 1f : Mathf.Clamp01(v / max);
    static float Canteen(GameResources_Manager res) => res != null ? Ratio(res.canteen, res.canteen_Max) : -1f;
    static float Hyd(Player_Manager p) =>
        p != null && p.playerStats_Manager != null ? Ratio(p.playerStats_Manager.player_Hydration, p.playerStats_Manager.player_MaxHydration) : -1f;
}
