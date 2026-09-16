using UnityEngine;

public class CautionUI : MonoBehaviour
{
    [Header("Target Panels")]
    [SerializeField] private GameObject[] panelObjects;

    [Header("Blink Duration Settings")]
    [Tooltip("Time in seconds the panels remain active/visible.")]
    public float activeDuration = 1.0f;

    [Tooltip("Time in seconds the panels remain inactive/hidden.")]
    public float inactiveDuration = 0.5f;

    private float timer;
    private bool isPanelActive = true;

    void Start()
    {
        // Automatically target this GameObject if none are assigned in the Inspector
        if (panelObjects == null || panelObjects.Length == 0)
        {
            panelObjects = new GameObject[] { gameObject };
        }

        // Initialize timer to start counting down the active state
        timer = activeDuration;
        SetPanelsActive(isPanelActive);
    }

    void Update()
    {
        if (panelObjects == null || panelObjects.Length == 0) return;

        timer -= Time.deltaTime;

        // Toggle state when the timer runs out
        if (timer <= 0f)
        {
            isPanelActive = !isPanelActive;
            SetPanelsActive(isPanelActive);

            // Reset timer based on the new state
            timer = isPanelActive ? activeDuration : inactiveDuration;
        }
    }

    private void SetPanelsActive(bool state)
    {
        for (int i = 0; i < panelObjects.Length; i++)
        {
            if (panelObjects[i] != null)
            {
                panelObjects[i].SetActive(state);
            }
        }
    }
}