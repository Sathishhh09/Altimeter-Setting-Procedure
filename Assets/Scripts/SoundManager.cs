using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("AudioSource used for continuous looping audio (e.g., page voiceovers, background music).")]
    public AudioSource loopingAudioSource;

    [Tooltip("AudioSource used for one-shot non-looping sound effects (e.g., button clicks, alerts).")]
    public AudioSource sfxAudioSource;

    private void Awake()
    {
        // Automatically enforce looping configuration on startup
        if (loopingAudioSource != null)
        {
            loopingAudioSource.loop = true;
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.loop = false;
        }
    }

    /// <summary>
    /// Plays background audio continuously without restarting if the same clip is already playing.
    /// Uses the loopingAudioSource.
    /// </summary>
    public void PlayContinuousSound(AudioClip clip)
    {
        if (loopingAudioSource == null || clip == null) return;

        // Prevent restarting if the requested clip is already active
        if (loopingAudioSource.isPlaying && loopingAudioSource.clip == clip)
            return;

        loopingAudioSource.clip = clip;
        loopingAudioSource.Play();
    }

    /// <summary>
    /// Stops any currently playing background/looping audio.
    /// </summary>
    public void StopContinuousSound()
    {
        if (loopingAudioSource != null && loopingAudioSource.isPlaying)
        {
            loopingAudioSource.Stop();
            loopingAudioSource.clip = null;
        }
    }

    /// <summary>
    /// Plays a one-shot audio clip without looping or stopping current sounds.
    /// Uses the sfxAudioSource.
    /// </summary>
    public void Playsound(AudioClip clip)
    {
        if (sfxAudioSource != null && clip != null)
        {
            sfxAudioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Stops any active non-looping SFX immediately.
    /// </summary>
    public void StopSFX()
    {
        if (sfxAudioSource != null && sfxAudioSource.isPlaying)
        {
            sfxAudioSource.Stop();
        }
    }
}