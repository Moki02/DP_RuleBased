using UnityEngine;


public class Player_Manager : MonoBehaviour
{
    public static Player_Manager instance;

    [HideInInspector] public CharacterController characterController;
    [HideInInspector] public Animator animator;
    [Header("DEBUG MENU")]

    [HideInInspector] public PlayerAnimator_Manager playerAnimator_Manager;
    [HideInInspector] public PlayerLocomotion_Manager playerLocomotion_Manager;
    [HideInInspector] public PlayerInput_Manager playerInput_Manager;
    [HideInInspector] public PlayerStats_Manager playerStats_Manager;
    [HideInInspector] public PlayerHydration_Manager playerHydration_Manager;
    [HideInInspector] public PlayerDeath_Manager playerDeath_Manager;
    [HideInInspector] public PlayerInteraction_Manager playerInteraction_Manager;

    [HideInInspector] public Game_Manager game_Manager;

    private void Awake()
    {
        instance = this;

        playerInput_Manager = GetComponent<PlayerInput_Manager>();
        playerLocomotion_Manager = GetComponent<PlayerLocomotion_Manager>();
        playerAnimator_Manager = GetComponent<PlayerAnimator_Manager>();
        playerStats_Manager = GetComponent<PlayerStats_Manager>();
        playerHydration_Manager = GetComponent<PlayerHydration_Manager>();
        playerDeath_Manager = GetComponent<PlayerDeath_Manager>();
        playerInteraction_Manager = GetComponent<PlayerInteraction_Manager>();

        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        game_Manager = Game_Manager.instance;
        game_Manager.player_Manager = this;

        Player_Camera.instance.player = this;
        PlayerInput_Manager.instance.player = this;
        PlayerLocomotion_Manager.instance.player = this;
        PlayerInteraction_Manager.instance.player = this;
    }

   

    private void Update()
    {
        if (!Game_Manager.instance.CanControlPlayer())
            return;

        if (playerHydration_Manager.isPumping)
            return;

        playerLocomotion_Manager.HandleAllMovement();
    }

    private void LateUpdate()
    {
        Player_Camera.instance.HandleAllCamereActions();
    }



}
