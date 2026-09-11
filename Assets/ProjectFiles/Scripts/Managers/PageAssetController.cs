using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class PageAssetController : MonoBehaviour
{
    [System.Serializable]
    public class PageAssetItem
    {
        public GameObject asset;

        [Tooltip("If true, this asset will be enabled only the first time this page is reached")]
        public bool enableOnce = false;

        [HideInInspector] public bool hasBeenActivated = false;
    }

    [System.Serializable]
    public class PageAssets
    {
        [Tooltip("Explicit 0-based page index (e.g., 0 = Page 1, 24 = Page 25)")]
        public int pageIndex = 0;

        [Tooltip("Assets configuration for this page")]
        public List<PageAssetItem> assets = new List<PageAssetItem>();

        [Header("Page Event")]
        [Tooltip("UnityEvent triggered whenever this page becomes active.")]
        public UnityEvent onPageEntered;
    }

    [Header("All Page Assets (Assign every asset once here)")]
    [SerializeField] private List<GameObject> allAssets = new List<GameObject>();

    [Header("Per Page Asset Configuration")]
    [SerializeField] private List<PageAssets> pageAssets = new List<PageAssets>();

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void HandlePageChanged(int targetPageIndex)
    {
        DisableAllAssets();

        PageAssets currentPage = GetPageAssetsByExactIndex(targetPageIndex);

        if (currentPage == null)
            return;

        // Activate page assets
        if (currentPage.assets != null)
        {
            foreach (var item in currentPage.assets)
            {
                if (item == null || item.asset == null)
                    continue;

                if (item.enableOnce)
                {
                    if (item.hasBeenActivated)
                        continue;

                    item.asset.SetActive(true);
                    item.hasBeenActivated = true;
                }
                else
                {
                    item.asset.SetActive(true);
                }
            }
        }

        // Trigger page-specific UnityEvent
        currentPage.onPageEntered?.Invoke();
    }

    private PageAssets GetPageAssetsByExactIndex(int targetIndex)
    {
        // 1. First try matching by the explicit pageIndex field
        foreach (var page in pageAssets)
        {
            if (page != null && page.pageIndex == targetIndex)
            {
                return page;
            }
        }

        // 2. Fallback to list element position if within bounds
        if (targetIndex >= 0 && targetIndex < pageAssets.Count)
        {
            return pageAssets[targetIndex];
        }

        return null;
    }

    private void DisableAllAssets()
    {
        foreach (GameObject obj in allAssets)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
}