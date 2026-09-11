using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; // Required for UnityEvent

public class Altimeter : MonoBehaviour
{
    public enum AxisOption
    {
        XAxis,
        YAxis,
        ZAxis,
        NegativeXAxis,
        NegativeYAxis,
        NegativeZAxis,
        Custom
    }

    [Header("UI References")]
    [SerializeField] private Slider altitudeSlider;
    [SerializeField] private TMP_Text firstAltitudeText;
    [SerializeField] private TMP_Text secondAltitudeText;

    [Header("Slider Settings")]
    [SerializeField] private float sliderMin = 900f;
    [SerializeField] private float sliderMax = 1100f;

    [Header("Auto Fill Settings")]
    [SerializeField] private float targetQNH = 5000f; // Threshold value to reach
    [SerializeField] private bool autoFillOnStart = false; // Auto fill slider on start if true

    [Header("QNH Randomization Settings")]
    [SerializeField] private bool randomizeTargetQNHOnStart = true;
    [SerializeField] private int minQNHRange = 1001;
    [SerializeField] private int maxQNHRange = 1025;

    [Header("Rotation Settings")]
    [Tooltip("The GameObject that will rotate when the slider changes (e.g., Altimeter Needle/Dial).")]
    [SerializeField] private Transform targetToRotate;

    [Tooltip("Select which axis the target object should rotate around.")]
    [SerializeField] private AxisOption selectedAxis = AxisOption.ZAxis;

    [Tooltip("Used only if 'Custom' is selected above.")]
    [SerializeField] private Vector3 customRotationAxis = Vector3.forward;

    [Tooltip("Total degrees to rotate when reaching max altitude. Positive values rotate clockwise, negative counter-clockwise.")]
    [SerializeField] private float maxRotationDegrees = -360f;

    [Header("Events")]
    [Tooltip("Triggered automatically when the slider reaches the target altitude and becomes non-interactable.")]
    public UnityEvent onTargetReached;

    private Quaternion initialRotation;

    private void Start()
    {
        // Cache the starting rotation of the target object
        if (targetToRotate != null)
        {
            initialRotation = targetToRotate.localRotation;
        }

        // Randomize target QNH if option enabled and autoFill isn't taking precedence
        if (randomizeTargetQNHOnStart && !autoFillOnStart)
        {
            SetRandomTargetQNH();
        }

        if (altitudeSlider != null)
        {
            // Set slider range from Inspector settings
            altitudeSlider.minValue = sliderMin;
            altitudeSlider.maxValue = sliderMax;

            // Register listener to track slider changes in real-time
            altitudeSlider.onValueChanged.AddListener(UpdateAltitudeUI);

            // Auto fill to target value on start if option enabled
            if (autoFillOnStart)
            {
                altitudeSlider.value = targetQNH;
            }

            // Initialize text fields and rotation with the starting slider value
            UpdateAltitudeUI(altitudeSlider.value);
        }
    }

    /// <summary>
    /// Randomizes targetQNH to a whole number within minQNHRange and maxQNHRange (inclusive).
    /// </summary>
    public void SetRandomTargetQNH()
    {
        // Adding +1 to maxQNHRange because integer Random.Range max limit is exclusive
        targetQNH = Random.Range(minQNHRange, maxQNHRange + 1);
    }

    /// <summary>
    /// Set a custom target QNH range directly from script using whole numbers.
    /// </summary>
    public void SetQNHRange(int minRange, int maxRange)
    {
        minQNHRange = minRange;
        maxQNHRange = maxRange;
    }

    // Called automatically whenever the slider moves
    public void UpdateAltitudeUI(float value)
    {
        // Check if slider reached or exceeded target value while still interactable
        if (value >= targetQNH && altitudeSlider != null && altitudeSlider.interactable)
        {
            // Force the slider value to exactly clamp at targetQNH
            altitudeSlider.value = targetQNH; 
            altitudeSlider.interactable = false; // Disable slider interaction
            value = targetQNH; // Update local value variable for text update

            // Trigger the completion event
            onTargetReached?.Invoke();
        }

        // Update Text Display
        string formattedValue = value.ToString("F0") + " ft";

        if (firstAltitudeText != null)
            firstAltitudeText.text = formattedValue;

        if (secondAltitudeText != null)
            secondAltitudeText.text = formattedValue;

        // Update Object Rotation
        RotateObject(value);
    }

    private void RotateObject(float currentValue)
    {
        if (targetToRotate == null) return;

        // Normalize current value between 0.0 and 1.0 based on sliderMin/sliderMax range
        float t = Mathf.InverseLerp(sliderMin, sliderMax, currentValue);

        // Calculate rotation angle (0 to maxRotationDegrees)
        float currentAngle = t * maxRotationDegrees;

        // Determine axis vector based on enum selection
        Vector3 chosenAxis = GetRotationAxisVector();

        // Apply rotation relative to the object's initial orientation
        targetToRotate.localRotation = initialRotation * Quaternion.AngleAxis(currentAngle, chosenAxis);
    }

    // Returns the Vector3 corresponding to the selected AxisOption
    private Vector3 GetRotationAxisVector()
    {
        switch (selectedAxis)
        {
            case AxisOption.XAxis: return Vector3.right;
            case AxisOption.YAxis: return Vector3.up;
            case AxisOption.ZAxis: return Vector3.forward;
            case AxisOption.NegativeXAxis: return Vector3.left;
            case AxisOption.NegativeYAxis: return Vector3.down;
            case AxisOption.NegativeZAxis: return Vector3.back;
            case AxisOption.Custom: return customRotationAxis;
            default: return Vector3.forward;
        }
    }

    // Public helper method if you want to trigger auto-fill programmatically at runtime
    public void AutoFillToTarget()
    {
        if (altitudeSlider != null)
        {
            altitudeSlider.value = targetQNH;
        }
    }

    // Public method to reset interactable state and generate a new random target if desired
    public void ResetSlider()
    {
        if (altitudeSlider != null)
        {
            altitudeSlider.interactable = true;
            altitudeSlider.value = sliderMin;
        }
    }

    private void OnDestroy()
    {
        // Clean up event listener when destroyed
        if (altitudeSlider != null)
        {
            altitudeSlider.onValueChanged.RemoveListener(UpdateAltitudeUI);
        }
    }
}