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

    private void HandlePageChanged(int pageIndex)
    {
        ApplySkyboxForPage(pageIndex);
    }

    private void ApplySkyboxForPage(int pageIndex)
    {
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
            RenderSettings.skybox = targetMaterial;
            DynamicGI.UpdateEnvironment(); // Force global illumination refresh
        }
        else
        {
            Debug.LogWarning($"[SkyboxSwitcher] No skybox material assigned for page index {pageIndex} and no default material set.");
        }
    }
}