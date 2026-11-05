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
        Collapsed
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
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;

            case GameState.Paused:
            case GameState.SkillTree:
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
