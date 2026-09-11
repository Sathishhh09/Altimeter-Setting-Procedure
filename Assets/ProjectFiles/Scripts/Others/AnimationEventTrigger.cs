using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class AnimationEventTrigger : MonoBehaviour
{
    // ============================================================
    // EVENT SETUP
    // ============================================================

    [System.Serializable]
    public class TriggerEventData
    {
        [Header("Event Name")]
        public string eventName;

        [Header("Event")]
        public UnityEvent onTriggered;

        [Header("Navigation")]
        [Tooltip("If enabled, navigation will be unlocked when this event is triggered.")]
        public bool enableNavigation = false;
    }

    // ============================================================
    // EVENTS
    // ============================================================

    [Header("Trigger Events")]
    [Tooltip("Add as many independent events as required.")]
    [SerializeField]
    private List<TriggerEventData> triggerEvents =
        new List<TriggerEventData>();

    // ============================================================
    // TRIGGER EVENT
    // ============================================================

    /// <summary>
    /// Triggers one specific event from the list.
    ///
    /// Example:
    /// TriggerEvent(0);
    /// TriggerEvent(1);
    /// TriggerEvent(2);
    /// </summary>
    public void TriggerEvent(int eventIndex)
    {
        if (eventIndex < 0 ||
            eventIndex >= triggerEvents.Count)
        {
            Debug.LogWarning(
                "AnimationEventTrigger: Invalid event index: "
                + eventIndex,
                this
            );

            return;
        }

        TriggerEventData eventData =
            triggerEvents[eventIndex];

        if (eventData == null)
            return;

        // ========================================================
        // INVOKE THIS EVENT ONLY
        // ========================================================

        eventData.onTriggered?.Invoke();

        // ========================================================
        // NAVIGATION UNLOCK
        // ========================================================

        if (eventData.enableNavigation)
        {
            PageNavigationController
                .RequestNavigationUnlock();
        }
    }

    // ============================================================
    // INDIVIDUAL EVENT METHODS
    // ============================================================

    public void TriggerEvent0()
    {
        TriggerEvent(0);
    }

    public void TriggerEvent1()
    {
        TriggerEvent(1);
    }

    public void TriggerEvent2()
    {
        TriggerEvent(2);
    }

    public void TriggerEvent3()
    {
        TriggerEvent(3);
    }

    public void TriggerEvent4()
    {
        TriggerEvent(4);
    }

    public void TriggerEvent5()
    {
        TriggerEvent(5);
    }

    public void TriggerEvent6()
    {
        TriggerEvent(6);
    }

    public void TriggerEvent7()
    {
        TriggerEvent(7);
    }

    public void TriggerEvent8()
    {
        TriggerEvent(8);
    }

    public void TriggerEvent9()
    {
        TriggerEvent(9);
    }
}