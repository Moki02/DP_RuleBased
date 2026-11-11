using UnityEngine;
using UnityEngine.InputSystem;


public class Player_Camera : MonoBehaviour
{
    public static Player_Camera instance;
    public Player_Manager player;
    public Camera cameraObject;
    [SerializeField] Transform cameraPivotTransform;

    // CHANGE THESE TO TWEAK CAMERA PERFORMANCE
    [Header("Camera Settings")]
    private float cameraSmoothSpeed = 1;        // THE BIGGER THE NUMBER, THE LONGER FOR THE CAMERA TO REACH ITS POSITION DURING MOVEMENT
    [SerializeField] float minimumPivot = -30;  // LOWEST POINT YOU ARE ABLE TO LOOK DOWN
    [SerializeField] float maximumPivot = 60;   // HIGHEST POINT YOU ARE ABLE TO LOOK UP
    [SerializeField] float cameraCollisionRadius = 0.2f;
    [SerializeField] LayerMask collideWithLayer;

    [Header("Camera Values")]
    private Vector3 cameraVelocity;
    private Vector3 cameraObjectPosition; // USED FOR CAMERA COLLISIONS (MOVES THE CAMERA OBJECT TO THIS POSITION UPON COLLIDING)
    [SerializeField] float leftAndRightLookAngle;
    [SerializeField] float upAndDownLookAngle;
    private float cameraZPosition; // VALUES USE FOR CAMERA COLLISIONS
    private float targetcameraZPosition; // VALUES USE FOR CAMERA COLLISIONS

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



    private void Start()
    {
        cameraZPosition = cameraObject.transform.localPosition.z;
    }

    public void HandleAllCamereActions()
    {
        if (player == null)
            return;

        HandleFollowTarget();

        if (Game_Manager.instance.CanControlPlayer())
        {
            HandleRotation();
        }

        HandleCollision();
    }

    /// <summary>
    /// Turns the camera left/right by <paramref name="degrees"/> and snaps it onto the player.
    /// Used when zone 2 turns and carries the player with it (Zone2Rotator, GDD R8.26), so the
    /// player's view of the zone stays the same.
    /// </summary>
    public void TurnWithPlayer(float degrees)
    {
        leftAndRightLookAngle += degrees;
        transform.rotation = Quaternion.Euler(0f, leftAndRightLookAngle, 0f);
        if (player != null)
        {
            transform.position = player.transform.position;
            cameraVelocity = Vector3.zero;
        }
    }

    private void HandleFollowTarget()
    {
        Vector3 targetCameraPosition = Vector3.SmoothDamp(transform.position, player.transform.position, ref cameraVelocity, cameraSmoothSpeed * Time.deltaTime);
        transform.position = targetCameraPosition;
    }

    private void HandleRotation()
    {
        // IF LOCKED ON, FORCE ROTATION TOWARDS TARGET

        // ELSE ROTATE REGULARLY

        // ROTATE LEFT AND RIGHT BASED ON HORIZONTAL MOVEMENT ON THE RIGHT JOYSTICK
        leftAndRightLookAngle +=
          PlayerInput_Manager.instance.mouseHorizontalInput * 0.15f;

        upAndDownLookAngle -=
            PlayerInput_Manager.instance.mouseVerticalInput * 0.15f;
        // CLAMP THE UP AND DOWN LOOK ANGLE BETWEEN A MIN AND MAX VALUE
        upAndDownLookAngle = Mathf.Clamp(upAndDownLookAngle, minimumPivot, maximumPivot);

        Vector3 cameraRotation = Vector3.zero;
        Quaternion targetRotation;

        // ROTATE THIS GAMEOBJECT LEFT AND RIGHT
        cameraRotation.y = leftAndRightLookAngle;
        targetRotation = Quaternion.Euler(cameraRotation);
        transform.rotation = targetRotation;

        // ROTATE THIS PIVOT GAMEOBJECT UP AND DOWN
        cameraRotation = Vector3.zero;
        cameraRotation.x = upAndDownLookAngle;
        targetRotation = Quaternion.Euler(cameraRotation);
        cameraPivotTransform.localRotation = targetRotation;
    }

    private void HandleCollision()
    {
        targetcameraZPosition = cameraZPosition;

        RaycastHit hit;
        // DIRECTION FOR COLLISION CHECK
        Vector3 direction = cameraObject.transform.position - cameraPivotTransform.position;
        direction.Normalize();

        // WE CHECK IF THERES IS AN OBJECT IN FRONT OF OUR DESIRED DIRECTION * (SEE ABOVE)
        if (Physics.SphereCast(cameraPivotTransform.position, cameraCollisionRadius, direction, out hit, Mathf.Abs(targetcameraZPosition), collideWithLayer))
        {
            // IF THERE IS, WE GET OUR DISTANCE FROM IT
            float distanceFromHitObject = Vector3.Distance(cameraPivotTransform.position, hit.point);
            // WE THEN EQUATE OUR TARGET Z POSITION TO THE FOLLOWING
            targetcameraZPosition = -(distanceFromHitObject - cameraCollisionRadius);

        }

        // IF OUR TARGET POSITION IS LESS THAN OUR COLLISION RADIUS, WE SUBTRACT OUR COLLISION RADIUS (MAKING IT SNAP BACK)
        if (Mathf.Abs(targetcameraZPosition) < cameraCollisionRadius)
        {
            targetcameraZPosition = -cameraCollisionRadius;
        }

        // WE THEN APPLY OUR FINAL POSITION USING A LERP OVER A TIME OF 0.2F
        cameraObjectPosition.z = Mathf.Lerp(cameraObject.transform.localPosition.z, targetcameraZPosition, 0.2f);
        cameraObject.transform.localPosition = cameraObjectPosition;
    }
}
