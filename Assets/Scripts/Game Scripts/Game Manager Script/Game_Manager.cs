using UnityEngine;

public class Game_Manager : MonoBehaviour
{
    public static Game_Manager instance;

    [HideInInspector] public GameResources_Manager gameResources_Manager;
    [HideInInspector] public GameClock_Manager gameClock_Manager;
    [HideInInspector] public Player_Manager player_Manager;

    public enum GameState
    {
        Exploring,
        Minigame,
        Paused,
        SkillTree,
        Collapsed,
        // Added for the maze playtest (map screen, Build and Skills menu, R7.1): the player can't
        // move and the cursor is free, but the world keeps running (time scale stays 1).
        // Kept last so saved scenes keep their old values.
        Menu
    }

    [Header("Game State")]
    public GameState currentGameState = GameState.Exploring;

    [Header("Game UI")]
    public GameObject pauseMenu;
    public GameObject skillTreeMenu;

    private void Awake()
    {
        instance = this;

        gameClock_Manager = GetComponent<GameClock_Manager>();
        gameResources_Manager = GetComponent<GameResources_Manager>();
    }

    private void Start()
    {
        ChangeGameState(GameState.Exploring);
    }


    public void ChangeGameState(GameState newState)
    {
        currentGameState = newState;

        if (pauseMenu != null)
            pauseMenu.SetActive(newState == GameState.Paused);
        
        // Dev note: Haven't added yet but this is to show the skill tree
        if (skillTreeMenu != null)
            skillTreeMenu.SetActive(newState == GameState.SkillTree);

        switch (currentGameState)
        {
            case GameState.Exploring:
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case GameState.Minigame:
            case GameState.Menu:
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;

            case GameState.Paused:
            case GameState.SkillTree:
                // Added for the maze playtest: say so when the pause menu can't be seen, instead of
                // leaving a frozen screen with no explanation. (UI only shows inside a Canvas.)
                if (newState == GameState.Paused &&
                    (pauseMenu == null || pauseMenu.GetComponentInParent<Canvas>(true) == null))
                    Debug.LogWarning("[Game_Manager] Paused, but the pause menu isn't set or isn't inside a Canvas, " +
                                     "so it can't be seen. Press Esc again to resume.", this);
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
            case GameState.Collapsed:
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
        }
    }

    public bool CanControlPlayer()
    {
        return currentGameState == GameState.Exploring;
    }

    // Dev note: Being called by a button inside the Player Pause UI
    public void ResumeGame()
    {
        ChangeGameState(GameState.Exploring);
    }

}
