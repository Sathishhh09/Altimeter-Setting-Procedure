using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class PageStartandEnd : MonoBehaviour
{
    [System.Serializable]
    public class PageEventGroup
    {
        [Tooltip("The page index this event group corresponds to.")]
        public int pageIndex;

        [Tooltip("Fired when user ENTERS this specific page.")]
        public UnityEvent onPageEntered;

        [Tooltip("Fired when user EXITS this specific page.")]
        public UnityEvent onPageExited;
    }

    [Header("Per-Page Specific Events")]
    [SerializeField] private List<PageEventGroup> pageEvents = new List<PageEventGroup>();

    [Header("Global Fallback Events (Optional)")]
    public UnityEvent<int> OnAnyPageEntered;
    public UnityEvent<int> OnAnyPageExited;

    private int lastPageIndex = -1;
    private bool isInitialized = false;

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
        InitializeFirstPage();
    }

    private void InitializeFirstPage()
    {
        if (isInitialized) return;

        lastPageIndex = PageNavigationController.CurrentIndex;
        isInitialized = true;

        TriggerPageEntered(lastPageIndex);
    }

    private void HandlePageChanged(int newPageIndex)
    {
        if (!isInitialized)
        {
            InitializeFirstPage();
            if (lastPageIndex == newPageIndex) return;
        }

        // 1. Fire Exit Event for the page we are leaving
        if (lastPageIndex != -1 && lastPageIndex != newPageIndex)
        {
            TriggerPageExited(lastPageIndex);
        }

        // 2. Fire Enter Event for the page we are entering
        TriggerPageEntered(newPageIndex);

        // 3. Update cached page index
        lastPageIndex = newPageIndex;
    }

    private void TriggerPageExited(int pageIndex)
    {
        OnAnyPageExited?.Invoke(pageIndex);

        // Trigger specific event for this index
        PageEventGroup group = pageEvents.Find(p => p.pageIndex == pageIndex);
        group?.onPageExited?.Invoke();

        Debug.Log($"[PageNavigation] Exited Page: {pageIndex}");
    }

    private void TriggerPageEntered(int pageIndex)
    {
        OnAnyPageEntered?.Invoke(pageIndex);

        // Trigger specific event for this index
        PageEventGroup group = pageEvents.Find(p => p.pageIndex == pageIndex);
        group?.onPageEntered?.Invoke();

        Debug.Log($"[PageNavigation] Entered Page: {pageIndex}");
    }
}