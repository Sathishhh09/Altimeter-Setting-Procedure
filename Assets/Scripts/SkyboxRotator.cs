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
    public bool clampX = false;
    public bool clampY = false;
    public bool clampZ = false;

    public Vector3 minRotation = new Vector3(-45f, 0f, -45f);
    public Vector3 maxRotation = new Vector3(45f, 360f, 45f);

    [Header("Active States")]
    [Tooltip("Toggle which axes are currently continuously rotating.")]
    public bool isRotatingX = false;
    public bool isRotatingY = false;
    public bool isRotatingZ = false;

    [Header("Angle Oscillation States")]
    public bool isOscillatingX = false;
    public bool isOscillatingY = false;
    public bool isOscillatingZ = false;

    private Vector3 targetMaxAngle = Vector3.zero;
    private Vector3 oscillationTime = Vector3.zero;
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

        // --- X-Axis Logic ---
        if (isOscillatingX)
        {
            oscillationTime.x += Time.deltaTime * Mathf.Abs(rotationSpeed.x);
            currentRotation.x = Mathf.PingPong(oscillationTime.x, targetMaxAngle.x);
        }
        else if (isRotatingX)
        {
            currentRotation.x += rotationSpeed.x * Time.deltaTime;
            currentRotation.x = ProcessAxisAngle(currentRotation.x, clampX, minRotation.x, maxRotation.x);
        }

        // --- Y-Axis Logic ---
        if (isOscillatingY)
        {
            oscillationTime.y += Time.deltaTime * Mathf.Abs(rotationSpeed.y);
            currentRotation.y = Mathf.PingPong(oscillationTime.y, targetMaxAngle.y);
        }
        else if (isRotatingY)
        {
            currentRotation.y += rotationSpeed.y * Time.deltaTime;
            currentRotation.y = ProcessAxisAngle(currentRotation.y, clampY, minRotation.y, maxRotation.y);
        }

        // --- Z-Axis Logic ---
        if (isOscillatingZ)
        {
            oscillationTime.z += Time.deltaTime * Mathf.Abs(rotationSpeed.z);
            currentRotation.z = Mathf.PingPong(oscillationTime.z, targetMaxAngle.z);
        }
        else if (isRotatingZ)
        {
            currentRotation.z += rotationSpeed.z * Time.deltaTime;
            currentRotation.z = ProcessAxisAngle(currentRotation.z, clampZ, minRotation.z, maxRotation.z);
        }

        // Apply updated matrices to the skybox material
        ApplyRotationToMaterial();
    }

    // ========================================================================
    // NEW ANGLE OSCILLATION FUNCTIONS (Ping-Pong back & forth)
    // ========================================================================

    #region Oscillation Controls
    public void RotateToAngleX(float maxAngle)
    {
        targetMaxAngle.x = maxAngle;
        oscillationTime.x = 0f;
        isOscillatingX = true;
        isRotatingX = false; // Disable continuous rotation mode
    }

    public void RotateToAngleY(float maxAngle)
    {
        targetMaxAngle.y = maxAngle;
        oscillationTime.y = 0f;
        isOscillatingY = true;
        isRotatingY = false;
    }

    public void RotateToAngleZ(float maxAngle)
    {
        targetMaxAngle.z = maxAngle;
        oscillationTime.z = 0f;
        isOscillatingZ = true;
        isRotatingZ = false;
    }

    public void StopOscillationX() => isOscillatingX = false;
    public void StopOscillationY() => isOscillatingY = false;
    public void StopOscillationZ() => isOscillatingZ = false;
    #endregion

    // ========================================================================
    // EVENT-CALLABLE PUBLIC FUNCTIONS (WITH SPEED PARAMETERS)
    // ========================================================================

    #region X-Axis Controls
    public void StartRotationX(float speed)
    {
        rotationSpeed.x = speed;
        isOscillatingX = false;
        isRotatingX = true;
    }
    public void StartRotationX()
    {
        isOscillatingX = false;
        isRotatingX = true;
    }
    public void StopRotationX() => isRotatingX = false;
    public void ToggleRotationX(float speed)
    {
        rotationSpeed.x = speed;
        isOscillatingX = false;
        isRotatingX = !isRotatingX;
    }
    public void ResetRotationX()
    {
        currentRotation.x = 0f;
        oscillationTime.x = 0f;
        ApplyRotationToMaterial();
    }
    #endregion

    #region Y-Axis Controls
    public void StartRotationY(float speed)
    {
        rotationSpeed.y = speed;
        isOscillatingY = false;
        isRotatingY = true;
    }
    public void StartRotationY()
    {
        isOscillatingY = false;
        isRotatingY = true;
    }
    public void StopRotationY() => isRotatingY = false;
    public void ToggleRotationY(float speed)
    {
        rotationSpeed.y = speed;
        isOscillatingY = false;
        isRotatingY = !isRotatingY;
    }
    public void ResetRotationY()
    {
        currentRotation.y = 0f;
        oscillationTime.y = 0f;
        ApplyRotationToMaterial();
    }
    #endregion

    #region Z-Axis Controls
    public void StartRotationZ(float speed)
    {
        rotationSpeed.z = speed;
        isOscillatingZ = false;
        isRotatingZ = true;
    }
    public void StartRotationZ()
    {
        isOscillatingZ = false;
        isRotatingZ = true;
    }
    public void StopRotationZ() => isRotatingZ = false;
    public void ToggleRotationZ(float speed)
    {
        rotationSpeed.z = speed;
        isOscillatingZ = false;
        isRotatingZ = !isRotatingZ;
    }
    public void ResetRotationZ()
    {
        currentRotation.z = 0f;
        oscillationTime.z = 0f;
        ApplyRotationToMaterial();
    }
    #endregion

    #region Global Controls
    public void StartAllRotations(Vector3 speeds)
    {
        rotationSpeed = speeds;
        isOscillatingX = isOscillatingY = isOscillatingZ = false;
        isRotatingX = isRotatingY = isRotatingZ = true;
    }

    public void StartAllRotations(float uniformSpeed)
    {
        rotationSpeed = new Vector3(uniformSpeed, uniformSpeed, uniformSpeed);
        isOscillatingX = isOscillatingY = isOscillatingZ = false;
        isRotatingX = isRotatingY = isRotatingZ = true;
    }

    public void StartAllRotations()
    {
        isOscillatingX = isOscillatingY = isOscillatingZ = false;
        isRotatingX = isRotatingY = isRotatingZ = true;
    }

    public void StopAllRotations()
    {
        isRotatingX = isRotatingY = isRotatingZ = false;
        isOscillatingX = isOscillatingY = isOscillatingZ = false;
    }

    public void ResetAllRotations()
    {
        currentRotation = Vector3.zero;
        oscillationTime = Vector3.zero;
        ApplyRotationToMaterial();
    }
    #endregion

    // ========================================================================
    // HELPER FUNCTIONS
    // ========================================================================

    private void ApplyRotationToMaterial()
    {
        if (skyboxMaterial == null) return;

        Matrix4x4 rotationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(currentRotation), Vector3.one);
        skyboxMaterial.SetMatrix("_RotationMatrix", rotationMatrix);
        skyboxMaterial.SetFloat("_Rotation", currentRotation.y);
    }

    private float ProcessAxisAngle(float currentAngle, bool isClamped, float minAngle, float maxAngle)
    {
        if (isClamped)
        {
            return Mathf.Clamp(currentAngle, minAngle, maxAngle);
        }
        else
        {
            return currentAngle % 360f;
        }
    }

    public void SetSkybox(Material newSkybox)
    {
        if (newSkybox == null) return;

        skyboxMaterial = newSkybox;
        RenderSettings.skybox = skyboxMaterial;
        ApplyRotationToMaterial();
    }
}