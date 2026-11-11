// ============================================================================
// Player_Camera.cs
// The third-person camera: follows the player, turns with the mouse (left/right on this
// object, up/down on the pivot child), and slides in towards the player when a wall is
// between them so the view never goes through walls.
//
// DESIGN RULES: R8.26 (zone 2 turning carries the player and camera: TurnWithPlayer).
//
// HOW TO USE: on the "Player Camera (Left & Right)" prefab. Drag in the Camera, the pivot
// child, and pick which layers block the camera (walls). Player_Manager calls
// HandleAllCamereActions every LateUpdate and fills in "Player" by itself.
// HOW TO EDIT: look limits and collision size in the Inspector. Mouse sensitivity is the
// 0.15 in HandleRotation. Follow smoothing is cameraSmoothSpeed below.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>Third-person follow camera with mouse look and wall collision.</summary>
public class Player_Camera : MonoBehaviour
{
    /// <summary>The camera in the scene (there is only one).</summary>
    public static Player_Camera instance;
    [Tooltip("Filled in by Player_Manager at Start.")]
    public Player_Manager player;
    [Tooltip("The Camera object (a child of the pivot).")]
    public Camera cameraObject;
    [Tooltip("Child that tilts up and down; the camera hangs off it.")]
    [SerializeField] Transform cameraPivotTransform;

    // CHANGE THESE TO TWEAK CAMERA PERFORMANCE
    private float cameraSmoothSpeed = 1;        // THE BIGGER THE NUMBER, THE LONGER FOR THE CAMERA TO REACH ITS POSITION DURING MOVEMENT
    [Header("Camera Settings")]
    [Tooltip("Lowest the camera can look, in degrees (negative = down).")]
    [SerializeField] float minimumPivot = -30;  // LOWEST POINT YOU ARE ABLE TO LOOK DOWN
    [Tooltip("Highest the camera can look, in degrees.")]
    [SerializeField] float maximumPivot = 60;   // HIGHEST POINT YOU ARE ABLE TO LOOK UP
    [Tooltip("Radius in metres of the sphere used to check for walls behind the player.")]
    [SerializeField] float cameraCollisionRadius = 0.2f;
    [Tooltip("Layers that push the camera in (walls, props).")]
    [SerializeField] LayerMask collideWithLayer;

    private Vector3 cameraVelocity;
    private Vector3 cameraObjectPosition; // USED FOR CAMERA COLLISIONS (MOVES THE CAMERA OBJECT TO THIS POSITION UPON COLLIDING)
    [Header("Camera Values")]
    [Tooltip("Current left/right angle in degrees (shown for debugging; changed by the mouse).")]
    [SerializeField] float leftAndRightLookAngle;
    [Tooltip("Current up/down angle in degrees (shown for debugging; changed by the mouse).")]
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
        // The camera's starting distance behind the pivot is the "normal" distance.
        cameraZPosition = cameraObject.transform.localPosition.z;
    }

    /// <summary>Follow, mouse look (only when the player has control) and wall collision. Called by Player_Manager.LateUpdate.</summary>
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
    /// player's view of the zone stays the same. Also used with 0 after a revive, to snap.
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

        // ROTATE LEFT AND RIGHT BASED ON HORIZONTAL MOVEMENT OF THE MOUSE (0.15 = SENSITIVITY)
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
