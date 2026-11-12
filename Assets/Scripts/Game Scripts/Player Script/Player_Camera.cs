// ============================================================================
// Player_Camera.cs
// The third-person camera: follows the player, turns with the mouse (left/right on this
// object, up/down on the pivot child), and slides in towards the player when a wall, the
// floor or a prop is between them, so the view never goes through anything.
//
// The camera orbits a point at about head height ("Pivot Height") and sits "Camera
// Distance" metres behind it, a little higher ("Camera Height"), slightly over the right
// shoulder ("Shoulder Offset"). When something blocks that spot, it moves in at once (so it
// never clips) and eases back out when the way is clear.
//
// DESIGN RULES: R8.26 (zone 2 turning carries the player and camera: TurnWithPlayer);
// Section 8 layout notes (close camera, about 2.5 to 3 m, with collision; slides in at corners).
//
// HOW TO USE: on the "Player Camera (Left & Right)" prefab. Drag in the Camera and the pivot
// child. "Collide With Layer" picks what blocks the camera; left at Nothing it uses every
// layer (the player's own colliders and trigger boxes are always ignored).
// Player_Manager calls HandleAllCamereActions every LateUpdate and fills in "Player".
// HOW TO EDIT: everything is under "Camera Position" and "Camera Collision" in the Inspector.
// Mouse sensitivity is the 0.15 in HandleRotation, times the player's setting
// (GameSettings.MouseSensitivity). Follow smoothing is cameraSmoothSpeed below.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>Third-person follow camera with mouse look and wall / floor / prop collision.</summary>
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
    [Tooltip("Lowest the camera can look, in degrees (negative = camera goes low and looks up).")]
    [SerializeField] float minimumPivot = -30;  // LOWEST POINT YOU ARE ABLE TO LOOK DOWN
    [Tooltip("Highest the camera can look, in degrees (camera goes high and looks down).")]
    [SerializeField] float maximumPivot = 60;   // HIGHEST POINT YOU ARE ABLE TO LOOK UP

    [Header("Camera Position")]
    [Tooltip("Height above the player's feet that the camera orbits around (m). About head height for a 1.8 m character.")]
    [SerializeField] float pivotHeight = 1.6f;
    [Tooltip("How far behind the pivot the camera sits when nothing is in the way (m). GDD: about 2.5 to 3.")]
    [SerializeField] float cameraDistance = 3f;
    [Tooltip("How far above the pivot line the camera sits (m). Raises the view a little so you see over the player.")]
    [SerializeField] float cameraHeight = 0.3f;
    [Tooltip("Sideways offset (m). 0 = straight behind; 0.4 = over the right shoulder; negative = left.")]
    [SerializeField] float shoulderOffset = 0f;
    [Tooltip("Up/down angle the camera starts at (degrees). A few degrees looks slightly down at the player.")]
    [SerializeField] float startPitch = 10f;

    [Header("Camera Collision")]
    [Tooltip("Radius in metres of the sphere used to check for walls, floor and props.")]
    [SerializeField] float cameraCollisionRadius = 0.25f;
    [Tooltip("Layers that push the camera in (walls, floor, props). Nothing = every layer.")]
    [SerializeField] LayerMask collideWithLayer;
    [Tooltip("The camera never comes closer to the pivot than this (m).")]
    [SerializeField] float minimumDistance = 0.4f;
    [Tooltip("How fast the camera eases back out once the way is clear, in metres per second. Moving in is instant.")]
    [SerializeField] float returnSpeed = 6f;

    private Vector3 cameraVelocity;
    [Header("Camera Values")]
    [Tooltip("Current left/right angle in degrees (shown for debugging; changed by the mouse).")]
    [SerializeField] float leftAndRightLookAngle;
    [Tooltip("Current up/down angle in degrees (shown for debugging; changed by the mouse).")]
    [SerializeField] float upAndDownLookAngle;

    private float currentDistance;                     // how far along the line the camera is right now (0..1 of full)
    private readonly RaycastHit[] hits = new RaycastHit[16];

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
        // Place the pivot and the camera from the settings above (the prefab's own positions
        // are replaced, so everything is tuned in one place).
        cameraPivotTransform.localPosition = new Vector3(0f, pivotHeight, 0f);
        cameraObject.transform.localPosition = DesiredLocal();
        cameraObject.transform.localRotation = Quaternion.identity;
        currentDistance = 1f;

        upAndDownLookAngle = Mathf.Clamp(startPitch, minimumPivot, maximumPivot);
        cameraPivotTransform.localRotation = Quaternion.Euler(upAndDownLookAngle, 0f, 0f);
    }

    /// <summary>Follow, mouse look (only when the player has control) and collision. Called by Player_Manager.LateUpdate.</summary>
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
        // ROTATE LEFT AND RIGHT BASED ON HORIZONTAL MOVEMENT OF THE MOUSE (0.15 = SENSITIVITY)
        // × the player's Mouse sensitivity from the Settings screen (1 = normal).
        float sensitivity = 0.15f * TheLastDrop.GameSettings.MouseSensitivity;
        leftAndRightLookAngle +=
          PlayerInput_Manager.instance.mouseHorizontalInput * sensitivity;

        upAndDownLookAngle -=
            PlayerInput_Manager.instance.mouseVerticalInput * sensitivity;
        // CLAMP THE UP AND DOWN LOOK ANGLE BETWEEN A MIN AND MAX VALUE
        upAndDownLookAngle = Mathf.Clamp(upAndDownLookAngle, minimumPivot, maximumPivot);

        // ROTATE THIS GAMEOBJECT LEFT AND RIGHT
        transform.rotation = Quaternion.Euler(0f, leftAndRightLookAngle, 0f);

        // ROTATE THIS PIVOT GAMEOBJECT UP AND DOWN
        cameraPivotTransform.localRotation = Quaternion.Euler(upAndDownLookAngle, 0f, 0f);
    }

    // Where the camera sits (in the pivot's space) when nothing is in the way.
    private Vector3 DesiredLocal() => new Vector3(shoulderOffset, cameraHeight, -cameraDistance);

    private void HandleCollision()
    {
        Vector3 desiredLocal = DesiredLocal();
        float fullLength = desiredLocal.magnitude;
        if (fullLength < 0.001f) return;

        Vector3 origin = cameraPivotTransform.position;
        Vector3 desiredWorld = cameraPivotTransform.TransformPoint(desiredLocal);
        Vector3 direction = (desiredWorld - origin) / fullLength;

        // Find the nearest thing between the pivot and the camera's spot: walls, floor, props.
        // The player's own colliders and trigger boxes (hub area, sections, rooms) are skipped.
        int mask = collideWithLayer.value != 0 ? collideWithLayer.value : Physics.DefaultRaycastLayers;
        int count = Physics.SphereCastNonAlloc(origin, cameraCollisionRadius, direction, hits,
                                               fullLength, mask, QueryTriggerInteraction.Ignore);
        float allowed = fullLength;
        for (int i = 0; i < count; i++)
        {
            var col = hits[i].collider;
            if (col == null || IsPlayerCollider(col)) continue;
            // distance 0 means the sphere started inside it (e.g. right against a wall): stay close.
            float d = hits[i].distance > 0f ? hits[i].distance : 0f;
            if (d < allowed) allowed = d;
        }

        float minFraction = Mathf.Clamp01(minimumDistance / fullLength);
        float targetFraction = Mathf.Clamp(allowed / fullLength, minFraction, 1f);

        // Move in straight away (never clip through a wall); ease back out smoothly.
        if (targetFraction < currentDistance)
            currentDistance = targetFraction;
        else
            currentDistance = Mathf.MoveTowards(currentDistance, targetFraction,
                                                returnSpeed * Time.deltaTime / fullLength);

        cameraObject.transform.localPosition = desiredLocal * currentDistance;
    }

    // True for any collider on the player (its CharacterController, the hitbox, the hat...).
    private bool IsPlayerCollider(Collider col)
    {
        return player != null && col.transform.IsChildOf(player.transform);
    }
}
