// ============================================================================
// UI_FaceCamera.cs
// Turns a world-space UI (e.g. the bar over a lock basin) to face the camera every frame,
// so it can always be read.
//
// DESIGN RULES: none (a helper).
//
// HOW TO USE: put it on the world-space Canvas (or its parent). Needs a camera tagged MainCamera.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using UnityEngine;

/// <summary>Keeps this object turned the same way as the main camera.</summary>
public class UI_FaceCamera : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    // LateUpdate: after the camera has moved this frame, so the UI doesn't lag a frame behind.
    private void LateUpdate()
    {
        if (mainCamera == null)
            return;

        transform.rotation = mainCamera.transform.rotation;
    }
}
