using UnityEngine;

public class TransitionLayerManager : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("The starting/minimum transition layer limit.")]
    [SerializeField] private float startTransitionLayerLimit = 0f;

    [Tooltip("The ending/maximum transition layer limit.")]
    [SerializeField] private float endTransitionLayerLimit = 100f;

    [Tooltip("The caution threshold limit.")]
    [SerializeField] private float cautionLimit = 80f;

    [Tooltip("The current progress of the transition.")]
    [SerializeField] private float currentTransitionLevel = 0f;

    [Tooltip("Speed at which the current transition level increases per second.")]
    [SerializeField] private float transitionUpdateSpeed = 10f;

    private bool isCautionTriggered = false;

    // Public Properties for accessing fields from other scripts
    public float StartTransitionLayerLimit => startTransitionLayerLimit;
    public float EndTransitionLayerLimit => endTransitionLayerLimit;
    public float CautionLimit => cautionLimit;
    public float CurrentTransitionLevel => currentTransitionLevel;
    public float TransitionUpdateSpeed => transitionUpdateSpeed;

    private void Start()
    {
        // No modification to currentTransitionLevel on start
    }

    private void Update()
    {
        IncreaseTransitionLevel();
        CheckCautionLimit();
    }

    /// <summary>
    /// Continuously increases the current transition level over time without stopping at the end limit.
    /// </summary>
    private void IncreaseTransitionLevel()
    {
        currentTransitionLevel += transitionUpdateSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Checks if the current transition level has exceeded the caution limit.
    /// </summary>
    private void CheckCautionLimit()
    {
        if (currentTransitionLevel > cautionLimit && !isCautionTriggered)
        {
            Debug.LogWarning($"[TransitionLayerManager] Caution! Current transition level ({currentTransitionLevel:F2}) has exceeded the caution limit ({cautionLimit}).");
            isCautionTriggered = true;
        }
    }
}