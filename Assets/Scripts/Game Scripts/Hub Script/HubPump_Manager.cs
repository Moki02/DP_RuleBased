// ============================================================================
// HubPump_Manager.cs
// The hand pump (poso). Hold E within 2 m: one stroke every 0.5 s (0.35 s with W1 Greased
// Handle), 0.3 L per stroke into the Raw Basin. When the Raw Basin is full the handle locks
// and FULL shows; no water is lost (R4.2).
//
// The drought makes it weaker: the output is multiplied by OutputFactor, which the drought
// stage system sets (water table depth, R4.1 table and W4 Deep-Set Cylinder). Until that
// system exists the factor is 1.
//
// DESIGN RULES: R4.1, R4.2, section 4 table.
//
// HOW TO USE: on the Hand Pump prefab.
// HOW TO EDIT: GameBalance > Sources (Pump Liters Per Stroke, Pump Stroke Interval).
// ============================================================================

using System;
using UnityEngine;
using TMPro;
using TheLastDrop;

public class HubPump_Manager : Interactable_Manager
{
    /// <summary>
    /// Multiplies the pump's output (0 = dry, 1 = full). The drought stage system sets this
    /// (water table, Deep-Set Cylinder). Null = 1.
    /// </summary>
    public static Func<float> OutputFactor;

    /// <summary>A short reason for the label, e.g. "water table 5.5 m". Set with OutputFactor.</summary>
    public static Func<string> OutputReason;

    [Header("Pump Settings (old scenes; GameBalance wins when present)")]
    public float interactionDistance = 2f;
    public float strokeInterval = 0.5f;
    public float waterPerStroke = 0.2f;

    [Header("Pump UI")]
    public TMP_Text fullPopup;

    private Player_Manager player;
    private GameResources_Manager resources;
    private GameBalance balance;

    private float strokeTimer;
    private float fullShownUntil;

    /// <summary>The current output multiplier (1 = full, 0 = dry).</summary>
    public static float CurrentFactor => OutputFactor != null ? Mathf.Clamp01(OutputFactor()) : 1f;

    float StrokeInterval => Skills.F("pumpStrokeInterval", balance != null ? balance.pumpStrokeInterval : strokeInterval);
    float LitresPerStroke => balance != null ? balance.pumpLitersPerStroke : waterPerStroke;
    float Reach => balance != null ? balance.interactRange : interactionDistance;

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance.gameResources_Manager;
        balance = BalanceProvider.Current;

        interactionType = InteractionType.Hold;

        if (fullPopup != null)
            fullPopup.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (fullPopup != null && fullPopup.gameObject.activeSelf && Time.time > fullShownUntil)
            fullPopup.gameObject.SetActive(false);
    }

    public override bool CanInteract(Player_Manager targetPlayer)
    {
        if (targetPlayer == null)
            return false;

        if (targetPlayer.playerHydration_Manager.IsCollapsed())
            return false;

        Vector3 playerPosition = targetPlayer.transform.position;
        Vector3 pumpPosition = GetInteractionPosition();

        playerPosition.y = 0f;
        pumpPosition.y = 0f;

        return Vector3.Distance(playerPosition, pumpPosition) <= Reach;
    }

    public override string GetInteractionText()
    {
        // R4.6: the player should never have to guess why a source slowed down.
        float f = CurrentFactor;
        string reason = OutputReason != null ? OutputReason() : null;
        string state = f <= 0.001f ? "DRY" : Mathf.RoundToInt(f * 100f) + "%";
        if (resources != null && resources.rawBasin >= resources.rawBasin_Max - 0.0001f)
            return $"Hand Pump: Raw Basin FULL";
        return $"Hold [E] to Pump  (Hand Pump: {state}{(string.IsNullOrEmpty(reason) ? "" : ", " + reason)})";
    }

    public override void Interact(float deltaTime)
    {
        if (player == null || resources == null)
            return;

        player.playerHydration_Manager.isPumping = true;

        strokeTimer += deltaTime;

        float interval = Mathf.Max(0.05f, StrokeInterval);
        while (strokeTimer >= interval)
        {
            strokeTimer -= interval;
            PumpStroke();
        }
    }

    public override void StopInteraction()
    {
        strokeTimer = 0f;

        if (player != null)
            player.playerHydration_Manager.isPumping = false;
    }

    private void PumpStroke()
    {
        // R4.2: the handle locks when the basin is full; nothing is lost.
        if (resources.rawBasin >= resources.rawBasin_Max)
        {
            ShowFull();
            return;
        }

        float litres = LitresPerStroke * CurrentFactor;
        if (litres <= 0f) return; // dry (Stage 4 without the Deep-Set Cylinder)

        float added = resources.AddRawWater(litres);
        RunStats.AddCollected(added);
        if (added < litres) ShowFull();
    }

    private void ShowFull()
    {
        fullShownUntil = Time.time + 1.5f;
        if (fullPopup != null)
            fullPopup.gameObject.SetActive(true);
        else
            SimpleHud.Toast("Raw Basin FULL", 1.5f);
    }
}
