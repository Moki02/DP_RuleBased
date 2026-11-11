// ============================================================================
// LockBasin_Manager.cs
// A gate's lock basin. Hold E next to it to pour water from the canteen in; the bar rises.
// Two ways to work (pick with "Open With Valve Minigame"):
//
//   OFF (old scenes, as before): when the basin is full the gate opens.
//   ON  (maze playtest, R8.9):   when the basin is full the prompt says "Press [E] to open
//        the valve" and the Valve Timing minigame starts. Win: the gate opens for good.
//        Lose: the water stays, but the minigame is locked for 10 s (R9.3). Esc: no penalty.
//        At night the gate is shut even if it was opened, and reopens at dawn (R8.21, R8.23).
//
// The rules themselves live in GateLockLogic (plain C#, tested). Water poured stays in the
// basin between visits.
//
// DESIGN RULES: R8.9 (lock basins: 4 / 10 / 18 L), R9.1 to R9.3, R9.6 (difficulty),
// R8.21 / R8.23 (night), R11.6 (an exit gate's win is the game's win).
//
// HOW TO USE: the Lock Basin prefab has this. In the maze playtest GateManager places one
// per gate and fills in every field; you don't set anything by hand. In an old scene,
// drag the gate wall into "Gate Object" and set "Basin Max Water".
// HOW TO EDIT: litres and pour rate come from GameBalance in the maze playtest. The prompt
// texts are under "Text (what the player reads)".
// ============================================================================

using System;
using System.Collections.Generic;
using TheLastDrop;
using TMPro;
using UnityEngine;

/// <summary>A gate's lock basin: pour water in, then (optionally) win the valve minigame to open the gate.</summary>
public class LockBasin_Manager : Interactable_Manager, INightLockable
{
    [Header("Lock Basin Settings")]
    [Tooltip("Litres needed to fill the basin.")]
    public float basin_MaxWater = 4f;
    [Tooltip("Litres in the basin now (set this to start part-full).")]
    public float basin_Water = 0f;
    [Tooltip("Litres per second poured while E is held.")]
    public float pourRate = 1f;

    [Header("Lock Basin UI")]
    [Tooltip("The bar above the basin that rises as it fills.")]
    public UI_StatBar basinBar;
    [Tooltip("The \"2 / 4 L\" text above the basin.")]
    public TMP_Text basinText;

    [Header("Gate")]
    [Tooltip("Old scenes: the gate wall to hide when the gate opens.")]
    public GameObject gateObject;
    [Tooltip("Maze playtest: every piece of the gate (one per gate tile). Hidden while open, shown when shut.")]
    public List<GameObject> gateObjects = new List<GameObject>();

    [Header("Valve minigame (maze playtest)")]
    [Tooltip("ON: a full basin starts the Valve Timing minigame, and winning it opens the gate. OFF: a full basin opens the gate (old behaviour).")]
    public bool openWithValveMinigame = false;
    [Tooltip("Band width (degrees), needle speed (degrees/second), hits to win, misses to lose. Set by GateManager from the gate's ring.")]
    public ValveTimingSettings minigameSettings;
    [Tooltip("Difficulty t (0-1) shown on the F1 debug line. -1 = exit valve / not known.")]
    public float difficultyT = -1f;
    [Tooltip("Seconds the minigame stays locked after a loss (R9.3).")]
    public float lockoutSeconds = 10f;
    [Tooltip("HYD the player would lose for a lost minigame (R9.3). The playtest only logs it.")]
    public float failHydLog = 5f;
    [Tooltip("True for zone 2's exit gates: winning one is the game's win (R11.6).")]
    public bool isExitGate = false;

    [Header("Reach (maze playtest)")]
    [Tooltip("0 = old behaviour (the basin must be inside the player's interaction box). " +
             "More than 0 = the player can use the basin from anywhere within this many metres.")]
    public float proximityRange = 0f;

    [Header("Text (what the player reads)")]
    [Tooltip("{0} = litres in, {1} = litres needed.")]
    public string pourPrompt = "Hold [E] to pour water  ({0:0.0} / {1:0.#} L)";
    public string openPrompt = "Press [E] to open the valve";
    [Tooltip("{0} = seconds left.")]
    public string jammedPrompt = "Valve jammed ({0}s)";
    public string nightPrompt = "Gate locked for the night";

    /// <summary>The lock's rules and state (null before Start).</summary>
    public GateLockLogic Lock { get; private set; }

    /// <summary>True if the player can walk through this gate right now.</summary>
    public bool IsPassable => Lock != null && Lock.IsPassable;

    /// <summary>Called when the gate opens for good (minigame won, or old-style full basin).</summary>
    public event Action<LockBasin_Manager> Opened;

    private GameResources_Manager resources;
    private bool isUnlocked = false;   // old mode only
    private bool nightLocked;

    private void Awake()
    {
        Lock = new GateLockLogic(basin_MaxWater);
    }

    private void Start()
    {
        resources = Game_Manager.instance.gameResources_Manager;

        interactionType = InteractionType.Hold;

        // GateManager may change basin_MaxWater after Awake, so make the lock again with the final size.
        float startWater = Mathf.Clamp(basin_Water, 0f, basin_MaxWater);
        bool wasOpen = Lock != null && Lock.State == GateLockState.Open;
        Lock = new GateLockLogic(basin_MaxWater);
        Lock.Restore(startWater, wasOpen);
        Lock.NightLocked = nightLocked;
        basin_Water = Lock.Litres;

        if (!openWithValveMinigame && basin_Water >= basin_MaxWater)
            UnlockGate();

        ApplyGateLook();
        UpdateBasinUI();
    }

    private void Update()
    {
        if (proximityRange > 0f) FollowPlayerWhenClose();
    }

    // ------------------------------------------------------------------ Interaction

    public override bool CanInteract(Player_Manager player)
    {
        if (player == null || isUnlocked)
            return false;
        if (player.playerHydration_Manager != null && player.playerHydration_Manager.IsCollapsed())
            return false;
        if (proximityRange > 0f && !IsPlayerClose(player))
            return false;

        if (!openWithValveMinigame)
            return resources != null && (resources.canteen > 0f || resources.infiniteCanteen);

        switch (Lock.State)
        {
            case GateLockState.Empty:
            case GateLockState.Filling:
                return resources != null && (resources.canteen > 0f || resources.infiniteCanteen);
            case GateLockState.Full:
                return Game_Manager.instance == null || Game_Manager.instance.CanControlPlayer();
            default:
                return false; // open, or the minigame is running
        }
    }

    public override string GetInteractionText()
    {
        if (!openWithValveMinigame || Lock.CanPour)
            return string.Format(pourPrompt, Lock.Litres, basin_MaxWater);
        if (Lock.NightLocked)
            return nightPrompt;
        float left = Lock.LockoutLeft(Time.time);
        if (left > 0f)
            return string.Format(jammedPrompt, Mathf.CeilToInt(left));
        return openPrompt;
    }

    public override void Interact(float deltaTime)
    {
        if (resources == null || isUnlocked)
            return;

        if (openWithValveMinigame && Lock.State == GateLockState.Full)
        {
            // A Press interaction calls this with deltaTime 0 on the frame E goes down.
            if (deltaTime <= 0f) StartMinigame();
            return;
        }

        // Pour from the canteen (an infinite canteen gives without running out).
        float wanted = Mathf.Min(pourRate * Skills.F("drinkPourRateMult", 1f) * deltaTime, basin_MaxWater - Lock.Litres); // M4 Quick Hands
        float taken = resources.TakeCanteenWater(wanted);
        Lock.Pour(taken);
        basin_Water = Lock.Litres;
        UpdateBasinUI();

        if (Lock.State == GateLockState.Full)
        {
            if (openWithValveMinigame)
                interactionType = InteractionType.Press;   // next E press starts the minigame
            else
                UnlockGate();
        }
    }

    // ------------------------------------------------------------------ Minigame

    private void StartMinigame()
    {
        var game = ValveTimingGame.Instance;
        if (game == null)
        {
            Debug.LogError("[LockBasin] No ValveTimingGame in the scene.", this);
            return;
        }
        if (!Lock.StartMinigame(Time.time))
            return;
        if (!game.Begin(minigameSettings, OnMinigameResult, difficultyT))
        {
            Lock.FinishMinigame(MinigameResult.Quit, Time.time, 0f); // couldn't start: back to Full
            return;
        }
        SetPlayingState(true);
    }

    private void OnMinigameResult(MinigameResult result)
    {
        SetPlayingState(false);
        Lock.FinishMinigame(result, Time.time, lockoutSeconds);

        switch (result)
        {
            case MinigameResult.Won:
                Debug.Log($"[LockBasin] {name}: valve won, gate open.", this);
                ApplyGateLook();
                Opened?.Invoke(this);
                break;
            case MinigameResult.Lost:
                Debug.Log($"[LockBasin] {name}: valve lost: -{failHydLog:0.#} HYD (R9.3), " +
                          $"minigame locked for {lockoutSeconds:0.#} s. The water stays.", this);
                SurvivalRules.MinigameLost();
                break;
        }
    }

    // Same as ValveGate: the player can't move and the cursor is free while the minigame is up.
    private static void SetPlayingState(bool playing)
    {
        var p = Player_Manager.instance;
        if (p != null && p.playerHydration_Manager != null)
            p.playerHydration_Manager.isPlayingMinigame = playing;

        var gm = Game_Manager.instance;
        if (gm == null) return;
        if (playing)
            gm.ChangeGameState(Game_Manager.GameState.Minigame);
        else if (gm.currentGameState == Game_Manager.GameState.Minigame)
            gm.ChangeGameState(Game_Manager.GameState.Exploring);
    }

    // ------------------------------------------------------------------ Gate look

    // Dev note: For now I just make the wall disappear but in the future I'll improve this buy opening the path. (moving up, down, left, or right)
    private void UnlockGate()
    {
        isUnlocked = true;

        if (gateObject != null)
            gateObject.SetActive(false);

        Debug.Log("Lock Basin Full! Gate Unlocked!");
        Opened?.Invoke(this);
    }

    /// <summary>Night lockdown (R8.21, R8.23): true shuts the gate, false puts it back how it was by day.</summary>
    public void SetNightLock(bool locked)
    {
        nightLocked = locked;
        if (Lock != null) Lock.NightLocked = locked;
        if (openWithValveMinigame) ApplyGateLook();
    }

    /// <summary>Loading a save: sets the litres and whether the gate was opened.</summary>
    public void RestoreState(float litres, bool open)
    {
        Lock.Restore(litres, open);
        basin_Water = Lock.Litres;
        interactionType = Lock.State == GateLockState.Full ? InteractionType.Press : InteractionType.Hold;
        ApplyGateLook();
        UpdateBasinUI();
    }

    // Gate pieces are shown (solid) unless the gate is open and it isn't night.
    private void ApplyGateLook()
    {
        if (!openWithValveMinigame) return;
        bool shut = !Lock.IsPassable;
        foreach (var piece in gateObjects)
            if (piece != null && piece.activeSelf != shut) piece.SetActive(shut);
    }

    // ------------------------------------------------------------------ Reach

    private bool IsPlayerClose(Player_Manager player)
    {
        Vector3 d = player.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude <= proximityRange;
    }

    // The player's interaction box only reaches about 1 m in front of them. To let the player
    // use the basin from anywhere within Proximity Range (R8.9: "within 2 m"), the interaction
    // point jumps into the middle of that box while the player is close enough, and goes back
    // onto the basin when they walk away.
    private void FollowPlayerWhenClose()
    {
        if (interactionPoint == null)
        {
            interactionPoint = new GameObject("BasinInteractionPoint").transform;
            interactionPoint.SetParent(transform, false);
        }
        var player = Player_Manager.instance;
        var reach = PlayerInteraction_Manager.instance != null ? PlayerInteraction_Manager.instance.interaction_Hitbox : null;
        if (player != null && reach != null && IsPlayerClose(player))
            interactionPoint.position = reach.transform.TransformPoint(reach.center);
        else
            interactionPoint.localPosition = Vector3.zero;
    }

    private void UpdateBasinUI()
    {
        if (basinBar != null)
            basinBar.SetStat(basin_Water, basin_MaxWater);

        if (basinText != null)
        {
            basinText.text =
                Mathf.Floor(basin_Water * 10f) / 10f +
                " / " +
                basin_MaxWater.ToString("0.#") +
                " L";
        }
    }
}
