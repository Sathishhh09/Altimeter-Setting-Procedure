using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class QNHManager : MonoBehaviour
{
    // ============================================================
    // NESTED CONFIGURATION CLASSES
    // ============================================================

    public enum QNHAnswerMode
    {
        [Tooltip("Generates a dynamic random QNH value based on min/max range.")]
        DynamicRandom,
        [Tooltip("Uses the pre-entered 'generatedTargetQNH' field directly as the correct answer.")]
        PresetGenerated,
        [Tooltip("Uses the 'correctAnswer' field as the correct answer.")]
        StaticAnswer,
        [Tooltip("Validates if the entered value falls between a user-defined minimum and maximum range.")]
        RangeAnswer
    }

    [System.Serializable]
    public class PageQNHConfig
    {
        [Header("Page Setup")]
        [Tooltip("Page index where this configuration applies.")]
        public int pageIndex;

        [Header("Answer Selection Strategy")]
        [Tooltip("Choose how the correct answer/target QNH for this page is determined.")]
        public QNHAnswerMode answerMode = QNHAnswerMode.DynamicRandom;

        [Header("Target Generation & Display")]
        [Tooltip("TMP Text element to display the generated or selected target QNH for this page.")]
        public TMP_Text qnhDisplayText;

        [Tooltip("Target QNH value. Auto-generates if set to DynamicRandom and currently 0. Used as-is if set to PresetGenerated.")]
        public float generatedTargetQNH;

        [Header("Validation & Input References")]
        public TMP_InputField inputField;
        public TMP_Text enteredQNHText;
        public Image feedbackImage;

        [Header("Page Objects")]
        [Tooltip("Objects to enable when this field is answered correctly on this page.")]
        public GameObject[] objectsToEnable;

        [Header("Static Answer Settings")]
        [Tooltip("Static answer used when Answer Mode is set to StaticAnswer.")]
        public float correctAnswer;

        [Header("Range Answer Settings")]
        [Tooltip("Minimum threshold for acceptable input when Answer Mode is set to RangeAnswer.")]
        public float minCorrectRange;

        [Tooltip("Maximum threshold for acceptable input when Answer Mode is set to RangeAnswer.")]
        public float maxCorrectRange;

        [Header("Auto-Fill Settings")]
        [Tooltip("If TRUE: field will automatically validate without waiting for user input.")]
        public bool isAutoFillField = false;

        [Tooltip("Delay in seconds before auto-filling.")]
        public float autoFillDelay = 0.5f;

        [Header("Page Specific Events")]
        [Tooltip("Fires specifically when this page's QNH answer is correctly solved or verified.")]
        public UnityEvent onPageCorrectAnswer;

        [HideInInspector]
        public bool solved;

        [HideInInspector]
        public float currentEnteredValue;
    }

    // ============================================================
    // PAGE CONFIGURATION LIST
    // ============================================================

    [Header("Page Configurations (Sequential Order)")]
    public List<PageQNHConfig> pageConfigurations = new List<PageQNHConfig>();

    // ============================================================
    // RANDOMIZATION & GENERATION SETTINGS
    // ============================================================

    [Header("Randomization Settings")]
    [SerializeField] private int minQNHRange = 1001;
    [SerializeField] private int maxQNHRange = 1025;
    [SerializeField] private float defaultTargetQNH = 1013f;

    // ============================================================
    // FEEDBACK & AUDIO
    // ============================================================

    [Header("Common Wrong Feedback")]
    public TMP_Text feedbackText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;

    [Header("Feedback Sprites")]
    public Sprite correctSprite;
    public Sprite wrongSprite;

    // ============================================================
    // BUTTONS & VALIDATION SETTINGS
    // ============================================================

    [Header("Buttons")]
    public Button validateButton;
    public Button autoFillButton;

    [Header("Validation Settings")]
    public int maxWrongAttempts = 3;
    public float matchTolerance = 0.01f;

    // ============================================================
    // EVENTS
    // ============================================================

    [Header("Global Events")]
    public UnityEvent onTargetReached;
    public UnityEvent onQNHMatched;
    public UnityEvent onQNHMismatch;
    public UnityEvent<float> onEnteredQNHChanged;
    public UnityEvent onPageFieldsCompleted;
    public UnityEvent onAllAnswersVerified;

    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private float currentTargetQNH;
    private int wrongAttempts;
    private bool solved;
    private bool isValidating;
    private int previousPageIndex = -1;

    private readonly Dictionary<int, string> savedValues = new Dictionary<int, string>();
    private readonly Dictionary<int, bool> savedImageStates = new Dictionary<int, bool>();

    // ============================================================
    // CURRENT ACTIVE FIELD PROPERTIES
    // ============================================================

    private PageQNHConfig CurrentConfig
    {
        get
        {
            int currentPage = PageNavigationController.CurrentIndex;
            foreach (PageQNHConfig config in pageConfigurations)
            {
                if (config.pageIndex == currentPage && !config.solved)
                {
                    return config;
                }
            }
            return null;
        }
    }

    private TMP_InputField ActiveInputField => CurrentConfig != null ? CurrentConfig.inputField : null;
    private TMP_Text ActiveTextUI => CurrentConfig != null ? CurrentConfig.enteredQNHText : null;
    private Image ActiveImage => CurrentConfig != null ? CurrentConfig.feedbackImage : null;

    private float ActiveAnswer
    {
        get
        {
            if (CurrentConfig == null) return 0f;

            switch (CurrentConfig.answerMode)
            {
                case QNHAnswerMode.DynamicRandom:
                case QNHAnswerMode.PresetGenerated:
                    return GetTargetQNHForPage(CurrentConfig.pageIndex);

                case QNHAnswerMode.StaticAnswer:
                    return CurrentConfig.correctAnswer;

                case QNHAnswerMode.RangeAnswer:
                    // Return average of range for auto-fill or text preview purposes
                    return (CurrentConfig.minCorrectRange + CurrentConfig.maxCorrectRange) / 2f;

                default:
                    return CurrentConfig.correctAnswer;
            }
        }
    }

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
        foreach (PageQNHConfig config in pageConfigurations)
        {
            if (config.inputField != null)
            {
                PageQNHConfig capturedConfig = config;
                config.inputField.onValueChanged.AddListener((value) => OnInputFieldChanged(capturedConfig, value));
            }
        }

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

        ResetAll();
        SetupPageTargetQNH(PageNavigationController.CurrentIndex);
        HideFeedback();
    }

    // ============================================================
    // PAGE CHANGE LOGIC & TARGET SETUP
    // ============================================================

    private void OnPageChanged(int pageIndex)
    {
        HideFeedback();
        SetupPageTargetQNH(pageIndex);

        if (previousPageIndex > pageIndex)
        {
            for (int i = 0; i < pageConfigurations.Count; i++)
            {
                PageQNHConfig config = pageConfigurations[i];
                if (config.pageIndex == previousPageIndex)
                {
                    if (config.inputField != null)
                    {
                        savedValues[i] = config.inputField.text;
                        config.inputField.text = "";
                    }

                    if (config.feedbackImage != null)
                    {
                        savedImageStates[i] = config.feedbackImage.gameObject.activeSelf;
                        config.feedbackImage.gameObject.SetActive(false);
                    }

                    config.solved = false;
                    config.currentEnteredValue = 0f;
                    EnableFieldObjects(config, false);
                }
            }
        }

        for (int i = 0; i < pageConfigurations.Count; i++)
        {
            PageQNHConfig config = pageConfigurations[i];
            if (config.pageIndex == pageIndex)
            {
                if (config.inputField != null && savedValues.ContainsKey(i))
                {
                    config.inputField.text = savedValues[i];
                }

                if (config.feedbackImage != null && savedImageStates.ContainsKey(i))
                {
                    config.feedbackImage.gameObject.SetActive(savedImageStates[i]);
                }
            }
        }

        previousPageIndex = pageIndex;
        ActivateOnlyCurrentField();
    }

    private void SetupPageTargetQNH(int pageIndex)
    {
        if (!pageConfigurations.Exists(c => c.pageIndex == pageIndex))
        {
            return;
        }

        currentTargetQNH = GetTargetQNHForPage(pageIndex);
        onTargetReached?.Invoke();
    }

    // ============================================================
    // TARGET QNH GENERATION & RETRIEVAL
    // ============================================================

    public float GetTargetQNHForPage(int pageIndex)
    {
        PageQNHConfig config = pageConfigurations.Find(c => c.pageIndex == pageIndex);

        if (config == null)
        {
            Debug.LogWarning($"[QNHManager] No QNH configuration found for page index {pageIndex}. Returning default target value.");
            return defaultTargetQNH;
        }

        if (config.answerMode == QNHAnswerMode.DynamicRandom && config.generatedTargetQNH == 0f)
        {
            config.generatedTargetQNH = Random.Range(minQNHRange, maxQNHRange + 1);
            Debug.Log($"[QNHManager] Dynamically Generated QNH {config.generatedTargetQNH} for page index {pageIndex}");
        }

        float activeTargetValue = config.answerMode switch
        {
            QNHAnswerMode.StaticAnswer => config.correctAnswer,
            QNHAnswerMode.RangeAnswer => config.minCorrectRange, // Displays lower threshold on UI text element by default
            _ => config.generatedTargetQNH
        };

        if (config.qnhDisplayText != null)
        {
            if (config.answerMode == QNHAnswerMode.RangeAnswer)
            {
                config.qnhDisplayText.text = $"{config.minCorrectRange:F0}-{config.maxCorrectRange:F0}";
            }
            else
            {
                config.qnhDisplayText.text = activeTargetValue.ToString("F0");
            }
        }

        return activeTargetValue;
    }

    public float GetCurrentTargetQNH() => currentTargetQNH;

    public void RegenerateQNHForPage(int pageIndex)
    {
        PageQNHConfig config = pageConfigurations.Find(c => c.pageIndex == pageIndex);

        if (config == null)
        {
            Debug.LogWarning($"[QNHManager] Cannot regenerate QNH. No config found for Page {pageIndex}.");
            return;
        }

        config.generatedTargetQNH = Random.Range(minQNHRange, maxQNHRange + 1);

        if (config.qnhDisplayText != null)
        {
            config.qnhDisplayText.text = config.generatedTargetQNH.ToString("F0");
        }

        if (PageNavigationController.CurrentIndex == pageIndex)
        {
            currentTargetQNH = config.generatedTargetQNH;
        }
    }

    // ============================================================
    // NUMPAD / KEYPAD INPUT HANDLING
    // ============================================================

    public void OnDigitPressed(string digit)
    {
        if (solved || isValidating || ActiveInputField == null || !ActiveInputField.interactable)
            return;

        HideFeedback();

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 6 : 4;
        if (ActiveInputField.text.Length >= maxLength)
            return;

        ActiveInputField.text += digit;
    }

    public void OnDecimalPressed()
    {
        if (solved || isValidating || ActiveInputField == null || !ActiveInputField.interactable)
            return;

        HideFeedback();

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 6 : 4;
        if (ActiveInputField.text.Length >= maxLength)
            return;

        if (!ActiveInputField.text.Contains("."))
        {
            ActiveInputField.text = string.IsNullOrEmpty(ActiveInputField.text) ? "0." : ActiveInputField.text + ".";
        }
    }

    public void OnBackspacePressed()
    {
        if (solved || isValidating || ActiveInputField == null || !ActiveInputField.interactable)
            return;

        HideFeedback();

        if (ActiveInputField.text.Length > 0)
        {
            ActiveInputField.text = ActiveInputField.text.Substring(0, ActiveInputField.text.Length - 1);
        }
    }

    public void OnValidatePressed()
    {
        ValidateQNH();
    }

    // ============================================================
    // DIAL / INPUT MANIPULATION
    // ============================================================

    public void ModifyEnteredQNH(float delta)
    {
        PageQNHConfig current = CurrentConfig;
        if (current == null || solved || isValidating) return;

        current.currentEnteredValue += delta;
        UpdateUI(current);
        onEnteredQNHChanged?.Invoke(current.currentEnteredValue);
    }

    public void SetEnteredQNH(float value)
    {
        PageQNHConfig current = CurrentConfig;
        if (current == null || solved || isValidating) return;

        current.currentEnteredValue = value;
        UpdateUI(current);
        onEnteredQNHChanged?.Invoke(current.currentEnteredValue);
    }

    private void OnInputFieldChanged(PageQNHConfig config, string textValue)
    {
        if (float.TryParse(textValue, out float result))
        {
            config.currentEnteredValue = result;
            if (config == CurrentConfig)
            {
                onEnteredQNHChanged?.Invoke(result);
            }
        }
        else if (string.IsNullOrWhiteSpace(textValue))
        {
            config.currentEnteredValue = 0f;
        }
    }

    // ============================================================
    // VALIDATION LOGIC
    // ============================================================

    public void ValidateQNH()
    {
        if (solved || isValidating || CurrentConfig == null) return;

        PageQNHConfig current = CurrentConfig;
        bool isCorrect = false;

        if (current.answerMode == QNHAnswerMode.RangeAnswer)
        {
            isCorrect = current.currentEnteredValue >= current.minCorrectRange && 
                        current.currentEnteredValue <= current.maxCorrectRange;
        }
        else
        {
            float targetAnswer = ActiveAnswer;
            isCorrect = Mathf.Abs(current.currentEnteredValue - targetAnswer) <= matchTolerance;
        }

        if (!isCorrect)
        {
            wrongAttempts++;

            if (audioSource != null && wrongSound != null)
            {
                audioSource.PlayOneShot(wrongSound);
            }

            ShowFeedback();
            onQNHMismatch?.Invoke();

            if (wrongAttempts >= maxWrongAttempts && autoFillButton != null)
            {
                autoFillButton.gameObject.SetActive(true);
            }

            StartCoroutine(ShowWrongIconRoutine());
            return;
        }

        StartCoroutine(ValidateAndAdvanceRoutine());
    }

    private IEnumerator ValidateAndAdvanceRoutine()
    {
        isValidating = true;
        HideFeedback();

        PageQNHConfig current = CurrentConfig;

        if (ActiveImage != null)
        {
            ActiveImage.sprite = correctSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        if (audioSource != null && correctSound != null)
        {
            audioSource.PlayOneShot(correctSound);
        }

        if (current != null)
        {
            current.solved = true;

            if (current.inputField != null)
            {
                current.inputField.interactable = false;
            }

            EnableFieldObjects(current, true);

            current.onPageCorrectAnswer?.Invoke();
        }

        onQNHMatched?.Invoke();
        wrongAttempts = 0;

        if (autoFillButton != null)
        {
            autoFillButton.gameObject.SetActive(false);
        }

        yield return null;
        isValidating = false;

        CheckNextFieldOrAutoFill();
    }

    private void CheckNextFieldOrAutoFill()
    {
        PageQNHConfig nextField = CurrentConfig;

        if (nextField == null)
        {
            onPageFieldsCompleted?.Invoke();
            PageNavigationController.RequestNavigationUnlock();
            CheckTotalPuzzleCompletion();
            return;
        }

        ActivateOnlyCurrentField();
    }

    private IEnumerator AutoFillSpecificFieldRoutine(PageQNHConfig targetField)
    {
        isValidating = true;

        if (targetField.autoFillDelay > 0f)
        {
            yield return new WaitForSeconds(targetField.autoFillDelay);
        }

        float targetValue = targetField.answerMode switch
        {
            QNHAnswerMode.StaticAnswer => targetField.correctAnswer,
            QNHAnswerMode.RangeAnswer => (targetField.minCorrectRange + targetField.maxCorrectRange) / 2f,
            _ => GetTargetQNHForPage(targetField.pageIndex)
        };

        targetField.currentEnteredValue = targetValue;
        UpdateUI(targetField);
        onEnteredQNHChanged?.Invoke(targetValue);

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
        {
            ActiveImage.gameObject.SetActive(false);
        }

        PageQNHConfig current = CurrentConfig;
        if (current != null)
        {
            current.currentEnteredValue = 0f;
            UpdateUI(current);

            if (current.inputField != null)
            {
                current.inputField.Select();
                current.inputField.ActivateInputField();
            }
        }

        isValidating = false;
    }

    // ============================================================
    // AUTO-FILL MANUAL
    // ============================================================

    public void AutoFill()
    {
        if (solved || isValidating || CurrentConfig == null) return;

        HideFeedback();
        float targetValue = ActiveAnswer;

        PageQNHConfig current = CurrentConfig;
        current.currentEnteredValue = targetValue;
        UpdateUI(current);
        onEnteredQNHChanged?.Invoke(targetValue);

        ValidateQNH();
    }

    // ============================================================
    // FIELD ACTIVATION & UI UPDATES
    // ============================================================

    private void ActivateOnlyCurrentField()
    {
        int currentPage = PageNavigationController.CurrentIndex;

        foreach (PageQNHConfig config in pageConfigurations)
        {
            if (config.inputField != null)
            {
                config.inputField.interactable = false;
            }
        }

        PageQNHConfig active = CurrentConfig;

        if (active != null && !active.solved && !active.isAutoFillField)
        {
            if (active.inputField != null)
            {
                active.inputField.interactable = true;
                active.inputField.Select();
                active.inputField.ActivateInputField();
            }
        }

        UpdatePageObjectsVisibility(currentPage);

        if (active != null && active.isAutoFillField)
        {
            StartCoroutine(AutoFillSpecificFieldRoutine(active));
        }
    }

    private void UpdateUI(PageQNHConfig config)
    {
        if (config == null) return;

        string formattedVal = config.currentEnteredValue == 0f ? "" : config.currentEnteredValue.ToString("F0");

        if (config.enteredQNHText != null)
        {
            config.enteredQNHText.text = formattedVal;
        }

        if (config.inputField != null && config.inputField.text != formattedVal)
        {
            config.inputField.text = formattedVal;
        }
    }

    private void EnableFieldObjects(PageQNHConfig config, bool enable)
    {
        if (config?.objectsToEnable == null) return;

        foreach (GameObject obj in config.objectsToEnable)
        {
            if (obj != null)
            {
                obj.SetActive(enable);
            }
        }
    }

    private void UpdatePageObjectsVisibility(int currentPageIndex)
    {
        foreach (PageQNHConfig config in pageConfigurations)
        {
            if (config.objectsToEnable == null) continue;

            bool shouldBeActive = config.solved && config.pageIndex == currentPageIndex;

            foreach (GameObject obj in config.objectsToEnable)
            {
                if (obj != null)
                {
                    obj.SetActive(shouldBeActive);
                }
            }
        }
    }

    // ============================================================
    // FEEDBACK & PUZZLE COMPLETION
    // ============================================================

    private void ShowFeedback()
    {
        if (feedbackText != null) feedbackText.gameObject.SetActive(true);
    }

    private void HideFeedback()
    {
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    public void CompleteTarget()
    {
        Debug.Log($"[QNHManager] Target QNH completed: {currentTargetQNH}");
        onTargetReached?.Invoke();
    }

    private void CheckTotalPuzzleCompletion()
    {
        foreach (PageQNHConfig config in pageConfigurations)
        {
            if (!config.solved) return;
        }

        solved = true;

        if (validateButton != null) validateButton.interactable = false;
        if (autoFillButton != null) autoFillButton.gameObject.SetActive(false);

        HideFeedback();
        onAllAnswersVerified?.Invoke();
    }

    public void ResetAll()
    {
        solved = false;
        isValidating = false;
        wrongAttempts = 0;

        savedValues.Clear();
        savedImageStates.Clear();

        if (validateButton != null) validateButton.interactable = true;
        if (autoFillButton != null) autoFillButton.gameObject.SetActive(false);

        HideFeedback();

        foreach (PageQNHConfig config in pageConfigurations)
        {
            config.solved = false;
            config.currentEnteredValue = 0f;

            if (config.answerMode == QNHAnswerMode.DynamicRandom)
            {
                config.generatedTargetQNH = 0f;
            }

            if (config.inputField != null)
            {
                config.inputField.text = "";
                config.inputField.interactable = false;
            }

            if (config.enteredQNHText != null)
            {
                config.enteredQNHText.text = "";
            }

            if (config.feedbackImage != null)
            {
                config.feedbackImage.gameObject.SetActive(false);
            }

            EnableFieldObjects(config, false);
        }

        ActivateOnlyCurrentField();
    }

    public void SetQNHRange(int minRange, int maxRange)
    {
        if (minRange > maxRange)
        {
            Debug.LogWarning("[QNHManager] Invalid QNH range. Minimum cannot be greater than maximum.");
            return;
        }

        minQNHRange = minRange;
        maxQNHRange = maxRange;
    }
}