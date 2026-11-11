// ============================================================================
// PlayerInput_Manager.cs
// Reads the keyboard and mouse through the Input System ("Player_Controls" actions) and
// stores the result in plain fields other scripts read: move, mouse look, sprint, interact.
// It also reacts to two buttons directly: Drink (plays the drink animation) and Pause (Esc).
//
// Drinking: HOLD Q is handled inside PlayerHydration_Manager (it gives the water every frame
// Q is held). The Drink action here only starts the drink animation once per press, so
// water is never counted twice.
//
// DESIGN RULES: Controls table (WASD, Shift sprint, E interact, Q drink, Esc pause).
//
// HOW TO USE: on the Player prefab. Key bindings are in the Player_Controls Input Actions asset.
// HOW TO EDIT: change keys in that asset, not here.
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Turns Input System actions into simple values (move, look, sprint, interact) for the other player scripts.</summary>
public class PlayerInput_Manager : MonoBehaviour
{
    /// <summary>The input reader in this scene.</summary>
    public static PlayerInput_Manager instance;
    [Tooltip("Filled in by Player_Manager at Start.")]
    public Player_Manager player;

    Player_Controls playerControls;

    [Header("CAMERA MOVEMENT INPUT")]
    [Tooltip("Mouse movement this frame (read only; shown for debugging).")]
    [SerializeField] Vector2 mouseInput;
    public float mouseVerticalInput;
    public float mouseHorizontalInput;

    [Header("PLAYER MOVEMENT INPUT")]
    [Tooltip("WASD as a direction (read only; shown for debugging).")]
    [SerializeField] Vector2 movementInput;
    public float movementVerticalInput;
    public float movementHorizontalInput;
    [Tooltip("0 = still, 0.5 = slow (stick pushed a little), 1 = full movement.")]
    public float moveAmount;

    [Header("PLAYER ACTION INPUT")]
    [Tooltip("True while Shift is held.")]
    public bool sprintInput = false;

    [Header("Interaction Input")]
    [Tooltip("True while E is held (Hold interactions).")]
    public bool interactInput;
    [Tooltip("True only in the frame E was pressed (Press interactions). Cleared in LateUpdate.")]
    public bool interactPressed;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

    }

    // "Pressed" lasts one frame: every script has read it in Update, so clear it now.
    private void LateUpdate()
    {
        interactPressed = false;
    }

    private void OnEnable()
    {
        if (playerControls == null)
        {
            playerControls = new Player_Controls();

            GetInputs();
        }

        playerControls.Enable();
    }

    private void OnDisable()
    {
        interactInput = false;
        interactPressed = false;

        // Fix: the actions were never switched off, so after a scene reload (Restart, collapse
        // reload, main menu) the old player's actions kept running alongside the new ones.
        // Esc then fired twice in one press (pause, then straight away resume), so pause stopped working.
        if (playerControls != null)
            playerControls.Disable();
    }

    private void OnDestroy()
    {
        if (playerControls != null)
            playerControls.Dispose();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (playerControls == null)
            return;

        // Only switch back on if this script is on (not after OnDisable turned them off).
        if (focus && isActiveAndEnabled)
        {
            playerControls.Enable();
        }
        else
        {
            playerControls.Disable();
        }
    }

    private void Update()
    {
        HandleAllInputs();
    }

    // Hooks each action to the field it fills. Done once, when the actions are created.
    private void GetInputs()
    {
        playerControls.PlayerCamera.MouseMovement.performed += i => mouseInput = i.ReadValue<Vector2>();
        playerControls.PlayerCamera.MouseMovement.canceled += i => mouseInput = Vector2.zero;

        playerControls.PlayerMovement.PlayerMovement.performed += i => movementInput = i.ReadValue<Vector2>();
        playerControls.PlayerMovement.PlayerMovement.canceled += i => movementInput = Vector2.zero;

        playerControls.PlayerAction.PlayerSprint.performed += i => sprintInput = true;
        playerControls.PlayerAction.PlayerSprint.canceled += i => sprintInput = false;

        playerControls.PlayerAction.PlayerDrink.performed += i => HandleDrinkInput();

        playerControls.PlayerAction.PlayerPause.performed += i => HandlePauseInput();

        playerControls.PlayerAction.PlayerInteract.performed += i =>
        {
            interactInput = true;
            interactPressed = true;
        };

        playerControls.PlayerAction.PlayerInteract.canceled += i =>
        {
            interactInput = false;
        };
    }

    private void HandleAllInputs()
    {
        HandlePlayerMovementInput();
        HandleCameraMovementInput();

    }

    private void HandlePlayerMovementInput()
    {
        movementVerticalInput = movementInput.y;
        movementHorizontalInput = movementInput.x;

        // Round to two speeds: a little push = 0.5 (walk), more = 1 (full).
        moveAmount = Mathf.Clamp01(Mathf.Abs(movementVerticalInput) + Mathf.Abs(movementHorizontalInput));

        if (moveAmount <= 0.5f && moveAmount > 0)
        {
            moveAmount = 0.5f;
        }
        else if (moveAmount > 0.5f && moveAmount <= 1)
        {
            moveAmount = 1;
        }

        if (player == null)
        {
            return;
        }
    }


    private void HandleCameraMovementInput()
    {
        mouseVerticalInput = mouseInput.y;
        mouseHorizontalInput = mouseInput.x;
    }

    // Q pressed: start the drink animation. The water itself is given by
    // PlayerHydration_Manager while Q is held.
    private void HandleDrinkInput()
    {
        if (player == null)
            return;

        // Fix: don't play the drink animation while paused, in a menu or minigame, or
        // collapsed; no water is given then (PlayerHydration_Manager checks the same thing).
        if (Game_Manager.instance != null && !Game_Manager.instance.CanControlPlayer())
            return;

        player.playerHydration_Manager.HandleDrinkAction();
    }

    // Esc: pause while exploring, or resume while paused. Other states (menus, minigames)
    // handle Esc themselves.
    private void HandlePauseInput()
    {
        if (Game_Manager.instance == null)
            return;

        if (Game_Manager.instance.currentGameState ==
            Game_Manager.GameState.Exploring)
        {
            Game_Manager.instance.ChangeGameState(
                Game_Manager.GameState.Paused
            );
        }
        // Esc again resumes (added for the maze playtest). Before, the only way out of the
        // pause was the Resume button, so a scene whose pause menu couldn't be seen or
        // clicked (no Canvas or no EventSystem) stayed frozen for good.
        else if (Game_Manager.instance.currentGameState ==
                 Game_Manager.GameState.Paused)
        {
            Game_Manager.instance.ResumeGame();
        }
    }
}
