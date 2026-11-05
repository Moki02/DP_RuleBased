using UnityEngine;

public class PlayerAnimator_Manager : MonoBehaviour
{
    Player_Manager player;

    int speed;

    [Header("Animation State")]
    public bool isPerformingAction;
    public bool applyRootMotion;
    public bool canRotate = true;
    public bool canMove = true;

    private void Awake()
    {
        player = GetComponent<Player_Manager>();

        speed = Animator.StringToHash("Speed");
    }


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


    public void ResetActionAnimation()
    {
        isPerformingAction = false;
        applyRootMotion = false;
        canRotate = true;
        canMove = true;

        player.animator.applyRootMotion = false;
    }
}