using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class GlobalCameraController : MonoBehaviour
{
    [System.Serializable]
    public class CameraPointConfig
    {
        public Transform target;
        
        [Tooltip("If checked, the camera instantly snaps to the target instead of interpolating.")]
        public bool isInstantSnap = false;

        [Tooltip("Duration of movement in seconds (ignored if isInstantSnap is true).")]
        public float moveDuration = 1f;

        [Tooltip("Ease curve for motion. Overrides global ease curve.")]
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    [System.Serializable]
    public class SecondaryCameraPoint
    {
        public int pageIndex;
        public CameraPointConfig config;
    }

    [Header("Primary Page Camera Points (Index = Page Index)")]
    [SerializeField] private List<CameraPointConfig> pageCameraPoints = new();

    [Header("Secondary Camera Points (Used after first visit)")]
    [SerializeField] private List<SecondaryCameraPoint> secondaryCameraPoints = new();

    [Header("Default Fallback Movement Settings")]
    [SerializeField] private float defaultMoveDuration = 1f;
    [SerializeField] private AnimationCurve defaultEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Events")]
    public UnityEvent OnMoveStart;
    public UnityEvent OnMoveEnd;

    private Coroutine routine;
    private int currentPageIndex = 0;
    private Camera mainCamera;

    // Tracks how many times each page was visited
    private Dictionary<int, int> pageVisitCount = new();

    // ================= LIFECYCLE =================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += MoveToPage;
        mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= MoveToPage;
    }

    // ================= PAGE MOVEMENT =================

    private void MoveToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pageCameraPoints.Count)
            return;

        currentPageIndex = pageIndex;

        // Track visits
        if (!pageVisitCount.ContainsKey(pageIndex))
            pageVisitCount[pageIndex] = 0;

        pageVisitCount[pageIndex]++;

        CameraPointConfig config = GetConfigForPage(pageIndex);

        if (config != null && config.target != null)
            StartMove(config);
    }

    private CameraPointConfig GetConfigForPage(int pageIndex)
    {
        int visitCount = pageVisitCount[pageIndex];

        // First visit → use primary
        if (visitCount == 1)
            return pageCameraPoints[pageIndex];

        // Second+ visits → try secondary
        for (int i = 0; i < secondaryCameraPoints.Count; i++)
        {
            if (secondaryCameraPoints[i].pageIndex == pageIndex)
            {
                if (secondaryCameraPoints[i].config != null && secondaryCameraPoints[i].config.target != null)
                    return secondaryCameraPoints[i].config;
            }
        }

        // Fallback to primary if no secondary found
        return pageCameraPoints[pageIndex];
    }

    public void ResetToPageDefault()
    {
        MoveToPage(currentPageIndex);
    }

    // ================= DIRECT MOVEMENT OVERLOADS =================

    public void MoveTo(Transform target)
    {
        if (target == null) return;

        CameraPointConfig defaultConfig = new CameraPointConfig
        {
            target = target,
            isInstantSnap = false,
            moveDuration = defaultMoveDuration,
            easeCurve = defaultEase
        };

        StartMove(defaultConfig);
    }

    public void MoveTo(CameraPointConfig config)
    {
        if (config == null || config.target == null) return;
        StartMove(config);
    }

    // ================= MOVEMENT CORE =================

    private void StartMove(CameraPointConfig config)
    {
        if (mainCamera == null) return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(MoveRoutine(config));
    }

    private IEnumerator MoveRoutine(CameraPointConfig config)
    {
        OnMoveStart?.Invoke();

        Transform camTransform = mainCamera.transform;
        Transform target = config.target;

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;

        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;

        // Instant snap execution
        if (config.isInstantSnap || config.moveDuration <= 0f)
        {
            camTransform.position = endPos;
            camTransform.rotation = endRot;
        }
        else
        {
            float t = 0f;
            AnimationCurve curve = config.easeCurve ?? defaultEase;

            while (t < config.moveDuration)
            {
                float progress = curve.Evaluate(t / config.moveDuration);

                camTransform.position = Vector3.Lerp(startPos, endPos, progress);
                camTransform.rotation = Quaternion.Slerp(startRot, endRot, progress);

                t += Time.deltaTime;
                yield return null;
            }

            camTransform.position = endPos;
            camTransform.rotation = endRot;
        }

        OnMoveEnd?.Invoke();
    }
}