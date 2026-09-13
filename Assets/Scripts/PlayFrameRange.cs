using System.Collections;
using UnityEngine;

public class PlayFrameRange : MonoBehaviour
{
    [Header("Animator Settings")]
    public Animator animator;
    public string stateName = "YourAnimationStateName"; // Name of the state in Animator Controller

    [Header("Frame Settings")]
    public float totalFrames = 1000f;
    public float startFrame = 600f;
    public float endFrame = 800f;

    private Coroutine frameCheckCoroutine;

    // Call this method from your UI Button
    public void PlaySpecificFrames()
    {
        if (animator == null) return;

        // Stop any running frame monitoring coroutine
        if (frameCheckCoroutine != null)
        {
            StopCoroutine(frameCheckCoroutine);
        }

        // Calculate normalized time (0.0 to 1.0)
        float startNormalizedTime = startFrame / totalFrames;
        float endNormalizedTime = endFrame / totalFrames;

        // Ensure playback speed is normal
        animator.speed = 1f;

        // Jump directly to frame 600 and play
        animator.Play(stateName, 0, startNormalizedTime);

        // Start checking for when the animation reaches frame 800
        frameCheckCoroutine = StartCoroutine(CheckEndFrame(endNormalizedTime));
    }

    private IEnumerator CheckEndFrame(float targetNormalizedTime)
    {
        // Wait one frame to allow Animator to register the new state/time
        yield return null;

        while (true)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // Get normalized time within a single loop (0.0 to 1.0)
            float currentTime = stateInfo.normalizedTime % 1f;

            // Stop animation when reaching target frame
            if (currentTime >= targetNormalizedTime)
            {
                animator.speed = 0f; // Freeze on frame 800 (or use animator.Play("Idle") to transition)
                break;
            }

            yield return null;
        }
    }
}