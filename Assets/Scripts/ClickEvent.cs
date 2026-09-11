using System.Collections;
using UnityEngine;
using UnityEngine.Events; // Required for UnityEvent

public class ClickEvent : MonoBehaviour
{
    public enum MovementAxis
    {
        XAxis,
        YAxis,
        ZAxis
    }

    public enum MovementSpace
    {
        Local,
        World
    }

    [Header("Movement Axis Settings")]
    [Tooltip("Choose which axis the object should move along.")]
    public MovementAxis moveAxis = MovementAxis.ZAxis;

    [Tooltip("Choose whether to move along Local axes or World axes.")]
    public MovementSpace moveSpace = MovementSpace.Local;

    [Header("Movement Settings")]
    [Tooltip("Distance to move along the selected axis.")]
    public float moveDistance = 5.0f;

    [Tooltip("Duration of the movement in seconds.")]
    public float duration = 2.0f;

    [Tooltip("Easing curve controlling movement speed over time.")]
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Glow Material Settings")]
    [Tooltip("Target Renderer to change material on. Defaults to this object's Renderer if unassigned.")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Material to display while waiting to be clicked.")]
    [SerializeField] private Material glowMaterial;

    [Header("Events")]
    [Tooltip("Event fired automatically when the movement finishes.")]
    public UnityEvent onMovementComplete;

    private bool isMoving = false;
    private Camera mainCamera;
    private Material originalMaterial;

    private void Start()
    {
        mainCamera = Camera.main;

        // Auto-get Renderer component if not assigned manually in Inspector
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        // Cache original material and apply glow material at start
        if (targetRenderer != null)
        {
            originalMaterial = targetRenderer.material;

            if (glowMaterial != null)
            {
                targetRenderer.material = glowMaterial;
            }
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isMoving)
        {
            CheckForObjectClick();
        }
    }

    private void CheckForObjectClick()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                // Revert to original material upon click
                RestoreOriginalMaterial();

                StartCoroutine(MoveAlongCurve());
            }
        }
    }

    /// <summary>
    /// Reverts the target renderer back to its original starting material.
    /// </summary>
    public void RestoreOriginalMaterial()
    {
        if (targetRenderer != null && originalMaterial != null)
        {
            targetRenderer.material = originalMaterial;
        }
    }

    /// <summary>
    /// Re-applies the glowing material (useful for resets).
    /// </summary>
    public void ApplyGlowMaterial()
    {
        if (targetRenderer != null && glowMaterial != null)
        {
            targetRenderer.material = glowMaterial;
        }
    }

    private IEnumerator MoveAlongCurve()
    {
        isMoving = true;

        Vector3 startPosition = transform.position;
        Vector3 moveDirection = GetDirectionVector();
        Vector3 targetPosition = startPosition + (moveDirection * moveDistance);

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            float curveValue = moveCurve.Evaluate(normalizedTime);

            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);

            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;

        // Trigger the UnityEvent
        onMovementComplete?.Invoke();
    }

    private Vector3 GetDirectionVector()
    {
        if (moveSpace == MovementSpace.Local)
        {
            switch (moveAxis)
            {
                case MovementAxis.XAxis: return transform.right;
                case MovementAxis.YAxis: return transform.up;
                case MovementAxis.ZAxis: return transform.forward;
                default: return transform.forward;
            }
        }
        else
        {
            switch (moveAxis)
            {
                case MovementAxis.XAxis: return Vector3.right;
                case MovementAxis.YAxis: return Vector3.up;
                case MovementAxis.ZAxis: return Vector3.forward;
                default: return Vector3.forward;
            }
        }
    }
}