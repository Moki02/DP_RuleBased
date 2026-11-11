using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerInput_Manager : MonoBehaviour
{
    public static PlayerInput_Manager instance;
    public Player_Manager player;

    Player_Controls playerControls;

    [Header("CAMERA MOVEMENT INPUT")]
    [SerializeField] Vector2 mouseInput;
    public float mouseVerticalInput;
    public float mouseHorizontalInput;

    [Header("PLAYER MOVEMENT INPUT")]
    [SerializeField] Vector2 movementInput; 
    public float movementVerticalInput;
    public float movementHorizontalInput;
    public float moveAmount;

    [Header("PLAYER ACTION INPUT")]
    public bool sprintInput = false;

    [Header("Interaction Input")]
    public bool interactInput;
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
    }

    private void OnApplicationFocus(bool focus)
    {
        if (focus)
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

    private void HandleDrinkInput()
    {
        if (player == null)
            return;

        player.playerHydration_Manager.HandleDrinkAction();
    }

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
