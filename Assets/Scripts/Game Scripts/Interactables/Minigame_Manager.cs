using UnityEngine;
using UnityEngine.UI;

public class MinigameGate_Manager : Interactable_Manager
{
    [Header("Minigame UI")]
    public GameObject minigameHUD;
    public RectTransform timingTrack;
    public RectTransform triangle;
    public RectTransform redArea;
    public UI_StatBar progressBar;

    [Header("Timing Settings")]
    public float triangleSpeed = 300f;

    [Header("Progress Settings")]
    public float maxProgress = 50f;
    public float progressPerHit = 10f;

    [Header("Gate")]
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

    public override bool CanInteract(Player_Manager targetPlayer)
    {
        if (targetPlayer == null || isUnlocked)
            return false;

        return !targetPlayer.playerHydration_Manager.IsCollapsed() &&
               Game_Manager.instance.CanControlPlayer();
    }

    public override string GetInteractionText()
    {
        return "Press [E] to Open Valve Minigame";
    }

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

        isPlaying = true;

        currentProgress = 0f;
        trianglePosition = 0f;

        minigameHUD.SetActive(true);

        if (progressBar != null)
            progressBar.SetStat(currentProgress, maxProgress);

        player.playerHydration_Manager.isPlayingMinigame = true;

        Game_Manager.instance.ChangeGameState(
            Game_Manager.GameState.Minigame
        );
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

    public void ExitMinigame()
    {
        if (!isPlaying)
            return;

        isPlaying = false;

        player.playerHydration_Manager.isPlayingMinigame = false;

        if (minigameHUD != null)
            minigameHUD.SetActive(false);

        Game_Manager.instance.ChangeGameState(
            Game_Manager.GameState.Exploring
        );
    }
}
