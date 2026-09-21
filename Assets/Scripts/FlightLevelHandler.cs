using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        [HideInInspector] public float currentValue;
        [HideInInspector] public bool isCounting = false;
    }

    [Header("Counters Configuration")]
    [SerializeField] private List<TextCounterSetting> counters = new List<TextCounterSetting>();

    private void Start()
    {
        // Set initial values on start based on settings
        foreach (var counter in counters)
        {
            counter.currentValue = counter.startValue;
            UpdateCounterText(counter);
            counter.isCounting = true;
        }
    }

    private void Update()
    {
        foreach (var counter in counters)
        {
            if (!counter.isCounting) continue;

            if (counter.direction == CountDirection.Increase)
            {
                if (counter.currentValue < counter.targetValue)
                {
                    counter.currentValue += counter.speed * Time.deltaTime;
                    counter.currentValue = Mathf.Min(counter.currentValue, counter.targetValue);
                    UpdateCounterText(counter);

                    if (counter.currentValue >= counter.targetValue)
                    {
                        counter.isCounting = false;
                    }
                }
                else
                {
                    counter.isCounting = false;
                }
            }
            else // Decrease
            {
                if (counter.currentValue > counter.targetValue)
                {
                    counter.currentValue -= counter.speed * Time.deltaTime;
                    counter.currentValue = Mathf.Max(counter.currentValue, counter.targetValue);
                    UpdateCounterText(counter);

                    if (counter.currentValue <= counter.targetValue)
                    {
                        counter.isCounting = false;
                    }
                }
                else
                {
                    counter.isCounting = false;
                }
            }
        }
    }

    /// <summary>
    /// Starts counting on all configured text elements.
    /// Call this from a UI Button OnClick event or UnityEvent.
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