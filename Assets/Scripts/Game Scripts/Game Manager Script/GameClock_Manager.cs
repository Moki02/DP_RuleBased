// ============================================================================
// GameClock_Manager.cs
// The game's clock. Counts days (starting at 1), runs daylight and then the night
// lockdown, and tells other scripts when night starts and when a new day starts.
// The counting itself is done by GameClockLogic (plain C#, tested); this script runs
// it every frame and shows "Day N - mm:ss" on the HUD.
//
// DESIGN RULES: R1.5 (a day is 240 s of daylight), R8.19 (then a 60 s night lockdown).
//
// HOW TO USE: it's on the Game Manager prefab. Drag the HUD's "Game Clock" text into
// "Clock Text". Other scripts listen with:
//     GameClock_Manager.Instance.NightStarted += day => ...;
//     GameClock_Manager.Instance.NewDay       += day => ...;
// HOW TO EDIT: lengths come from GameBalance ("Day Length Seconds", "Night Lockdown
// Seconds") when the scene has a BalanceProvider. Scenes without one (the old Zone 1
// scenes) use "Day Length" below and have no night. Debug key 6 runs the clock faster.
// ============================================================================

using System;
using TMPro;
using TheLastDrop;
using UnityEngine;

/// <summary>Runs the day/night clock and shows it on the HUD.</summary>
public class GameClock_Manager : MonoBehaviour, ISaveable
{
    /// <summary>The clock in this scene (set in Awake).</summary>
    public static GameClock_Manager Instance { get; private set; }

    [Header("Game Clock")]
    [Tooltip("Seconds per day, used ONLY when the scene has no BalanceProvider (old scenes, no night). " +
             "Otherwise GameBalance > Day Length Seconds is used.")]
    public float dayLength = 240f;

    [Tooltip("The current day (read only: shown here so you can watch it in Play mode).")]
    public int currentDay = 1;

    [Tooltip("Seconds since dawn (read only: shown here so you can watch it in Play mode).")]
    public float dayTimer = 0f;

    [Header("Clock UI")]
    [Tooltip("HUD text that shows \"Day N - mm:ss\".")]
    [SerializeField] TMP_Text clockText;

    /// <summary>The clock logic. Null before Awake.</summary>
    public GameClockLogic Clock { get; private set; }

    /// <summary>The current day, starting at 1.</summary>
    public int Day => Clock != null ? Clock.Day : currentDay;

    /// <summary>True during the night lockdown.</summary>
    public bool IsNight => Clock != null && Clock.IsNight;

    /// <summary>Fired when the night lockdown starts. The number is the current day.</summary>
    public event Action<int> NightStarted;

    /// <summary>Fired at dawn. The number is the new day.</summary>
    public event Action<int> NewDay;

    GameBalance balance;

    private void Awake()
    {
        Instance = this;
        balance = BalanceProvider.Current;

        Clock = balance != null
            ? new GameClockLogic(balance.dayLengthSeconds, balance.nightLockdownSeconds)
            : new GameClockLogic(dayLength, 0f);   // old scenes: same behaviour as before, no night

        // Pass the logic's events on to anyone listening to this script.
        Clock.NightStarted += day => NightStarted?.Invoke(day);
        Clock.NewDay += day => NewDay?.Invoke(day);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        UpdateClockUI();
    }

    private void Update()
    {
        // The pause menu sets the time scale to 0, which stops the clock too.
        if (Time.timeScale == 0f)
            return;

        Clock.Advance(Time.deltaTime);
        currentDay = Clock.Day;
        dayTimer = Clock.TimeOfDay;

        UpdateClockUI();
    }

    // Dev Note: This is only a day and time indicator and doesn't have the day effects yet
    private void UpdateClockUI()
    {
        if (clockText == null || Clock == null)
            return;

        // Daylight shows the time since dawn; the night shows how long until dawn.
        float shown = Clock.IsNight ? Clock.SecondsLeftInPhase : Clock.TimeOfDay;
        int minutes = Mathf.FloorToInt(shown / 60f);
        int seconds = Mathf.FloorToInt(shown % 60f);

        clockText.text = "Day " + Clock.Day +
                         (Clock.IsNight ? " - NIGHT " : " - ") +
                         minutes.ToString("00") + ":" +
                         seconds.ToString("00") +
                         (Clock.SpeedMultiplier > 1f ? "  (x" + Clock.SpeedMultiplier.ToString("0.#") + ")" : "");
    }

    /// <summary>Debug key 6: switches between normal speed and GameBalance's "Debug Clock Speed".</summary>
    public void ToggleDebugSpeed()
    {
        float fast = balance != null ? balance.debugClockSpeed : 1f;
        Clock.SpeedMultiplier = Clock.SpeedMultiplier > 1f ? 1f : fast;
        Debug.Log($"[Clock] Speed x{Clock.SpeedMultiplier}");
    }

    /// <summary>Pauses or unpauses ONLY the clock (the world keeps running). Used by the Build and Skills menu.</summary>
    public void SetClockPaused(bool paused)
    {
        Clock.Paused = paused;
    }

    /// <summary>Freezes the whole game (time scale 0).</summary>
    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    /// <summary>Unfreezes the whole game (time scale 1).</summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    // ------------------------------------------------------------------ Saving (R11.8)

    /// <summary>The clock is restored first: zone 2 and the night lockdown follow the day.</summary>
    public int RestoreOrder => RestoreOrders.Clock;

    /// <summary>Saves the day and the time since dawn.</summary>
    public void Capture(SaveData data)
    {
        data.day = Clock.Day;
        data.timeOfDay = Clock.TimeOfDay;
    }

    /// <summary>Jumps the clock to the saved day and time (no night/dawn events fire).</summary>
    public void Restore(SaveData data)
    {
        Clock.SetTime(data.day, data.timeOfDay);
        currentDay = Clock.Day;
        dayTimer = Clock.TimeOfDay;
        UpdateClockUI();
    }

    // Dev Note: Will implement the drought stages in the future
}
