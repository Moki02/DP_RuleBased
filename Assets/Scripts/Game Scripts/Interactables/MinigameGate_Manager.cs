// ============================================================================
// MinigameGate_Manager.cs
// The team's first (Week 9) gate minigame: a triangle slides along a bar; press the timing
// button while it is over the red area to fill the progress bar. A full bar opens the gate
// for good. Used by the older Zone 1 scenes. The maze game uses ValveGate / LockBasin with
// ValveTimingGame instead.
//
// The file was renamed from Minigame_Manager.cs on 2026-11-12 so it matches the class name
// (keep both the same, or Unity can't add it from Add Component).
//
// DESIGN RULES: R9.1 (the world keeps running: Minigame state, time scale 1). It has no
// lose state, so R9.3 (-5 HYD) doesn't apply here.
//
// HOW TO USE: put it on the gate, drag in the HUD pieces and the gate object, and wire the
// HUD's button to PressTimingButton() and its close button to ExitMinigame().
// HOW TO EDIT: speeds and progress amounts are the fields below.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>The Week 9 timing minigame on a gate: fill the bar to open the gate.</summary>
public class MinigameGate_Manager : Interactable_Manager
{
    [Header("Minigame UI")]
    [Tooltip("The minigame's HUD panel (switched on while playing).")]
    public GameObject minigameHUD;
    [Tooltip("The bar the triangle slides along.")]
    public RectTransform timingTrack;
    [Tooltip("The sliding triangle.")]
    public RectTransform triangle;
    [Tooltip("The red target area on the bar.")]
    public RectTransform redArea;
    [Tooltip("Shows how full the progress is.")]
    public UI_StatBar progressBar;

    [Header("Timing Settings")]
    [Tooltip("How fast the triangle slides, in UI pixels per second.")]
    public float triangleSpeed = 300f;

    [Header("Progress Settings")]
    [Tooltip("Progress needed to open the gate.")]
    public float maxProgress = 50f;
    [Tooltip("Progress added by each hit (50 / 10 = 5 hits).")]
    public float progressPerHit = 10f;

    [Header("Gate")]
    [Tooltip("The gate object; it is switched off (opened for good) when the bar is full.")]
    public GameObject gateObject;

    private Player_Manager player;
    private float currentProgress = 0f;
    private float trianglePosition = 0f;

    private bool isPlaying = false;
    private bool isUnlocked = false;

    private void Awake()
    {
        interactionType = InteractionType.Press;
    }

    private void Start()
    {
        player = Player_Manager.instance;

        if (minigameHUD != null)
            minigameHUD.SetActive(false);

        if (progressBar != null)
            progressBar.SetStat(0f, maxProgress);
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        MoveTriangle();

    }

    /// <summary>True while the gate is shut and the player is free and not collapsed.</summary>
    public override bool CanInteract(Player_Manager targetPlayer)
    {
        if (targetPlayer == null || isUnlocked || Game_Manager.instance == null)
            return false;

        return (targetPlayer.playerHydration_Manager == null || !targetPlayer.playerHydration_Manager.IsCollapsed()) &&
               Game_Manager.instance.CanControlPlayer();
    }

    /// <summary>The prompt under the crosshair.</summary>
    public override string GetInteractionText()
    {
        return "Press [E] to Open Valve Minigame";
    }

    /// <summary>E pressed: starts the minigame.</summary>
    public override void Interact(float deltaTime)
    {
        if (isPlaying || isUnlocked)
            return;

        StartMinigame();
    }

    private void StartMinigame()
    {
        if (minigameHUD == null)
            return;
        if (player == null) player = Player_Manager.instance;

        isPlaying = true;

        currentProgress = 0f;
        trianglePosition = 0f;

        minigameHUD.SetActive(true);

        if (progressBar != null)
            progressBar.SetStat(currentProgress, maxProgress);

        if (player != null && player.playerHydration_Manager != null)
            player.playerHydration_Manager.isPlayingMinigame = true;

        if (Game_Manager.instance != null)
            Game_Manager.instance.ChangeGameState(Game_Manager.GameState.Minigame);
    }

    private void MoveTriangle()
    {
        if (timingTrack == null || triangle == null)
            return;

        float halfWidth =
            (timingTrack.rect.width - triangle.rect.width) * 0.5f;

        trianglePosition += triangleSpeed * Time.deltaTime;

        float x = Mathf.PingPong(
            trianglePosition,
            halfWidth * 2f
        ) - halfWidth;

        triangle.anchoredPosition = new Vector2(
            x,
            triangle.anchoredPosition.y
        );
    }

    /// <summary>The timing button (wire the HUD button to this): a hit if the triangle is over the red area.</summary>
    public void PressTimingButton()
    {
        if (!isPlaying || triangle == null || redArea == null)
            return;

        float triangleX = triangle.anchoredPosition.x;

        float redLeft =
            redArea.anchoredPosition.x - redArea.rect.width * 0.5f;

        float redRight =
            redArea.anchoredPosition.x + redArea.rect.width * 0.5f;

        // CHECK IF TRIANGLE IS INSIDE RED AREA
        if (triangleX >= redLeft && triangleX <= redRight)
        {
            currentProgress = Mathf.Min(
                currentProgress + progressPerHit,
                maxProgress
            );

            if (progressBar != null)
                progressBar.SetStat(currentProgress, maxProgress);

            if (currentProgress >= maxProgress)
                CompleteMinigame();
        }
    }

    private void CompleteMinigame()
    {
        isUnlocked = true;

        // OPEN GATE PERMANENTLY
        if (gateObject != null)
            gateObject.SetActive(false);

        ExitMinigame();

        Debug.Log("Minigame Complete! Gate Opened!");
    }

    /// <summary>Closes the minigame (also the HUD's close button) and gives control back.</summary>
    public void ExitMinigame()
    {
        if (!isPlaying)
            return;

        isPlaying = false;

        if (player != null && player.playerHydration_Manager != null)
            player.playerHydration_Manager.isPlayingMinigame = false;

        if (minigameHUD != null)
            minigameHUD.SetActive(false);

        if (Game_Manager.instance != null)
            Game_Manager.instance.ChangeGameState(Game_Manager.GameState.Exploring);
    }
}
