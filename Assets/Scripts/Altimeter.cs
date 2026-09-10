using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Altimeter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider altitudeSlider;
    [SerializeField] private TMP_Text firstAltitudeText;
    [SerializeField] private TMP_Text secondAltitudeText;

    [Header("Slider Settings")]
    [SerializeField] private float minAltitude = 0f;
    [SerializeField] private float maxAltitude = 10000f;

    [Header("Auto Fill Settings")]
    [SerializeField] private float targetAltitude = 5000f; // Threshold value to reach
    [SerializeField] private bool autoFillOnStart = false; // Auto fill slider on start if true

    private void Start()
    {
        if (altitudeSlider != null)
        {
            // Set slider range from Inspector settings
            altitudeSlider.minValue = minAltitude;
            altitudeSlider.maxValue = maxAltitude;

            // Register listener to track slider changes in real-time
            altitudeSlider.onValueChanged.AddListener(UpdateAltitudeUI);
            
            // Auto fill to target value on start if option enabled
            if (autoFillOnStart)
            {
                altitudeSlider.value = targetAltitude;
            }

            // Initialize text fields with the starting slider value
            UpdateAltitudeUI(altitudeSlider.value);
        }
    }

    // Called automatically whenever the slider moves
    public void UpdateAltitudeUI(float value)
    {
        // Check if slider reached or exceeded target value
        if (value >= targetAltitude && altitudeSlider != null && altitudeSlider.interactable)
        {
            // Force the slider value to exactly clamp at targetAltitude
            altitudeSlider.value = targetAltitude; 
            altitudeSlider.interactable = false; // Disable slider interaction
            value = targetAltitude; // Update local value variable for text update
        }

        string formattedValue = value.ToString("F0") + " ft"; // Display as whole number

        if (firstAltitudeText != null)
            firstAltitudeText.text = formattedValue;

        if (secondAltitudeText != null)
            secondAltitudeText.text = formattedValue;
    }

    // Public helper method if you want to trigger auto-fill programmatically at runtime
    public void AutoFillToTarget()
    {
        if (altitudeSlider != null)
        {
            altitudeSlider.value = targetAltitude;
        }
    }

    // Public method to reset interactable state if needed
    public void ResetSlider()
    {
        if (altitudeSlider != null)
        {
            altitudeSlider.interactable = true;
            altitudeSlider.value = minAltitude;
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