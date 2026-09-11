using UnityEngine;
using System.Collections.Generic;

public class PersistentAssetController : MonoBehaviour
{
    [System.Serializable]
    public class PageTransformData
    {
        public int pageIndex;

        [Header("Local Transform")]
        public Vector3 localPosition;
        public Vector3 localEulerRotation;
        public Vector3 localScale = Vector3.one;

        [Header("Behavior")]
        public bool ignoreFirstEnable;

        // Runtime flag
        [System.NonSerialized]
        public bool hasIgnoredOnce;
    }

    [SerializeField]
    private List<PageTransformData> pageTransforms =
        new List<PageTransformData>();

    // Sorted by page index
    private SortedDictionary<int, PageTransformData> lookup;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        InitializeLookup();
    }


    // ============================================================
    // INITIALIZE LOOKUP
    // ============================================================

    private void InitializeLookup()
    {
        lookup =
            new SortedDictionary<int, PageTransformData>();

        foreach (var data in pageTransforms)
        {
            if (data == null)
                continue;

            if (!lookup.ContainsKey(data.pageIndex))
            {
                data.hasIgnoredOnce = false;

                lookup.Add(
                    data.pageIndex,
                    data
                );
            }
        }
    }


    // ============================================================
    // ON ENABLE
    // ============================================================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;

        // IMPORTANT:
        // This GameObject may be enabled AFTER the page has
        // already changed.
        //
        // Therefore, immediately check the current page.
        //
        // Example:
        // GameObject is enabled on Page 38
        // CurrentIndex = 38
        //
        // The Page 38 transform will be applied immediately.

        if (PageNavigationController.CurrentIndex >= 0)
        {
            ApplyForPage(
                PageNavigationController.CurrentIndex
            );
        }
    }


    // ============================================================
    // ON DISABLE
    // ============================================================

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }


    // ============================================================
    // PAGE CHANGED
    // ============================================================

    private void OnPageChanged(int pageIndex)
    {
        ApplyForPage(pageIndex);
    }


    // ============================================================
    // APPLY FOR PAGE
    // ============================================================

    public void ApplyForPage(int pageIndex)
    {
        if (lookup == null || lookup.Count == 0)
        {
            InitializeLookup();
        }

        if (lookup == null || lookup.Count == 0)
            return;


        PageTransformData chosen = null;


        // ========================================================
        // FIND CLOSEST DEFINED PAGE <= CURRENT PAGE
        // ========================================================
        //
        // Example:
        //
        // Defined:
        // Page 10
        // Page 20
        // Page 38
        //
        // Current Page 35
        // -> Page 20 is used
        //
        // Current Page 38
        // -> Page 38 is used
        //
        // ========================================================

        foreach (var pair in lookup)
        {
            if (pair.Key <= pageIndex)
            {
                chosen = pair.Value;
            }
            else
            {
                break;
            }
        }


        if (chosen == null)
            return;


        // ========================================================
        // FIRST ENABLE IGNORE
        // ========================================================

        if (chosen.ignoreFirstEnable &&
            !chosen.hasIgnoredOnce)
        {
            chosen.hasIgnoredOnce = true;

            return;
        }


        // ========================================================
        // APPLY TRANSFORM
        // ========================================================

        transform.localPosition =
            chosen.localPosition;

        transform.localEulerAngles =
            chosen.localEulerRotation;

        transform.localScale =
            chosen.localScale;
    }


    // ============================================================
    // UPDATE PAGE TRANSFORM
    // ============================================================

    /// <summary>
    /// Overwrites the transform data for a specific page.
    /// Called after snapping.
    /// </summary>
    public void UpdatePageTransform(
        int pageIndex,
        Vector3 newLocalPos,
        Vector3 newLocalRot,
        Vector3 newLocalScale)
    {
        if (lookup == null)
        {
            InitializeLookup();
        }


        // ========================================================
        // EXISTING PAGE
        // ========================================================

        if (lookup.TryGetValue(
                pageIndex,
                out PageTransformData existingData))
        {
            existingData.localPosition =
                newLocalPos;

            existingData.localEulerRotation =
                newLocalRot;

            existingData.localScale =
                newLocalScale;

            return;
        }


        // ========================================================
        // NEW PAGE
        // ========================================================

        PageTransformData newData =
            new PageTransformData
            {
                pageIndex = pageIndex,

                localPosition = newLocalPos,

                localEulerRotation = newLocalRot,

                localScale = newLocalScale,

                ignoreFirstEnable = false,

                hasIgnoredOnce = true
            };


        lookup.Add(
            pageIndex,
            newData
        );
    }
}