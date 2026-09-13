using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class QNHEnterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QNHController qnhController;
    [Tooltip("Optional: Text UI element displaying the user's current entered QNH value.")]
    [SerializeField] private TMP_Text enteredQNHText;
    [Tooltip("Optional: InputField if the player types the QNH directly instead of turning a dial.")]
    [SerializeField] private TMP_InputField qnhInputField;

    [Header("Input Settings")]
    [Tooltip("Tolerance for comparing floating-point QNH values.")]
    [SerializeField] private float matchTolerance = 0.01f;

    [Header("Events")]
    [Tooltip("Triggered when the user successfully matches the target QNH from the target page.")]
    public UnityEvent onQNHMatched;
    [Tooltip("Triggered when the user enters an incorrect QNH value upon submission.")]
    public UnityEvent onQNHMismatch;
    [Tooltip("Triggered whenever the user changes their entered QNH value.")]
    public UnityEvent<float> onEnteredQNHChanged;

    private float currentEnteredQNH;
    private int sourcePageIndex = -1; // Page index from which to grab the target QNH

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void Start()
    {
        if (qnhController == null)
        {
            qnhController = FindFirstObjectByType<QNHController>();
        }

        if (qnhInputField != null)
        {
            qnhInputField.onValueChanged.AddListener(OnInputFieldChanged);
        }

        ResetEnteredQNH();
    }

    /// <summary>
    /// Handles updates when navigating pages.
    /// Automatically checks against the previous page by default (e.g., Page 2 checks Page 1's QNH).
    /// </summary>
    private void OnPageChanged(int currentPageIndex)
    {
        // By default, target QNH is read from the immediate previous page (Page Index - 1)
        sourcePageIndex = currentPageIndex - 1;
        ResetEnteredQNH();
    }

    /// <summary>
    /// Manually set which page's target QNH this entry controller should compare against.
    /// </summary>
    public void SetSourcePageIndex(int pageIndex)
    {
        sourcePageIndex = pageIndex;
    }

    /// <summary>
    /// Resets the player's input value back to 0 and updates UI.
    /// </summary>
    public void ResetEnteredQNH()
    {
        currentEnteredQNH = 0f;
        UpdateUI();
    }

    /// <summary>
    /// Overload to reset the input value to a specific starting value if needed at runtime.
    /// </summary>
    public void ResetEnteredQNH(float resetValue)
    {
        currentEnteredQNH = resetValue;
        UpdateUI();
    }

    /// <summary>
    /// Call this when the user rotates a dial or presses a button to step QNH up or down.
    /// Example: ModifyEnteredQNH(1f) or ModifyEnteredQNH(-1f)
    /// </summary>
    public void ModifyEnteredQNH(float delta)
    {
        currentEnteredQNH += delta;
        UpdateUI();
        onEnteredQNHChanged?.Invoke(currentEnteredQNH);
    }

    /// <summary>
    /// Set a precise QNH value programmatically.
    /// </summary>
    public void SetEnteredQNH(float value)
    {
        currentEnteredQNH = value;
        UpdateUI();
        onEnteredQNHChanged?.Invoke(currentEnteredQNH);
    }

    private void OnInputFieldChanged(string textValue)
    {
        if (float.TryParse(textValue, out float result))
        {
            currentEnteredQNH = result;
            onEnteredQNHChanged?.Invoke(currentEnteredQNH);
        }
    }

    /// <summary>
    /// Evaluates whether the current entered QNH matches the target QNH from the source (previous) page.
    /// Call this from a "Confirm" button or directly when the dial stops moving.
    /// </summary>
    public void ValidateQNH()
    {
        if (qnhController == null)
        {
            Debug.LogError("[QNHEnterController] Reference to QNHController missing!", this);
            return;
        }

        float targetQNH = GetTargetQNHFromSourcePage();

        if (Mathf.Abs(currentEnteredQNH - targetQNH) <= matchTolerance)
        {
            onQNHMatched?.Invoke();
        }
        else
        {
            onQNHMismatch?.Invoke();
        }
    }

    /// <summary>
    /// Helper to fetch target QNH generated for the designated source page.
    /// </summary>
    public float GetTargetQNHFromSourcePage()
    {
        // Search the controller's page config list for the specified target page index
        var configList = GetTargetPageConfigurations();
        if (configList != null)
        {
            var targetConfig = configList.Find(c => c.pageIndex == sourcePageIndex);
            if (targetConfig != null && targetConfig.generatedTargetQNH != 0)
            {
                return targetConfig.generatedTargetQNH;
            }
        }

        // Fallback if target page isn't registered/generated
        return qnhController.GetCurrentTargetQNH();
    }

    private List<QNHController.PageQNHConfig> GetTargetPageConfigurations()
    {
        // Access targetPageConfigurations via reflection or direct getter
        var field = typeof(QNHController).GetField("targetPageConfigurations", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        return field?.GetValue(qnhController) as List<QNHController.PageQNHConfig>;
    }

    private void UpdateUI()
    {
        if (enteredQNHText != null)
        {
            enteredQNHText.text = currentEnteredQNH.ToString("F0");
        }

        if (qnhInputField != null && qnhInputField.text != currentEnteredQNH.ToString("F0"))
        {
            qnhInputField.text = currentEnteredQNH.ToString("F0");
        }
    }
}