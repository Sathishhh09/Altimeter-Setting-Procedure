using System;
using System.Collections.Generic;
using UnityEngine;

public class PageAudioBridge : MonoBehaviour
{
    [System.Serializable]
    public class PageAudioRange
    {
        [Tooltip("Starting page index (0-based) for this clip.")]
        public int startPageIndex;

        [Tooltip("Ending page index (0-based) for this clip.")]
        public int endPageIndex;

        [Tooltip("The audio clip to play throughout this range.")]
        public AudioClip audioClip;
    }

    [Header("References")]
    [SerializeField] private SoundManager soundManager;

    [Header("Audio Ranges Configuration")]
    [SerializeField] private List<PageAudioRange> audioRanges = new();

    private AudioClip currentPlayingClip;

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

    private void HandlePageChanged(int newPageIndex)
    {
        PageAudioRange activeRange = GetRangeForPage(newPageIndex);

        if (activeRange != null && activeRange.audioClip != null)
        {
            if (currentPlayingClip != activeRange.audioClip)
            {
                currentPlayingClip = activeRange.audioClip;
                soundManager.PlayContinuousSound(currentPlayingClip);
            }
        }
        else
        {
            currentPlayingClip = null;
            // Updated call to match SoundManager's method name
            soundManager.StopContinuousSound(); 
        }
    }

    private PageAudioRange GetRangeForPage(int pageIndex)
    {
        for (int i = 0; i < audioRanges.Count; i++)
        {
            if (pageIndex >= audioRanges[i].startPageIndex && pageIndex <= audioRanges[i].endPageIndex)
            {
                return audioRanges[i];
            }
        }
        return null;
    }
}