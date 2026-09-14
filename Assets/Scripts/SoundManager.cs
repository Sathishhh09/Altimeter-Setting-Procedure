using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public AudioSource audioSource;

    public void Playsound(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
}
