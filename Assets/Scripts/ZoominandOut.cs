using UnityEngine;

public class ZoominandOut : MonoBehaviour
{
    [Header("Page Settings")]
    [Tooltip("The page index (0-based) where zooming should be allowed.")]
    [SerializeField] private int targetPageIndex = 0;

    [Header("Camera Reference")]
    [Tooltip("Optional: Drag your Camera here. If left empty, it will automatically use Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Field of View Constraints")]
    [Tooltip("Lower FOV means zoomed IN.")]
    [SerializeField] private float minFOV = 20.0f;
    [Tooltip("Higher FOV means zoomed OUT.")]
    [SerializeField] private float maxFOV = 60.0f;

    [Header("Sensitivity Settings")]
    [SerializeField] private float mouseScrollSensitivity = 10.0f;
    [SerializeField] private float touchPinchSensitivity = 0.05f;

    private float initialFOV;
    private bool isZoomAllowed = false;

    private void Awake()
    {
        // Fallback to Main Camera if not assigned in Inspector
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            initialFOV = targetCamera.fieldOfView;
        }
        else
        {
            Debug.LogError("ZoominandOut: No main camera found in scene!");
        }
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void Start()
    {
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Update()
    {
        if (!isZoomAllowed || targetCamera == null)
            return;

        HandleMouseZoom();
        HandleTouchZoom();
    }

    private void HandlePageChanged(int currentPageIndex)
    {
        isZoomAllowed = (currentPageIndex == targetPageIndex);

        // Reset FOV back to default when navigating away from the target page
        if (!isZoomAllowed && targetCamera != null)
        {
            targetCamera.fieldOfView = initialFOV;
        }
    }

    private void HandleMouseZoom()
    {
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) > 0.001f)
        {
            // Scrolling UP (positive delta) decreases FOV -> Zoom IN
            // Scrolling DOWN (negative delta) increases FOV -> Zoom OUT
            float targetFOV = targetCamera.fieldOfView - (scrollDelta * mouseScrollSensitivity);
            ApplyFOV(targetFOV);
        }
    }

    private void HandleTouchZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

            float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentMagnitude = (touch0.position - touch1.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;

            // Pinch OUT (positive difference) decreases FOV -> Zoom IN
            // Pinch IN (negative difference) increases FOV -> Zoom OUT
            float targetFOV = targetCamera.fieldOfView - (difference * touchPinchSensitivity);
            ApplyFOV(targetFOV);
        }
    }

    private void ApplyFOV(float targetFOV)
    {
        targetCamera.fieldOfView = Mathf.Clamp(targetFOV, minFOV, maxFOV);
    }
}