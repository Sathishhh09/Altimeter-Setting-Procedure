using UnityEngine;
using UnityEngine.UI;

public class CautionUI : MonoBehaviour
{
    [Header("Panel Settings")]
    [SerializeField] private Image panelImage;

    [Header("Alpha Pulse Settings")]
    [Range(0f, 1f)] public float minAlpha = 0.2f;
    [Range(0f, 1f)] public float maxAlpha = 0.8f;
    public float pulseSpeed = 2f;

    void Start()
    {
        // Automatically grab the Image component attached to this GameObject if not assigned in Inspector
        if (panelImage == null)
        {
            panelImage = GetComponent<Image>();
        }
    }

    void Update()
    {
        if (panelImage == null) return;

        // Calculate a value that smoothly goes back and forth between 0 and 1
        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);

        // Interpolate the alpha value between minAlpha and maxAlpha
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        // Apply the updated color back to the panel
        Color color = panelImage.color;
        color.a = currentAlpha;
        panelImage.color = color;
    }
}