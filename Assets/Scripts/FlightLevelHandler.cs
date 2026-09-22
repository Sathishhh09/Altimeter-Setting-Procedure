using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // Required for UnityEvent
using TMPro;

public class FlightLevelHandler : MonoBehaviour
{
    public enum CountDirection
    {
        Increase,
        Decrease
    }

    [System.Serializable]
    public class TextCounterSetting
    {
        [Header("UI Reference")]
        public TextMeshProUGUI levelText;

        [Header("Value Bounds")]
        public float startValue = 1000f;
        public float targetValue = 1240f;

        [Header("Formatting")]
        public string prefix = "";
        public string suffix = " FT";

        [Header("Settings")]
        public float speed = 10f;
        public CountDirection direction = CountDirection.Increase;

        [Header("Events")]
        [Tooltip("Triggered when this specific counter reaches its target value.")]
        public UnityEvent onTargetReached;

        [HideInInspector] public float currentValue;
        [HideInInspector] public bool isCounting = false;
    }

    [Header("Counters Configuration")]
    [SerializeField] private List<TextCounterSetting> counters = new List<TextCounterSetting>();

    [Header("Global Events")]
    [Tooltip("Triggered when ALL active counters have finished reaching their targets.")]
    public UnityEvent onAllTargetsReached;

    private void Start()
    {
        // Set initial values on start based on settings
        StartCounting();
    }

    private void Update()
    {
        bool anyCounterActive = false;

        foreach (var counter in counters)
        {
            if (!counter.isCounting) continue;

            anyCounterActive = true;

            if (counter.direction == CountDirection.Increase)
            {
                counter.currentValue += counter.speed * Time.deltaTime;
                
                if (counter.currentValue >= counter.targetValue)
                {
                    CompleteCounter(counter);
                }
                else
                {
                    UpdateCounterText(counter);
                }
            }
            else // Decrease
            {
                counter.currentValue -= counter.speed * Time.deltaTime;

                if (counter.currentValue <= counter.targetValue)
                {
                    CompleteCounter(counter);
                }
                else
                {
                    UpdateCounterText(counter);
                }
            }
        }

        // Check if all counters just finished during this frame
        if (anyCounterActive && CheckIfAllCompleted())
        {
            onAllTargetsReached?.Invoke();
        }
    }

    /// <summary>
    /// Helper method to finalize counter value, update UI, and invoke its individual event.
    /// </summary>
    private void CompleteCounter(TextCounterSetting counter)
    {
        counter.currentValue = counter.targetValue;
        counter.isCounting = false;
        UpdateCounterText(counter);
        
        // Trigger individual event
        counter.onTargetReached?.Invoke();
    }

    private bool CheckIfAllCompleted()
    {
        foreach (var counter in counters)
        {
            if (counter.isCounting) return false;
        }
        return true;
    }

    /// <summary>
    /// Starts counting on all configured text elements.
    /// </summary>
    public void StartCounting()
    {
        foreach (var counter in counters)
        {
            counter.currentValue = counter.startValue;
            UpdateCounterText(counter);
            counter.isCounting = true;
        }
    }

    /// <summary>
    /// Starts counting for a single specific counter by list index.
    /// </summary>
    public void StartCountingIndex(int index)
    {
        if (index >= 0 && index < counters.Count)
        {
            var counter = counters[index];
            counter.currentValue = counter.startValue;
            UpdateCounterText(counter);
            counter.isCounting = true;
        }
    }

    private void UpdateCounterText(TextCounterSetting counter)
    {
        if (counter.levelText != null)
        {
            counter.levelText.text = $"{counter.prefix}{Mathf.RoundToInt(counter.currentValue)}{counter.suffix}";
        }
    }
}