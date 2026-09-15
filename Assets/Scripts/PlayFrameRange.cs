using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayFrameRange : MonoBehaviour
{
    [Header("Animator Settings")]
    public Animator animator;
    public string stateName = "YourAnimationStateName";

    [Header("Frame Settings")]
    public float totalFrames = 1000f;
    public float startFrame = 600f;
    public float endFrame = 800f;

    [Header("Events")]
    public UnityEvent OnFrameRangeStart;
    public UnityEvent OnFrameRangeEnd;

    private Coroutine frameCheckCoroutine;

    public void PlaySpecificFrames()
    {
        if (animator == null) return;

        if (frameCheckCoroutine != null)
        {
            StopCoroutine(frameCheckCoroutine);
        }

        float startNormalizedTime = startFrame / totalFrames;
        float endNormalizedTime = endFrame / totalFrames;

        animator.speed = 1f;

        // Jump directly to start frame and play
        animator.Play(stateName, 0, startNormalizedTime);

        // Invoke start event
        OnFrameRangeStart?.Invoke();

        frameCheckCoroutine = StartCoroutine(CheckEndFrame(endNormalizedTime));
    }

    private IEnumerator CheckEndFrame(float targetNormalizedTime)
    {
        // Wait one frame to allow Animator to register the state/time transition
        yield return null;

        while (true)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // Get current normalized time within a single loop cycle
            float currentTime = stateInfo.normalizedTime % 1f;

            // Stop animation when reaching or passing target frame
            if (currentTime >= targetNormalizedTime)
            {
                animator.speed = 0f;

                // Invoke end event
                OnFrameRangeEnd?.Invoke();
                
                break;
            }

            yield return null;
        }
    }
}