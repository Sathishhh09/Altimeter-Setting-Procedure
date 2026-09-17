using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    [Header("Skybox Settings")]
    [Tooltip("Assign the Skybox material here. If left empty, it will use the current Scene Skybox.")]
    public Material skyboxMaterial;

    [Header("Rotation Settings")]
    [Tooltip("Rotation speed around X, Y, and Z axes in degrees per second.")]
    public Vector3 rotationSpeed = new Vector3(0f, 10f, 0f);

    [Header("Angle Limits")]
    [Tooltip("Enable clamping on specific axes.")]
    public bool clampX = false;
    public bool clampY = false;
    public bool clampZ = false;

    [Tooltip("Minimum allowed angles in degrees.")]
    public Vector3 minRotation = new Vector3(-45f, 0f, -45f);

    [Tooltip("Maximum allowed angles in degrees.")]
    public Vector3 maxRotation = new Vector3(45f, 360f, 45f);

    private Vector3 currentRotation = Vector3.zero;

    void Start()
    {
        if (skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }

        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }
        else
        {
            Debug.LogWarning("SkyboxRotator: No Skybox Material found!");
        }
    }

    void Update()
    {
        if (skyboxMaterial == null) return;

        // Increment rotation on all axes based on frame time
        currentRotation += rotationSpeed * Time.deltaTime;

        // Apply clamping or modulo per axis
        currentRotation.x = ProcessAxisAngle(currentRotation.x, clampX, minRotation.x, maxRotation.x);
        currentRotation.y = ProcessAxisAngle(currentRotation.y, clampY, minRotation.y, maxRotation.y);
        currentRotation.z = ProcessAxisAngle(currentRotation.z, clampZ, minRotation.z, maxRotation.z);

        // Create a full 3D rotation matrix from Euler angles
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(currentRotation), Vector3.one);

        // Pass the 3D rotation matrix to the material shader
        skyboxMaterial.SetMatrix("_RotationMatrix", rotationMatrix);
        
        // Keep standard Y-axis rotation in sync for basic shaders
        skyboxMaterial.SetFloat("_Rotation", currentRotation.y);
    }

    private float ProcessAxisAngle(float currentAngle, bool isClamped, float minAngle, float maxAngle)
    {
        if (isClamped)
        {
            // Lock value between min and max
            return Mathf.Clamp(currentAngle, minAngle, maxAngle);
        }
        else
        {
            // Wrap continuously between 0 and 360 degrees
            return currentAngle % 360f;
        }
    }

    public void SetSkybox(Material newSkybox)
    {
        if (newSkybox == null) return;

        skyboxMaterial = newSkybox;
        RenderSettings.skybox = skyboxMaterial;
    }
}