using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Collections.Generic;

public class QNHController : MonoBehaviour
{
    [System.Serializable]
    public class PageQNHConfig
    {
        [Tooltip("Page index (0-based) where this target applies.")]
        public int pageIndex;

        [Tooltip("TMP_Text reference to display the generated target QNH for this specific page.")]
        public TMP_Text qnhDisplayText;

        [Tooltip("Generated QNH value for this page (automatically assigned at runtime if randomization is enabled).")]
        public float generatedTargetQNH;
    }

    [Header("Target Page Configuration")]
    [Tooltip("Configure target page indices, UI references, and view generated QNH values in the Inspector at runtime.")]
    [SerializeField] private List<PageQNHConfig> targetPageConfigurations = new List<PageQNHConfig>();

    [Header("QNH Target & Randomization Settings")]
    [SerializeField] private bool randomizeTargetQNHOnPageChange = true;
    [SerializeField] private int minQNHRange = 1001;
    [SerializeField] private int maxQNHRange = 1025;
    [SerializeField] private float defaultTargetQNH = 1013f; // Fallback if current page is NOT configured or randomization is off

    [Header("Events")]
    [Tooltip("Triggered automatically whenever a target QNH is setup or regenerated across pages.")]
    public UnityEvent onTargetReached;

    private float currentTargetQNH;

    private void OnEnable()
    {
        // Listen to page changes from PageNavigationController
        PageNavigationController.OnPageChanged += OnPageChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void Start()
    {
        // Initialize target for the starting page
        SetupPageTargetQNH(PageNavigationController.CurrentIndex);
    }

    /// <summary>
    /// Triggered whenever the PageNavigationController changes pages.
    /// </summary>
    private void OnPageChanged(int pageIndex)
    {
        SetupPageTargetQNH(pageIndex);
    }

    /// <summary>
    /// Checks if the page has a configuration, generates a target QNH if needed, and updates all displays.
    /// </summary>
    private void SetupPageTargetQNH(int pageIndex)
    {
        PageQNHConfig config = targetPageConfigurations.Find(c => c.pageIndex == pageIndex);

        if (config != null && randomizeTargetQNHOnPageChange)
        {
            // If runtime target hasn't been generated for this page yet, create one
            if (config.generatedTargetQNH == 0)
            {
                config.generatedTargetQNH = Random.Range(minQNHRange, maxQNHRange + 1);
            }
            currentTargetQNH = config.generatedTargetQNH;
        }
        else if (config != null && !randomizeTargetQNHOnPageChange && config.generatedTargetQNH != 0)
        {
            currentTargetQNH = config.generatedTargetQNH;
        }
        else
        {
            // Fallback for non-configured pages
            currentTargetQNH = defaultTargetQNH;
        }

        // Update the page-specific text field if configured
        if (config != null && config.qnhDisplayText != null)
        {
            config.qnhDisplayText.text = currentTargetQNH.ToString("F0");
        }

        // Automatically fire event for all pages upon processing
        onTargetReached?.Invoke();
    }

    /// <summary>
    /// Programmatically completes or confirms reaching the target QNH.
    /// </summary>
    public void CompleteTarget()
    {
        onTargetReached?.Invoke();
    }

    /// <summary>
    /// Forces a new random target QNH for a specific page index at runtime.
    /// </summary>
    public void RegenerateQNHForPage(int pageIndex)
    {
        PageQNHConfig config = targetPageConfigurations.Find(c => c.pageIndex == pageIndex);
        if (config != null)
        {
            config.generatedTargetQNH = Random.Range(minQNHRange, maxQNHRange + 1);

            // Update the text field for this page config
            if (config.qnhDisplayText != null)
            {
                config.qnhDisplayText.text = config.generatedTargetQNH.ToString("F0");
            }

            if (PageNavigationController.CurrentIndex == pageIndex)
            {
                SetupPageTargetQNH(pageIndex);
            }
            else
            {
                onTargetReached?.Invoke();
            }
        }
    }

    /// <summary>
    /// Public getter for current target QNH.
    /// </summary>
    public float GetCurrentTargetQNH() => currentTargetQNH;

    /// <summary>
    /// Set a custom target QNH range directly from script using whole numbers.
    /// </summary>
    public void SetQNHRange(int minRange, int maxRange)
    {
        minQNHRange = minRange;
        maxQNHRange = maxRange;
    }
}