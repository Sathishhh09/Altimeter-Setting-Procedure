using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlaySound : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    [SerializeField] private AudioClip soundToPlay;

    [Header("Events")]
    public UnityEvent onSoundFinished;

    private void Start()
    {
        // Verify both references exist before playing
        if (audioSource == null)
        {
            Debug.LogError("AudioSource reference is missing on " + gameObject.name, this);
            return;
        }

        if (soundToPlay == null)
        {
            Debug.LogWarning("No AudioClip assigned to PlaySound script on " + gameObject.name, this);
            return;
        }

        StartCoroutine(PlayAudioAndTriggerEvent());
    }

    private IEnumerator PlayAudioAndTriggerEvent()
    {
        // Assign and play the clip on the external AudioSource
        audioSource.clip = soundToPlay;
        audioSource.Play();

        // Wait for the duration of the audio clip
        yield return new WaitForSeconds(soundToPlay.length);

        // Trigger the UnityEvent
        onSoundFinished?.Invoke();
    }
}