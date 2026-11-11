// ============================================================================
// Game_Manager.cs
// Holds the game's current STATE and applies what each state means for the time scale
// and the mouse cursor:
//   Exploring  normal play: time runs, cursor locked and hidden, the player can move.
//   Minigame   a minigame is up (Valve Timing): time runs, cursor free.
//   Menu       map / Build and Skills / survivor note (MenuState): time runs, cursor free.
//   Paused     Esc pause menu: time stops (time scale 0), cursor free.
//   SkillTree  old skill tree screen: time stops, cursor free.
//   Collapsed  the player collapsed, or the game is held frozen behind a full-screen
//              panel (game over, win screen, title screen; see MainMenu.Freeze): time stops,
//              cursor free. Esc, Tab, M and B do nothing in this state.
// Only this script should set Time.timeScale and the cursor; other scripts call
// ChangeGameState(...) (or MenuState.Enter / Exit for menus).
//
// DESIGN RULES: R1.5 (the clock stops only when the game is paused), R7.1 and R9.1 (menus
// and minigames don't pause the world).
//
// HOW TO USE: it's on the Game Manager prefab, next to GameClock_Manager and
// GameResources_Manager. Drag the pause menu object into "Pause Menu".
// HOW TO EDIT: to add a state, add it at the END of GameState (saved scenes store the
// number) and give it a case in ChangeGameState.
// NOTE: Start puts the game in Exploring. The title screen (MainMenu) may already have
// frozen the game by then; MainMenu re-freezes it on its next Update, so that is fine.
// ============================================================================

using UnityEngine;

/// <summary>The game's current state (exploring, menu, paused ...) and the time scale and cursor that go with it.</summary>
public class Game_Manager : MonoBehaviour
{
    /// <summary>The Game_Manager in this scene (set in Awake).</summary>
    public static Game_Manager instance;

    [HideInInspector] public GameResources_Manager gameResources_Manager;
    [HideInInspector] public GameClock_Manager gameClock_Manager;
    [HideInInspector] public Player_Manager player_Manager;

    /// <summary>What the player is doing right now. See the table at the top of this file.</summary>
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
    [Tooltip("The current state (read only: change it with ChangeGameState, not here).")]
    public GameState currentGameState = GameState.Exploring;

    [Header("Game UI")]
    [Tooltip("The pause menu object (must be inside a Canvas). Shown only in the Paused state. " +
             "If it's empty or can't be seen, MainMenu shows a plain pause panel instead.")]
    public GameObject pauseMenu;
    [Tooltip("The old skill tree screen. Shown only in the SkillTree state. Empty is fine (the Tab menu replaced it).")]
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


    /// <summary>Switches to <paramref name="newState"/> and sets the time scale, cursor and menus for it.</summary>
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

    /// <summary>True only while exploring: the player may move, look around and interact.</summary>
    public bool CanControlPlayer()
    {
        return currentGameState == GameState.Exploring;
    }

    /// <summary>Back to normal play. Called by the pause menu's Resume button (and Esc while paused).</summary>
    public void ResumeGame()
    {
        ChangeGameState(GameState.Exploring);
    }

}
