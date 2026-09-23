using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class MCQController : MonoBehaviour
{
    // =========================================================
    // PANELS
    // =========================================================

    [Header("Panels")]
    public GameObject mcqPanel;
    public GameObject explanationPanel;

    // =========================================================
    // BACKGROUND BLUR
    // =========================================================

    [Header("Background Blur")]

    [Tooltip("The separate full-screen background panel.")]
    public GameObject backgroundBlurPanel;

    [Tooltip("Blur material used by the background panel.")]
    public Material backgroundBlurMaterial;

    [Range(0f, 10f)]
    public float blurAmount = 3f;

    // =========================================================
    // QUESTION UI
    // =========================================================

    [Header("Question UI")]
    public TMP_Text questionText;
    public Image referenceImage;

    // =========================================================
    // OPTIONS
    // =========================================================

    [Header("Options - Clickable Buttons")]
    public Button[] optionButtons;

    [Header("Options - Answer Texts")]
    public TMP_Text[] optionTexts;

    [Header("Options - Separate UI Images")]
    public Image[] optionUIImages;

    // =========================================================
    // OPTION ANIMATION
    // =========================================================

    [Header("Option Animation")]
    public float popScaleMultiplier = 1.15f;
    public float popDuration = 0.12f;
    public float correctSecondPopDelay = 0.08f;

    // =========================================================
    // EXPLANATION
    // =========================================================

    [Header("Explanation UI")]
    public TMP_Text explanationText;
    public Button explanationActionButton;

    [Header("Explanation Entry Buttons")]
    public Button rightExplanationButton;
    public Button wrongExplanationButton;

    // =========================================================
    // SPRITES
    // =========================================================

    [Header("Option UI Sprites")]
    public Sprite initialSprite;
    public Sprite correctSprite;
    public Sprite wrongSprite;

    // =========================================================
    // DATA
    // =========================================================

    [Header("Data")]
    public MCQQuestionData questionData;

    // =========================================================
    // AUDIO
    // =========================================================

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctClip;
    public AudioClip wrongClip;

    // =========================================================
    // NAVIGATION
    // =========================================================

    [Header("Navigation")]
    public UnityEvent Nextpage;

    // =========================================================
    // STATE
    // =========================================================

    class MCQState
    {
        public bool answeredCorrectly;

        public HashSet<int> wrongAttempts =
            new HashSet<int>();
    }

    private MCQState state =
        new MCQState();

    // =========================================================
    // ORIGINAL SCALES
    // =========================================================

    private Vector3[] originalButtonScales;
    private Vector3[] originalTextScales;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        CacheOriginalScales();

        SetupBackgroundBlur();
    }

    private void OnEnable()
    {
        LoadQuestion();
        BindButtons();
        RestoreState();

        EnableBackgroundBlur();
    }

    // =========================================================
    // BACKGROUND BLUR SETUP
    // =========================================================

    void SetupBackgroundBlur()
    {
        if (backgroundBlurPanel == null)
            return;

        Image image =
            backgroundBlurPanel.GetComponent<Image>();

        if (image == null)
        {
            Debug.LogWarning(
                "BackgroundBlurPanel does not have an Image component."
            );

            return;
        }

        if (backgroundBlurMaterial != null)
        {
            image.material =
                backgroundBlurMaterial;

            SetBlurAmount();
        }
    }

    // =========================================================
    // SET BLUR AMOUNT
    // =========================================================

    void SetBlurAmount()
    {
        if (backgroundBlurMaterial == null)
            return;

        if (backgroundBlurMaterial.HasProperty("_BlurSize"))
        {
            backgroundBlurMaterial.SetFloat(
                "_BlurSize",
                blurAmount
            );
        }

        if (backgroundBlurMaterial.HasProperty("_Alpha"))
        {
            backgroundBlurMaterial.SetFloat(
                "_Alpha",
                1f
            );
        }
    }

    // =========================================================
    // ENABLE BACKGROUND BLUR
    // =========================================================

    void EnableBackgroundBlur()
    {
        if (backgroundBlurPanel != null)
        {
            backgroundBlurPanel.SetActive(true);
        }

        SetBlurAmount();
    }

    // =========================================================
    // DISABLE BACKGROUND BLUR
    // =========================================================

    void DisableBackgroundBlur()
    {
        if (backgroundBlurPanel != null)
        {
            backgroundBlurPanel.SetActive(false);
        }
    }

    // =========================================================
    // CACHE SCALES
    // =========================================================

    void CacheOriginalScales()
    {
        if (optionButtons != null)
        {
            originalButtonScales =
                new Vector3[optionButtons.Length];

            for (int i = 0;
                i < optionButtons.Length;
                i++)
            {
                if (optionButtons[i] != null)
                {
                    originalButtonScales[i] =
                        optionButtons[i]
                            .transform
                            .localScale;
                }
            }
        }

        if (optionTexts != null)
        {
            originalTextScales =
                new Vector3[optionTexts.Length];

            for (int i = 0;
                i < optionTexts.Length;
                i++)
            {
                if (optionTexts[i] != null)
                {
                    originalTextScales[i] =
                        optionTexts[i]
                            .transform
                            .localScale;
                }
            }
        }
    }

    // =========================================================
    // LOAD QUESTION
    // =========================================================

    void LoadQuestion()
    {
        if (questionData == null)
        {
            Debug.LogError(
                "No MCQQuestionData assigned to " +
                gameObject.name
            );

            return;
        }

        // =====================================================
        // QUESTION
        // =====================================================

        if (questionText != null)
        {
            questionText.text =
                questionData.questionText;
        }

        // =====================================================
        // EXPLANATION
        // =====================================================

        if (explanationText != null)
        {
            explanationText.text =
                questionData.explanationText;
        }

        // =====================================================
        // REFERENCE IMAGE
        // =====================================================

        if (referenceImage != null)
        {
            if (questionData.referenceImage != null)
            {
                referenceImage.gameObject.SetActive(true);

                referenceImage.sprite =
                    questionData.referenceImage;

                referenceImage.color =
                    Color.white;
            }
            else
            {
                referenceImage.gameObject.SetActive(false);
            }
        }

        // =====================================================
        // OPTIONS
        // =====================================================

        if (questionData.options == null)
        {
            Debug.LogError(
                "MCQQuestionData options array is NULL."
            );

            return;
        }

        int optionCount =
            Mathf.Min(
                4,
                questionData.options.Length
            );

        for (int i = 0; i < 4; i++)
        {
            // =================================================
            // BUTTON
            // =================================================

            if (i < optionButtons.Length &&
                optionButtons[i] != null)
            {
                int index = i;

                optionButtons[i].interactable =
                    true;

                optionButtons[i]
                    .onClick
                    .RemoveAllListeners();

                optionButtons[i]
                    .onClick
                    .AddListener(
                        () =>
                            OnOptionSelected(index)
                    );
            }

            // =================================================
            // ANSWER TEXT
            // =================================================

            if (i < optionTexts.Length &&
                optionTexts[i] != null)
            {
                if (i < optionCount)
                {
                    optionTexts[i].text =
                        questionData.options[i];

                    optionTexts[i]
                        .gameObject
                        .SetActive(true);
                }
                else
                {
                    optionTexts[i].text = "";

                    optionTexts[i]
                        .gameObject
                        .SetActive(false);
                }
            }

            // =================================================
            // OPTION UI IMAGE
            // =================================================

            if (i < optionUIImages.Length &&
                optionUIImages[i] != null)
            {
                if (i < optionCount)
                {
                    optionUIImages[i]
                        .gameObject
                        .SetActive(true);

                    optionUIImages[i].sprite =
                        initialSprite;

                    // IMPORTANT:
                    // NEVER fade or blur this image.

                    optionUIImages[i].color =
                        Color.white;
                }
                else
                {
                    optionUIImages[i]
                        .gameObject
                        .SetActive(false);
                }
            }

            ResetOptionScale(i);
        }

        // =====================================================
        // PANELS
        // =====================================================

        if (mcqPanel != null)
            mcqPanel.SetActive(true);

        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        HideExplanationButtons();
    }

    // =========================================================
    // RESTORE STATE
    // =========================================================

    void RestoreState()
    {
        foreach (int wrong in state.wrongAttempts)
        {
            if (wrong >= 0 &&
                wrong < optionUIImages.Length &&
                optionUIImages[wrong] != null)
            {
                optionUIImages[wrong].sprite =
                    wrongSprite;

                optionUIImages[wrong].color =
                    Color.white;
            }

            if (wrong >= 0 &&
                wrong < optionButtons.Length &&
                optionButtons[wrong] != null)
            {
                optionButtons[wrong]
                    .interactable = false;
            }
        }

        if (state.answeredCorrectly)
        {
            int correctIndex =
                questionData.correctOptionIndex;

            if (correctIndex >= 0 &&
                correctIndex < optionUIImages.Length &&
                optionUIImages[correctIndex] != null)
            {
                optionUIImages[correctIndex].sprite =
                    correctSprite;

                optionUIImages[correctIndex].color =
                    Color.white;
            }

            for (int i = 0;
                i < optionUIImages.Length;
                i++)
            {
                if (i == correctIndex)
                    continue;

                if (optionUIImages[i] != null)
                {
                    optionUIImages[i].sprite =
                        wrongSprite;

                    optionUIImages[i].color =
                        Color.white;
                }
            }

            DisableAllOptions();

            ShowRightExplanation();
        }
    }

    // =========================================================
    // OPTION CLICK
    // =========================================================

    void OnOptionSelected(int index)
    {
        if (questionData == null)
            return;

        if (index < 0 ||
            index >= questionData.options.Length)
            return;

        bool isCorrect =
            index ==
            questionData.correctOptionIndex;

        // =====================================================
        // CORRECT
        // =====================================================

        if (isCorrect)
        {
            if (state.answeredCorrectly)
                return;

            state.answeredCorrectly = true;

            PlaySound(correctClip);

            // Selected option = CORRECT
            if (index < optionUIImages.Length &&
                optionUIImages[index] != null)
            {
                optionUIImages[index].sprite =
                    correctSprite;

                optionUIImages[index].color =
                    Color.white;
            }

            // Other options = WRONG
            for (int i = 0;
                i < optionUIImages.Length;
                i++)
            {
                if (i == index)
                    continue;

                if (optionUIImages[i] != null)
                {
                    optionUIImages[i].sprite =
                        wrongSprite;

                    optionUIImages[i].color =
                        Color.white;
                }
            }

            DisableAllOptions();

            StartCoroutine(
                CorrectPopSequence(index)
            );

            ShowRightExplanation();

            Nextpage?.Invoke();
        }

        // =====================================================
        // WRONG
        // =====================================================

        else
        {
            if (!state.wrongAttempts.Contains(index))
            {
                state.wrongAttempts.Add(index);
            }

            PlaySound(wrongClip);

            // Wrong UI
            if (index < optionUIImages.Length &&
                optionUIImages[index] != null)
            {
                optionUIImages[index].sprite =
                    wrongSprite;

                // NEVER fade it
                optionUIImages[index].color =
                    Color.white;
            }

            // Disable selected button
            if (index < optionButtons.Length &&
                optionButtons[index] != null)
            {
                optionButtons[index]
                    .interactable = false;
            }

            StartCoroutine(
                WrongPopSequence(index)
            );

            ShowWrongExplanation();
        }
    }

    // =========================================================
    // WRONG POP
    // =========================================================

    IEnumerator WrongPopSequence(int index)
    {
        yield return StartCoroutine(
            PopOnce(index)
        );
    }

    // =========================================================
    // CORRECT POP
    // =========================================================

    IEnumerator CorrectPopSequence(int index)
    {
        yield return StartCoroutine(
            PopOnce(index)
        );

        yield return new WaitForSecondsRealtime(
            correctSecondPopDelay
        );

        yield return StartCoroutine(
            PopOnce(index)
        );
    }

    // =========================================================
    // POP
    // =========================================================

    IEnumerator PopOnce(int index)
    {
        if (index < 0)
            yield break;

        Transform buttonTransform = null;
        Transform textTransform = null;

        if (index < optionButtons.Length &&
            optionButtons[index] != null)
        {
            buttonTransform =
                optionButtons[index].transform;
        }

        if (index < optionTexts.Length &&
            optionTexts[index] != null)
        {
            textTransform =
                optionTexts[index].transform;
        }

        Vector3 buttonOriginal =
            GetOriginalButtonScale(index);

        Vector3 textOriginal =
            GetOriginalTextScale(index);

        Vector3 buttonBig =
            buttonOriginal *
            popScaleMultiplier;

        Vector3 textBig =
            textOriginal *
            popScaleMultiplier;

        float elapsed = 0f;

        // =====================================================
        // UP
        // =====================================================

        while (elapsed < popDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    popDuration
                );

            t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            if (buttonTransform != null)
            {
                buttonTransform.localScale =
                    Vector3.Lerp(
                        buttonOriginal,
                        buttonBig,
                        t
                    );
            }

            if (textTransform != null)
            {
                textTransform.localScale =
                    Vector3.Lerp(
                        textOriginal,
                        textBig,
                        t
                    );
            }

            yield return null;
        }

        elapsed = 0f;

        // =====================================================
        // DOWN
        // =====================================================

        while (elapsed < popDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    popDuration
                );

            t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            if (buttonTransform != null)
            {
                buttonTransform.localScale =
                    Vector3.Lerp(
                        buttonBig,
                        buttonOriginal,
                        t
                    );
            }

            if (textTransform != null)
            {
                textTransform.localScale =
                    Vector3.Lerp(
                        textBig,
                        textOriginal,
                        t
                    );
            }

            yield return null;
        }

        ResetOptionScale(index);
    }

    // =========================================================
    // SCALE
    // =========================================================

    Vector3 GetOriginalButtonScale(int index)
    {
        if (originalButtonScales != null &&
            index >= 0 &&
            index < originalButtonScales.Length)
        {
            return originalButtonScales[index];
        }

        return Vector3.one;
    }

    Vector3 GetOriginalTextScale(int index)
    {
        if (originalTextScales != null &&
            index >= 0 &&
            index < originalTextScales.Length)
        {
            return originalTextScales[index];
        }

        return Vector3.one;
    }

    void ResetOptionScale(int index)
    {
        if (index >= 0 &&
            index < optionButtons.Length &&
            optionButtons[index] != null)
        {
            optionButtons[index]
                .transform
                .localScale =
                GetOriginalButtonScale(index);
        }

        if (index >= 0 &&
            index < optionTexts.Length &&
            optionTexts[index] != null)
        {
            optionTexts[index]
                .transform
                .localScale =
                GetOriginalTextScale(index);
        }
    }

    // =========================================================
    // BUTTON BINDING
    // =========================================================

    void BindButtons()
    {
        if (rightExplanationButton != null)
        {
            rightExplanationButton
                .onClick
                .RemoveAllListeners();

            rightExplanationButton
                .onClick
                .AddListener(
                    OpenExplanation
                );
        }

        if (wrongExplanationButton != null)
        {
            wrongExplanationButton
                .onClick
                .RemoveAllListeners();

            wrongExplanationButton
                .onClick
                .AddListener(
                    OpenExplanation
                );
        }

        if (explanationActionButton != null)
        {
            explanationActionButton
                .onClick
                .RemoveAllListeners();

            explanationActionButton
                .onClick
                .AddListener(
                    CloseExplanation
                );
        }
    }

    // =========================================================
    // EXPLANATION
    // =========================================================

    void OpenExplanation()
    {
        if (mcqPanel != null)
            mcqPanel.SetActive(false);

        if (explanationPanel != null)
            explanationPanel.SetActive(true);

        DisableBackgroundBlur();
    }

    void CloseExplanation()
    {
        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        if (mcqPanel != null)
            mcqPanel.SetActive(true);

        EnableBackgroundBlur();
    }

    // =========================================================
    // EXPLANATION BUTTONS
    // =========================================================

    void HideExplanationButtons()
    {
        if (rightExplanationButton != null)
        {
            rightExplanationButton
                .gameObject
                .SetActive(false);
        }

        if (wrongExplanationButton != null)
        {
            wrongExplanationButton
                .gameObject
                .SetActive(false);
        }
    }

    void ShowRightExplanation()
    {
        if (rightExplanationButton != null)
        {
            rightExplanationButton
                .gameObject
                .SetActive(true);
        }

        if (wrongExplanationButton != null)
        {
            wrongExplanationButton
                .gameObject
                .SetActive(false);
        }
    }

    void ShowWrongExplanation()
    {
        if (wrongExplanationButton != null)
        {
            wrongExplanationButton
                .gameObject
                .SetActive(true);
        }

        if (rightExplanationButton != null)
        {
            rightExplanationButton
                .gameObject
                .SetActive(false);
        }
    }

    // =========================================================
    // DISABLE OPTIONS
    // =========================================================

    void DisableAllOptions()
    {
        foreach (Button btn in optionButtons)
        {
            if (btn != null)
                btn.interactable = false;
        }
    }

    // =========================================================
    // HIDE QUESTION
    // =========================================================

    public void HideQuestion()
    {
        DisableBackgroundBlur();

        if (mcqPanel != null)
            mcqPanel.SetActive(false);

        if (explanationPanel != null)
            explanationPanel.SetActive(false);
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetQuestionState()
    {
        state =
            new MCQState();

        DisableBackgroundBlur();

        if (mcqPanel != null)
            mcqPanel.SetActive(false);

        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        HideExplanationButtons();
    }

    // =========================================================
    // AUDIO
    // =========================================================

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null &&
            clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}