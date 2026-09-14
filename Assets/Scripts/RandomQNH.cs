using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class RandomQNH : MonoBehaviour
{
    [Header("Player Input")]
    public TMP_InputField qnhInput;

    [Header("Submit Button")]
    public Button submitButton;

    [Header("Feedback Objects")]
    public GameObject correctObject;
    public GameObject wrongObject;

    [Header("QNH Range")]
    public int minQNH = 1001;
    public int maxQNH = 1025;

    [Header("Feedback Duration")]
    public float displayTime = 2f;

    private Coroutine feedbackCoroutine;

    private void Start()
    {
        // Connect the Submit Button to CheckQNH
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(CheckQNH);
        }

        // Hide feedback objects at the start
        if (correctObject != null)
            correctObject.SetActive(false);

        if (wrongObject != null)
            wrongObject.SetActive(false);
    }

    public void CheckQNH()
    {
        if (qnhInput == null)
            return;

        // Try to convert player input to an integer
        if (int.TryParse(qnhInput.text, out int playerQNH))
        {
            // Check whether the value is between 1001 and 1025
            if (playerQNH >= minQNH && playerQNH <= maxQNH)
            {
                ShowCorrect();
            }
            else
            {
                ShowWrong();
            }
        }
        else
        {
            // Invalid or empty input
            ShowWrong();
        }
    }

    private void ShowCorrect()
    {
        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackCoroutine = StartCoroutine(CorrectFeedback());
    }

    private void ShowWrong()
    {
        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackCoroutine = StartCoroutine(WrongFeedback());
    }

    private IEnumerator CorrectFeedback()
    {
        // Hide wrong object
        if (wrongObject != null)
            wrongObject.SetActive(false);

        // Show correct object
        if (correctObject != null)
            correctObject.SetActive(true);

        yield return new WaitForSeconds(displayTime);

        // Hide correct object
        if (correctObject != null)
            correctObject.SetActive(false);

        feedbackCoroutine = null;
    }

    private IEnumerator WrongFeedback()
    {
        // Hide correct object
        if (correctObject != null)
            correctObject.SetActive(false);

        // Show wrong object
        if (wrongObject != null)
            wrongObject.SetActive(true);

        yield return new WaitForSeconds(displayTime);

        // Hide wrong object
        if (wrongObject != null)
            wrongObject.SetActive(false);

        feedbackCoroutine = null;
    }

    private void OnDestroy()
    {
        // Remove the listener when this object is destroyed
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(CheckQNH);
        }
    }
}