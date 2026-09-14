using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RandomQNH : MonoBehaviour
{
    [Header("Page Settings")]
    [Tooltip("The 0-based index of the page where this validation logic should run.")]
    [SerializeField] private int targetPageIndex = 6;

    [Header("UI Component References")]
    [SerializeField] private TMP_InputField inputField;
    
    [Tooltip("Optional direct reference to the Text component. If unassigned, auto-fetches from inputField.textComponent.")]
    [SerializeField] private TMP_Text enteredQNHText;
    
    [SerializeField] private Button submitButton;
    
    [Tooltip("The UI Image component in your Hierarchy where feedback will be displayed.")]
    [SerializeField] private Image feedbackImage;

    [Header("Feedback Sprites")]
    [Tooltip("Assign the correct checkmark sprite directly from the Inspector.")]
    [SerializeField] private Sprite correctSprite;

    [Tooltip("Assign the wrong cross sprite directly from the Inspector.")]
    [SerializeField] private Sprite wrongSprite;

    [Header("Validation Range Settings")]
    [Tooltip("Minimum allowed value (inclusive).")]
    [SerializeField] private float minRange = 1001f;

    [Tooltip("Maximum allowed value (inclusive).")]
    [SerializeField] private float maxRange = 1025f;

    [Header("Navigation Lock Integration")]
    [Tooltip("If true, unlocks navigation on PageNavigationController when answered correctly.")]
    [SerializeField] private bool unlockNavigationOnCorrect = true;

    private void Awake()
    {
        Debug.Log("[RandomQNH] Awake called. Initializing script and resetting state.");
        ResetState();
    }

    private void OnEnable()
    {
        Debug.Log("[RandomQNH] OnEnable called. Subscribing to events.");
        PageNavigationController.OnPageChanged += HandlePageChanged;

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(ValidateInput);
            Debug.Log("[RandomQNH] Added click listener to submitButton.");
        }
        else
        {
            Debug.LogWarning("[RandomQNH] Submit Button reference is missing!");
        }

        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnInputFieldSubmit);
            Debug.Log("[RandomQNH] Added submit listener to inputField.");
        }
        else
        {
            Debug.LogWarning("[RandomQNH] InputField reference is missing!");
        }

        CheckCurrentPage(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        Debug.Log("[RandomQNH] OnDisable called. Unsubscribing from events.");
        PageNavigationController.OnPageChanged -= HandlePageChanged;

        if (submitButton != null)
            submitButton.onClick.RemoveListener(ValidateInput);

        if (inputField != null)
            inputField.onSubmit.RemoveListener(OnInputFieldSubmit);
    }

    private void HandlePageChanged(int newPageIndex)
    {
        Debug.Log($"[RandomQNH] HandlePageChanged fired. New Page Index: {newPageIndex}");
        CheckCurrentPage(newPageIndex);
    }

    private void CheckCurrentPage(int currentPageIndex)
    {
        bool isTargetPage = (currentPageIndex == targetPageIndex);
        Debug.Log($"[RandomQNH] CheckCurrentPage: Current Page = {currentPageIndex}, Target Page = {targetPageIndex}. IsTargetPage? {isTargetPage}");

        if (!isTargetPage)
        {
            Debug.Log("[RandomQNH] Current page is NOT the target page. Executing ResetState().");
            ResetState();
        }
        else
        {
            Debug.Log("[RandomQNH] Current page matches the target page. Validation is active.");
        }
    }

    private void OnInputFieldSubmit(string text)
    {
        Debug.Log($"[RandomQNH] OnInputFieldSubmit fired with raw text parameter: '{text}'. Triggering ValidateInput().");
        ValidateInput();
    }

    /// <summary>
    /// Validates user input directly from the text field/TMP_Text element against the range 1001-1025.
    /// </summary>
    public void ValidateInput()
    {
        Debug.Log($"[RandomQNH] ValidateInput() called. Current Page Index: {PageNavigationController.CurrentIndex}");

        if (PageNavigationController.CurrentIndex != targetPageIndex)
        {
            Debug.LogWarning($"[RandomQNH] ValidateInput aborted! Page mismatch. Current: {PageNavigationController.CurrentIndex}, Target: {targetPageIndex}");
            return;
        }

        TMP_Text targetTextComponent = enteredQNHText != null ? enteredQNHText : (inputField != null ? inputField.textComponent : null);

        if (targetTextComponent == null)
        {
            Debug.LogError("[RandomQNH] No active TMP_Text component found to read input from!");
            return;
        }

        string rawInput = targetTextComponent.text.Trim();
        Debug.Log($"[RandomQNH] Extracted raw string from targetTextComponent: '{rawInput}'");

        if (float.TryParse(rawInput, out float userValue))
        {
            bool isCorrect = userValue >= minRange && userValue <= maxRange;
            Debug.Log($"[RandomQNH] Parsed float value: {userValue}. Allowed Range: [{minRange} - {maxRange}]. Result: {(isCorrect ? "CORRECT" : "INCORRECT")}");
            
            SetFeedbackImage(isCorrect ? correctSprite : wrongSprite);

            if (isCorrect)
            {
                if (unlockNavigationOnCorrect)
                {
                    Debug.Log("[RandomQNH] Answer is correct. Requesting navigation unlock via PageNavigationController.");
                    PageNavigationController.RequestNavigationUnlock();
                }
                else
                {
                    Debug.Log("[RandomQNH] Answer is correct, but unlockNavigationOnCorrect is set to FALSE.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[RandomQNH] Failed to parse input '{rawInput}' into a float value. Displaying wrong feedback sprite.");
            SetFeedbackImage(wrongSprite);
        }
    }

    private void SetFeedbackImage(Sprite sprite)
    {
        if (feedbackImage == null)
        {
            Debug.LogWarning("[RandomQNH] SetFeedbackImage called, but feedbackImage UI Reference is null!");
            return;
        }

        if (sprite != null)
        {
            feedbackImage.sprite = sprite;
            feedbackImage.gameObject.SetActive(true);
            Debug.Log($"[RandomQNH] Set feedbackImage sprite to '{sprite.name}' and activated GameObject.");
        }
        else
        {
            feedbackImage.gameObject.SetActive(false);
            Debug.LogWarning("[RandomQNH] SetFeedbackImage received a null sprite. Deactivating feedbackImage GameObject.");
        }
    }

    public void ResetState()
    {
        Debug.Log("[RandomQNH] ResetState() executing.");

        if (inputField != null)
        {
            inputField.text = string.Empty;
            Debug.Log("[RandomQNH] Cleared inputField.text.");
        }

        if (enteredQNHText != null)
        {
            enteredQNHText.text = string.Empty;
            Debug.Log("[RandomQNH] Cleared enteredQNHText.text.");
        }

        if (feedbackImage != null)
        {
            feedbackImage.gameObject.SetActive(false);
            Debug.Log("[RandomQNH] Deactivated feedbackImage GameObject.");
        }
    }
}