// ============================================================================
// PlayerAnimator_Manager.cs
// Talks to the player's Animator: hands the movement to PlayerSyntyAnimator (which sets all
// the Synty locomotion parameters), and plays one-off "action" animations (drink, pump, pour,
// collapse, wake up) with flags saying whether the player may move or turn during them.
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: on the Player prefab. The Animator uses AC_Salok_Player
// (Assets/_Project/Animation). Action states are found by name in any layer (e.g. "Drink" in
// UpperBodyActions, "Collapse" in Actions); see Assets/_Project/Animation/README.md.
// HOW TO EDIT: locomotion tuning is on PlayerSyntyAnimator. Action blend time is below (0.2 s).
// ============================================================================

using UnityEngine;

/// <summary>Sets the player's animation values and plays action animations.</summary>
public class PlayerAnimator_Manager : MonoBehaviour
{
    Player_Manager player;
    PlayerSyntyAnimator synty;

    [Header("Animation State")]
    [Tooltip("True while a full-body action plays; the walk/run animation isn't updated meanwhile. Set by code.")]
    public bool isPerformingAction;
    [Tooltip("True if the current action moves the player by its animation (root motion). Set by code.")]
    public bool applyRootMotion;
    [Tooltip("Can the player turn during the current action? Set by code.")]
    public bool canRotate = true;
    [Tooltip("Can the player move during the current action? Set by code.")]
    public bool canMove = true;

    [Header("Bottle")]
    public GameObject bottle;
    public Transform hipSocket;
    public Transform handSocket;

    private void Awake()
    {
        player = GetComponent<Player_Manager>();

        // The Synty locomotion driver. Added here if the scene doesn't have it yet, so an old
        // scene or prefab still animates (with its default Inspector values).
        synty = GetComponent<PlayerSyntyAnimator>();
        if (synty == null)
            synty = gameObject.AddComponent<PlayerSyntyAnimator>();
    }

    private void Start()
    {
        AttachBottleToHip();
    }

    public int GetDrinkingLayer()
    {
        return synty != null ? synty.FindActionLayer("Drink_Start") : -1;
    }

    /// <summary>Hands this frame's movement to PlayerSyntyAnimator (it used to set the old "Speed" value).</summary>
    public void UpdateAnimatorMovementParameters(float moveAmount, bool isSprinting)
    {
        if (synty != null)
            synty.SetMovementInput(moveAmount, isSprinting);
    }

    // Dev note: To manually play an animation that's inside the animator
    /// <summary>
    /// Blends (over 0.2 s) into the Animator state named <paramref name="targetAnimation"/> and
    /// sets the action flags. The state can be in any layer (it is looked up by name). Call
    /// ResetActionAnimation when it ends (usually from an animation event); PlayerSyntyAnimator
    /// also does it once the state has gone back to its layer's Empty state.
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

        int layer = synty != null ? synty.FindActionLayer(targetAnimation) : 0;

        // A missing state only logs a warning; PlayerSyntyAnimator then clears the flags.
        if (layer >= 0)
        {
            player.animator.CrossFade(
                targetAnimation,
                0.2f,
                layer
            );
        }

        if (synty != null)
            synty.OnActionStarted(targetAnimation, layer);
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

    /// <summary>Blends the action layers out so the locomotion shows again (doesn't change the flags).</summary>
    public void ReturnToLocomotion()
    {
        if (synty != null)
            synty.ReturnToLocomotion();
    }


    // Called by an animation event in Drink_Start.
    public void AttachBottleToHand()
    {
        if (bottle == null || handSocket == null)
            return;

        bottle.transform.SetParent(handSocket, false);
        bottle.transform.localPosition = Vector3.zero;
        bottle.transform.localRotation = Quaternion.identity;
    }

    // Called by an animation event in Drink_End.
    public void AttachBottleToHip()
    {
        if (bottle == null || hipSocket == null)
            return;

        bottle.transform.SetParent(hipSocket, false);
        bottle.transform.localPosition = Vector3.zero;
        bottle.transform.localRotation = Quaternion.identity;
    }
}
