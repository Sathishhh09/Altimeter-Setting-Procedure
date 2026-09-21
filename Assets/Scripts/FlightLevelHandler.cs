using UnityEngine;
using TMPro;

public class FlightLevelHandler : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Values")]
    [SerializeField] private float startValue = 0f;
    [SerializeField] private float endValue = 100f;
    [SerializeField] private float speed = 10f;

    private float currentValue;
    private bool isCounting = false;

    void Start()
    {
        // Set initial text display on game start
        currentValue = startValue;
        UpdateText();
    }

    void Update()
    {
        if (!isCounting) return;

        if (currentValue < endValue)
        {
            currentValue += speed * Time.deltaTime;
            currentValue = Mathf.Min(currentValue, endValue);
            UpdateText();
        }
        else
        {
            // Stop updates once target is reached
            isCounting = false;
        }
    }

    /// <summary>
    /// Call this method from a UI Button OnClick event, UnityEvent, or another script.
    /// </summary>
    public void StartCounting()
    {
        currentValue = startValue;
        UpdateText();
        isCounting = true;
    }

    /// <summary>
    /// Optional: Call this if you want to trigger counting with new values dynamically from another script.
    /// </summary>
    public void StartCountingCustom(float newStart, float newEnd, float newSpeed)
    {
        startValue = newStart;
        endValue = newEnd;
        speed = newSpeed;
        
        StartCounting();
    }

    private void UpdateText()
    {
        if (levelText != null)
        {
            levelText.text = Mathf.RoundToInt(currentValue).ToString();
        }
    }
}