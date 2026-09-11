using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSequenceManager : MonoBehaviour
{
    [System.Serializable]
    public class PageAudioData
    {
        [Tooltip("Page index (0-based) this audio configuration applies to.")]
        public int pageIndex;

        [Header("Audio Clips")]
        public AudioClip introClip;
        public AudioClip midClip;
        public AudioClip endClip;
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource source1;
    [SerializeField] private AudioSource source2;

    [Header("Page Audio Configurations")]
    [SerializeField] private List<PageAudioData> pageAudioList = new();

    // Fast lookup dictionary
    private readonly Dictionary<int, PageAudioData> pageAudioMap = new();
    private Coroutine currentSequenceCoroutine;
    private double nextStartTime;

    private void Awake()
    {
        InitializeDictionary();
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void InitializeDictionary()
    {
        pageAudioMap.Clear();
        foreach (var data in pageAudioList)
        {
            if (!pageAudioMap.ContainsKey(data.pageIndex))
            {
                pageAudioMap.Add(data.pageIndex, data);
            }
            else
            {
                Debug.LogWarning($"[AudioSequenceManager] Duplicate page audio configuration found for page index: {data.pageIndex}");
            }
        }
    }

    /// <summary>
    /// Listener for PageNavigationController.OnPageChanged
    /// </summary>
    private void HandlePageChanged(int pageIndex)
    {
        StopCurrentSequence();

        if (pageAudioMap.TryGetValue(pageIndex, out PageAudioData audioData))
        {
            PlayFullSequence(audioData);
        }
    }

    /// <summary>
    /// Stops any running audio schedules and halts both sources.
    /// </summary>
    public void StopCurrentSequence()
    {
        if (currentSequenceCoroutine != null)
        {
            StopCoroutine(currentSequenceCoroutine);
            currentSequenceCoroutine = null;
        }

        if (source1) source1.Stop();
        if (source2) source2.Stop();
    }

    /// <summary>
    /// Plays the Intro -> Mid -> End sequence using DSP scheduled timing for the given page data.
    /// </summary>
    public void PlayFullSequence(PageAudioData data)
    {
        if (data == null) return;

        // Verify audio sources exist
        if (!source1 || !source2)
        {
            Debug.LogError("[AudioSequenceManager] AudioSource references missing!");
            return;
        }

        currentSequenceCoroutine = StartCoroutine(ExecuteSequence(data));
    }

    private IEnumerator ExecuteSequence(PageAudioData data)
    {
        double startTime = AudioSettings.dspTime + 0.1; // 100ms buffer to prevent start hitching

        // 1. Play Intro (Source 1)
        if (data.introClip != null)
        {
            source1.clip = data.introClip;
            source1.PlayScheduled(startTime);
            startTime += (double)data.introClip.samples / data.introClip.frequency;
        }

        // 2. Play Mid Audio (Source 2) right after Intro
        if (data.midClip != null)
        {
            source2.clip = data.midClip;
            source2.PlayScheduled(startTime);
            nextStartTime = startTime;
            startTime += (double)data.midClip.samples / data.midClip.frequency;
        }

        // 3. Schedule End Audio (Source 1) right after Mid
        if (data.endClip != null)
        {
            // Wait until mid clip starts playing before scheduling the end clip
            if (data.midClip != null)
            {
                yield return new WaitUntil(() => AudioSettings.dspTime >= nextStartTime - 0.05);
            }

            source1.clip = data.endClip;
            source1.PlayScheduled(startTime);
        }
    }

    /// <summary>
    /// Plays UI/Navigation button sound effects without interrupting scheduled DSP playback.
    /// </summary>
    public void NavigationButtonSound(AudioClip audioClip)
    {
        if (audioClip != null && source1 != null)
        {
            source1.PlayOneShot(audioClip);
        }
    }
}