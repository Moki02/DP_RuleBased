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
// Crouch (C toggles, new): GameBalance > Crouch Speed (2 m/s), the capsule gets shorter. No
// sprinting while crouched: pressing Shift stands the player up (if there's room above).
// Jump (Space, new): GameBalance > Jump Height (1.2 m). PlayerHydration_Manager decides if a
// jump is allowed and takes its HYD cost (GameBalance > Jump Hyd Cost). Only from the ground.
//
// The speed eases in and out over a fraction of a second ("Speed Change Damping") so the
// start and stop steps of the animations line up with the feet. The top speeds are unchanged.
// When standing still and the camera is turned far away, the player turns in place to face
// it ("Turn To Camera When Idle"). PlayerSyntyAnimator reads the public state below.
//
// DESIGN RULES: hydration states table (section 2), R5.4. Crouch and jump are new (GDD to do).
//
// HOW TO USE: on the Player prefab; Player_Manager calls HandleAllMovement every frame.
// HOW TO EDIT: speeds are in GameBalance (Player, Crouch and jump) and the Build and Skills
// effects CSV. Gravity, damping, crouch height and turn-in-place are in the Inspector.
// ============================================================================

using System;
using UnityEngine;

/// <summary>Moves and turns the player (walk, sprint, crouch, jump, thirst and heavy-carry slowdowns).</summary>
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

    [Tooltip("How quickly the player speeds up and slows down (higher = snappier). About 12 reaches full " +
             "speed in a quarter of a second, which matches the start and stop animations. 0 = instant (old feel).")]
    [SerializeField] float speedChangeDamping = 12f;

    [Tooltip("How fast the player falls, in m/s². Real gravity is -9.81; games often use more so falls feel snappy.")]
    [SerializeField] float gravity = -20f;
    [Tooltip("Small downward push while standing on the ground (m/s), so the player stays stuck to slopes and steps.")]
    [SerializeField] float groundedPull = -2f;
    [Tooltip("If the player ever drops below this height (fell through the floor), they're put back at floor level.")]
    [SerializeField] float fallResetHeight = -20f;
    private float verticalVelocity;

    [Header("Ground Check")]
    [Tooltip("How far below the feet still counts as 'on the ground' for the animations (steps, small bumps).")]
    [SerializeField] float groundProbeDistance = 0.25f;
    [Tooltip("What counts as ground (and as a ceiling when standing up from a crouch).")]
    [SerializeField] LayerMask groundLayers = ~0;

    [Header("Crouch")]
    [Tooltip("Capsule height while crouched, as a fraction of the standing height.")]
    [Range(0.3f, 1f)] [SerializeField] float crouchHeightRatio = 0.65f;
    [Tooltip("Old scenes (no GameBalance): crouch speed in m/s.")]
    [SerializeField] float fallbackCrouchSpeed = 2f;

    [Header("Jump")]
    [Tooltip("Old scenes (no GameBalance): jump height in metres.")]
    [SerializeField] float fallbackJumpHeight = 1.2f;
    [Tooltip("Seconds after landing before the next jump is allowed.")]
    [SerializeField] float jumpCooldown = 0.25f;
    [Tooltip("Seconds after getting control back (minigame, menu, pause) before Space can jump. " +
             "Stops the last Space press of the Valve minigame from leaking into a jump.")]
    [SerializeField] float jumpBlockAfterControlRegained = 0.3f;
    [Tooltip("Slowest steering speed in the air (m/s), so a standing jump can still drift a little.")]
    [SerializeField] float minAirSpeed = 1.5f;

    [Header("Turn In Place")]
    [Tooltip("Standing still: turn in place to face where the camera looks once it is turned this far away.")]
    [SerializeField] bool turnToCameraWhenIdle = true;
    [Tooltip("Degrees between the player's facing and the camera before an idle turn starts.")]
    [Range(10f, 180f)] [SerializeField] float idleTurnStartAngle = 90f;
    [Tooltip("Idle turn speed in degrees per second.")]
    [SerializeField] float idleTurnSpeed = 180f;

    // ------------------------------------------------------------------ State (read by PlayerSyntyAnimator)

    /// <summary>True while crouched (C toggles).</summary>
    public bool IsCrouching { get; private set; }
    /// <summary>True while sprinting this frame (Shift, moving, allowed by hydration, not crouched).</summary>
    public bool IsSprinting { get; private set; }
    /// <summary>True on the ground (a short probe under the feet smooths steps and bumps).</summary>
    public bool IsGrounded { get; private set; } = true;
    /// <summary>True from take-off until the jump stops rising.</summary>
    public bool IsJumping { get; private set; }
    /// <summary>Seconds in the air (0 on the ground).</summary>
    public float FallingDuration { get; private set; }
    /// <summary>True while turning in place to face the camera.</summary>
    public bool IsTurningInPlace { get; private set; }
    /// <summary>Signed degrees still to turn while IsTurningInPlace (+ = right).</summary>
    public float TurnInPlaceAngle { get; private set; }
    /// <summary>The player's horizontal velocity (m/s, world space).</summary>
    public Vector3 PlanarVelocity => planarVelocity;
    /// <summary>Camera-relative input direction (world, flat, normalised; zero with no input).</summary>
    public Vector3 InputDirection => moveAmount > 0f ? moveDirection : Vector3.zero;
    /// <summary>True while Dehydrated (the slower, tired walk).</summary>
    public bool IsDehydrated { get; private set; }

    /// <summary>Fired once at take-off.</summary>
    public event Action Jumped;
    /// <summary>Fired once when touching the ground after being in the air.</summary>
    public event Action Landed;

    private Vector3 planarVelocity;
    private float airborneSpeed;
    private float nextJumpTime;
    private float lastHandledTime = -10f;
    private float controlRegainedTime = -10f;
    private bool previousSprintInput;
    private bool idleTurning;
    private float standingHeight = -1f;
    private Vector3 standingCenter;

    float CrouchSpeed
    {
        get { var b = TheLastDrop.BalanceProvider.Current; return b != null ? b.crouchSpeed : fallbackCrouchSpeed; }
    }

    float JumpHeight
    {
        get { var b = TheLastDrop.BalanceProvider.Current; return b != null ? b.jumpHeight : fallbackJumpHeight; }
    }

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
        // Control was lost (pause, menu, minigame, pumping) and is back: block jumps for a moment.
        if (Time.unscaledTime - lastHandledTime > 0.1f)
            controlRegainedTime = Time.unscaledTime;
        lastHandledTime = Time.unscaledTime;

        GetMovementValues();
        CacheStandingCapsule();
        HandleCrouchInput();

        if (player.playerAnimator_Manager.canMove)
        {
            HandleGroundedMovement();
        }
        else
        {
            planarVelocity = Vector3.zero;
            IsSprinting = false;
        }

        if (player.playerAnimator_Manager.canRotate)
        {
            HandleRotation();
        }
        else
        {
            IsTurningInPlace = false;
            idleTurning = false;
        }

        HandleJumpInput();
        HandleGravity();
        UpdateGroundState();

        previousSprintInput = player.playerInput_Manager.sprintInput;
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

        CollisionFlags flags = cc.Move(Vector3.up * verticalVelocity * Time.deltaTime);

        // Hit a ceiling on the way up: start falling straight away (no sticking to it).
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

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
        IsJumping = false;
        FallingDuration = 0f;
    }

    /// <summary>Walks, sprints or crouch-walks along the input, relative to the camera.</summary>
    public void HandleGroundedMovement()
    {
        PlayerHydration_Manager hydration = player.playerHydration_Manager;

        if (hydration != null && hydration.IsCollapsed())
        {
            planarVelocity = Vector3.zero;
            IsSprinting = false;
            return;
        }

        moveDirection = Player_Camera.instance.transform.forward * verticalMovement;
        moveDirection += Player_Camera.instance.transform.right * horizontalMovement;

        moveDirection.y = 0;
        moveDirection.Normalize();

        // No sprinting while crouched: pressing Shift stands the player up (if there's room).
        bool sprintInput = player.playerInput_Manager.sprintInput;
        if (IsCrouching && sprintInput && !previousSprintInput)
            TryStandUp();

        bool isSprinting = sprintInput && moveAmount > 0 && !IsCrouching;

        if (hydration != null && !hydration.CanSprint())
        {
            isSprinting = false;
        }

        float movementSpeed = player_WalkingSpeed;

        if (IsCrouching)
        {
            movementSpeed = CrouchSpeed;
        }
        else if (isSprinting)
        {
            movementSpeed = player_RunningSpeed;
        }

        var balance = TheLastDrop.BalanceProvider.Current;

        // Dehydrated: walk × 0.75 (M5 Marathoner removes it).
        IsDehydrated = hydration != null &&
                       hydration.currentHydrationState ==
                       PlayerHydration_Manager.HydrationState.Dehydrated;
        if (IsDehydrated)
        {
            float slow = balance != null ? balance.dehydratedSpeedMult : 0.75f;
            movementSpeed *= TheLastDrop.Skills.F("dehydratedWalkMult", slow);
        }

        // R5.4 Jerry Can: slower while carrying more than 6 L (checks the water, not the container).
        var res = GameResources_Manager.instance;
        float heavyMult = TheLastDrop.Skills.F("heavySpeedMult", 1f);
        if (res != null && heavyMult < 1f && balance != null && res.canteen > balance.heavyCarryThreshold)
            movementSpeed *= heavyMult;

        // In the air: keep the speed of the take-off (steering still works; a standing jump
        // can still drift a little).
        if (!IsGrounded)
            movementSpeed = Mathf.Max(airborneSpeed, minAirSpeed);

        Vector3 targetVelocity = moveAmount > 0 ? moveDirection * movementSpeed : Vector3.zero;

        if (speedChangeDamping <= 0f)
            planarVelocity = targetVelocity;
        else
            planarVelocity = Vector3.Lerp(planarVelocity, targetVelocity, 1f - Mathf.Exp(-speedChangeDamping * Time.deltaTime));

        if (targetVelocity == Vector3.zero && planarVelocity.sqrMagnitude < 0.0025f)
            planarVelocity = Vector3.zero;

        if (planarVelocity != Vector3.zero)
        {
            player.characterController.Move(planarVelocity * Time.deltaTime);
        }

        IsSprinting = isSprinting;
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
            HandleIdleTurn();
            return;
        }

        idleTurning = false;
        IsTurningInPlace = false;
        TurnInPlaceAngle = 0f;

        Quaternion newRotation = Quaternion.LookRotation(targetRotationDirection);
        Quaternion targetRotation = Quaternion.Slerp(transform.rotation, newRotation, player_RotationalSpeed * Time.deltaTime);
        transform.rotation = targetRotation;
    }

    // Standing still with the camera turned far away: turn in place to face where it looks.
    private void HandleIdleTurn()
    {
        IsTurningInPlace = false;
        TurnInPlaceAngle = 0f;

        if (!turnToCameraWhenIdle || !IsGrounded || planarVelocity.sqrMagnitude > 0.01f || Player_Camera.instance == null)
        {
            idleTurning = false;
            return;
        }

        Vector3 cameraForward = Player_Camera.instance.transform.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.0001f)
            return;
        cameraForward.Normalize();

        float angle = Vector3.SignedAngle(transform.forward, cameraForward, Vector3.up);

        if (!idleTurning && Mathf.Abs(angle) > idleTurnStartAngle)
            idleTurning = true;
        if (idleTurning && Mathf.Abs(angle) < 5f)
            idleTurning = false;
        if (!idleTurning)
            return;

        float step = Mathf.Min(Mathf.Abs(angle), idleTurnSpeed * Time.deltaTime) * Mathf.Sign(angle);
        transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * transform.rotation;

        IsTurningInPlace = true;
        TurnInPlaceAngle = angle;
    }

    // ------------------------------------------------------------------ Crouch

    private void CacheStandingCapsule()
    {
        if (standingHeight > 0f || player.characterController == null)
            return;
        standingHeight = player.characterController.height;
        standingCenter = player.characterController.center;
    }

    private void HandleCrouchInput()
    {
        if (!player.playerInput_Manager.crouchPressed)
            return;

        if (IsCrouching)
            TryStandUp();
        else if (IsGrounded && !player.playerAnimator_Manager.isPerformingAction)
            SetCrouch(true);
    }

    private void SetCrouch(bool crouch)
    {
        var cc = player.characterController;
        if (cc != null && standingHeight > 0f)
        {
            float height = crouch ? standingHeight * crouchHeightRatio : standingHeight;
            cc.height = height;
            // Keep the feet where they are: lower the centre by half the height lost.
            cc.center = new Vector3(standingCenter.x, standingCenter.y - (standingHeight - height) * 0.5f, standingCenter.z);
        }
        IsCrouching = crouch;
    }

    /// <summary>Stands up if there is room above the head. Returns true if standing afterwards.</summary>
    public bool TryStandUp()
    {
        if (!IsCrouching)
            return true;
        if (!HasRoomToStand())
            return false;
        SetCrouch(false);
        return true;
    }

    private bool HasRoomToStand()
    {
        var cc = player.characterController;
        if (cc == null || standingHeight <= 0f)
            return true;

        float radius = cc.radius * 0.9f;
        Vector3 crouchTop = transform.TransformPoint(cc.center) + Vector3.up * (cc.height * 0.5f - cc.radius);
        float distance = standingHeight - cc.height;
        return !Physics.SphereCast(crouchTop, radius, Vector3.up, out _, distance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------ Jump

    private void HandleJumpInput()
    {
        if (!player.playerInput_Manager.jumpPressed)
            return;

        if (!IsGrounded || IsJumping || Time.time < nextJumpTime)
            return;
        if (Time.unscaledTime - controlRegainedTime < jumpBlockAfterControlRegained)
            return;
        if (player.playerAnimator_Manager.isPerformingAction)
            return;

        var hydration = player.playerHydration_Manager;
        if (hydration != null && !hydration.CanJump())
            return;
        if (!TryStandUp())
            return;
        if (hydration != null && !hydration.TryPayJumpCost())
            return;

        verticalVelocity = Mathf.Sqrt(2f * JumpHeight * -gravity);
        airborneSpeed = new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude;
        IsJumping = true;
        IsGrounded = false;
        idleTurning = false;
        Jumped?.Invoke();
    }

    // ------------------------------------------------------------------ Ground state

    private void UpdateGroundState()
    {
        var cc = player.characterController;
        if (cc == null || !cc.enabled)
            return;

        if (IsJumping && verticalVelocity <= 0f)
            IsJumping = false;

        bool grounded = cc.isGrounded;

        // A short probe under the feet: going down a step or over a bump isn't "falling".
        if (!grounded && !IsJumping && verticalVelocity <= 0f)
        {
            Vector3 bottom = transform.TransformPoint(cc.center) + Vector3.down * (cc.height * 0.5f - cc.radius);
            grounded = Physics.SphereCast(bottom + Vector3.up * 0.05f, cc.radius * 0.9f, Vector3.down, out _,
                                          groundProbeDistance + 0.05f, groundLayers, QueryTriggerInteraction.Ignore);
        }

        if (grounded)
        {
            if (!IsGrounded)
            {
                nextJumpTime = Time.time + jumpCooldown;
                Landed?.Invoke();
            }
            FallingDuration = 0f;
        }
        else
        {
            FallingDuration += Time.deltaTime;
            // Can't stay crouched in the air.
            if (IsCrouching)
                TryStandUp();
        }

        // Walked off a ledge: keep the speed it was left at (a jump sets it at take-off).
        if (!grounded && IsGrounded)
            airborneSpeed = planarVelocity.magnitude;

        IsGrounded = grounded;
    }
}
