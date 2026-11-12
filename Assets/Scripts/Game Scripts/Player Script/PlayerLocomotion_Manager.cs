// ============================================================================
// PlayerLocomotion_Manager.cs
// Moves and turns the player from the movement input, relative to the camera.
// Speed = walking or sprinting speed (PlayerStats_Manager; M1 / M2 change them through
// UpgradeManager), then:
//   - no sprinting while Dehydrated or Collapsed (hydration states table);
//   - Dehydrated: × 0.75 (GameBalance > Dehydrated Speed Mult; M5 Marathoner makes it × 1);
//   - S7 Jerry Can: × 0.9 while the canteen holds more than 6 L (R5.4).
// No movement at all while collapsed.
// Gravity: the player is always pulled down onto the ground. (Without it, stepping up onto a
// ledge, a gate threshold or a prop left the player floating at that height, higher each time.)
//
// DESIGN RULES: hydration states table (section 2), R5.4.
//
// HOW TO USE: on the Player prefab; Player_Manager calls HandleAllMovement every frame.
// HOW TO EDIT: speeds are in GameBalance (Player) and the Build and Skills effects CSV.
// Gravity strength is "Gravity" in the Inspector (Movement Settings).
// ============================================================================

using UnityEngine;

/// <summary>Moves and turns the player (walk, sprint, thirst and heavy-carry slowdowns).</summary>
public class PlayerLocomotion_Manager : MonoBehaviour
{
    public static PlayerLocomotion_Manager instance;
    public Player_Manager player;

    [HideInInspector] public float verticalMovement;
    [HideInInspector] public float horizontalMovement;
    [HideInInspector] public float moveAmount;

    [Header("Movement Settings")]
    private Vector3 targetRotationDirection;
    [SerializeField] float player_WalkingSpeed;
    [SerializeField] float player_RunningSpeed;
    [SerializeField] float player_RotationalSpeed;
    private Vector3 moveDirection;

    [Tooltip("How fast the player falls, in m/s². Real gravity is -9.81; games often use more so falls feel snappy.")]
    [SerializeField] float gravity = -20f;
    [Tooltip("Small downward push while standing on the ground (m/s), so the player stays stuck to slopes and steps.")]
    [SerializeField] float groundedPull = -2f;
    [Tooltip("If the player ever drops below this height (fell through the floor), they're put back at floor level.")]
    [SerializeField] float fallResetHeight = -20f;
    private float verticalVelocity;

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


    private void GetMovementValues()
    {
        verticalMovement = player.playerInput_Manager.movementVerticalInput;
        horizontalMovement = player.playerInput_Manager.movementHorizontalInput;
        moveAmount = player.playerInput_Manager.moveAmount;

        player_WalkingSpeed = player.playerStats_Manager.player_WalkingSpeed;
        player_RunningSpeed = player.playerStats_Manager.player_RunningSpeed;
        player_RotationalSpeed = player.playerStats_Manager.player_RotationalSpeed;
    }

    /// <summary>Moves and turns the player this frame (when the current animation allows it).</summary>
    public void HandleAllMovement()
    {
        if (player.playerAnimator_Manager.canMove)
        {
            HandleGroundedMovement();
        }

        if (player.playerAnimator_Manager.canRotate)
        {
            HandleRotation();
        }

        HandleGravity();
    }

    // Pulls the player down every frame, so walking off anything brings them back to the floor.
    private void HandleGravity()
    {
        var cc = player.characterController;
        if (cc == null || !cc.enabled)
            return;

        if (cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedPull;         // standing: just keep a light grip on the ground
        else
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -50f); // falling

        cc.Move(Vector3.up * verticalVelocity * Time.deltaTime);

        // Safety net: fell through the world (e.g. an art floor without a collider): put them back on top.
        if (transform.position.y < fallResetHeight)
        {
            cc.enabled = false;
            transform.position = new Vector3(transform.position.x, 1f, transform.position.z);
            cc.enabled = true;
            verticalVelocity = 0f;
            Debug.LogWarning("[PlayerLocomotion_Manager] The player fell through the floor and was put back. " +
                             "Check that the floor here has a collider.", this);
        }
    }

    /// <summary>Stops any fall in progress (call after moving the player somewhere, e.g. a respawn).</summary>
    public void ResetFall()
    {
        verticalVelocity = 0f;
    }

    /// <summary>Walks or sprints along the input, relative to the camera.</summary>
    public void HandleGroundedMovement()
    {
        GetMovementValues();

        PlayerHydration_Manager hydration =
            player.GetComponent<PlayerHydration_Manager>();

        if (hydration != null && hydration.IsCollapsed())
            return;

        moveDirection = Player_Camera.instance.transform.forward * verticalMovement;
        moveDirection += Player_Camera.instance.transform.right * horizontalMovement;

        moveDirection.y = 0;
        moveDirection.Normalize();

        bool isSprinting =
            player.playerInput_Manager.sprintInput &&
            moveAmount > 0;

        if (hydration != null && !hydration.CanSprint())
        {
            isSprinting = false;
        }

        float movementSpeed = player_WalkingSpeed;

        if (isSprinting)
        {
            movementSpeed = player_RunningSpeed;
        }

        var balance = TheLastDrop.BalanceProvider.Current;

        // Dehydrated: walk × 0.75 (M5 Marathoner removes it).
        if (hydration != null &&
            hydration.currentHydrationState ==
            PlayerHydration_Manager.HydrationState.Dehydrated)
        {
            float slow = balance != null ? balance.dehydratedSpeedMult : 0.75f;
            movementSpeed *= TheLastDrop.Skills.F("dehydratedWalkMult", slow);
        }

        // R5.4 Jerry Can: slower while carrying more than 6 L (checks the water, not the container).
        var res = GameResources_Manager.instance;
        float heavyMult = TheLastDrop.Skills.F("heavySpeedMult", 1f);
        if (res != null && heavyMult < 1f && balance != null && res.canteen > balance.heavyCarryThreshold)
            movementSpeed *= heavyMult;

        if (moveAmount > 0)
        {
            player.characterController.Move(
                moveDirection * movementSpeed * Time.deltaTime
            );
        }

        player.playerAnimator_Manager.UpdateAnimatorMovementParameters(moveAmount, isSprinting);
    }

    private void HandleRotation()
    {
        targetRotationDirection = Vector3.zero;
        targetRotationDirection = Player_Camera.instance.cameraObject.transform.forward * verticalMovement;
        targetRotationDirection = targetRotationDirection + Player_Camera.instance.cameraObject.transform.right * horizontalMovement;
        targetRotationDirection.Normalize();
        targetRotationDirection.y = 0;

        if (targetRotationDirection == Vector3.zero)
        {
            targetRotationDirection = transform.forward;
        }

        Quaternion newRotation = Quaternion.LookRotation(targetRotationDirection);
        Quaternion targetRotation = Quaternion.Slerp(transform.rotation, newRotation, player_RotationalSpeed * Time.deltaTime);
        transform.rotation = targetRotation;
    }

}
