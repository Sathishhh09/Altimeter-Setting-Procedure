using UnityEngine;
using System.Collections.Generic;
using TMPro; // Added for TextMeshPro UI support

public class TransitionLayerManager : MonoBehaviour
{
    [System.Serializable]
    public class PageTransitionConfig
    {
        [Tooltip("The page index where these transition settings apply.")]
        public int pageIndex = 1;

        [Header("Transition Boundaries (Altitude in Feet)")]
        [Tooltip("Starting transition limit (e.g. 3500, 4000, 4500...).")]
        [Range(3500f, 7000f)]
        public float startTransitionLayerLimit = 3500f;

        [Tooltip("Caution threshold limit (e.g. 4000, 4500, 5000...).")]
        [Range(3500f, 7000f)]
        public float cautionLimit = 4000f;

        [Tooltip("Ending transition limit (e.g. 4500, 5000, 5500...).")]
        [Range(3500f, 7000f)]
        public float endTransitionLayerLimit = 4500f;

        [Header("UI Reference")]
        [Tooltip("Text component used to display the start transition limit value.")]
        public TMP_Text startTransitionText;

        [Header("Object Activation")]
        [Tooltip("The GameObject to enable when the altitude reaches or exceeds endTransitionLayerLimit.")]
        public GameObject targetGameObject;

        [Header("Unlock Rules")]
        [Tooltip("If true, automatically unlocks navigation on PageNavigationController once this page's caution threshold is met.")]
        public bool autoUnlockNavigation = true;

        /// <summary>
        /// Validates that altitude levels follow strict 500 ft step increments.
        /// </summary>
        public void ValidateSteps()
        {
            startTransitionLayerLimit = SnapToStep(startTransitionLayerLimit, 500f, 3500f, 7000f);
            cautionLimit = SnapToStep(cautionLimit, 500f, 3500f, 7000f);
            endTransitionLayerLimit = SnapToStep(endTransitionLayerLimit, 500f, 3500f, 7000f);
        }

        private float SnapToStep(float value, float step, float min, float max)
        {
            float snapped = Mathf.Round(value / step) * step;
            return Mathf.Clamp(snapped, min, max);
        }

        /// <summary>
        /// Updates the assigned TMP_Text component with the current start transition limit value.
        /// </summary>
        public void UpdateUI()
        {
            if (startTransitionText != null)
            {
                startTransitionText.text = $"{startTransitionLayerLimit:F0} FT";
            }
        }
    }

    [Header("Page Navigation Sync")]
    [Tooltip("Configure transition layer rules and limits for each specific page index.")]
    [SerializeField] private List<PageTransitionConfig> pageConfigs = new();

    [Header("Flight Data Source")]
    [Tooltip("Reference to the script supplying the altitude value.")]
    [SerializeField] private A320PFD altimeterController;

    [Header("Runtime State")]
    [Tooltip("The current progress of the transition (read-only in runtime).")]
    [SerializeField] private float currentTransitionLevel = 0f;

    // Internal State
    private readonly HashSet<int> cautionTriggeredPages = new();
    private readonly HashSet<int> completedPages = new();
    private readonly HashSet<int> activatedObjectPages = new();
    private PageTransitionConfig activeConfig;

    // Public Properties
    public float CurrentTransitionLevel => currentTransitionLevel;
    public PageTransitionConfig ActiveConfig => activeConfig;

    private void OnValidate()
    {
        // Enforce 500ft increments within [3500, 7000] inside Unity Inspector
        if (pageConfigs != null)
        {
            foreach (var config in pageConfigs)
            {
                config.ValidateSteps();
            }
        }
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        if (altimeterController == null)
        {
            altimeterController = FindFirstObjectByType<A320PFD>();
        }

        // Initialize active config for current starting page
        UpdateActiveConfig(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        // Update transition level directly from PFD altimeter
        if (altimeterController != null)
        {
            currentTransitionLevel = altimeterController.altitude;
        }

        // Evaluate rules and update UI if current page has an assigned configuration
        if (activeConfig != null)
        {
            activeConfig.UpdateUI();
            CheckCautionLimit(activeConfig);
            CheckEndTransitionLimit(activeConfig);
            EvaluatePageCompletion(activeConfig);
        }
    }

    private void HandlePageChanged(int newPageIndex)
    {
        UpdateActiveConfig(newPageIndex);
    }

    /// <summary>
    /// Finds and sets the configuration active for the current page index.
    /// </summary>
    private void UpdateActiveConfig(int pageIndex)
    {
        activeConfig = pageConfigs.Find(config => config.pageIndex == pageIndex);

        if (activeConfig != null)
        {
            activeConfig.UpdateUI();
        }
    }

    /// <summary>
    /// Checks if the altitude exceeds the caution limit for the active page configuration and prints a caution message.
    /// </summary>
    private void CheckCautionLimit(PageTransitionConfig config)
    {
        bool isTriggered = cautionTriggeredPages.Contains(config.pageIndex);

        if (currentTransitionLevel > config.cautionLimit && !isTriggered)
        {
            Debug.LogWarning($"⚠️ CAUTION: Altitude has crossed the caution limit! Page: {config.pageIndex} | Current Altitude: {currentTransitionLevel:F0} ft | Caution Limit: {config.cautionLimit:F0} ft");
            cautionTriggeredPages.Add(config.pageIndex);
        }
        else if (currentTransitionLevel <= config.cautionLimit && isTriggered)
        {
            Debug.Log($"[TransitionLayerManager] Altitude returned below caution limit on Page {config.pageIndex} ({currentTransitionLevel:F0} ft). Resetting caution state.");
            cautionTriggeredPages.Remove(config.pageIndex);
        }
    }

    /// <summary>
    /// Checks if the altitude has crossed the end transition limit and enables the target GameObject.
    /// </summary>
    private void CheckEndTransitionLimit(PageTransitionConfig config)
    {
        if (activatedObjectPages.Contains(config.pageIndex)) return;

        if (currentTransitionLevel >= config.endTransitionLayerLimit)
        {
            if (config.targetGameObject != null)
            {
                config.targetGameObject.SetActive(true);
                Debug.Log($"[TransitionLayerManager] Altitude ({currentTransitionLevel:F0} ft) crossed end transition limit ({config.endTransitionLayerLimit:F0} ft) on Page {config.pageIndex}. Activated target GameObject: {config.targetGameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[TransitionLayerManager] End transition limit reached on Page {config.pageIndex}, but no Target GameObject is assigned in the Inspector!");
            }

            activatedObjectPages.Add(config.pageIndex);
        }
    }

    /// <summary>
    /// Checks if the current altitude passes the page's caution threshold to trigger unlock.
    /// </summary>
    private void EvaluatePageCompletion(PageTransitionConfig config)
    {
        if (completedPages.Contains(config.pageIndex)) return;

        if (currentTransitionLevel >= config.cautionLimit)
        {
            completedPages.Add(config.pageIndex);
            Debug.Log($"[TransitionLayerManager] Page {config.pageIndex} transition condition met at {currentTransitionLevel:F0} ft. Unlocking navigation.");

            if (config.autoUnlockNavigation)
            {
                PageNavigationController.RequestNavigationUnlock();
            }
        }
    }

    /// <summary>
    /// Helper method to fetch configured settings for any specific page index.
    /// </summary>
    public PageTransitionConfig GetConfigForPage(int pageIndex)
    {
        return pageConfigs.Find(config => config.pageIndex == pageIndex);
    }

    /// <summary>
    /// Returns 0.0 to 1.0 progress representing current altitude within active transition bounds.
    /// </summary>
    public float GetNormalizedProgress()
    {
        if (activeConfig == null) return 0f;
        float range = activeConfig.endTransitionLayerLimit - activeConfig.startTransitionLayerLimit;
        if (range <= 0f) return 0f;

        return Mathf.Clamp01((currentTransitionLevel - activeConfig.startTransitionLayerLimit) / range);
    }
}