using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class QNHEnterController : MonoBehaviour
{
    // ============================================================
    // PAGE QNH FIELD
    // ============================================================

    [System.Serializable]
    public class PageQNHField
    {
        [Header("References")]
        public TMP_InputField inputField;
        public TMP_Text enteredQNHText;
        public Image feedbackImage;


        [Header("Page Objects")]
        [Tooltip("Objects to turn ON when answered correctly, only visible on this page.")]
        public GameObject[] objectsToEnable;


        [Header("Settings")]
        [Tooltip("If checked, dynamically fetches the generated QNH from target source page/QNHController.")]
        public bool useDynamicQNHAnswer = true;

        [Tooltip("Fallback answer if dynamic QNH is turned off or controller is not found.")]
        public float correctAnswer;

        [Tooltip("Page index where this QNH input field is active.")]
        public int pageIndex;

        [Tooltip("Page index from which to fetch target QNH. If set to -1, defaults to (pageIndex - 1).")]
        public int sourcePageIndex = -1;


        [Header("Auto-Fill Automation")]
        [Tooltip("If TRUE: this field will NOT wait for user input. It auto-fills as soon as preceding conditions are met.")]
        public bool isAutoFillField = false;

        [Tooltip("Delay in seconds before auto-filling this field after previous inputs succeed.")]
        public float autoFillDelay = 0.5f;


        [HideInInspector]
        public bool solved;

        [HideInInspector]
        public float currentEnteredValue;
    }


    // ============================================================
    // DYNAMIC QNH SOURCE
    // ============================================================

    [Header("Dynamic QNH Source")]
    [Tooltip("Reference to the QNHController instance generating target QNH values.")]
    public QNHController qnhController;


    // ============================================================
    // PAGE FIELDS
    // ============================================================

    [Header("Page Fields (Sequential Order)")]
    public PageQNHField[] pageFields;


    // ============================================================
    // FEEDBACK
    // ============================================================

    [Header("Common Wrong Feedback")]
    public TMP_Text feedbackText;


    // ============================================================
    // AUDIO
    // ============================================================

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;


    // ============================================================
    // FEEDBACK SPRITES
    // ============================================================

    [Header("Feedback Sprites")]
    public Sprite correctSprite;
    public Sprite wrongSprite;


    // ============================================================
    // BUTTONS
    // ============================================================

    [Header("Buttons")]
    public Button validateButton;
    public Button autoFillButton;


    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Settings")]
    public int maxWrongAttempts = 3;
    public float matchTolerance = 0.01f;


    // ============================================================
    // EVENTS
    // ============================================================

    [Header("Events")]
    public UnityEvent onQNHMatched;
    public UnityEvent onQNHMismatch;
    public UnityEvent<float> onEnteredQNHChanged;
    public UnityEvent onPageFieldsCompleted;
    public UnityEvent onAllAnswersVerified;


    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private int wrongAttempts;
    private bool solved;
    private bool isValidating;
    private int previousPageIndex = -1;


    // ============================================================
    // SAVED PAGE STATE
    // ============================================================

    private readonly Dictionary<int, string> savedValues =
        new Dictionary<int, string>();

    private readonly Dictionary<int, bool> savedImageStates =
        new Dictionary<int, bool>();


    // ============================================================
    // CURRENT ACTIVE FIELD RETRIEVAL
    // ============================================================

    private PageQNHField CurrentField
    {
        get
        {
            int currentPage =
                PageNavigationController.CurrentIndex;

            foreach (PageQNHField field in pageFields)
            {
                if (
                    field.pageIndex == currentPage &&
                    !field.solved
                )
                {
                    return field;
                }
            }

            return null;
        }
    }


    // ============================================================
    // ACTIVE UI REFERENCES
    // ============================================================

    private TMP_InputField ActiveInputField =>
        CurrentField != null
            ? CurrentField.inputField
            : null;


    private TMP_Text ActiveTextUI =>
        CurrentField != null
            ? CurrentField.enteredQNHText
            : null;


    private Image ActiveImage =>
        CurrentField != null
            ? CurrentField.feedbackImage
            : null;


    // ============================================================
    // ACTIVE ANSWER
    // ============================================================

    private float ActiveAnswer
    {
        get
        {
            if (CurrentField == null)
                return 0f;


            // ----------------------------------------------------
            // DYNAMIC QNH ANSWER
            // ----------------------------------------------------

            if (CurrentField.useDynamicQNHAnswer)
            {
                int targetSourcePage =
                    CurrentField.sourcePageIndex >= 0
                        ? CurrentField.sourcePageIndex
                        : CurrentField.pageIndex - 1;


                return GetTargetQNHFromSourcePage(
                    targetSourcePage
                );
            }


            // ----------------------------------------------------
            // MANUAL ANSWER
            // ----------------------------------------------------

            return CurrentField.correctAnswer;
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
        // --------------------------------------------------------
        // FIND QNH CONTROLLER
        // --------------------------------------------------------

        if (qnhController == null)
        {
            qnhController =
                FindFirstObjectByType<QNHController>();
        }


        if (qnhController == null)
        {
            Debug.LogError(
                "[QNHEnterController] " +
                "QNHController was not found in the scene."
            );
        }


        // --------------------------------------------------------
        // INPUT FIELD LISTENERS
        // --------------------------------------------------------

        foreach (PageQNHField field in pageFields)
        {
            if (field.inputField != null)
            {
                PageQNHField capturedField = field;

                field.inputField.onValueChanged.AddListener(
                    (value) =>
                        OnInputFieldChanged(
                            capturedField,
                            value
                        )
                );
            }
        }


        // --------------------------------------------------------
        // VALIDATE BUTTON
        // --------------------------------------------------------

        if (validateButton != null)
        {
            validateButton.onClick.RemoveAllListeners();

            validateButton.onClick.AddListener(
                ValidateQNH
            );
        }


        // --------------------------------------------------------
        // AUTO-FILL BUTTON
        // --------------------------------------------------------

        if (autoFillButton != null)
        {
            autoFillButton.onClick.RemoveAllListeners();

            autoFillButton.onClick.AddListener(
                AutoFill
            );
        }


        // --------------------------------------------------------
        // INITIAL RESET
        // --------------------------------------------------------

        HideFeedback();

        ResetAll();
    }


    // ============================================================
    // PAGE CHANGE LOGIC
    // ============================================================

    private void OnPageChanged(int pageIndex)
    {
        HideFeedback();


        // --------------------------------------------------------
        // BACKWARD NAVIGATION
        // --------------------------------------------------------

        if (previousPageIndex > pageIndex)
        {
            for (int i = 0; i < pageFields.Length; i++)
            {
                PageQNHField field = pageFields[i];

                if (field.pageIndex == previousPageIndex)
                {
                    // ------------------------------------------------
                    // SAVE INPUT VALUE
                    // ------------------------------------------------

                    if (field.inputField != null)
                    {
                        savedValues[i] =
                            field.inputField.text;

                        field.inputField.text = "";
                    }


                    // ------------------------------------------------
                    // SAVE IMAGE STATE
                    // ------------------------------------------------

                    if (field.feedbackImage != null)
                    {
                        savedImageStates[i] =
                            field.feedbackImage.gameObject.activeSelf;

                        field.feedbackImage.gameObject.SetActive(false);
                    }


                    // ------------------------------------------------
                    // RESET FIELD STATE
                    // ------------------------------------------------

                    field.solved = false;
                    field.currentEnteredValue = 0f;

                    EnableFieldObjects(
                        field,
                        false
                    );
                }
            }
        }


        // --------------------------------------------------------
        // RESTORE SAVED VALUES
        // --------------------------------------------------------

        for (int i = 0; i < pageFields.Length; i++)
        {
            PageQNHField field = pageFields[i];

            if (field.pageIndex == pageIndex)
            {
                if (
                    field.inputField != null &&
                    savedValues.ContainsKey(i)
                )
                {
                    field.inputField.text =
                        savedValues[i];
                }


                if (
                    field.feedbackImage != null &&
                    savedImageStates.ContainsKey(i)
                )
                {
                    field.feedbackImage.gameObject.SetActive(
                        savedImageStates[i]
                    );
                }
            }
        }


        previousPageIndex = pageIndex;


        // --------------------------------------------------------
        // ACTIVATE CURRENT FIELD
        // --------------------------------------------------------

        ActivateOnlyCurrentField();
    }


    // ============================================================
    // DIAL / VALUE MANIPULATION
    // ============================================================

    public void ModifyEnteredQNH(float delta)
    {
        PageQNHField current = CurrentField;

        if (
            current == null ||
            solved ||
            isValidating
        )
        {
            return;
        }


        current.currentEnteredValue += delta;

        UpdateUI(current);

        onEnteredQNHChanged?.Invoke(
            current.currentEnteredValue
        );
    }


    public void SetEnteredQNH(float value)
    {
        PageQNHField current = CurrentField;

        if (
            current == null ||
            solved ||
            isValidating
        )
        {
            return;
        }


        current.currentEnteredValue = value;

        UpdateUI(current);

        onEnteredQNHChanged?.Invoke(
            current.currentEnteredValue
        );
    }


    // ============================================================
    // INPUT FIELD CHANGE
    // ============================================================

    private void OnInputFieldChanged(
        PageQNHField field,
        string textValue
    )
    {
        if (
            float.TryParse(
                textValue,
                out float result
            )
        )
        {
            field.currentEnteredValue = result;


            if (field == CurrentField)
            {
                onEnteredQNHChanged?.Invoke(
                    result
                );
            }
        }
        else if (string.IsNullOrWhiteSpace(textValue))
        {
            field.currentEnteredValue = 0f;
        }
    }


    // ============================================================
    // VALIDATION
    // ============================================================

    public void ValidateQNH()
    {
        if (
            solved ||
            isValidating ||
            CurrentField == null
        )
        {
            return;
        }


        PageQNHField current =
            CurrentField;


        // --------------------------------------------------------
        // GET ACTIVE ANSWER
        // --------------------------------------------------------

        float targetAnswer =
            ActiveAnswer;


        // --------------------------------------------------------
        // DEBUG
        // --------------------------------------------------------

        // Debug.Log(
        //     $"[QNHEnterController] " +
        //     $"Page = {current.pageIndex}, " +
        //     $"Source Page = " +
        //     $"{(
        //         current.sourcePageIndex >= 0
        //             ? current.sourcePageIndex
        //             : current.pageIndex - 1
        //     )}, " +
        //     $"Entered = {current.currentEnteredValue}, " +
        //     $"Expected = {targetAnswer}"
        // );


        // --------------------------------------------------------
        // CHECK ANSWER
        // --------------------------------------------------------

        if (
            Mathf.Abs(
                current.currentEnteredValue -
                targetAnswer
            ) > matchTolerance
        )
        {
            wrongAttempts++;


            // ----------------------------------------------------
            // WRONG SOUND
            // ----------------------------------------------------

            if (
                audioSource != null &&
                wrongSound != null
            )
            {
                audioSource.PlayOneShot(
                    wrongSound
                );
            }


            // ----------------------------------------------------
            // WRONG FEEDBACK
            // ----------------------------------------------------

            ShowFeedback();

            onQNHMismatch?.Invoke();


            // ----------------------------------------------------
            // SHOW AUTO-FILL BUTTON
            // ----------------------------------------------------

            if (
                wrongAttempts >= maxWrongAttempts &&
                autoFillButton != null
            )
            {
                autoFillButton.gameObject.SetActive(true);
            }


            StartCoroutine(
                ShowWrongIconRoutine()
            );

            return;
        }


        // --------------------------------------------------------
        // CORRECT ANSWER
        // --------------------------------------------------------

        StartCoroutine(
            ValidateAndAdvanceRoutine()
        );
    }


    // ============================================================
    // VALIDATE AND ADVANCE
    // ============================================================

    private IEnumerator ValidateAndAdvanceRoutine()
    {
        isValidating = true;


        // --------------------------------------------------------
        // HIDE OLD FEEDBACK
        // --------------------------------------------------------

        HideFeedback();


        PageQNHField current =
            CurrentField;


        // --------------------------------------------------------
        // CORRECT IMAGE
        // --------------------------------------------------------

        if (ActiveImage != null)
        {
            ActiveImage.sprite =
                correctSprite;

            ActiveImage.gameObject.SetActive(true);
        }


        // --------------------------------------------------------
        // CORRECT SOUND
        // --------------------------------------------------------

        if (
            audioSource != null &&
            correctSound != null
        )
        {
            audioSource.PlayOneShot(
                correctSound
            );
        }


        // --------------------------------------------------------
        // MARK FIELD SOLVED
        // --------------------------------------------------------

        if (current != null)
        {
            current.solved = true;


            if (current.inputField != null)
            {
                current.inputField.interactable =
                    false;
            }


            EnableFieldObjects(
                current,
                true
            );
        }


        // --------------------------------------------------------
        // MATCH EVENT
        // --------------------------------------------------------

        onQNHMatched?.Invoke();

        wrongAttempts = 0;


        // --------------------------------------------------------
        // HIDE AUTO-FILL BUTTON
        // --------------------------------------------------------

        if (autoFillButton != null)
        {
            autoFillButton.gameObject.SetActive(false);
        }


        yield return null;


        isValidating = false;


        // --------------------------------------------------------
        // NEXT FIELD
        // --------------------------------------------------------

        CheckNextFieldOrAutoFill();
    }


    // ============================================================
    // CHECK NEXT FIELD
    // ============================================================

    private void CheckNextFieldOrAutoFill()
    {
        PageQNHField nextField =
            CurrentField;


        // --------------------------------------------------------
        // NO MORE FIELDS
        // --------------------------------------------------------

        if (nextField == null)
        {
            onPageFieldsCompleted?.Invoke();

            PageNavigationController
                .RequestNavigationUnlock();

            CheckTotalPuzzleCompletion();

            return;
        }


        // --------------------------------------------------------
        // ACTIVATE NEXT FIELD
        // --------------------------------------------------------

        ActivateOnlyCurrentField();


        // NOTE:
        // ActivateOnlyCurrentField() already starts the
        // auto-fill coroutine when the next field is configured
        // as an auto-fill field.
    }


    // ============================================================
    // AUTO-FILL SPECIFIC FIELD
    // ============================================================

    private IEnumerator AutoFillSpecificFieldRoutine(
        PageQNHField targetField
    )
    {
        isValidating = true;


        // --------------------------------------------------------
        // DELAY
        // --------------------------------------------------------

        if (targetField.autoFillDelay > 0f)
        {
            yield return new WaitForSeconds(
                targetField.autoFillDelay
            );
        }


        // --------------------------------------------------------
        // GET ANSWER
        // --------------------------------------------------------

        float targetValue =
            targetField.useDynamicQNHAnswer
                ? GetTargetQNHFromSourcePage(
                    targetField.sourcePageIndex >= 0
                        ? targetField.sourcePageIndex
                        : targetField.pageIndex - 1
                )
                : targetField.correctAnswer;


        // --------------------------------------------------------
        // DIRECTLY SET VALUE
        //
        // We do NOT use SetEnteredQNH() here because
        // SetEnteredQNH() intentionally blocks changes while
        // isValidating is true.
        // --------------------------------------------------------

        targetField.currentEnteredValue =
            targetValue;

        UpdateUI(targetField);

        onEnteredQNHChanged?.Invoke(
            targetValue
        );


        // --------------------------------------------------------
        // VALIDATION COMPLETE
        // --------------------------------------------------------

        isValidating = false;


        StartCoroutine(
            ValidateAndAdvanceRoutine()
        );
    }


    // ============================================================
    // WRONG ICON ROUTINE
    // ============================================================

    private IEnumerator ShowWrongIconRoutine()
    {
        isValidating = true;


        // --------------------------------------------------------
        // SHOW WRONG IMAGE
        // --------------------------------------------------------

        if (ActiveImage != null)
        {
            ActiveImage.sprite =
                wrongSprite;

            ActiveImage.gameObject.SetActive(true);
        }


        ShowFeedback();


        yield return new WaitForSeconds(
            0.7f
        );


        // --------------------------------------------------------
        // HIDE WRONG IMAGE
        // --------------------------------------------------------

        if (ActiveImage != null)
        {
            ActiveImage.gameObject.SetActive(false);
        }


        // --------------------------------------------------------
        // RESET CURRENT VALUE
        // --------------------------------------------------------

        PageQNHField current =
            CurrentField;


        if (current != null)
        {
            current.currentEnteredValue =
                0f;

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
    // MANUAL AUTO-FILL
    // ============================================================

    public void AutoFill()
    {
        if (
            solved ||
            isValidating ||
            CurrentField == null
        )
        {
            return;
        }


        HideFeedback();


        // --------------------------------------------------------
        // GET ANSWER
        // --------------------------------------------------------

        float targetValue =
            ActiveAnswer;


        // --------------------------------------------------------
        // DIRECTLY SET CURRENT FIELD
        // --------------------------------------------------------

        PageQNHField current =
            CurrentField;

        current.currentEnteredValue =
            targetValue;

        UpdateUI(current);

        onEnteredQNHChanged?.Invoke(
            targetValue
        );


        // --------------------------------------------------------
        // VALIDATE
        // --------------------------------------------------------

        ValidateQNH();
    }


    // ============================================================
    // TARGET QNH RESOLUTION
    // ============================================================

public float GetTargetQNHFromSourcePage(int sourcePageIndex)
{
    if (qnhController == null)
    {
        qnhController =
            FindFirstObjectByType<QNHController>();
    }

    if (qnhController == null)
    {
        Debug.LogError(
            "[QNHEnterController] QNHController not found!"
        );

        return 0f;
    }

    float target =
        qnhController.GetTargetQNHForPage(
            sourcePageIndex
        );

    Debug.Log(
        $"[QNHEnterController] " +
        $"Current Index = {PageNavigationController.CurrentIndex}, " +
        $"Source Index = {sourcePageIndex}, " +
        $"Target QNH = {target}"
    );

    return target;
}


    // ============================================================
    // FIELD ACTIVATION
    // ============================================================

    private void ActivateOnlyCurrentField()
    {
        int currentPage =
            PageNavigationController.CurrentIndex;


        // --------------------------------------------------------
        // DISABLE ALL INPUT FIELDS
        // --------------------------------------------------------

        foreach (PageQNHField field in pageFields)
        {
            if (field.inputField != null)
            {
                field.inputField.interactable =
                    false;
            }
        }


        // --------------------------------------------------------
        // GET CURRENT ACTIVE FIELD
        // --------------------------------------------------------

        PageQNHField active =
            CurrentField;


        // --------------------------------------------------------
        // NORMAL INPUT FIELD
        // --------------------------------------------------------

        if (
            active != null &&
            !active.solved &&
            !active.isAutoFillField
        )
        {
            if (active.inputField != null)
            {
                active.inputField.interactable =
                    true;

                active.inputField.Select();

                active.inputField.ActivateInputField();
            }
        }


        // --------------------------------------------------------
        // PAGE OBJECT VISIBILITY
        // --------------------------------------------------------

        UpdatePageObjectsVisibility(
            currentPage
        );


        // --------------------------------------------------------
        // AUTO-FILL FIELD
        // --------------------------------------------------------

        if (
            active != null &&
            active.isAutoFillField
        )
        {
            StartCoroutine(
                AutoFillSpecificFieldRoutine(
                    active
                )
            );
        }
    }


    // ============================================================
    // UPDATE UI
    // ============================================================

    private void UpdateUI(
        PageQNHField field
    )
    {
        if (field == null)
            return;


        string formattedVal =
            field.currentEnteredValue == 0f
                ? ""
                : field.currentEnteredValue.ToString("F0");


        // --------------------------------------------------------
        // TEXT UI
        // --------------------------------------------------------

        if (field.enteredQNHText != null)
        {
            field.enteredQNHText.text =
                formattedVal;
        }


        // --------------------------------------------------------
        // INPUT FIELD
        // --------------------------------------------------------

        if (
            field.inputField != null &&
            field.inputField.text != formattedVal
        )
        {
            field.inputField.text =
                formattedVal;
        }
    }


    // ============================================================
    // ENABLE FIELD OBJECTS
    // ============================================================

    private void EnableFieldObjects(
        PageQNHField field,
        bool enable
    )
    {
        if (
            field?.objectsToEnable == null
        )
        {
            return;
        }


        foreach (GameObject obj in field.objectsToEnable)
        {
            if (obj != null)
            {
                obj.SetActive(enable);
            }
        }
    }


    // ============================================================
    // PAGE OBJECT VISIBILITY
    // ============================================================

    private void UpdatePageObjectsVisibility(
        int currentPageIndex
    )
    {
        foreach (PageQNHField field in pageFields)
        {
            if (field.objectsToEnable == null)
                continue;


            bool shouldBeActive =
                field.solved &&
                field.pageIndex == currentPageIndex;


            foreach (GameObject obj in field.objectsToEnable)
            {
                if (obj != null)
                {
                    obj.SetActive(
                        shouldBeActive
                    );
                }
            }
        }
    }


    // ============================================================
    // SHOW FEEDBACK
    // ============================================================

    private void ShowFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(
                true
            );
        }
    }


    // ============================================================
    // HIDE FEEDBACK
    // ============================================================

    private void HideFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(
                false
            );
        }
    }


    // ============================================================
    // TOTAL PUZZLE COMPLETION
    // ============================================================

    private void CheckTotalPuzzleCompletion()
    {
        foreach (PageQNHField field in pageFields)
        {
            if (!field.solved)
            {
                return;
            }
        }


        solved = true;


        // --------------------------------------------------------
        // DISABLE VALIDATE BUTTON
        // --------------------------------------------------------

        if (validateButton != null)
        {
            validateButton.interactable =
                false;
        }


        // --------------------------------------------------------
        // HIDE AUTO-FILL BUTTON
        // --------------------------------------------------------

        if (autoFillButton != null)
        {
            autoFillButton.gameObject.SetActive(
                false
            );
        }


        HideFeedback();


        // --------------------------------------------------------
        // ALL ANSWERS VERIFIED
        // --------------------------------------------------------

        onAllAnswersVerified?.Invoke();
    }


    // ============================================================
    // RESET ALL
    // ============================================================

    public void ResetAll()
    {
        solved = false;
        isValidating = false;
        wrongAttempts = 0;


        // --------------------------------------------------------
        // CLEAR SAVED PAGE STATE
        // --------------------------------------------------------

        savedValues.Clear();
        savedImageStates.Clear();


        // --------------------------------------------------------
        // VALIDATE BUTTON
        // --------------------------------------------------------

        if (validateButton != null)
        {
            validateButton.interactable =
                true;
        }


        // --------------------------------------------------------
        // AUTO-FILL BUTTON
        // --------------------------------------------------------

        if (autoFillButton != null)
        {
            autoFillButton.gameObject.SetActive(
                false
            );
        }


        HideFeedback();


        // --------------------------------------------------------
        // RESET ALL FIELDS
        // --------------------------------------------------------

        foreach (PageQNHField field in pageFields)
        {
            field.solved = false;
            field.currentEnteredValue = 0f;


            if (field.inputField != null)
            {
                field.inputField.text = "";

                field.inputField.interactable =
                    false;
            }


            if (field.enteredQNHText != null)
            {
                field.enteredQNHText.text = "";
            }


            if (field.feedbackImage != null)
            {
                field.feedbackImage.gameObject.SetActive(
                    false
                );
            }


            EnableFieldObjects(
                field,
                false
            );
        }


        // --------------------------------------------------------
        // ACTIVATE CURRENT PAGE FIELD
        // --------------------------------------------------------

        ActivateOnlyCurrentField();
    }
}