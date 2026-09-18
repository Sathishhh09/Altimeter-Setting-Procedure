using UnityEngine;
using UnityEngine.UI;

public class CautionUI : MonoBehaviour
{
    [Header("Panel Settings")]
    [SerializeField] private Image[] panelImages;

    [Header("Alpha Pulse Settings")]
    [Range(0f, 1f)] public float minAlpha = 0.2f;
    [Range(0f, 1f)] public float maxAlpha = 0.8f;
    public float pulseSpeed = 2f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip cautionAudioClip;
    [Range(0f, 1f)] public float audioVolume = 1.0f;

    void Start()
    {
        // Automatically fetch Image components if none are assigned
        if (panelImages == null || panelImages.Length == 0)
        {
            panelImages = GetComponentsInChildren<Image>();
        }

        // Setup and play continuous audio
        SetupAudio();
    }

    void Update()
    {
        PulseAlpha();
    }

    private void SetupAudio()
    {
        // Get AudioSource component if not manually assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            // Assign the clip if provided
            if (cautionAudioClip != null)
            {
                audioSource.clip = cautionAudioClip;
            }

            audioSource.loop = true; // Set to repeat continuously
            audioSource.volume = audioVolume;

            // Start playing if a clip is assigned and not already playing
            if (audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }

    private void PulseAlpha()
    {
        if (panelImages == null || panelImages.Length == 0) return;

        // Calculate smooth pulse factor between 0 and 1
        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        // Apply updated alpha to all assigned panel images
        for (int i = 0; i < panelImages.Length; i++)
        {
            if (panelImages[i] != null)
            {
                Color color = panelImages[i].color;
                color.a = currentAlpha;
                panelImages[i].color = color;
            }
        }
    }

    // Automatically stop/resume sound if the GameObject is disabled or enabled
    void OnEnable()
    {
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    void OnDisable()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}