// ============================================================================
// Player_Manager.cs
// The "hub" of the player: finds all the other Player scripts on the same object so any
// script can reach them through Player_Manager.instance (e.g.
// Player_Manager.instance.playerHydration_Manager), links the player to the camera and
// input, and runs movement and the camera each frame.
//
// DESIGN RULES: none of its own (movement stops whenever Game_Manager says the player
// can't be controlled: pause, menus, minigames, collapse; and while pumping).
//
// HOW TO USE: on the Player prefab, together with CharacterController, Animator and the
// other Player*_Manager scripts. Nothing to drag in.
// HOW TO EDIT: nothing to tune here; speeds are in PlayerStats_Manager.
// ============================================================================

using UnityEngine;


/// <summary>Holds references to all the player's scripts and drives movement and the camera.</summary>
public class Player_Manager : MonoBehaviour
{
    /// <summary>The player in this scene.</summary>
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

    // Start (not Awake): the camera and Game Manager are other objects, so they are only
    // sure to have run their own Awake (and set their "instance") by now.
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

        // The hand pump holds the player in place while pumping.
        if (playerHydration_Manager.isPumping)
            return;

        playerLocomotion_Manager.HandleAllMovement();
    }

    // LateUpdate: the camera moves after the player has moved this frame, so it doesn't jitter.
    private void LateUpdate()
    {
        Player_Camera.instance.HandleAllCamereActions();
    }



}
