using System;
using System.Collections.Generic;
using UnityEngine;

public class SkyboxSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class PageSkyboxMapping
    {
        [Tooltip("Page index (0-based) that triggers this skybox.")]
        public int pageIndex;

        [Tooltip("Skybox material to apply for this page.")]
        public Material skyboxMaterial;
    }

    [Header("Skybox Settings")]
    [Tooltip("Default material used if no specific mapping matches the page index.")]
    [SerializeField] private Material defaultSkybox;

    [Tooltip("Map specific page indices to specific skybox materials.")]
    [SerializeField] private List<PageSkyboxMapping> pageSkyboxes = new();

    private Material activeOverrideSkybox = null;

    private void OnEnable()
    {
        // Subscribe to page changes from PageNavigationController
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void Start()
    {
        // Apply skybox for the initial page state on startup
        ApplySkyboxForPage(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks or dangling event references
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    /// <summary>
    /// Enables and applies a specific skybox material, overriding page mappings.
    /// </summary>
    /// <param name="customSkybox">The skybox material to apply.</param>
    public void SetSkybox(Material customSkybox)
    {
        if (customSkybox == null)
        {
            Debug.LogWarning("[SkyboxSwitcher] Passed skybox material is null.");
            return;
        }

        activeOverrideSkybox = customSkybox;
        ApplyMaterial(activeOverrideSkybox);
    }

    /// <summary>
    /// Disables the custom override skybox and restores the page-mapped or default skybox.
    /// </summary>
    public void ResetSkybox()
    {
        activeOverrideSkybox = null;
        ApplySkyboxForPage(PageNavigationController.CurrentIndex);
    }

    private void HandlePageChanged(int pageIndex)
    {
        // If an override skybox is enabled, don't swap skyboxes on page navigation
        if (activeOverrideSkybox != null) return;

        ApplySkyboxForPage(pageIndex);
    }

    private void ApplySkyboxForPage(int pageIndex)
    {
        if (activeOverrideSkybox != null)
        {
            ApplyMaterial(activeOverrideSkybox);
            return;
        }

        Material targetMaterial = defaultSkybox;

        // Check if there is a specific material mapped to this page index
        foreach (var entry in pageSkyboxes)
        {
            if (entry.pageIndex == pageIndex)
            {
                targetMaterial = entry.skyboxMaterial;
                break;
            }
        }

        if (targetMaterial != null)
        {
            ApplyMaterial(targetMaterial);
        }
        else
        {
            Debug.LogWarning($"[SkyboxSwitcher] No skybox material assigned for page index {pageIndex} and no default material set.");
        }
    }

    private void ApplyMaterial(Material material)
    {
        RenderSettings.skybox = material;
        DynamicGI.UpdateEnvironment(); // Force global illumination refresh
    }
}