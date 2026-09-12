using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// EXPERIMENTAL - gyro-based alternative to TouchCameraPan.
///
/// Put this on the SAME camera object as TouchCameraPan, but only have
/// ONE of the two components enabled at a time - they both drive
/// transform.localRotation every frame and will fight each other if both
/// are active simultaneously.
///
/// Like TouchCameraPan, this works in LOCAL space (relative to the
/// animated cockpit parent), not world space - for the same reason:
/// the cockpit is animated via keyframes, so any camera rotation logic
/// that isn't relative to the parent's current orientation will appear
/// to "tilt" incorrectly whenever the cockpit moves.
///
/// GYRO-SPECIFIC ISSUE THIS HANDLES: the device's raw gyro attitude is
/// in device/world space and has no idea where "forward" is supposed to
/// mean in your scene. Without recentering, whatever direction the
/// phone happened to be pointing when this script starts becomes an
/// arbitrary, probably-wrong "forward". This script captures the
/// device's starting attitude at enable-time and treats ONLY rotation
/// relative to that starting attitude as pan input - so however the
/// user happened to be holding the phone when the page loaded becomes
/// the neutral/centered look direction, matching the cockpit's forward.
/// </summary>
public class GyroCameraPan : MonoBehaviour
{
    [Header("Head Movement Limits")]
    [SerializeField] private float leftLimit = 80f;
    [SerializeField] private float rightLimit = 80f;
    [SerializeField] private float upLimit = 50f;
    [SerializeField] private float downLimit = 60f;

    [Header("Gyro")]
    [Tooltip("Multiplies gyro-derived rotation. 1 = 1:1 with physical device rotation.")]
    [SerializeField] private float gyroSensitivity = 1f;

    private Quaternion referenceAttitude = Quaternion.identity;
    private bool hasReference;

    private float startYaw;
    private float startPitch;

    private void OnEnable()
    {
        hasReference = false;

        var attitudeSensor = UnityEngine.InputSystem.AttitudeSensor.current;

        if (attitudeSensor == null)
        {
            Debug.LogWarning(
                "GyroCameraPan: No AttitudeSensor device found on this platform/device. " +
                "This script will do nothing. Falling back to TouchCameraPan is recommended.");
            return;
        }

        InputSystem.EnableDevice(attitudeSensor);

        // Capture the camera's starting LOCAL orientation, same baseline
        // concept as TouchCameraPan, so pan limits are relative to the
        // cockpit's forward.
        Vector3 angles = transform.localEulerAngles;

        startYaw = angles.y;

        startPitch = angles.x;
        if (startPitch > 180f)
            startPitch -= 360f;
    }

    private void Update()
    {
        var attitudeSensor = UnityEngine.InputSystem.AttitudeSensor.current;
        if (attitudeSensor == null) return;

        Quaternion currentAttitude = attitudeSensor.attitude.ReadValue();

        if (!hasReference)
        {
            referenceAttitude = currentAttitude;
            hasReference = true;
            return;
        }

        Quaternion relative = Quaternion.Inverse(referenceAttitude) * currentAttitude;
        Vector3 relativeEuler = relative.eulerAngles;

        float deltaYaw = NormalizeAngle(relativeEuler.y) * gyroSensitivity;

        float yaw = Mathf.Clamp(
            startYaw + deltaYaw,
            startYaw - leftLimit,
            startYaw + rightLimit);

        // Pitch is intentionally fixed — ignore device tilt entirely
        transform.localRotation = Quaternion.Euler(startPitch, yaw, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    /// <summary>
    /// Re-centers gyro "forward" to wherever the device is currently
    /// pointing. Useful if you want a manual recenter button, or need to
    /// call this after re-enabling the component following a flare-style
    /// camera takeover (mirrors TouchCameraPan.SyncStateToCurrentRotation).
    /// </summary>
    public void Recenter()
    {
        hasReference = false;

        Vector3 angles = transform.localEulerAngles;

        startYaw = angles.y;

        startPitch = angles.x;
        if (startPitch > 180f)
            startPitch -= 360f;
    }
}