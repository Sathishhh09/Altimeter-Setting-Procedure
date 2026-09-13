using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Collections.Generic;

public class QNHController : MonoBehaviour
{
    // ================================================================
    // PAGE QNH CONFIGURATION
    // ================================================================

    [System.Serializable]
    public class PageQNHConfig
    {
        [Header("Page")]
        public int pageIndex;

        [Header("Display")]
        public TMP_Text qnhDisplayText;

        [Header("Generated Target")]
        public float generatedTargetQNH;
    }


    // ================================================================
    // QNH SETTINGS
    // ================================================================

    [Header("Target Page Configuration")]
    [SerializeField]
    private List<PageQNHConfig> targetPageConfigurations =
        new List<PageQNHConfig>();

    [Header("Randomization Settings")]
    [SerializeField]
    private bool randomizeTargetQNHOnPageChange = true;

    [SerializeField]
    private int minQNHRange = 1001;

    [SerializeField]
    private int maxQNHRange = 1025;

    [SerializeField]
    private float defaultTargetQNH = 1013f;


    // ================================================================
    // EVENTS
    // ================================================================

    [Header("Events")]
    public UnityEvent onTargetReached;


    // ================================================================
    // CURRENT TARGET
    // ================================================================

    private float currentTargetQNH;


    // ================================================================
    // UNITY LIFECYCLE
    // ================================================================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
    }


    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }


    private void Start()
    {
        SetupPageTargetQNH(
            PageNavigationController.CurrentIndex
        );
    }


    // ================================================================
    // PAGE CHANGE
    // ================================================================

    private void OnPageChanged(int pageIndex)
    {
        SetupPageTargetQNH(pageIndex);
    }


    // ================================================================
    // SETUP CURRENT PAGE TARGET
    // ================================================================

    private void SetupPageTargetQNH(int pageIndex)
    {
        currentTargetQNH = GetTargetQNHForPage(pageIndex);

        PageQNHConfig config =
            targetPageConfigurations.Find(
                c => c.pageIndex == pageIndex
            );


        // ------------------------------------------------------------
        // UPDATE DISPLAY
        // ------------------------------------------------------------

        if (config != null && config.qnhDisplayText != null)
        {
            config.qnhDisplayText.text =
                currentTargetQNH.ToString("F0");
        }


        // ------------------------------------------------------------
        // DEBUG
        // ------------------------------------------------------------

        Debug.Log(
            $"[QNHController] " +
            $"Current Page = {pageIndex}, " +
            $"Target QNH = {currentTargetQNH}"
        );


        // ------------------------------------------------------------
        // EVENT
        // ------------------------------------------------------------

        onTargetReached?.Invoke();
    }


    // ================================================================
    // GET TARGET QNH FOR ANY PAGE
    // ================================================================

public float GetTargetQNHForPage(int pageIndex)
{
    PageQNHConfig config =
        targetPageConfigurations.Find(
            c => c.pageIndex == pageIndex
        );

    if (config == null)
    {
        Debug.LogError(
            $"[QNHController] No QNH configuration found for page index {pageIndex}"
        );

        return defaultTargetQNH;
    }

    if (config.generatedTargetQNH == 0f)
    {
        config.generatedTargetQNH =
            Random.Range(
                minQNHRange,
                maxQNHRange + 1
            );

        Debug.Log(
            $"[QNHController] Generated QNH " +
            $"{config.generatedTargetQNH} " +
            $"for page index {pageIndex}"
        );
    }

    if (config.qnhDisplayText != null)
    {
        config.qnhDisplayText.text =
            config.generatedTargetQNH.ToString("F0");
    }

    return config.generatedTargetQNH;
}


    // ================================================================
    // GET CURRENT PAGE TARGET
    // ================================================================

    public float GetCurrentTargetQNH()
    {
        return currentTargetQNH;
    }


    // ================================================================
    // COMPLETE TARGET
    // ================================================================

    public void CompleteTarget()
    {
        Debug.Log(
            $"[QNHController] " +
            $"Target QNH completed: {currentTargetQNH}"
        );

        onTargetReached?.Invoke();
    }


    // ================================================================
    // REGENERATE QNH FOR A SPECIFIC PAGE
    // ================================================================

    public void RegenerateQNHForPage(int pageIndex)
    {
        PageQNHConfig config =
            targetPageConfigurations.Find(
                c => c.pageIndex == pageIndex
            );


        // ------------------------------------------------------------
        // NO CONFIGURATION
        // ------------------------------------------------------------

        if (config == null)
        {
            Debug.LogWarning(
                $"[QNHController] " +
                $"Cannot regenerate QNH. " +
                $"No configuration found for Page {pageIndex}."
            );

            return;
        }


        // ------------------------------------------------------------
        // GENERATE NEW VALUE
        // ------------------------------------------------------------

        config.generatedTargetQNH =
            Random.Range(
                minQNHRange,
                maxQNHRange + 1
            );


        // ------------------------------------------------------------
        // UPDATE DISPLAY
        // ------------------------------------------------------------

        if (config.qnhDisplayText != null)
        {
            config.qnhDisplayText.text =
                config.generatedTargetQNH.ToString("F0");
        }


        // ------------------------------------------------------------
        // IF THIS IS THE CURRENT PAGE
        // ------------------------------------------------------------

        if (
            PageNavigationController.CurrentIndex ==
            pageIndex
        )
        {
            currentTargetQNH =
                config.generatedTargetQNH;
        }


        // ------------------------------------------------------------
        // DEBUG
        // ------------------------------------------------------------

        Debug.Log(
            $"[QNHController] " +
            $"Regenerated Page {pageIndex} QNH = " +
            $"{config.generatedTargetQNH}"
        );
    }


    // ================================================================
    // SET QNH RANGE
    // ================================================================

    public void SetQNHRange(
        int minRange,
        int maxRange
    )
    {
        if (minRange > maxRange)
        {
            Debug.LogWarning(
                "[QNHController] " +
                "Invalid QNH range. " +
                "Minimum cannot be greater than maximum."
            );

            return;
        }


        minQNHRange = minRange;
        maxQNHRange = maxRange;


        Debug.Log(
            $"[QNHController] " +
            $"QNH Range set to {minQNHRange} - {maxQNHRange}"
        );
    }
}