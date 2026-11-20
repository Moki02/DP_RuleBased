// ============================================================================
// PlayerSyntyAnimator.cs
// Drives the Synty "Animation - Base Locomotion" controller (AC_Salok_Player) from OUR movement.
// It never moves or turns the player: PlayerLocomotion_Manager does that. This script only
// reads what the player is doing and sets the Animator's parameters, ported from Synty's
// SamplePlayerAnimationController (non-strafing mode: IsStrafing = 0).
//
// GAIT MAPPING (why the feet don't slide): Synty's clips were recorded at walk 1.4, run 2.5
// and sprint 7 m/s, and the locomotion blend tree has a clip at each of those MoveSpeed values.
// Feeding the player's real speed into MoveSpeed blends the two nearest clips, so the blended
// stride matches the real speed:
//   - walk 4 m/s       -> MoveSpeed 4   = run clip blended with a third of sprint  (gait Run)
//   - sprint 6.5 m/s   -> MoveSpeed 6.5 = almost all sprint clip                    (gait Sprint)
//   - crouch 2 m/s     -> crouch-walk clip (recorded at 1.4) played 1.43× faster    (gait Walk)
//   - dehydrated 3 m/s -> tired walk: MoveSpeed capped at "Tired Anim Move Speed"
//                         (mostly the walk clip), played faster so the feet cover 3 m/s,
//                         head and body looking down, less lean                     (gait Walk)
// Playback speed only scales the locomotion and crouch blend trees (the LocomotionPlaybackSpeed
// parameter), never jumps, actions or the additive layers.
//
// ACTIONS: PlayerAnimator_Manager.PlayTargetActionAnimation finds which layer holds a state
// (FindActionLayer) and this script fades that layer in, then out again once it is back in its
// "Empty" state. Holds are hooked up here, with no change to the interactables: holding E at
// the hand pump plays "Pump", at a lock basin "Pour"; letting go returns to locomotion.
// Safety net: an action that has ended (layer back to Empty) always clears isPerformingAction,
// so the player can never stay frozen.
//
// DESIGN RULES: none of its own (purely visual).
//
// HOW TO USE: on the Player, next to the Animator (PlayerAnimator_Manager adds it if missing).
// The Animator needs AC_Salok_Player (Assets/_Project/Animation) with Apply Root Motion off.
// HOW TO EDIT: every number is in the Inspector, grouped by header. See
// Assets/_Project/Animation/README.md for which clip plays when.
// ============================================================================

using UnityEngine;

/// <summary>Sets the Synty locomotion parameters from the player's movement and runs the action layers.</summary>
[DefaultExecutionOrder(50)] // after Player_Manager (movement) so this frame's values are used
[RequireComponent(typeof(Animator))]
public class PlayerSyntyAnimator : MonoBehaviour
{
    // ------------------------------------------------------------------ Parameter hashes

    static readonly int MovementInputTappedHash = Animator.StringToHash("MovementInputTapped");
    static readonly int MovementInputPressedHash = Animator.StringToHash("MovementInputPressed");
    static readonly int MovementInputHeldHash = Animator.StringToHash("MovementInputHeld");
    static readonly int ShuffleDirectionXHash = Animator.StringToHash("ShuffleDirectionX");
    static readonly int ShuffleDirectionZHash = Animator.StringToHash("ShuffleDirectionZ");
    static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    static readonly int CurrentGaitHash = Animator.StringToHash("CurrentGait");
    static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    static readonly int FallingDurationHash = Animator.StringToHash("FallingDuration");
    static readonly int InclineAngleHash = Animator.StringToHash("InclineAngle");
    static readonly int StrafeDirectionXHash = Animator.StringToHash("StrafeDirectionX");
    static readonly int StrafeDirectionZHash = Animator.StringToHash("StrafeDirectionZ");
    static readonly int ForwardStrafeHash = Animator.StringToHash("ForwardStrafe");
    static readonly int CameraRotationOffsetHash = Animator.StringToHash("CameraRotationOffset");
    static readonly int IsStrafingHash = Animator.StringToHash("IsStrafing");
    static readonly int IsTurningInPlaceHash = Animator.StringToHash("IsTurningInPlace");
    static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    static readonly int IsStoppedHash = Animator.StringToHash("IsStopped");
    static readonly int IsStartingHash = Animator.StringToHash("IsStarting");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int LeanValueHash = Animator.StringToHash("LeanValue");
    static readonly int HeadLookXHash = Animator.StringToHash("HeadLookX");
    static readonly int HeadLookYHash = Animator.StringToHash("HeadLookY");
    static readonly int BodyLookXHash = Animator.StringToHash("BodyLookX");
    static readonly int BodyLookYHash = Animator.StringToHash("BodyLookY");
    static readonly int LocomotionStartDirectionHash = Animator.StringToHash("LocomotionStartDirection");
    static readonly int LocomotionPlaybackSpeedHash = Animator.StringToHash("LocomotionPlaybackSpeed");
    // The old placeholder Player.controller (still used by test scenes).
    static readonly int LegacySpeedHash = Animator.StringToHash("Speed");
    static readonly int LegacyLocomotionHash = Animator.StringToHash("Walking Blend Tree");

    /// <summary>Name of the resting state in every action layer.</summary>
    public const string EmptyState = "Empty";

    enum Gait { Idle = 0, Walk = 1, Run = 2, Sprint = 3 }

    // ------------------------------------------------------------------ Inspector

    [Header("Synty clip speeds (m/s the clips were recorded at)")]
    [Tooltip("Speed of Synty's walk clip. Gait thresholds sit halfway between these.")]
    [SerializeField] float clipWalkSpeed = 1.4f;
    [Tooltip("Speed of Synty's run (jog) clip.")]
    [SerializeField] float clipRunSpeed = 2.5f;
    [Tooltip("Speed of Synty's sprint clip (the fastest clip in the blend tree).")]
    [SerializeField] float clipSprintSpeed = 7f;
    [Tooltip("Speed of Synty's crouch-walk clip.")]
    [SerializeField] float clipCrouchSpeed = 1.4f;

    [Header("Gait mapping")]
    [Tooltip("MoveSpeed = real speed × this, for normal walking and sprinting. 1 = the blend tree " +
             "picks the clips by real speed (no sliding). Lower = a calmer gait played faster.")]
    [SerializeField] float animSpeedScale = 1f;
    [Tooltip("Fastest the locomotion clips may be played to keep the feet matched.")]
    [SerializeField] float maxPlaybackSpeed = 1.75f;
    [Tooltip("How quickly MoveSpeed and the playback speed follow the real speed (higher = snappier).")]
    [SerializeField] float animSpeedDamping = 10f;

    [Header("Dehydrated tired walk")]
    [Tooltip("Show the tired walk while Dehydrated (HYD 1-29) and walking.")]
    [SerializeField] bool tiredWalkEnabled = true;
    [Tooltip("MoveSpeed used for the tired walk. 1.4 = pure walk clip; up to 1.9 still counts as the Walk gait. " +
             "The clip is then played faster so the feet cover the real speed (3 m/s).")]
    [SerializeField] float tiredAnimMoveSpeed = 1.9f;
    [Tooltip("Head looks down by this much while tired (0 to 1 of Synty's head-look range).")]
    [Range(0f, 1f)] [SerializeField] float tiredHeadDown = 0.35f;
    [Tooltip("Upper body leans down by this much while tired (0 to 1 of Synty's body-look range).")]
    [Range(0f, 1f)] [SerializeField] float tiredBodyDown = 0.25f;
    [Tooltip("Lean into turns × this while tired (1 = normal lean).")]
    [Range(0f, 1f)] [SerializeField] float tiredLeanScale = 0.4f;
    [Tooltip("Seconds to blend into and out of the tired look.")]
    [SerializeField] float tiredBlendTime = 0.6f;

    [Header("Input timing (shuffles, starts)")]
    [Tooltip("Seconds of held movement before it counts as 'held' rather than a tap.")]
    [SerializeField] float buttonHoldThreshold = 0.15f;

    [Header("Lean")]
    [SerializeField] bool enableLean = true;
    [Tooltip("Lean amount by speed (0 = still, 1 = sprint clip speed).")]
    [SerializeField] AnimationCurve leanCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Lean × this (all states).")]
    [Range(0f, 2f)] [SerializeField] float leanScale = 1f;

    [Header("Head and body look")]
    [SerializeField] bool enableHeadLook = true;
    [SerializeField] bool enableBodyLook = true;
    [Tooltip("Head turn shape by turn rate (-1 to 1 in, -1 to 1 out).")]
    [SerializeField] AnimationCurve headLookXCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
    [Tooltip("Body turn shape by turn rate (-1 to 1 in, -1 to 1 out).")]
    [SerializeField] AnimationCurve bodyLookXCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
    [Tooltip("The head also turns toward where the camera faces.")]
    [SerializeField] bool headFollowsCamera = true;
    [Tooltip("Camera angle (degrees left/right of the player) that gives a full head turn.")]
    [SerializeField] float headLookMaxAngle = 80f;
    [Tooltip("Beyond this angle (camera looking at the player's face) the head stops following.")]
    [SerializeField] float headLookCutoffAngle = 130f;
    [Tooltip("Lowest head/body look from the camera pitch (Synty sample: -0.1, a little down).")]
    [Range(-1f, 0f)] [SerializeField] float lookDownLimit = -0.3f;

    [Header("Actions")]
    [Tooltip("Action layers, by name. Each has an 'Empty' state it rests in.")]
    [SerializeField] string[] actionLayerNames = { "Actions", "UpperBodyActions" };
    [Tooltip("How fast an action layer fades in and out (weight per second, unscaled time).")]
    [SerializeField] float actionLayerFadeSpeed = 8f;
    [Tooltip("Play 'Pump' while holding E at the hand pump.")]
    [SerializeField] bool pumpAnimation = true;
    [Tooltip("Play 'Pour' while holding E at a lock basin.")]
    [SerializeField] bool pourAnimation = true;

    [Header("Debug (read only)")]
    [SerializeField] float debugRealSpeed;
    [SerializeField] float debugMoveSpeedParam;
    [SerializeField] float debugPlaybackSpeed;
    [SerializeField] string debugGait;
    [SerializeField] float debugTiredWeight;
    [SerializeField] string debugAction;

    // ------------------------------------------------------------------ Runtime

    Animator animator;
    Player_Manager player;
    PlayerLocomotion_Manager locomotion;
    bool hasPlaybackParam;
    bool hasSyntyParams;
    bool hasLegacySpeed;
    int[] actionLayers = new int[0];

    // Movement input handed over by PlayerAnimator_Manager each frame the player moves.
    float moveAmountInput;
    int lastMovementInputFrame = -10;

    float inputDuration;
    bool inputTapped, inputPressed, inputHeld;

    float moveSpeedParam;
    float playbackParam = 1f;
    Gait gait;
    float tiredWeight;
    bool wasJumpingOrGrounded = true;
    float fallTimer;

    bool isStarting, isStopped = true;
    float locomotionStartTimer;
    float locomotionStartDirection;
    float newDirectionDifferenceAngle;
    float strafeDirectionX, strafeDirectionZ = 1f;
    float cameraRotationOffset;

    float leanDelay, headLookDelay, bodyLookDelay;
    float leanValue, headLookX, headLookY, bodyLookX, bodyLookY;
    float headCameraX;
    float rotationRate;
    Vector3 previousForward;

    string currentAction;
    int currentActionLayer = -1;
    int actionStartFrame = -10;
    float actionIdleSince = -1f;
    string currentHold;

    const float AnimationDampTime = 5f;

    // ------------------------------------------------------------------ Setup

    void Awake()
    {
        animator = GetComponent<Animator>();
        player = GetComponent<Player_Manager>();
        previousForward = transform.forward;
    }

    void Start()
    {
        locomotion = player != null ? player.playerLocomotion_Manager : GetComponent<PlayerLocomotion_Manager>();
        CacheAnimatorInfo();

        if (animator != null && animator.applyRootMotion)
        {
            // Our movement is code-driven and the clips are in-place versions.
            animator.applyRootMotion = false;
        }
    }

    void CacheAnimatorInfo()
    {
        hasPlaybackParam = false;
        hasSyntyParams = false;
        hasLegacySpeed = false;
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == LocomotionPlaybackSpeedHash) hasPlaybackParam = true;
            if (p.nameHash == MoveSpeedHash) hasSyntyParams = true;
            if (p.nameHash == LegacySpeedHash) hasLegacySpeed = true;
        }

        var layers = new System.Collections.Generic.List<int>();
        foreach (string layerName in actionLayerNames)
        {
            int index = animator.GetLayerIndex(layerName);
            if (index >= 0) layers.Add(index);
        }
        actionLayers = layers.ToArray();
    }

    /// <summary>Called by PlayerAnimator_Manager every frame the player's movement runs.</summary>
    public void SetMovementInput(float moveAmount, bool isSprinting)
    {
        moveAmountInput = moveAmount;
        lastMovementInputFrame = Time.frameCount;

        // Old scenes still on the placeholder Player.controller: set its "Speed" like before.
        if (!hasSyntyParams && hasLegacySpeed && animator != null &&
            (player == null || player.playerAnimator_Manager == null || !player.playerAnimator_Manager.isPerformingAction))
        {
            float speedAmount = moveAmount > 0f ? (isSprinting ? 0.5f : 0.25f) : 0f;
            animator.SetFloat(LegacySpeedHash, speedAmount, 0.1f, Time.deltaTime);
        }
    }

    /// <summary>True if any layer has a state with this name.</summary>
    public bool HasActionState(string stateName)
    {
        return FindLayerOf(stateName) >= 0;
    }

    int FindLayerOf(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return -1;

        int shortHash = Animator.StringToHash(stateName);
        for (int i = animator.layerCount - 1; i >= 0; i--)
        {
            int fullHash = Animator.StringToHash(animator.GetLayerName(i) + "." + stateName);
            if (animator.HasState(i, fullHash) || animator.HasState(i, shortHash))
                return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------ Actions

    /// <summary>The layer that has a state with this name (searched in every layer), or -1 (with a warning).</summary>
    public int FindActionLayer(string stateName)
    {
        int layer = FindLayerOf(stateName);
        if (layer < 0 && animator != null && animator.runtimeAnimatorController != null)
            Debug.LogWarning($"[PlayerSyntyAnimator] No Animator state called \"{stateName}\" in " +
                             $"{animator.runtimeAnimatorController.name}; the action flags still apply and are cleared shortly.", this);
        return layer;
    }

    /// <summary>Called by PlayerAnimator_Manager when an action starts on a layer.</summary>
    public void OnActionStarted(string stateName, int layer)
    {
        currentAction = stateName;
        currentActionLayer = layer;
        actionStartFrame = Time.frameCount;
        actionIdleSince = -1f;
    }

    /// <summary>Blends every action layer back to its Empty state (the locomotion shows again).</summary>
    public void ReturnToLocomotion()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        foreach (int layer in actionLayers)
        {
            if (!IsLayerResting(layer))
                animator.CrossFadeInFixedTime(EmptyState, 0.25f, layer);
        }
        // Old placeholder controller (no action layers): back to its walk blend tree as before.
        if (actionLayers.Length == 0 && animator.HasState(0, LegacyLocomotionHash))
            animator.CrossFade(LegacyLocomotionHash, 0.2f, 0);
        currentAction = null;
        currentActionLayer = -1;
        currentHold = null;
    }

    bool IsLayerResting(int layer)
    {
        if (animator.IsInTransition(layer))
            return animator.GetNextAnimatorStateInfo(layer).IsName(EmptyState);
        return animator.GetCurrentAnimatorStateInfo(layer).IsName(EmptyState);
    }

    // Holds: E at the hand pump plays Pump, E at a lock basin plays Pour (until E is let go).
    void UpdateHoldActions()
    {
        string wanted = null;
        var interaction = PlayerInteraction_Manager.instance;
        var held = interaction != null ? interaction.ActiveHold : null;
        bool canControl = Game_Manager.instance == null || Game_Manager.instance.CanControlPlayer();

        if (canControl && held != null)
        {
            if (pumpAnimation && held is HubPump_Manager)
                wanted = "Pump";
            else if (pourAnimation && held is LockBasin_Manager basin && basin.Lock != null && basin.Lock.CanPour)
                wanted = "Pour";
        }
        if (wanted != null && !HasActionState(wanted))
            wanted = null; // old placeholder controller: no hold animations

        if (wanted == currentHold)
            return;

        var animatorManager = player != null ? player.playerAnimator_Manager : null;
        if (animatorManager == null)
            return;

        // End the old hold, but only if it is still the action playing (a collapse wins).
        if (currentHold != null && currentAction == currentHold)
        {
            animatorManager.ResetActionAnimation();
            ReturnToLocomotion();
        }
        currentHold = null;

        if (wanted != null && currentAction == null && !IsDead())
        {
            // Hold actions loop until E is released; the player stays put meanwhile.
            animatorManager.PlayTargetActionAnimation(wanted, true, false, false, false);
            currentHold = wanted;
        }
    }

    bool IsDead()
    {
        return player != null && player.playerDeath_Manager != null && player.playerDeath_Manager.isDead;
    }

    // Fade action layers in while busy and out once resting; clear stale action flags.
    void UpdateActionLayers(float unscaledDeltaTime)
    {
        bool anyBusy = false;
        foreach (int layer in actionLayers)
        {
            bool busy = !IsLayerResting(layer);
            anyBusy |= busy;
            float target = busy ? 1f : 0f;
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(layer), target, actionLayerFadeSpeed * unscaledDeltaTime);
            animator.SetLayerWeight(layer, weight);
        }

        // The animator only knows about a CrossFade after its next update: wait two frames.
        if (Time.frameCount <= actionStartFrame + 2)
            return;

        if (!anyBusy)
        {
            if (actionIdleSince < 0f) actionIdleSince = Time.unscaledTime;

            // Safety net: the action ended (back to Empty) but nobody cleared the flags.
            var animatorManager = player != null ? player.playerAnimator_Manager : null;
            if (animatorManager != null && Time.unscaledTime - actionIdleSince > 0.15f && currentHold == null &&
                !IsDead() && (animatorManager.isPerformingAction || !animatorManager.canMove || !animatorManager.canRotate))
            {
                animatorManager.ResetActionAnimation();
            }
            if (currentHold == null)
            {
                currentAction = null;
                currentActionLayer = -1;
            }
        }
        else
        {
            actionIdleSince = -1f;
        }
    }

    // ------------------------------------------------------------------ Update

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        if (locomotion == null)
            locomotion = player != null ? player.playerLocomotion_Manager : GetComponent<PlayerLocomotion_Manager>();

        UpdateHoldActions();
        UpdateActionLayers(Time.unscaledDeltaTime);

        float dt = Time.deltaTime;
        if (dt <= 0f || locomotion == null || !hasSyntyParams)
            return; // paused or collapsed (time stopped): keep the pose; old controller: Speed only

        UpdateLocomotionParameters(dt);
        debugAction = currentAction;
    }

    void UpdateLocomotionParameters(float dt)
    {
        bool fresh = lastMovementInputFrame >= Time.frameCount - 1;
        float moveAmount = fresh ? moveAmountInput : 0f;

        // --- Input timing (tap / press / hold), as in Synty's sample.
        if (moveAmount > 0f)
        {
            if (inputDuration == 0f)
            {
                inputTapped = true;
            }
            else if (inputDuration < buttonHoldThreshold)
            {
                inputTapped = false;
                inputPressed = true;
                inputHeld = false;
            }
            else
            {
                inputTapped = false;
                inputPressed = false;
                inputHeld = true;
            }
            inputDuration += dt;
        }
        else
        {
            inputDuration = 0f;
            inputTapped = inputPressed = inputHeld = false;
        }

        Vector3 moveDirection = fresh ? locomotion.InputDirection : Vector3.zero;
        Vector3 velocity = fresh || !locomotion.IsGrounded ? locomotion.PlanarVelocity : Vector3.zero;
        float realSpeed = Mathf.Round(new Vector3(velocity.x, 0f, velocity.z).magnitude * 1000f) / 1000f;

        bool crouching = locomotion.IsCrouching;
        bool grounded = locomotion.IsGrounded;
        bool tired = tiredWalkEnabled && locomotion.IsDehydrated && !crouching && !locomotion.IsSprinting;

        float tiredTarget = tired ? 1f : 0f;
        tiredWeight = Mathf.MoveTowards(tiredWeight, tiredTarget, dt / Mathf.Max(0.01f, tiredBlendTime));

        // --- Gait mapping: MoveSpeed and playback so the feet match the real speed.
        float cap;
        float scaled = realSpeed;
        if (crouching)
            cap = clipCrouchSpeed;
        else if (tired)
            cap = tiredAnimMoveSpeed;
        else
        {
            cap = clipSprintSpeed;
            scaled = realSpeed * animSpeedScale;
        }

        float targetParam = Mathf.Min(scaled, cap);
        float targetPlayback = targetParam > 0.01f ? Mathf.Clamp(realSpeed / targetParam, 1f, maxPlaybackSpeed) : 1f;
        if (!grounded) targetPlayback = 1f;

        float k = 1f - Mathf.Exp(-animSpeedDamping * dt);
        moveSpeedParam = Mathf.Lerp(moveSpeedParam, targetParam, k);
        if (targetParam == 0f && moveSpeedParam < 0.01f) moveSpeedParam = 0f;
        playbackParam = Mathf.Lerp(playbackParam, targetPlayback, k);

        if (moveSpeedParam < 0.01f)
            gait = Gait.Idle;
        else if (crouching || tired)
            gait = Gait.Walk;
        else if (moveSpeedParam < (clipWalkSpeed + clipRunSpeed) * 0.5f)
            gait = Gait.Walk;
        else if (moveSpeedParam < (clipRunSpeed + clipSprintSpeed) * 0.5f)
            gait = Gait.Run;
        else
            gait = Gait.Sprint;

        // --- Starts and stops.
        Vector3 forward = transform.forward;
        newDirectionDifferenceAngle = moveDirection != Vector3.zero && forward != moveDirection
            ? Vector3.SignedAngle(forward, moveDirection, Vector3.up)
            : 0f;
        CheckIfStarting(moveDirection, realSpeed, dt);
        isStopped = moveDirection.magnitude == 0f && realSpeed < 0.5f;

        // --- Facing (non-strafing): strafe values at rest, turn in place from locomotion.
        UpdateStrafeDirection(1f, 0f, dt);
        float turnTarget = locomotion.IsTurningInPlace ? locomotion.TurnInPlaceAngle : 0f;
        cameraRotationOffset = Mathf.Lerp(cameraRotationOffset, turnTarget, Mathf.Clamp01(20f * dt));

        // --- Falling time counts from the top of a jump (or from leaving a ledge), like the sample.
        bool jumping = locomotion.IsJumping;
        if (grounded || jumping)
        {
            fallTimer = 0f;
            wasJumpingOrGrounded = true;
        }
        else
        {
            if (wasJumpingOrGrounded) fallTimer = 0f;
            wasJumpingOrGrounded = false;
            fallTimer += dt;
        }

        // --- Lean, head and body look.
        leanDelay = OverrideDelayTimer(leanDelay, dt);
        headLookDelay = OverrideDelayTimer(headLookDelay, dt);
        bodyLookDelay = OverrideDelayTimer(bodyLookDelay, dt);
        bool turningInPlace = locomotion.IsTurningInPlace;
        bool headOn = enableHeadLook && headLookDelay == 0f && !isStarting;
        bool bodyOn = enableBodyLook && bodyLookDelay == 0f && !(isStarting || turningInPlace);
        bool leanOn = enableLean && leanDelay == 0f && !(isStarting || turningInPlace);
        if (!grounded) leanOn = false;               // in the air: no lean (sample)
        if (crouching) { leanOn = false; bodyOn = false; }
        CalculateRotationalAdditives(leanOn, headOn, bodyOn, realSpeed, turningInPlace, dt);

        // --- Send everything to the Animator.
        animator.SetFloat(LeanValueHash, leanValue);
        animator.SetFloat(HeadLookXHash, headLookX);
        animator.SetFloat(HeadLookYHash, headLookY);
        animator.SetFloat(BodyLookXHash, bodyLookX);
        animator.SetFloat(BodyLookYHash, bodyLookY);

        animator.SetFloat(IsStrafingHash, 0f);
        animator.SetFloat(InclineAngleHash, 0f);
        animator.SetFloat(MoveSpeedHash, moveSpeedParam);
        animator.SetInteger(CurrentGaitHash, (int)gait);
        if (hasPlaybackParam) animator.SetFloat(LocomotionPlaybackSpeedHash, playbackParam);

        animator.SetFloat(StrafeDirectionXHash, strafeDirectionX);
        animator.SetFloat(StrafeDirectionZHash, strafeDirectionZ);
        animator.SetFloat(ForwardStrafeHash, 1f);
        animator.SetFloat(CameraRotationOffsetHash, cameraRotationOffset);

        animator.SetBool(MovementInputHeldHash, inputHeld);
        animator.SetBool(MovementInputPressedHash, inputPressed);
        animator.SetBool(MovementInputTappedHash, inputTapped);
        animator.SetFloat(ShuffleDirectionXHash, 0f);
        animator.SetFloat(ShuffleDirectionZHash, 1f);

        animator.SetBool(IsTurningInPlaceHash, turningInPlace);
        animator.SetBool(IsCrouchingHash, crouching);
        animator.SetBool(IsJumpingHash, jumping);
        animator.SetFloat(FallingDurationHash, fallTimer);
        animator.SetBool(IsGroundedHash, grounded);
        animator.SetBool(IsWalkingHash, tired);
        animator.SetBool(IsStoppedHash, isStopped);
        animator.SetBool(IsStartingHash, isStarting);
        animator.SetFloat(LocomotionStartDirectionHash, locomotionStartDirection);

        debugRealSpeed = realSpeed;
        debugMoveSpeedParam = moveSpeedParam;
        debugPlaybackSpeed = playbackParam;
        debugGait = gait.ToString();
        debugTiredWeight = tiredWeight;
    }

    // Synty sample: a start animation plays when input begins from (nearly) standing still.
    void CheckIfStarting(Vector3 moveDirection, float speed2D, float dt)
    {
        locomotionStartTimer = OverrideDelayTimer(locomotionStartTimer, dt);

        bool starting = false;
        if (locomotionStartTimer <= 0f)
        {
            if (moveDirection.magnitude > 0.01f && speed2D < 1f && !locomotion.IsCrouching && locomotion.IsGrounded)
                starting = true;

            if (starting)
            {
                if (!isStarting)
                    locomotionStartDirection = newDirectionDifferenceAngle;

                const float delayTime = 0.2f;
                leanDelay = delayTime;
                headLookDelay = delayTime;
                bodyLookDelay = delayTime;
                locomotionStartTimer = delayTime;
            }
        }
        else
        {
            starting = true;
        }

        isStarting = starting;
    }

    void UpdateStrafeDirection(float targetZ, float targetX, float dt)
    {
        strafeDirectionZ = Mathf.Lerp(strafeDirectionZ, targetZ, AnimationDampTime * dt);
        strafeDirectionX = Mathf.Lerp(strafeDirectionX, targetX, AnimationDampTime * dt);
        strafeDirectionZ = Mathf.Round(strafeDirectionZ * 1000f) / 1000f;
        strafeDirectionX = Mathf.Round(strafeDirectionX * 1000f) / 1000f;
    }

    // Synty sample's leans and looks, plus: head follows the camera, and the tired look.
    void CalculateRotationalAdditives(bool leansOn, bool headOn, bool bodyOn, float speed2D, bool turningInPlace, float dt)
    {
        Vector3 currentForward = transform.forward;
        rotationRate = currentForward != previousForward
            ? Vector3.SignedAngle(currentForward, previousForward, Vector3.up) / dt * -1f
            : 0f;
        previousForward = currentForward;

        const float maxRotationRate = 275f;
        const float smoothness = 5f;

        // Lean into turns (less when tired).
        float leanInput = leansOn ? rotationRate : 0f;
        float leanTarget = Mathf.Clamp(leanInput / maxRotationRate, -1f, 1f) *
                           leanCurve.Evaluate(speed2D / Mathf.Max(0.01f, clipSprintSpeed)) *
                           leanScale * Mathf.Lerp(1f, tiredLeanScale, tiredWeight);
        leanValue = Mathf.Lerp(leanValue, leanTarget, smoothness * dt);

        // Head: into turns, or toward the turn-in-place target, plus where the camera faces.
        float headRate;
        if (headOn && turningInPlace)
            headRate = Mathf.Clamp(cameraRotationOffset / 200f, -1f, 1f);
        else
            headRate = headLookXCurve.Evaluate(Mathf.Clamp((headOn ? rotationRate : 0f) / maxRotationRate, -1f, 1f));

        float cameraTargetX = 0f;
        float cameraPitch = 0f;
        var cam = Player_Camera.instance != null ? Player_Camera.instance.cameraObject : null;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            if (headOn && headFollowsCamera && camForward.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.SignedAngle(currentForward, camForward.normalized, Vector3.up);
                if (Mathf.Abs(angle) < headLookCutoffAngle)
                    cameraTargetX = Mathf.Clamp(angle / Mathf.Max(1f, headLookMaxAngle), -1f, 1f);
            }

            float tilt = cam.transform.eulerAngles.x;
            tilt = (tilt > 180f ? tilt - 360f : tilt) / -180f;  // looking up = positive (Synty sample)
            cameraPitch = Mathf.Clamp(tilt, lookDownLimit, 1f);
        }
        headCameraX = Mathf.Lerp(headCameraX, cameraTargetX, smoothness * dt);
        headLookX = Mathf.Lerp(headLookX, Mathf.Clamp(headRate + headCameraX, -1f, 1f), smoothness * dt);

        // Body: into turns.
        float bodyRate = bodyLookXCurve.Evaluate(Mathf.Clamp((bodyOn ? rotationRate : 0f) / maxRotationRate, -1f, 1f));
        bodyLookX = Mathf.Lerp(bodyLookX, bodyRate, smoothness * dt);

        // Up/down: camera pitch, and head/body down while tired.
        float headY = (headOn ? cameraPitch : 0f) - tiredHeadDown * tiredWeight;
        float bodyY = (bodyOn ? cameraPitch : 0f) - tiredBodyDown * tiredWeight;
        headLookY = Mathf.Lerp(headLookY, Mathf.Clamp(headY, -1f, 1f), smoothness * dt);
        bodyLookY = Mathf.Lerp(bodyLookY, Mathf.Clamp(bodyY, -1f, 1f), smoothness * dt);
    }

    static float OverrideDelayTimer(float timeVariable, float dt)
    {
        if (timeVariable > 0f)
            return Mathf.Clamp(timeVariable - dt, 0f, 1f);
        return 0f;
    }
}
