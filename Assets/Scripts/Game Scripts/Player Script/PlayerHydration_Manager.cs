// ============================================================================
// PlayerHydration_Manager.cs
// The player's HYD: drain every second, the four states, drinking, Sick, and collapse.
//
//   drain = base drain for the activity (standing / walking / sprinting / pumping / minigame)
//           × every multiplier in HydrationModifiers (drought stage, events, ...)
//           × skills (E1 Salakot, E2 Steady Breath)
//           × Thirsty sprint (×1.5 unless M3 Pacing) × Sick (×1.5) × hub shade (×0.5)
//
// Drinking (R2.3): HOLD Q to drink from the canteen at 0.5 L/s (×1.5 with M4 Quick Hands).
// Each litre gives 25 HYD (30 with E3 Water Discipline). It stops at full HYD or an empty
// canteen, so no water is wasted.
//
// DESIGN RULES: R2.1 to R2.5, the hydration states table, R11.1 (collapse at 0).
//
// HOW TO USE: on the Player prefab. "Hub Area" is the base's box (the Build Playtest Scene
// menu sets it). In scenes with GameBalance, every number below comes from GameBalance and
// the Inspector numbers are only used in old scenes.
// HOW TO EDIT: GameBalance > Hydration. Skills change values through the Build and Skills CSV.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TheLastDrop;

public class PlayerHydration_Manager : MonoBehaviour, ISaveable
{
    public static PlayerHydration_Manager instance;
    public Player_Manager player;
    GameResources_Manager resources;

    public enum HydrationState
    {
        Hydrated,
        Thirsty,
        Dehydrated,
        Collapsed
    }

    [Header("Hydration State")]
    public HydrationState currentHydrationState = HydrationState.Hydrated;

    [Header("Hydration Drain (old scenes; GameBalance wins when present)")]
    public float drain_Idle = 0.25f;
    public float drain_Walk = 0.5f;
    public float drain_Sprint = 1.2f;
    public float drain_Pump = 0.6f;

    [Header("Drinking (old scenes; GameBalance wins when present)")]
    public float drinkRate = 0.5f;
    public float refillRate = 1f;
    public float hydrationPerLiter = 25f;

    [Header("Sickness (old scenes; GameBalance wins when present)")]
    public float sickDuration = 30f;
    public float sickDrainMultiplier = 1.5f;
    private float sickTimer;

    [Header("Extra multipliers (kept for old scenes; new systems use HydrationModifiers)")]
    public float droughtHeatMultiplier = 1f;
    public float eventMultiplier = 1f;
    public float skillMultiplier = 1f;

    [Header("Hub Settings")]
    public BoxCollider hubArea;
    public float hubDrainMultiplier = 0.5f;

    [Header("Activity")]
    public bool isPumping;
    public bool isPlayingMinigame;

    [Header("Playtest")]
    [Tooltip("Tick to switch hydration off: no drain, no drinking, no collapse. HYD stays where it is. " +
             "Turned off automatically when GameBalance > Survival On is ticked.")]
    public bool hydrationOff = false;

    public event Action OnCollapsed;

    /// <summary>The drain per second worked out last frame (for the debug HUD).</summary>
    public float LastDrainPerSecond { get; private set; }

    /// <summary>True while drinking from the canteen (Q held).</summary>
    public bool IsDrinking { get; private set; }

    /// <summary>Seconds of Sick left (0 = not sick).</summary>
    public float SickSecondsLeft => sickTimer;

    private bool collapseTriggered = false;
    GameBalance balance;

    // ------------------------------------------------------------------ Values (GameBalance, then skills)

    float DrainIdle => balance != null ? balance.drainIdle : drain_Idle;
    float DrainWalk => balance != null ? balance.drainWalk : drain_Walk;
    float DrainSprint => balance != null ? balance.drainSprint : drain_Sprint;
    float DrainPump => balance != null ? balance.drainPump : drain_Pump;
    float DrainMinigame => balance != null ? balance.drainMinigame : drain_Idle;
    float HubShade => balance != null ? balance.hubShadeMult : hubDrainMultiplier;
    float ThirstyBelow => balance != null ? balance.thirstyAt + 1f : 60f;      // 59 → below 60 is Thirsty
    float DehydratedBelow => balance != null ? balance.dehydratedAt + 1f : 30f; // 29 → below 30 is Dehydrated
    float SickMult => balance != null ? balance.sickMult : sickDrainMultiplier;

    /// <summary>HYD per litre drunk: 25, or 30 with E3 Water Discipline.</summary>
    public float HydPerLiter => Skills.F("hydPerLiter", balance != null ? balance.hydPerLiter : hydrationPerLiter);
    /// <summary>Drinking rate in L/s (M4 Quick Hands makes it 50% faster).</summary>
    public float DrinkRate => (balance != null ? balance.drinkRate : drinkRate) * Skills.F("drinkPourRateMult", 1f);
    /// <summary>Hub tap refill rate in L/s.</summary>
    public float RefillRate => balance != null ? balance.refillRate : refillRate;
    /// <summary>Seconds of Sick after raw water: 30, or 15 with E4 Iron Stomach.</summary>
    public float SickDuration => Skills.F("sickDuration", balance != null ? balance.sickSeconds : sickDuration);
    float ThirstySprintMult => Skills.F("thirstySprintMult", balance != null ? balance.thirstySprintMult : 1.5f);

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(this);
            return;
        }

        player = GetComponent<Player_Manager>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        resources = Game_Manager.instance.gameResources_Manager;
        balance = BalanceProvider.Current;
        if (balance != null && balance.survivalOn)
        {
            hydrationOff = false;
            player.playerStats_Manager.player_MaxHydration = balance.hydMax;
        }

        UpdateHydrationState();
    }

    private void Update()
    {
        if (Time.timeScale == 0 || resources == null || hydrationOff)
        {
            IsDrinking = false;
            return;
        }

        UpdateHydrationState();

        if (currentHydrationState == HydrationState.Collapsed)
        {
            IsDrinking = false;
            HandleCollapse();
            return;
        }

        HandleSickness();
        HandleHydrationDrain();
        HandleHoldToDrink();

        UpdateHydrationState();
        HandleCollapse();
    }

    // Dev note: To make it easier to check the hydration status of the player, I used states instead of using plenty of if else value
    private void UpdateHydrationState()
    {
        float hydration = player.playerStats_Manager.player_Hydration;

        if (hydration <= 0)
            currentHydrationState = HydrationState.Collapsed;
        else if (hydration < DehydratedBelow)
            currentHydrationState = HydrationState.Dehydrated;
        else if (hydration < ThirstyBelow)
            currentHydrationState = HydrationState.Thirsty;
        else
            currentHydrationState = HydrationState.Hydrated;
    }

    public bool CanSprint()
    {
        return currentHydrationState != HydrationState.Dehydrated &&
               currentHydrationState != HydrationState.Collapsed;
    }

    public bool IsCollapsed()
    {
        return currentHydrationState == HydrationState.Collapsed;
    }

    private void HandleSickness()
    {
        if (sickTimer > 0)
            sickTimer = Mathf.Max(0, sickTimer - Time.deltaTime);
    }

    private void HandleHydrationDrain()
    {
        float moveAmount = player.playerInput_Manager.moveAmount;
        bool canMove = Game_Manager.instance == null || Game_Manager.instance.CanControlPlayer();
        bool moving = moveAmount > 0 && canMove && !isPlayingMinigame;
        bool sprinting = moving && player.playerInput_Manager.sprintInput && CanSprint();
        bool inHub = IsInsideHub();

        float drain;
        if (isPlayingMinigame) drain = DrainMinigame;          // R9.1: same as standing
        else if (isPumping) drain = DrainPump;
        else if (sprinting) drain = DrainSprint;
        else if (moving) drain = DrainWalk;
        else drain = DrainIdle;

        // Thirsty: sprinting costs ×1.5 (M3 Pacing removes it).
        if (currentHydrationState == HydrationState.Thirsty && sprinting)
            drain *= ThirstySprintMult;

        // Skills: E1 Salakot (all drain), E2 Steady Breath (walking and sprinting).
        drain *= Skills.F("drainMultAll", 1f);
        if (moving) drain *= Skills.F("drainMultMove", 1f);

        // Drought stage, events and anything else other systems add (R2.1: multiply, never add).
        var ctx = new DrainContext
        {
            Moving = moving,
            Sprinting = sprinting,
            Pumping = isPumping,
            InHub = inHub,
            Day = GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Day : 1
        };
        drain *= HydrationModifiers.Product(ctx);

        // Old-scene multipliers (1 unless someone set them in the Inspector).
        drain *= droughtHeatMultiplier * eventMultiplier * skillMultiplier;

        if (sickTimer > 0) drain *= SickMult;
        if (inHub) drain *= HubShade;                           // R2.2

        LastDrainPerSecond = drain;
        player.playerStats_Manager.ChangeHydration(-drain * Time.deltaTime);
    }

    public bool IsInsideHub()
    {
        if (hubArea == null)
            return false;

        return hubArea.bounds.Contains(player.transform.position);
    }

    // ------------------------------------------------------------------ Drinking (R2.3)

    // Q held: drink 0.5 L/s from the canteen until HYD is full or the canteen is empty.
    private void HandleHoldToDrink()
    {
        IsDrinking = false;
        var kb = Keyboard.current;
        if (kb == null || !kb.qKey.isPressed) return;
        if (Game_Manager.instance != null && !Game_Manager.instance.CanControlPlayer()) return;

        var stats = player.playerStats_Manager;
        float roomInHyd = stats.player_MaxHydration - stats.player_Hydration;
        if (roomInHyd <= 0.001f || resources.canteen <= 0f) return;

        float hydPerL = HydPerLiter;
        float litres = Mathf.Min(DrinkRate * Time.deltaTime, resources.canteen, roomInHyd / hydPerL);
        float taken = resources.TakeCanteenWater(litres);
        if (taken <= 0f) return;
        stats.ChangeHydration(taken * hydPerL);
        IsDrinking = true;
    }

    // Q pressed: plays the drink animation once (the water itself is handled by HandleHoldToDrink).
    public void HandleDrinkAction()
    {
        if (resources == null || hydrationOff)
            return;

        if (currentHydrationState == HydrationState.Collapsed)
            return;

        if (resources.canteen <= 0)
            return;

        if (player.playerStats_Manager.player_Hydration >=
            player.playerStats_Manager.player_MaxHydration)
            return;

        if (player.playerAnimator_Manager.isPerformingAction)
            return;

        // Not an "action": the player can keep moving and turning while drinking.
        player.playerAnimator_Manager.PlayTargetActionAnimation(
            "Drink",
            false,
            false,
            true,
            true
        );
    }

    // Called by an event in the "Drink" animation. Drinking is now hold-to-drink (above), so
    // this does nothing; it stays so the animation event doesn't log an error.
    public void DrinkFromCanteen()
    {
    }

    // Dev Note: This is being called by the end of the animation ("Consume" animation will change when we get new animation)
    public void EndDrinkAnimation()
    {
        if (currentHydrationState == HydrationState.Collapsed)
            return;

        player.playerAnimator_Manager.ResetActionAnimation();

        player.animator.CrossFade("Walking Blend Tree", 0.2f);
    }

    public void DrinkFromCleanStorage(float deltaTime)
    {
        float hydPerL = HydPerLiter;
        float amountNeeded =
            (player.playerStats_Manager.player_MaxHydration - player.playerStats_Manager.player_Hydration)
            / hydPerL;

        float amount = Mathf.Min(DrinkRate * deltaTime, amountNeeded);

        float consumed = resources.TakeCleanWater(amount);

        player.playerStats_Manager.ChangeHydration(consumed * hydPerL);
    }

    public void RefillFromHub(float deltaTime)
    {
        resources.RefillCanteen(RefillRate * deltaTime);
    }

    /// <summary>R2.5: drink raw water (Raw Basin or a muddy pool). Same HYD per litre, but Sick for 30 s (resets, doesn't stack).</summary>
    public void DrinkRawFromBasin(float amount)
    {
        if (amount <= 0)
            return;

        float hydPerL = HydPerLiter;
        float amountNeeded =
            (player.playerStats_Manager.player_MaxHydration - player.playerStats_Manager.player_Hydration)
            / hydPerL;

        float consumed = resources.TakeRawWater(
            Mathf.Min(amount, amountNeeded)
        );

        if (consumed > 0)
        {
            player.playerStats_Manager.ChangeHydration(consumed * hydPerL);
            MakeSick();
        }
    }

    /// <summary>R2.5: Sick for 30 s (15 with E4), resetting the timer.</summary>
    public void MakeSick()
    {
        sickTimer = SickDuration;
    }

    /// <summary>Takes HYD away (minigame loss, night lockdown). Does nothing while hydration is off.</summary>
    public void LoseHydration(float amount)
    {
        if (hydrationOff || amount <= 0f) return;
        player.playerStats_Manager.ChangeHydration(-amount);
    }

    private void HandleCollapse()
    {
        if (currentHydrationState != HydrationState.Collapsed)
        {
            collapseTriggered = false;
            return;
        }

        if (collapseTriggered)
            return;

        collapseTriggered = true;

        Debug.Log("Player Collapsed!");
        OnCollapsed?.Invoke();
    }


    public void ResetAfterRevive(float hydration)
    {
        player.playerStats_Manager.player_Hydration =
            Mathf.Clamp(
                hydration,
                1f,
                player.playerStats_Manager.player_MaxHydration
            );

        isPumping = false;
        isPlayingMinigame = false;
        sickTimer = 0f;

        collapseTriggered = false;
        UpdateHydrationState();
    }

    // ------------------------------------------------------------------ Saving (R11.8)

    /// <summary>Restored with the player.</summary>
    public int RestoreOrder => RestoreOrders.Player;

    /// <summary>Saves HYD and the Sick timer.</summary>
    public void Capture(SaveData data)
    {
        data.hydration = player.playerStats_Manager.player_Hydration;
        data.sickSeconds = sickTimer;
    }

    /// <summary>Puts HYD and the Sick timer back.</summary>
    public void Restore(SaveData data)
    {
        if (!data.hasSurvival) return;
        player.playerStats_Manager.player_Hydration = Mathf.Clamp(data.hydration, 1f, player.playerStats_Manager.player_MaxHydration);
        sickTimer = Mathf.Max(0f, data.sickSeconds);
        collapseTriggered = false;
        UpdateHydrationState();
    }
}
