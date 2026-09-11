using UnityEngine;
using System.Collections.Generic;

public class TransitionLayerManager : MonoBehaviour
{
    [System.Serializable]
    public class PageTransitionConfig
    {
        [Tooltip("The page index where these transition settings apply.")]
        public int pageIndex = 1;

        [Header("Transition Boundaries")]
        [Tooltip("The starting/minimum transition layer limit for this page.")]
        public float startTransitionLayerLimit = 0f;

        [Tooltip("The ending/maximum transition layer limit for this page.")]
        public float endTransitionLayerLimit = 100f;

        [Tooltip("The caution threshold limit for this page.")]
        public float cautionLimit = 80f;

        [Header("Unlock Rules")]
        [Tooltip("If true, automatically unlocks navigation on PageNavigationController once this page's threshold is met.")]
        public bool autoUnlockNavigation = true;
    }

    [Header("Page Navigation Sync")]
    [Tooltip("Configure transition layer rules and limits for each specific page index.")]
    [SerializeField] private List<PageTransitionConfig> pageConfigs = new();

    [Header("Flight Data Source")]
    [Tooltip("Reference to the script supplying the altitude value.")]
    [SerializeField] private A320PFD altimeterController;

    [Header("Runtime State")]
    [Tooltip("Speed at which the current transition level increases per second.")]
    [SerializeField] private float transitionUpdateSpeed = 10f;

    [Tooltip("The current progress of the transition (read-only in runtime).")]
    [SerializeField] private float currentTransitionLevel = 0f;

    // Internal State
    private readonly HashSet<int> cautionTriggeredPages = new();
    private readonly HashSet<int> completedPages = new();
    private PageTransitionConfig activeConfig;

    // Public Properties
    public float CurrentTransitionLevel => currentTransitionLevel;
    public float TransitionUpdateSpeed => transitionUpdateSpeed;
    public PageTransitionConfig ActiveConfig => activeConfig;

    private void Start()
    {
        if (altimeterController == null)
        {
            altimeterController = FindFirstObjectByType<A320PFD>();
        }

        // Initialize active config for starting page
        UpdateActiveConfig(PageNavigationController.CurrentIndex);
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Update()
    {
        // Update transition level from PFD altimeter
        if (altimeterController != null)
        {
            currentTransitionLevel = altimeterController.altitude;
        }

        // Evaluate rules if current page has an assigned configuration
        if (activeConfig != null)
        {
            CheckCautionLimit(activeConfig);
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
    }

    /// <summary>
    /// Checks if the altitude exceeds the caution limit for the active page configuration.
    /// </summary>
    private void CheckCautionLimit(PageTransitionConfig config)
    {
        bool isTriggered = cautionTriggeredPages.Contains(config.pageIndex);

        if (currentTransitionLevel > config.cautionLimit && !isTriggered)
        {
            Debug.LogWarning($"[TransitionLayerManager] Caution on Page {config.pageIndex}! Current level ({currentTransitionLevel:F2}) exceeded caution limit ({config.cautionLimit}).");
            cautionTriggeredPages.Add(config.pageIndex);
        }
        else if (currentTransitionLevel <= config.cautionLimit && isTriggered)
        {
            cautionTriggeredPages.Remove(config.pageIndex);
        }
    }

    /// <summary>
    /// Checks if the current altitude passes the page's upper boundary/caution threshold to trigger unlock.
    /// </summary>
    private void EvaluatePageCompletion(PageTransitionConfig config)
    {
        if (completedPages.Contains(config.pageIndex)) return;

        // Completion condition: level reaches or exceeds the page's target boundary
        if (currentTransitionLevel >= config.cautionLimit)
        {
            completedPages.Add(config.pageIndex);
            Debug.Log($"[TransitionLayerManager] Page {config.pageIndex} transition condition met at level {currentTransitionLevel}. Unlocking navigation.");

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
}