using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using TMPro;

public class TransitionLayerManager : MonoBehaviour
{
    public static event Action<int> OnStartObjectsActivated;
    public static event Action<int> OnEndObjectsActivated;

    [System.Serializable]
    public class PageTransitionConfig
    {
        [Tooltip("The page index where these transition settings apply.")]
        public int pageIndex = 1;

        [Header("Transition Boundaries (Altitude in Feet)")]
        [Tooltip("Starting transition limit.")]
        [Range(3500f, 7000f)]
        public float startTransitionLayerLimit = 3500f;

        [Tooltip("Caution threshold limit.")]
        [Range(3500f, 7000f)]
        public float cautionLimit = 4000f;

        [Tooltip("Ending transition limit.")]
        [Range(3500f, 7000f)]
        public float endTransitionLayerLimit = 4500f;

        [Header("UI Reference")]
        [Tooltip("Text component used to display the start transition limit value.")]
        public TMP_Text startTransitionText;

        [Header("Object Activation")]
        [Tooltip("If checked (true), starting and ending GameObjects will NOT be enabled automatically.")]
        public bool bypassObjectActivation = false;

        [Tooltip("GameObjects to enable when altitude reaches start transition limit.")]
        public GameObject[] startTargetGameObjects;

        [Tooltip("GameObjects to enable when altitude reaches end transition limit.")]
        public GameObject[] endTargetGameObjects;

        [Header("Inspector UI Triggers")]
        public UnityEvent onStartLimitReached;
        public UnityEvent onEndLimitReached;

        [Header("Unlock Rules")]
        [Tooltip("If true, automatically unlocks navigation on PageNavigationController once this page's caution threshold is met.")]
        public bool autoUnlockNavigation = true;

        public bool IsAscending => endTransitionLayerLimit >= startTransitionLayerLimit;

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

        public void UpdateUI()
        {
            if (startTransitionText != null)
            {
                startTransitionText.text = $"{startTransitionLayerLimit:F0} FT";
            }
        }
    }

    [Header("Global UI Reference")]
    [SerializeField] private TMP_Text currentTransitionLevelText;

    [Header("Page Navigation Sync")]
    [SerializeField] private List<PageTransitionConfig> pageConfigs = new();

    [Header("Flight Data Source")]
    [SerializeField] private A320PFD altimeterController;

    [Header("Runtime State")]
    [SerializeField] private float currentTransitionLevel = 0f;

    private readonly HashSet<int> cautionTriggeredPages = new();
    private readonly HashSet<int> completedPages = new();
    private readonly HashSet<int> startActivatedPages = new();
    private readonly HashSet<int> endActivatedPages = new();
    private PageTransitionConfig activeConfig;
    private int currentLoadedPageIndex = -1;

    public float CurrentTransitionLevel => currentTransitionLevel;
    public PageTransitionConfig ActiveConfig => activeConfig;

    private void OnValidate()
    {
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

        UpdateActiveConfig(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        if (altimeterController != null)
        {
            currentTransitionLevel = altimeterController.altitude;
        }

        UpdateCurrentTransitionLevelUI();

        if (activeConfig != null)
        {
            activeConfig.UpdateUI();
            CheckStartTransitionLimit(activeConfig);
            CheckCautionLimit(activeConfig);
            CheckEndTransitionLimit(activeConfig);
            EvaluatePageCompletion(activeConfig);
        }
    }

    private void UpdateCurrentTransitionLevelUI()
    {
        if (currentTransitionLevelText != null)
        {
            currentTransitionLevelText.text = $"{currentTransitionLevel:F0} FT";
        }
    }

    private void HandlePageChanged(int newPageIndex)
    {
        if (currentLoadedPageIndex != -1 && currentLoadedPageIndex != newPageIndex)
        {
            DeactivatePageObjects(currentLoadedPageIndex);
        }

        UpdateActiveConfig(newPageIndex);
    }

    private void UpdateActiveConfig(int pageIndex)
    {
        currentLoadedPageIndex = pageIndex;
        activeConfig = pageConfigs.Find(config => config.pageIndex == pageIndex);

        if (activeConfig != null)
        {
            // Reset cached activation states for re-visited pages
            startActivatedPages.Remove(pageIndex);
            endActivatedPages.Remove(pageIndex);
            cautionTriggeredPages.Remove(pageIndex);
            completedPages.Remove(pageIndex);

            activeConfig.UpdateUI();
        }
    }

    private void DeactivatePageObjects(int pageIndex)
    {
        PageTransitionConfig config = GetConfigForPage(pageIndex);
        if (config == null) return;

        if (config.startTargetGameObjects != null)
        {
            foreach (GameObject obj in config.startTargetGameObjects)
            {
                if (obj != null && obj.activeSelf)
                {
                    obj.SetActive(false);
                }
            }
        }

        if (config.endTargetGameObjects != null)
        {
            foreach (GameObject obj in config.endTargetGameObjects)
            {
                if (obj != null && obj.activeSelf)
                {
                    obj.SetActive(false);
                }
            }
        }
    }

    private void CheckStartTransitionLimit(PageTransitionConfig config)
    {
        if (startActivatedPages.Contains(config.pageIndex)) return;

        // Force activation if starting altitude is already past boundary on page load
        bool limitReached = config.IsAscending 
            ? currentTransitionLevel >= config.startTransitionLayerLimit 
            : currentTransitionLevel <= config.startTransitionLayerLimit;

        if (limitReached)
        {
            TriggerStartActivation(config);
        }
    }

    public void TriggerStartActivation(PageTransitionConfig config)
    {
        if (!config.bypassObjectActivation && config.startTargetGameObjects != null)
        {
            foreach (GameObject obj in config.startTargetGameObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        startActivatedPages.Add(config.pageIndex);
        config.onStartLimitReached?.Invoke();
        OnStartObjectsActivated?.Invoke(config.pageIndex);
    }

    private void CheckCautionLimit(PageTransitionConfig config)
    {
        bool isTriggered = cautionTriggeredPages.Contains(config.pageIndex);

        bool hasPassedCaution = config.IsAscending
            ? currentTransitionLevel > config.cautionLimit
            : currentTransitionLevel < config.cautionLimit;

        if (hasPassedCaution && !isTriggered)
        {
            cautionTriggeredPages.Add(config.pageIndex);
        }
        else if (!hasPassedCaution && isTriggered)
        {
            cautionTriggeredPages.Remove(config.pageIndex);
        }
    }

    private void CheckEndTransitionLimit(PageTransitionConfig config)
    {
        if (endActivatedPages.Contains(config.pageIndex)) return;

        bool limitReached = config.IsAscending
            ? currentTransitionLevel >= config.endTransitionLayerLimit
            : currentTransitionLevel <= config.endTransitionLayerLimit;

        if (limitReached)
        {
            TriggerEndActivation(config);
        }
    }

    public void TriggerEndActivation(PageTransitionConfig config)
    {
        if (!config.bypassObjectActivation && config.endTargetGameObjects != null)
        {
            foreach (GameObject obj in config.endTargetGameObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        endActivatedPages.Add(config.pageIndex);
        config.onEndLimitReached?.Invoke();
        OnEndObjectsActivated?.Invoke(config.pageIndex);
    }

    private void EvaluatePageCompletion(PageTransitionConfig config)
    {
        if (completedPages.Contains(config.pageIndex)) return;

        bool isMet = config.IsAscending
            ? currentTransitionLevel >= config.cautionLimit
            : currentTransitionLevel <= config.cautionLimit;

        if (isMet)
        {
            completedPages.Add(config.pageIndex);

            if (config.autoUnlockNavigation)
            {
                PageNavigationController.RequestNavigationUnlock();
            }
        }
    }

    public PageTransitionConfig GetConfigForPage(int pageIndex)
    {
        return pageConfigs.Find(config => config.pageIndex == pageIndex);
    }

    public float GetNormalizedProgress()
    {
        if (activeConfig == null) return 0f;
        float range = activeConfig.endTransitionLayerLimit - activeConfig.startTransitionLayerLimit;
        if (Mathf.Approximately(range, 0f)) return 0f;

        return Mathf.Clamp01((currentTransitionLevel - activeConfig.startTransitionLayerLimit) / range);
    }

    public void EnableBypassForPage(int pageIndex)
    {
        PageTransitionConfig config = GetConfigForPage(pageIndex);
        if (config != null)
        {
            config.bypassObjectActivation = true;
        }
    }
}