using UnityEngine;

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
    }

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

        if (hydration != null &&
            hydration.currentHydrationState ==
            PlayerHydration_Manager.HydrationState.Dehydrated)
        {
            movementSpeed *= 0.75f;
        }

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
