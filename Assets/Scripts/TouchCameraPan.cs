using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IMPORTANT: This script now drives LOCAL rotation (transform.localRotation),
/// not world rotation. This is required because the camera is a child of
/// the cockpit, and the cockpit itself is animated (position + rotation)
/// via keyframes. Setting world-space rotation directly (the old approach)
/// fights with the parent's animated rotation and causes the camera to
/// appear to "tilt" unexpectedly whenever the cockpit moves - because the
/// script's yaw/pitch baseline was captured once at Start() and never
/// accounted for the parent changing orientation afterward.
///
/// By working in local space, the pan is always relative to "forward as
/// the cockpit currently defines it" - so it stays correct no matter how
/// the parent is animating.
/// </summary>
public class TouchCameraPan : MonoBehaviour
{
    [Header("Touch Settings")]
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Head Movement Limits")]
    [SerializeField] private float leftLimit = 80f;
    [SerializeField] private float rightLimit = 80f;
    [SerializeField] private float upLimit = 50f;
    [SerializeField] private float downLimit = 60f;

    private float yaw;
    private float pitch;

    private float startYaw;
    private float startPitch;

    private Vector2 lastPosition;

    private void Start()
    {
        // Capture the camera's initial LOCAL orientation (relative to its
        // parent), not world orientation.
        Vector3 angles = transform.localEulerAngles;

        yaw = angles.y;

        pitch = angles.x;
        if (pitch > 180f)
            pitch -= 360f;

        startYaw = yaw;
        startPitch = pitch;
    }

    private void Update()
    {
        if (Touchscreen.current == null)
            return;

        var touch = Touchscreen.current.primaryTouch;

        if (!touch.press.isPressed)
            return;

        Vector2 currentPosition = touch.position.ReadValue();

        if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
        {
            lastPosition = currentPosition;
            return;
        }

        Vector2 delta = currentPosition - lastPosition;

        // Drag direction feels natural for cockpit viewing
        yaw -= delta.x * sensitivity;
        pitch += delta.y * sensitivity;

        // Clamp relative to initial forward view
        yaw = Mathf.Clamp(
            yaw,
            startYaw - leftLimit,
            startYaw + rightLimit);

        pitch = Mathf.Clamp(
            pitch,
            startPitch - downLimit,
            startPitch + upLimit);

        // LOCAL rotation - this is the key fix. The camera's pan offset
        // is now always relative to the parent cockpit's CURRENT
        // orientation, whatever that is at this moment, instead of a
        // fixed world-space direction that goes stale the instant the
        // parent animates.
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        lastPosition = currentPosition;
    }

    /// <summary>
    /// Re-syncs this script's internal yaw/pitch state to match the
    /// transform's CURRENT local rotation. Call this any time something
    /// external (e.g. FlareController) has directly set
    /// transform.localRotation while this script was disabled, BEFORE
    /// re-enabling this script - otherwise the next touch-drag will
    /// recalculate from stale yaw/pitch and the camera will snap back.
    ///
    /// Does NOT reset startYaw/startPitch, so pan limits stay relative to
    /// the camera's original local orientation at Start().
    /// </summary>
    public void SyncStateToCurrentRotation()
    {
        Vector3 angles = transform.localEulerAngles;

        yaw = angles.y;

        pitch = angles.x;
        if (pitch > 180f)
            pitch -= 360f;
    }
}