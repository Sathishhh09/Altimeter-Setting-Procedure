using System.Collections.Generic;
using UnityEngine;

public class PageMeshHighlightManager : MonoBehaviour
{
    // ==========================================================
    // MESH HIGHLIGHT ENTRY
    // ==========================================================

    [System.Serializable]
    public class MeshHighlightEntry
    {
        [Tooltip("The parent GameObject. All child MeshRenderer and SkinnedMeshRenderer components will be highlighted.")]
        [SerializeField] private GameObject targetObject;

        [Tooltip("Automatically highlight when this page opens.")]
        [SerializeField] private bool autoHighlightOnPageEnter = false;

        public GameObject TargetObject => targetObject;
        public bool AutoHighlightOnPageEnter => autoHighlightOnPageEnter;
    }


    // ==========================================================
    // PAGE HIGHLIGHT CONFIG
    // ==========================================================

    [System.Serializable]
    public class PageHighlightConfig
    {
        [Header("Page Index")]
        public int pageIndex = 0;

        [Header("Target Objects")]
        public List<MeshHighlightEntry> meshEntries =
            new List<MeshHighlightEntry>();
    }


    // ==========================================================
    // INSPECTOR VARIABLES
    // ==========================================================

    [Header("Highlight Material")]
    [SerializeField] private Material highlightMaterial;

    [Header("Page Configurations")]
    [SerializeField] private List<PageHighlightConfig> pageConfigs =
        new List<PageHighlightConfig>();


    // ==========================================================
    // RUNTIME DATA
    // ==========================================================

    private readonly HashSet<Renderer> highlightedRenderers =
        new HashSet<Renderer>();

    // Tracks entries that were manually disabled.
    private readonly HashSet<string> disabledEntries =
        new HashSet<string>();


    // ==========================================================
    // UNITY EVENTS
    // ==========================================================

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
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }


    // ==========================================================
    // PAGE CHANGED
    // ==========================================================

    private void HandlePageChanged(int currentPageIndex)
    {
        // Remove highlights from the previous page.
        ClearAllHighlightsGlobal();

        PageHighlightConfig config =
            GetConfigByPageIndex(currentPageIndex);

        if (config == null)
            return;

        for (int i = 0; i < config.meshEntries.Count; i++)
        {
            MeshHighlightEntry entry = config.meshEntries[i];

            if (entry == null)
                continue;

            // Auto highlight only if this entry was not disabled.
            if (entry.AutoHighlightOnPageEnter &&
                !IsEntryDisabled(currentPageIndex, i))
            {
                ApplyHighlightToEntry(entry);
            }
        }
    }


    // ==========================================================
    // UNITY EVENT HELPERS
    // ==========================================================

    public void EnableElement0ByPageIndex(int pageIndex)
    {
        EnableElementHighlight(pageIndex, 0);
    }

    public void DisableElement0ByPageIndex(int pageIndex)
    {
        DisableElementHighlight(pageIndex, 0);
    }

    public void EnableElement1ByPageIndex(int pageIndex)
    {
        EnableElementHighlight(pageIndex, 1);
    }

    public void DisableElement1ByPageIndex(int pageIndex)
    {
        DisableElementHighlight(pageIndex, 1);
    }

    public void EnableElement2ByPageIndex(int pageIndex)
    {
        EnableElementHighlight(pageIndex, 2);
    }

    public void DisableElement2ByPageIndex(int pageIndex)
    {
        DisableElementHighlight(pageIndex, 2);
    }


    // ==========================================================
    // ENABLE SINGLE ELEMENT
    // ==========================================================

    public void EnableElementHighlight(
        int pageIndex,
        int elementIndex)
    {
        PageHighlightConfig config =
            GetConfigByPageIndex(pageIndex);

        if (config == null)
            return;

        if (elementIndex < 0 ||
            elementIndex >= config.meshEntries.Count)
            return;

        disabledEntries.Remove(
            GetEntryKey(pageIndex, elementIndex)
        );

        ApplyHighlightToEntry(
            config.meshEntries[elementIndex]
        );
    }


    // ==========================================================
    // DISABLE SINGLE ELEMENT
    // ==========================================================

    public void DisableElementHighlight(
        int pageIndex,
        int elementIndex)
    {
        PageHighlightConfig config =
            GetConfigByPageIndex(pageIndex);

        if (config == null)
            return;

        if (elementIndex < 0 ||
            elementIndex >= config.meshEntries.Count)
            return;

        disabledEntries.Add(
            GetEntryKey(pageIndex, elementIndex)
        );

        RemoveHighlightFromEntry(
            config.meshEntries[elementIndex]
        );
    }


    // ==========================================================
    // ENABLE ALL PAGE ELEMENTS
    // ==========================================================

    public void EnableAllHighlightsForPageIndex(int pageIndex)
    {
        PageHighlightConfig config =
            GetConfigByPageIndex(pageIndex);

        if (config == null)
            return;

        for (int i = 0; i < config.meshEntries.Count; i++)
        {
            disabledEntries.Remove(
                GetEntryKey(pageIndex, i)
            );

            ApplyHighlightToEntry(
                config.meshEntries[i]
            );
        }
    }


    // ==========================================================
    // DISABLE ALL PAGE ELEMENTS
    // ==========================================================

    public void DisableAllHighlightsForPageIndex(int pageIndex)
    {
        PageHighlightConfig config =
            GetConfigByPageIndex(pageIndex);

        if (config == null)
            return;

        for (int i = 0; i < config.meshEntries.Count; i++)
        {
            disabledEntries.Add(
                GetEntryKey(pageIndex, i)
            );

            RemoveHighlightFromEntry(
                config.meshEntries[i]
            );
        }
    }


    // ==========================================================
    // APPLY HIGHLIGHT TO TARGET OBJECT
    // ==========================================================

    private void ApplyHighlightToEntry(
        MeshHighlightEntry entry)
    {
        if (entry == null)
            return;

        if (entry.TargetObject == null)
            return;

        /*
         * Get ALL Renderer components from:
         *
         * Target Object
         *      ├── Child 1
         *      │      └── MeshRenderer
         *      │
         *      ├── Child 2
         *      │      └── MeshRenderer
         *      │
         *      └── Child 3
         *             └── SkinnedMeshRenderer
         */

        Renderer[] renderers =
            entry.TargetObject.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            // Only allow MeshRenderer
            // and SkinnedMeshRenderer.
            if (renderer is MeshRenderer ||
                renderer is SkinnedMeshRenderer)
            {
                ApplyHighlightMaterial(renderer);
            }
        }
    }


    // ==========================================================
    // REMOVE HIGHLIGHT FROM TARGET OBJECT
    // ==========================================================

    private void RemoveHighlightFromEntry(
        MeshHighlightEntry entry)
    {
        if (entry == null)
            return;

        if (entry.TargetObject == null)
            return;

        Renderer[] renderers =
            entry.TargetObject.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer is MeshRenderer ||
                renderer is SkinnedMeshRenderer)
            {
                RemoveHighlightMaterial(renderer);
            }
        }
    }


    // ==========================================================
    // APPLY MATERIAL
    // ==========================================================

    private void ApplyHighlightMaterial(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (highlightMaterial == null)
            return;

        Material[] materials =
            renderer.sharedMaterials;

        // Check if already highlighted.
        foreach (Material material in materials)
        {
            if (material == highlightMaterial)
            {
                highlightedRenderers.Add(renderer);
                return;
            }
        }

        List<Material> newMaterials =
            new List<Material>(materials);

        // Add highlight material.
        newMaterials.Add(highlightMaterial);

        renderer.sharedMaterials =
            newMaterials.ToArray();

        highlightedRenderers.Add(renderer);
    }


    // ==========================================================
    // REMOVE MATERIAL
    // ==========================================================

    private void RemoveHighlightMaterial(
        Renderer renderer)
    {
        if (renderer == null)
            return;

        Material[] materials =
            renderer.sharedMaterials;

        List<Material> newMaterials =
            new List<Material>();

        foreach (Material material in materials)
        {
            if (material != highlightMaterial)
            {
                newMaterials.Add(material);
            }
        }

        renderer.sharedMaterials =
            newMaterials.ToArray();

        highlightedRenderers.Remove(renderer);
    }


    // ==========================================================
    // CLEAR ALL HIGHLIGHTS
    // ==========================================================

    private void ClearAllHighlightsGlobal()
    {
        // Create a temporary list because
        // RemoveHighlightMaterial modifies the HashSet.

        List<Renderer> renderersToClear =
            new List<Renderer>(highlightedRenderers);

        foreach (Renderer renderer in renderersToClear)
        {
            if (renderer != null)
            {
                RemoveHighlightMaterialDirect(renderer);
            }
        }

        highlightedRenderers.Clear();


        // Extra fallback:
        // Remove highlight from all configured target objects.

        foreach (PageHighlightConfig config in pageConfigs)
        {
            if (config == null)
                continue;

            foreach (MeshHighlightEntry entry in config.meshEntries)
            {
                RemoveHighlightFromEntry(entry);
            }
        }
    }


    // ==========================================================
    // REMOVE MATERIAL DIRECTLY
    // ==========================================================

    private void RemoveHighlightMaterialDirect(
        Renderer renderer)
    {
        if (renderer == null)
            return;

        Material[] materials =
            renderer.sharedMaterials;

        List<Material> newMaterials =
            new List<Material>();

        bool materialRemoved = false;

        foreach (Material material in materials)
        {
            if (material == highlightMaterial)
            {
                materialRemoved = true;
            }
            else
            {
                newMaterials.Add(material);
            }
        }

        if (materialRemoved)
        {
            renderer.sharedMaterials =
                newMaterials.ToArray();
        }
    }


    // ==========================================================
    // HELPERS
    // ==========================================================

    private string GetEntryKey(
        int pageIndex,
        int elementIndex)
    {
        return $"{pageIndex}_{elementIndex}";
    }


    private bool IsEntryDisabled(
        int pageIndex,
        int elementIndex)
    {
        return disabledEntries.Contains(
            GetEntryKey(pageIndex, elementIndex)
        );
    }


    private PageHighlightConfig GetConfigByPageIndex(
        int pageIndex)
    {
        foreach (PageHighlightConfig config in pageConfigs)
        {
            if (config != null &&
                config.pageIndex == pageIndex)
            {
                return config;
            }
        }

        return null;
    }
}