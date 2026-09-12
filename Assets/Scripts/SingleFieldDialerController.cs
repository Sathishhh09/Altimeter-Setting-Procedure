using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class SingleFieldDialerController : MonoBehaviour
{
    [System.Serializable]
    public class PageField
    {
        [Header("References")]
        public TMP_InputField inputField;
        public Image feedbackImage;

        [Header("Page Objects")]
        [Tooltip("Objects to turn ON when answered correctly, only visible on this page.")]
        public GameObject[] objectsToEnable;

        [Header("Settings")]
        public float correctAnswer;
        public int pageIndex;

        [Header("Auto-Fill Automation")]
        [Tooltip("If TRUE: this field will NOT wait for user input. It auto-fills as soon as preceding conditions are met.")]
        public bool isAutoFillField = false;

        [Tooltip("Delay in seconds before auto-filling this field after previous inputs succeed.")]
        public float autoFillDelay = 0.5f;

        [HideInInspector]
        public bool solved;
    }

    [Header("Page Fields (Sequential Order)")]
    public PageField[] pageFields;

    [Header("Common Wrong Feedback")]
    public TMP_Text feedbackText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;

    [Header("Feedback Sprites")]
    public Sprite correctSprite;
    public Sprite wrongSprite;

    [Header("Buttons")]
    public Button validateButton;
    public Button autoFillButton;

    [Header("Settings")]
    public int maxWrongAttempts = 3;
    public float tolerance = 0.001f;

    [Header("Events")]
    public UnityEvent OnCorrectAnswer;
    public UnityEvent OnWrongAnswer;
    public UnityEvent OnPageFieldsCompleted;
    public UnityEvent OnAllAnswersVerified;

    private int wrongAttempts;
    private bool solved;
    private bool isValidating;
    private int previousPageIndex = -1;

    private readonly Dictionary<int, string> savedValues = new();
    private readonly Dictionary<int, bool> savedImageStates = new();

    // ============================================================
    // CURRENT ACTIVE FIELD RETRIEVAL
    // ============================================================

    // Returns the first unsolved field for the current active page
    private PageField CurrentField
    {
        get
        {
            int currentPage = PageNavigationController.CurrentIndex;
            foreach (PageField field in pageFields)
            {
                if (field.pageIndex == currentPage && !field.solved)
                    return field;
            }
            return null;
        }
    }

    private TMP_InputField ActiveField => CurrentField != null ? CurrentField.inputField : null;
    private float ActiveAnswer => CurrentField != null ? CurrentField.correctAnswer : 0f;
    private Image ActiveImage => CurrentField != null ? CurrentField.feedbackImage : null;

    // ============================================================
    // LIFECYCLE & EVENT SUBSCRIPTIONS
    // ============================================================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
        ActivateOnlyCurrentField();
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void Start()
    {
        if (validateButton != null)
        {
            validateButton.onClick.RemoveAllListeners();
            validateButton.onClick.AddListener(OnValidatePressed);
        }

        if (autoFillButton != null)
        {
            autoFillButton.onClick.RemoveAllListeners();
            autoFillButton.onClick.AddListener(AutoFill);
        }

        HideFeedback();
        ResetAll();
    }

    // ============================================================
    // PAGE CHANGE LOGIC
    // ============================================================

    private void OnPageChanged(int pageIndex)
    {
        HideFeedback();

        // If navigated backward, reset states for previous page fields
        if (previousPageIndex > pageIndex)
        {
            for (int i = 0; i < pageFields.Length; i++)
            {
                PageField field = pageFields[i];
                if (field.pageIndex == previousPageIndex)
                {
                    if (field.inputField != null)
                    {
                        savedValues[i] = field.inputField.text;
                        field.inputField.text = "";
                    }

                    if (field.feedbackImage != null)
                    {
                        savedImageStates[i] = field.feedbackImage.gameObject.activeSelf;
                        field.feedbackImage.gameObject.SetActive(false);
                    }

                    field.solved = false;
                    EnableFieldObjects(field, false);
                }
            }
        }

        // Restore saved values when revisiting
        for (int i = 0; i < pageFields.Length; i++)
        {
            PageField field = pageFields[i];
            if (field.pageIndex == pageIndex)
            {
                if (field.inputField != null && savedValues.ContainsKey(i))
                    field.inputField.text = savedValues[i];

                if (field.feedbackImage != null && savedImageStates.ContainsKey(i))
                    field.feedbackImage.gameObject.SetActive(savedImageStates[i]);
            }
        }

        previousPageIndex = pageIndex;
        ActivateOnlyCurrentField();
    }

    // ============================================================
    // KEYPAD INPUT HANDLING
    // ============================================================

    public void OnDigitPressed(string digit)
    {
        if (solved || isValidating || ActiveField == null || !ActiveField.interactable)
            return;

        HideFeedback();

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 6 : 4;
        if (ActiveField.text.Length >= maxLength)
            return;

        ActiveField.text += digit;
    }

    public void OnDecimalPressed()
    {
        if (solved || isValidating || ActiveField == null || !ActiveField.interactable)
            return;

        HideFeedback();

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 6 : 4;
        if (ActiveField.text.Length >= maxLength)
            return;

        if (!ActiveField.text.Contains("."))
        {
            ActiveField.text = string.IsNullOrEmpty(ActiveField.text) ? "0." : ActiveField.text + ".";
        }
    }

    public void OnBackspacePressed()
    {
        if (solved || isValidating || ActiveField == null || !ActiveField.interactable)
            return;

        HideFeedback();

        if (ActiveField.text.Length > 0)
        {
            ActiveField.text = ActiveField.text.Substring(0, ActiveField.text.Length - 1);
        }
    }

    // ============================================================
    // VALIDATION & AUTO-FILL SEQUENCING
    // ============================================================

    public void OnValidatePressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (string.IsNullOrEmpty(ActiveField.text) || !float.TryParse(ActiveField.text, out float value))
            return;

        // WRONG ANSWER
        if (Mathf.Abs(value - ActiveAnswer) > tolerance)
        {
            wrongAttempts++;

            if (audioSource != null && wrongSound != null)
                audioSource.PlayOneShot(wrongSound);

            ShowFeedback();
            OnWrongAnswer?.Invoke();

            if (wrongAttempts >= maxWrongAttempts && autoFillButton != null)
                autoFillButton.gameObject.SetActive(true);

            StartCoroutine(ShowWrongIconRoutine());
            return;
        }

        // CORRECT ANSWER
        StartCoroutine(ValidateAndAdvanceRoutine());
    }

    private IEnumerator ValidateAndAdvanceRoutine()
    {
        isValidating = true;
        HideFeedback();

        PageField current = CurrentField;

        if (ActiveImage != null)
        {
            ActiveImage.sprite = correctSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        if (audioSource != null && correctSound != null)
            audioSource.PlayOneShot(correctSound);

        if (current != null)
        {
            current.solved = true;

            if (current.inputField != null)
                current.inputField.interactable = false;

            EnableFieldObjects(current, true);
        }

        OnCorrectAnswer?.Invoke();
        wrongAttempts = 0;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        yield return null;

        isValidating = false;

        // Advance to next field on current page or complete
        CheckNextFieldOrAutoFill();
    }

    private void CheckNextFieldOrAutoFill()
    {
        PageField nextField = CurrentField;

        // If all fields on this page are solved
        if (nextField == null)
        {
            OnPageFieldsCompleted?.Invoke();
            PageNavigationController.RequestNavigationUnlock();
            CheckTotalPuzzleCompletion();
            return;
        }

        ActivateOnlyCurrentField();

        // If the next consecutive field is set as an auto-fill receiver
        if (nextField.isAutoFillField)
        {
            StartCoroutine(AutoFillSpecificFieldRoutine(nextField));
        }
    }

    private IEnumerator AutoFillSpecificFieldRoutine(PageField targetField)
    {
        isValidating = true;

        if (targetField.autoFillDelay > 0f)
            yield return new WaitForSeconds(targetField.autoFillDelay);

        if (targetField.inputField != null)
            targetField.inputField.text = targetField.correctAnswer.ToString();

        isValidating = false;
        StartCoroutine(ValidateAndAdvanceRoutine());
    }

    private IEnumerator ShowWrongIconRoutine()
    {
        isValidating = true;

        if (ActiveImage != null)
        {
            ActiveImage.sprite = wrongSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        ShowFeedback();

        yield return new WaitForSeconds(0.7f);

        if (ActiveImage != null)
            ActiveImage.gameObject.SetActive(false);

        if (ActiveField != null)
        {
            ActiveField.text = "";
            ActiveField.Select();
            ActiveField.ActivateInputField();
        }

        isValidating = false;
    }

    public void AutoFill()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        HideFeedback();
        ActiveField.text = ActiveAnswer.ToString();
        OnValidatePressed();
    }

    // ============================================================
    // FIELD ACTIVATION & VISIBILITY
    // ============================================================

    private void ActivateOnlyCurrentField()
    {
        int currentPage = PageNavigationController.CurrentIndex;

        // Lock all inputs initially
        foreach (PageField field in pageFields)
        {
            if (field.inputField != null)
                field.inputField.interactable = false;
        }

        // Enable only the active unsolved field for this page
        PageField active = CurrentField;
        if (active != null && active.inputField != null && !active.solved && !active.isAutoFillField)
        {
            active.inputField.interactable = true;
            active.inputField.Select();
            active.inputField.ActivateInputField();
        }

        UpdatePageObjectsVisibility(currentPage);

        // If the very first field on this page is marked as auto-fill
        if (active != null && active.isAutoFillField)
        {
            StartCoroutine(AutoFillSpecificFieldRoutine(active));
        }
    }

    private void EnableFieldObjects(PageField field, bool enable)
    {
        if (field?.objectsToEnable == null) return;

        foreach (GameObject obj in field.objectsToEnable)
        {
            if (obj != null)
                obj.SetActive(enable);
        }
    }

    private void UpdatePageObjectsVisibility(int currentPageIndex)
    {
        foreach (PageField field in pageFields)
        {
            if (field.objectsToEnable == null) continue;

            bool shouldBeActive = field.solved && (field.pageIndex == currentPageIndex);

            foreach (GameObject obj in field.objectsToEnable)
            {
                if (obj != null)
                    obj.SetActive(shouldBeActive);
            }
        }
    }

    private void ShowFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(true);
    }

    private void HideFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    private void CheckTotalPuzzleCompletion()
    {
        foreach (PageField field in pageFields)
        {
            if (!field.solved)
                return;
        }

        solved = true;

        if (validateButton != null)
            validateButton.interactable = false;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        HideFeedback();
        OnAllAnswersVerified?.Invoke();
    }

    public void ResetAll()
    {
        solved = false;
        isValidating = false;
        wrongAttempts = 0;

        if (validateButton != null)
            validateButton.interactable = true;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        HideFeedback();

        foreach (PageField field in pageFields)
        {
            field.solved = false;

            if (field.inputField != null)
            {
                field.inputField.text = "";
                field.inputField.interactable = false;
            }

            if (field.feedbackImage != null)
                field.feedbackImage.gameObject.SetActive(false);

            EnableFieldObjects(field, false);
        }

        ActivateOnlyCurrentField();
    }
}