// ============================================================================
// PlayerAnimator_Manager.cs
// Talks to the player's Animator: sets the walk/run "Speed" value, and plays one-off
// "action" animations (drink, pump, collapse) with flags saying whether the player may move
// or turn during them.
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: on the Player prefab. The Animator needs a float parameter "Speed"
// (0 = idle, 0.25 = walk, 0.5 = run) and states named as the scripts ask (e.g. "Drink").
// HOW TO EDIT: the Speed values are in UpdateAnimatorMovementParameters.
// ============================================================================

using UnityEngine;

/// <summary>Sets the player's animation values and plays action animations.</summary>
public class PlayerAnimator_Manager : MonoBehaviour
{
    Player_Manager player;

    // The "Speed" parameter as a number (faster than looking it up by name every frame).
    int speed;

    [Header("Animation State")]
    [Tooltip("True while a full-body action plays; the walk/run animation isn't updated meanwhile. Set by code.")]
    public bool isPerformingAction;
    [Tooltip("True if the current action moves the player by its animation (root motion). Set by code.")]
    public bool applyRootMotion;
    [Tooltip("Can the player turn during the current action? Set by code.")]
    public bool canRotate = true;
    [Tooltip("Can the player move during the current action? Set by code.")]
    public bool canMove = true;

    private void Awake()
    {
        player = GetComponent<Player_Manager>();

        speed = Animator.StringToHash("Speed");
    }


    /// <summary>Sets the Animator's Speed: 0 idle, 0.25 walking, 0.5 sprinting (smoothed over 0.1 s).</summary>
    public void UpdateAnimatorMovementParameters(float moveAmount, bool isSprinting)
    {
        if (isPerformingAction)
            return;

        float speedAmount = 0;

        if (moveAmount > 0)
        {
            speedAmount = 0.25f;

            if (isSprinting)
            {
                speedAmount = 0.5f;
            }
        }

        player.animator.SetFloat(speed, speedAmount, 0.1f, Time.deltaTime);
    }

    // Dev note: To manually play an animation that's inside the animator
    /// <summary>
    /// Blends (over 0.2 s) into the Animator state named <paramref name="targetAnimation"/> and
    /// sets the action flags. Call ResetActionAnimation when it ends (usually from an animation event).
    /// </summary>
    public void PlayTargetActionAnimation(
        string targetAnimation,
        bool isPerformingAction,
        bool applyRootMotion = false,
        bool canRotate = false,
        bool canMove = false)
    {
        this.isPerformingAction = isPerformingAction;
        this.applyRootMotion = applyRootMotion;
        this.canRotate = canRotate;
        this.canMove = canMove;

        player.animator.applyRootMotion = applyRootMotion;

        player.animator.CrossFade(
            targetAnimation,
            0.2f,
            0
        );
    }


    /// <summary>Ends an action: the player can move and turn again.</summary>
    public void ResetActionAnimation()
    {
        isPerformingAction = false;
        applyRootMotion = false;
        canRotate = true;
        canMove = true;

        player.animator.applyRootMotion = false;
    }
}
