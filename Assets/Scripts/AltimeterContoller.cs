// A320PFD.cs
//
// Generates a complete A320-style Primary Flight Display entirely at runtime - no manually
// created UI objects required. Attach to any empty GameObject and press Play.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class A320PFD : MonoBehaviour
{
    // ==================================================
    // Custom Data Structure for Page Settings
    // ==================================================
    [System.Serializable]
    public struct PageAltimeterConfig
    {
        [Tooltip("Target page index from PageNavigationController.")]
        public int pageIndex;
        [Tooltip("Altimeter speed value for this specific page.")]
        public float altimeterSpeed;
    }

    // ==================================================
    // Inspector - Roll Control
    // ==================================================
    [Header("Roll Control")]
    [Range(-1f, 1f)] public float rollInput = 0f;
    [Min(1f)] public float maxRoll = 30f;
    [Min(0.01f)] public float smoothDuration = 1f;

    // ==================================================
    // Inspector - Flight Data
    // ==================================================
    [Header("Flight Data")]
    [Tooltip("Baseline/commanded airspeed. What the tape shows is this plus a bounded roll-reaction offset.")]
    public float speed = 140f;
    [Tooltip("Baseline/commanded altitude. What the tape shows is this plus a bounded roll-reaction offset.")]
    public float altitude = 3500f;
    [Range(0f, 359.99f)] public float heading = 270f;

    [Tooltip("When on, Heading is no longer a free value you set directly - holding a bank angle continuously turns the aircraft.")]
    public bool autoTurnWithRoll = true;

    [Header("Speed Dynamics")]
    [Tooltip("When on, displayed speed includes a bounded offset based on Roll Input.")]
    public bool speedReactsToRoll = true;

    [Tooltip("Maximum speed deviation (knots) in either direction, reached at full +/-1 Roll Input.")]
    [Min(0f)] public float speedRollDeviation = 15f;

    [Header("Altitude Dynamics")]
    [Tooltip("Default speed/rate at which altitude changes (feet/second) if not defined in the page list.")]
    public float altimeterSpeed = 10f;

    [Tooltip("Per-page configuration list for altimeter speeds.")]
    [SerializeField] private List<PageAltimeterConfig> pageAltimeterSpeeds = new List<PageAltimeterConfig>();

    [Tooltip("When on, displayed altitude includes a bounded offset based on Roll Input.")]
    public bool altitudeReactsToRoll = true;

    [Tooltip("Maximum altitude deviation (feet) in either direction, reached at full +/-1 Roll Input.")]
    [Min(0f)] public float altitudeRollDeviation = 10f;

    [Header("Pitch Dynamics")]
    [Tooltip("When on, the attitude indicator's horizon visibly moves up/down driven by altitude's actual current climb/descent RATE.")]
    public bool pitchReactsToAltitude = true;

    [Tooltip("Maximum visible pitch (degrees), reached once altitude is changing at Pitch Rate Reference (ft/min) or faster.")]
    [Min(0f)] public float maxPitchDegrees = 15f;

    [Tooltip("Climb/descent rate (ft/min) that produces the full Max Pitch Degrees.")]
    [Min(1f)] public float pitchRateReference = 1000f;

    // ==================================================
    // Inspector - ILS
    // ==================================================
    [Header("ILS")]
    [Range(-1f, 1f)] public float localizer = 0f;
    [Range(-1f, 1f)] public float glideslope = 0f;

    // ==================================================
    // Inspector - Display Settings
    // ==================================================
    [Header("Parent / Container")]
    [SerializeField] private RectTransform parentContainer;

    [Header("Display Settings")]
    public Vector2 pfdSize = new Vector2(1000f, 750f);
    public Color skyColor = new Color(0.15f, 0.47f, 0.82f);
    public Color groundColor = new Color(0.47f, 0.30f, 0.14f);
    public Color primaryColor = Color.white;
    public Color greenColor = new Color(0.15f, 0.85f, 0.20f);
    public Color yellowColor = new Color(1f, 0.82f, 0.10f);
    public Color magentaColor = new Color(0.92f, 0.10f, 0.80f);

    [Header("Text Sizes")]
    [Min(1)] public int headerLabelFontSize = 15;
    [Min(1)] public int tapeLabelFontSize = 20;
    [Min(1)] public int valueBoxFontSize = 24;
    [Min(1)] public int pitchLadderFontSize = 16;
    [Min(1)] public int rollScaleFontSize = 13;

    // ==================================================
    // Bake state
    // ==================================================
    [Header("Bake")]
    [SerializeField] private bool isBaked = false;

    // ==================================================
    // Runtime-generated references
    // ==================================================
    [HideInInspector] [SerializeField] private Canvas canvas;
    [HideInInspector] [SerializeField] private RectTransform pfdRoot;

    [HideInInspector] [SerializeField] private RectTransform attitudeContainer;
    [HideInInspector] [SerializeField] private RectTransform horizonPivot;
    [HideInInspector] [SerializeField] private RectTransform rollPointer;
    [HideInInspector] [SerializeField] private float attitudeRadius;
    [HideInInspector] [SerializeField] private float rollScaleRadius;
    [HideInInspector] [SerializeField] private float pitchPxPerDegree;

    private const float AttitudeCenterYOffset = -30f;

    // Speed tape
    [HideInInspector] [SerializeField] private RectTransform speedTapeArea;
    [HideInInspector] [SerializeField] private Text[] speedLabelPool;
    [HideInInspector] [SerializeField] private Text speedBoxText;
    [HideInInspector] [SerializeField] private float speedTapeHeight;
    private const float SpeedPxPerKnot = 4.2f;
    private const float SpeedStep = 10f;

    // Altitude tape
    [HideInInspector] [SerializeField] private RectTransform altTapeArea;
    [HideInInspector] [SerializeField] private Text[] altLabelPool;
    [HideInInspector] [SerializeField] private Text altBoxText;
    [HideInInspector] [SerializeField] private float altTapeHeight;
    private const float AltPxPerFoot = 0.3f;
    private const float AltStep = 100f;

    private const float GsAreaWidth = 70f;

    // Heading tape
    [HideInInspector] [SerializeField] private RectTransform headingTapeArea;
    [HideInInspector] [SerializeField] private Text[] headingLabelPool;
    [HideInInspector] [SerializeField] private Text headingBoxText;
    [HideInInspector] [SerializeField] private float headingTapeWidth;
    [HideInInspector] [SerializeField] private float headingPxPerDegree;
    private const float HeadingStep = 10f;

    private const float TurnRateConstant = 1091f;
    private const float MinSpeedForTurnRate = 20f;

    // FD / LOC / GS
    [HideInInspector] [SerializeField] private RectTransform fdVerticalBar;
    [HideInInspector] [SerializeField] private RectTransform fdHorizontalBar;
    [HideInInspector] [SerializeField] private RectTransform locPointer;
    [HideInInspector] [SerializeField] private RectTransform gsPointer;
    [HideInInspector] [SerializeField] private float locHalfRange;
    [HideInInspector] [SerializeField] private float gsHalfHeight;

    public float CurrentAircraftRoll => currentAircraftRoll;

    // Smoothing state
    [HideInInspector] [SerializeField] private float currentAircraftRoll;
    [HideInInspector] [SerializeField] private float rollVelocity;
    [HideInInspector] [SerializeField] private float currentPitch;
    [HideInInspector] [SerializeField] private float pitchVelocity;
    [HideInInspector] [SerializeField] private float previousDisplayAltitude;
    [HideInInspector] [SerializeField] private bool hasPreviousDisplayAltitude;
    [HideInInspector] [SerializeField] private float actualSpeed;
    [HideInInspector] [SerializeField] private float displaySpeed;
    [HideInInspector] [SerializeField] private float speedVelocity;
    [HideInInspector] [SerializeField] private float displayAltitude;
    [HideInInspector] [SerializeField] private float altVelocity;
    [HideInInspector] [SerializeField] private float displayHeading;
    [HideInInspector] [SerializeField] private float headingVelocity;

    private static Font cachedFont;

    // ==================================================
    // Lifecycle
    // ==================================================
    private void Awake()
    {
        if (isBaked)
            return;

        actualSpeed = speed;
        displaySpeed = speed;
        displayAltitude = altitude;
        displayHeading = heading;

        BuildPFD();
        isBaked = true;
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void Start()
    {
        // Sync the active page speed setting when starting up
        SetAltimeterSpeedForPage(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void HandlePageChanged(int pageIndex)
    {
        SetAltimeterSpeedForPage(pageIndex);
    }

    private void SetAltimeterSpeedForPage(int targetPageIndex)
    {
        if (pageAltimeterSpeeds == null)
            return;

        for (int i = 0; i < pageAltimeterSpeeds.Count; i++)
        {
            if (pageAltimeterSpeeds[i].pageIndex == targetPageIndex)
            {
                altimeterSpeed = pageAltimeterSpeeds[i].altimeterSpeed;
                return;
            }
        }
    }

    private void Update()
    {
        if (pfdRoot == null)
            return;

        UpdateAltitudeFromSpeed();
        UpdateRoll();
        UpdateHeadingFromRoll();
        UpdateSpeedFromRoll();
        UpdateSpeedTape();
        UpdateAltitudeTape();
        UpdatePitchFromAltitude();
        UpdateHeadingTape();
        UpdateILSAndFD();
    }

#if UNITY_EDITOR
    [ContextMenu("Bake PFD To Scene")]
    private void BakeToScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[A320PFD] Bake must be run in Edit Mode, not Play Mode.", this);
            return;
        }

        if (isBaked)
        {
            Debug.LogWarning("[A320PFD] Already baked.", this);
            return;
        }

        actualSpeed = speed;
        displaySpeed = speed;
        displayAltitude = altitude;
        displayHeading = heading;

        BuildPFD();
        isBaked = true;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log("[A320PFD] Baked successfully.", this);
    }
#endif

    private void OnDestroy()
    {
        if (isBaked)
            return;

        if (canvas != null)
            Destroy(canvas.gameObject);
        else if (pfdRoot != null)
            Destroy(pfdRoot.gameObject);
    }

    // ==================================================
    // Build
    // ==================================================
    private void BuildPFD()
    {
        BuildCanvas();
        BuildRootPanel();
        BuildAttitudeIndicator();
        BuildRollScale();
        BuildSpeedTape();
        BuildAltitudeTape();
        BuildHeadingTape();
        BuildFlightDirectorAndILS();
        BuildLabels();
    }

    private void BuildCanvas()
    {
        if (parentContainer != null)
            return;

        GameObject canvasGO = new GameObject("A320PFD_Canvas");
        RegisterCreatedObjectForUndo(canvasGO);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildRootPanel()
    {
        Transform parent = parentContainer != null ? (Transform)parentContainer : canvas.transform;

        pfdRoot = CreateRect("PFD_Root", parent, pfdSize, Vector2.zero);
        Image bg = pfdRoot.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.04f, 1f);
        bg.raycastTarget = false;

        pfdRoot.gameObject.AddComponent<RectMask2D>();
    }

    // -------------------- Attitude Indicator --------------------
    private void BuildAttitudeIndicator()
    {
        attitudeRadius = Mathf.Min(pfdSize.x, pfdSize.y) * 0.30f;
        Vector2 size = new Vector2(attitudeRadius * 2f, attitudeRadius * 2f);

        attitudeContainer = CreateRect("Attitude_Container", pfdRoot, size, new Vector2(0f, AttitudeCenterYOffset));

        Image frame = attitudeContainer.gameObject.AddComponent<Image>();
        frame.sprite = GetCircleSprite();
        frame.color = new Color(0f, 0f, 0f, 1f);
        frame.raycastTarget = false;
        Mask mask = attitudeContainer.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        float overSize = size.x * 2.2f;

        horizonPivot = CreateRect("Horizon_Pivot", attitudeContainer, new Vector2(overSize, overSize), Vector2.zero);

        RectTransform sky = CreateRect("Sky", horizonPivot, new Vector2(overSize, overSize), new Vector2(0f, overSize * 0.5f));
        Image skyImg = sky.gameObject.AddComponent<Image>();
        skyImg.color = skyColor;
        skyImg.raycastTarget = false;

        RectTransform ground = CreateRect("Ground", horizonPivot, new Vector2(overSize, overSize), new Vector2(0f, -overSize * 0.5f));
        Image groundImg = ground.gameObject.AddComponent<Image>();
        groundImg.color = groundColor;
        groundImg.raycastTarget = false;

        RectTransform horizonLine = CreateRect("Horizon_Line", horizonPivot, new Vector2(overSize, 3f), Vector2.zero);
        Image lineImg = horizonLine.gameObject.AddComponent<Image>();
        lineImg.color = primaryColor;
        lineImg.raycastTarget = false;

        BuildPitchLadder();
        BuildAircraftSymbol();
    }

    private void BuildPitchLadder()
    {
        pitchPxPerDegree = attitudeRadius / 45f;
        float pxPerDegree = pitchPxPerDegree;
        float[] majorAngles = { 10f, 20f, 30f, -10f, -20f, -30f };

        foreach (float angle in majorAngles)
        {
            float y = angle * pxPerDegree;
            float lineWidth = Mathf.Abs(angle) >= 30f ? attitudeRadius * 0.55f : attitudeRadius * 0.4f;

            RectTransform left = CreateRect($"Pitch_{angle}_L", horizonPivot, new Vector2(lineWidth, 3f), new Vector2(-(lineWidth * 0.5f + attitudeRadius * 0.18f), y));
            Image li = left.gameObject.AddComponent<Image>();
            li.color = primaryColor;
            li.raycastTarget = false;

            RectTransform right = CreateRect($"Pitch_{angle}_R", horizonPivot, new Vector2(lineWidth, 3f), new Vector2(lineWidth * 0.5f + attitudeRadius * 0.18f, y));
            Image ri = right.gameObject.AddComponent<Image>();
            ri.color = primaryColor;
            ri.raycastTarget = false;

            Text leftLabel = CreateText($"Pitch_{angle}_Label_L", horizonPivot, Mathf.Abs(angle).ToString("00"), pitchLadderFontSize, primaryColor, TextAnchor.MiddleCenter);
            SetRect(leftLabel.rectTransform, new Vector2(40f, 20f), new Vector2(-(lineWidth + attitudeRadius * 0.36f), y));

            Text rightLabel = CreateText($"Pitch_{angle}_Label_R", horizonPivot, Mathf.Abs(angle).ToString("00"), pitchLadderFontSize, primaryColor, TextAnchor.MiddleCenter);
            SetRect(rightLabel.rectTransform, new Vector2(40f, 20f), new Vector2(lineWidth + attitudeRadius * 0.36f, y));
        }
    }

    private void BuildAircraftSymbol()
    {
        RectTransform symbol = CreateRect("Aircraft_Symbol", attitudeContainer, new Vector2(attitudeRadius * 1.2f, attitudeRadius * 0.5f), Vector2.zero);

        RectTransform leftWing = CreateRect("AC_LeftWing", symbol, new Vector2(attitudeRadius * 0.45f, 6f), new Vector2(-attitudeRadius * 0.32f, 0f));
        Image lw = leftWing.gameObject.AddComponent<Image>();
        lw.color = yellowColor;
        lw.raycastTarget = false;

        RectTransform rightWing = CreateRect("AC_RightWing", symbol, new Vector2(attitudeRadius * 0.45f, 6f), new Vector2(attitudeRadius * 0.32f, 0f));
        Image rw = rightWing.gameObject.AddComponent<Image>();
        rw.color = yellowColor;
        rw.raycastTarget = false;

        RectTransform center = CreateRect("AC_Center", symbol, new Vector2(10f, 10f), Vector2.zero);
        Image ci = center.gameObject.AddComponent<Image>();
        ci.color = yellowColor;
        ci.raycastTarget = false;
    }

    // -------------------- Roll Scale --------------------
    private void BuildRollScale()
    {
        rollScaleRadius = attitudeRadius * 1.12f;
        float[] majorAngles = { -60f, -45f, -30f, -20f, -10f, 0f, 10f, 20f, 30f, 45f, 60f };

        foreach (float angle in majorAngles)
        {
            bool major = Mathf.Abs(angle) == 0f || Mathf.Abs(angle) == 30f || Mathf.Abs(angle) == 60f || Mathf.Abs(angle) == 45f;
            float tickLength = major ? 16f : 10f;

            float rad = angle * Mathf.Deg2Rad;
            Vector2 outerPos = new Vector2(rollScaleRadius * Mathf.Sin(rad), rollScaleRadius * Mathf.Cos(rad) + AttitudeCenterYOffset);

            RectTransform tick = CreateRect($"RollTick_{angle}", pfdRoot, new Vector2(3f, tickLength), outerPos);
            tick.localRotation = Quaternion.Euler(0f, 0f, -angle);
            Image ti = tick.gameObject.AddComponent<Image>();
            ti.color = primaryColor;
            ti.raycastTarget = false;

            if (major && angle != 0f)
            {
                Vector2 labelPos = new Vector2((rollScaleRadius + 10f) * Mathf.Sin(rad), (rollScaleRadius + 10f) * Mathf.Cos(rad) + AttitudeCenterYOffset);
                Text label = CreateText($"RollLabel_{angle}", pfdRoot, Mathf.Abs(angle).ToString("0"), rollScaleFontSize, primaryColor, TextAnchor.MiddleCenter);
                SetRect(label.rectTransform, new Vector2(30f, 18f), labelPos);
            }
        }

        rollPointer = CreateRect("RollPointer", pfdRoot, new Vector2(16f, 14f), new Vector2(0f, rollScaleRadius - 4f + AttitudeCenterYOffset));
        Image pointerImg = rollPointer.gameObject.AddComponent<Image>();
        pointerImg.sprite = GetTriangleSprite();
        pointerImg.color = yellowColor;
        pointerImg.raycastTarget = false;
    }

    // -------------------- Speed Tape --------------------
    private void BuildSpeedTape()
    {
        speedTapeHeight = pfdSize.y * 0.62f;
        Vector2 size = new Vector2(pfdSize.x * 0.11f, speedTapeHeight);
        Vector2 pos = new Vector2(-(attitudeRadius + size.x * 0.5f + 35f), 0f);

        speedTapeArea = CreateRect("Speed_Tape", pfdRoot, size, pos);
        speedTapeArea.gameObject.AddComponent<RectMask2D>();
        Image bg = speedTapeArea.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        int poolCount = Mathf.CeilToInt(speedTapeHeight / (SpeedStep * SpeedPxPerKnot)) + 3;
        speedLabelPool = new Text[poolCount];
        for (int i = 0; i < poolCount; i++)
        {
            Text t = CreateText($"SpeedLabel_{i}", speedTapeArea, "", tapeLabelFontSize, primaryColor, TextAnchor.MiddleRight);
            SetRect(t.rectTransform, new Vector2(size.x * 0.65f, 24f), Vector2.zero);
            speedLabelPool[i] = t;
        }

        RectTransform box = CreateRect("Speed_Box", pfdRoot, new Vector2(size.x + 14f, 34f), new Vector2(pos.x, 0f));
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0f, 0f, 0f, 0.9f);
        boxImg.raycastTarget = false;
        Outline outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = primaryColor;
        outline.effectDistance = new Vector2(2f, 2f);

        speedBoxText = CreateText("Speed_BoxText", box, "0", valueBoxFontSize, yellowColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(speedBoxText.rectTransform, new Vector2(size.x + 10f, 30f), Vector2.zero);
    }

    // -------------------- Altitude Tape --------------------
    private void BuildAltitudeTape()
    {
        altTapeHeight = pfdSize.y * 0.62f;
        Vector2 size = new Vector2(pfdSize.x * 0.13f, altTapeHeight);
        Vector2 pos = new Vector2(attitudeRadius + GsAreaWidth + size.x * 0.5f, 0f);

        Vector2 groupSize = new Vector2(size.x + 14f, altTapeHeight);
        RectTransform altGroup = CreateRect("Alt_Meter_Group", pfdRoot, groupSize, pos);
        Image groupBg = altGroup.gameObject.AddComponent<Image>();
        groupBg.color = new Color(0f, 0f, 0f, 0.35f);
        groupBg.raycastTarget = false;

        altTapeArea = CreateRect("Alt_Tape", altGroup, size, Vector2.zero);
        altTapeArea.gameObject.AddComponent<RectMask2D>();
        Image bg = altTapeArea.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        int poolCount = Mathf.CeilToInt(altTapeHeight / (AltStep * AltPxPerFoot)) + 3;
        altLabelPool = new Text[poolCount];
        for (int i = 0; i < poolCount; i++)
        {
            Text t = CreateText($"AltLabel_{i}", altTapeArea, "", tapeLabelFontSize, primaryColor, TextAnchor.MiddleLeft);
            SetRect(t.rectTransform, new Vector2(size.x * 0.7f, 24f), Vector2.zero);
            altLabelPool[i] = t;
        }

        RectTransform box = CreateRect("Alt_Box", altGroup, new Vector2(size.x + 14f, 34f), Vector2.zero);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0f, 0f, 0f, 0.9f);
        boxImg.raycastTarget = false;
        Outline outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = primaryColor;
        outline.effectDistance = new Vector2(2f, 2f);

        altBoxText = CreateText("Alt_BoxText", box, "0", valueBoxFontSize, yellowColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(altBoxText.rectTransform, new Vector2(size.x + 10f, 30f), Vector2.zero);
    }

    // -------------------- Heading Tape --------------------
    private void BuildHeadingTape()
    {
        headingTapeWidth = pfdSize.x * 0.5f;
        Vector2 size = new Vector2(headingTapeWidth, pfdSize.y * 0.07f);
        Vector2 pos = new Vector2(0f, -(attitudeRadius + size.y * 0.5f + 10f));

        headingTapeArea = CreateRect("Heading_Tape", pfdRoot, size, pos);
        headingTapeArea.gameObject.AddComponent<RectMask2D>();
        Image bg = headingTapeArea.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        headingPxPerDegree = size.x / 60f;

        int poolCount = Mathf.CeilToInt(size.x / (HeadingStep * headingPxPerDegree)) + 3;
        headingLabelPool = new Text[poolCount];
        for (int i = 0; i < poolCount; i++)
        {
            Text t = CreateText($"HeadingLabel_{i}", headingTapeArea, "", tapeLabelFontSize, primaryColor, TextAnchor.MiddleCenter);
            SetRect(t.rectTransform, new Vector2(50f, size.y - 6f), Vector2.zero);
            headingLabelPool[i] = t;
        }

        RectTransform box = CreateRect("Heading_Box", pfdRoot, new Vector2(56f, size.y + 6f), pos);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0f, 0f, 0f, 0.9f);
        boxImg.raycastTarget = false;
        Outline outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = primaryColor;
        outline.effectDistance = new Vector2(2f, 2f);

        headingBoxText = CreateText("Heading_BoxText", box, "000", valueBoxFontSize, yellowColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(headingBoxText.rectTransform, new Vector2(52f, size.y), Vector2.zero);
    }

    // -------------------- Flight Director / LOC / GS --------------------
    private void BuildFlightDirectorAndILS()
    {
        locHalfRange = attitudeRadius * 0.65f;
        gsHalfHeight = attitudeRadius * 0.65f;

        fdVerticalBar = CreateRect("FD_Vertical", attitudeContainer, new Vector2(4f, attitudeRadius * 1.1f), Vector2.zero);
        Image fdv = fdVerticalBar.gameObject.AddComponent<Image>();
        fdv.color = magentaColor;
        fdv.raycastTarget = false;

        fdHorizontalBar = CreateRect("FD_Horizontal", attitudeContainer, new Vector2(attitudeRadius * 1.1f, 4f), Vector2.zero);
        Image fdh = fdHorizontalBar.gameObject.AddComponent<Image>();
        fdh.color = magentaColor;
        fdh.raycastTarget = false;

        float locY = AttitudeCenterYOffset - (attitudeRadius + 40f);
        RectTransform locScale = CreateRect("LOC_Scale", pfdRoot, new Vector2(locHalfRange * 2f, 4f), new Vector2(0f, locY));
        Image locBg = locScale.gameObject.AddComponent<Image>();
        locBg.color = new Color(1f, 1f, 1f, 0.25f);
        locBg.raycastTarget = false;

        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            RectTransform dot = CreateRect($"LOC_Dot_{i}", pfdRoot, new Vector2(6f, 6f), new Vector2(i * (locHalfRange / 2f), locY));
            Image di = dot.gameObject.AddComponent<Image>();
            di.color = primaryColor;
            di.raycastTarget = false;
        }

        locPointer = CreateRect("LOC_Pointer", pfdRoot, new Vector2(16f, 16f), new Vector2(0f, locY));
        Image locImg = locPointer.gameObject.AddComponent<Image>();
        locImg.color = magentaColor;
        locImg.raycastTarget = false;

        Vector2 gsScalePos = new Vector2(attitudeRadius + GsAreaWidth * 0.5f, AttitudeCenterYOffset);
        RectTransform gsScale = CreateRect("GS_Scale", pfdRoot, new Vector2(4f, gsHalfHeight * 2f), gsScalePos);
        Image gsBg = gsScale.gameObject.AddComponent<Image>();
        gsBg.color = new Color(1f, 1f, 1f, 0.25f);
        gsBg.raycastTarget = false;

        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            RectTransform dot = CreateRect($"GS_Dot_{i}", pfdRoot, new Vector2(6f, 6f), new Vector2(gsScalePos.x, i * (gsHalfHeight / 2f) + AttitudeCenterYOffset));
            Image di = dot.gameObject.AddComponent<Image>();
            di.color = primaryColor;
            di.raycastTarget = false;
        }

        gsPointer = CreateRect("GS_Pointer", pfdRoot, new Vector2(16f, 16f), new Vector2(gsScalePos.x, AttitudeCenterYOffset));
        Image gsImg = gsPointer.gameObject.AddComponent<Image>();
        gsImg.color = magentaColor;
        gsImg.raycastTarget = false;
    }

    // -------------------- Labels --------------------
    private void BuildLabels()
    {
        CreateHeaderLabel("SPD", new Vector2(-(attitudeRadius + pfdSize.x * 0.055f + 35f), speedTapeHeight * 0.5f + 20f), greenColor);
        CreateHeaderLabel("ALT", new Vector2(attitudeRadius + GsAreaWidth + pfdSize.x * 0.13f, altTapeHeight * 0.5f + 20f), greenColor);
        CreateHeaderLabel("HDG", new Vector2(0f, AttitudeCenterYOffset - (attitudeRadius + 45f)), greenColor);
        CreateHeaderLabel("LOC", new Vector2(-(locHalfRange + 30f), AttitudeCenterYOffset - (attitudeRadius + 40f)), magentaColor);
        CreateHeaderLabel("GS", new Vector2(attitudeRadius + GsAreaWidth * 0.5f, gsHalfHeight + 16f + AttitudeCenterYOffset), magentaColor, 32f);

        float topY = pfdSize.y * 0.5f - 20f;
        CreateHeaderLabel("FD", new Vector2(-pfdSize.x * 0.30f, topY), magentaColor);
        CreateHeaderLabel("AP", new Vector2(-pfdSize.x * 0.15f, topY), greenColor);
        CreateHeaderLabel("CAT3", new Vector2(0f, topY), greenColor);
        CreateHeaderLabel("DUAL", new Vector2(pfdSize.x * 0.15f, topY), primaryColor);
        CreateHeaderLabel("QNH", new Vector2(pfdSize.x * 0.30f, topY), primaryColor);
    }

    private void CreateHeaderLabel(string content, Vector2 pos, Color color)
    {
        CreateHeaderLabel(content, pos, color, 80f);
    }

    private void CreateHeaderLabel(string content, Vector2 pos, Color color, float width)
    {
        Text t = CreateText($"Label_{content}", pfdRoot, content, headerLabelFontSize, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(t.rectTransform, new Vector2(width, 20f), pos);
    }

    // ==================================================
    // Update
    // ==================================================
    private void UpdateAltitudeFromSpeed()
    {
        altitude += altimeterSpeed * Time.deltaTime;
    }

    private void UpdateRoll()
    {
        float targetRoll = Mathf.Clamp(rollInput, -1f, 1f) * maxRoll;
        currentAircraftRoll = Mathf.SmoothDampAngle(currentAircraftRoll, targetRoll, ref rollVelocity, smoothDuration);

        float horizonRotation = -currentAircraftRoll;
        horizonPivot.localRotation = Quaternion.Euler(0f, 0f, horizonRotation);

        float clampedForPointer = Mathf.Clamp(currentAircraftRoll, -60f, 60f);
        rollPointer.anchoredPosition = new Vector2(
            rollScaleRadius * Mathf.Sin(clampedForPointer * Mathf.Deg2Rad),
            rollScaleRadius * Mathf.Cos(clampedForPointer * Mathf.Deg2Rad) - 4f + AttitudeCenterYOffset);
    }

    private void UpdateHeadingFromRoll()
    {
        if (!autoTurnWithRoll)
            return;

        float safeSpeed = Mathf.Max(speed, MinSpeedForTurnRate);
        float turnRateDegPerSec = (TurnRateConstant * Mathf.Tan(currentAircraftRoll * Mathf.Deg2Rad)) / safeSpeed;
        heading = Wrap360(heading + turnRateDegPerSec * Time.deltaTime);
    }

    private float GetRollFactor()
    {
        return Mathf.Clamp(currentAircraftRoll / Mathf.Max(maxRoll, 0.0001f), -1f, 1f);
    }

    private void UpdatePitchFromAltitude()
    {
        float targetPitch = 0f;

        if (pitchReactsToAltitude)
        {
            if (hasPreviousDisplayAltitude && Time.deltaTime > 0.0001f)
            {
                float altitudeRateFtPerMin = ((displayAltitude - previousDisplayAltitude) / Time.deltaTime) * 60f;
                float pitchFraction = Mathf.Clamp(altitudeRateFtPerMin / pitchRateReference, -1f, 1f);
                targetPitch = pitchFraction * maxPitchDegrees;
            }
        }

        previousDisplayAltitude = displayAltitude;
        hasPreviousDisplayAltitude = true;

        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, smoothDuration);
        horizonPivot.anchoredPosition = new Vector2(0f, -currentPitch * pitchPxPerDegree);
    }

    private void UpdateSpeedFromRoll()
    {
        float offset = speedReactsToRoll ? GetRollFactor() * speedRollDeviation : 0f;
        actualSpeed = Mathf.Max(0f, speed + offset);
    }

    private void UpdateSpeedTape()
    {
        displaySpeed = Mathf.SmoothDamp(displaySpeed, Mathf.Max(0f, actualSpeed), ref speedVelocity, smoothDuration);

        float nearestBase = Mathf.Round(displaySpeed / SpeedStep) * SpeedStep;
        int half = speedLabelPool.Length / 2;

        for (int i = 0; i < speedLabelPool.Length; i++)
        {
            float labelValue = nearestBase + (i - half) * SpeedStep;
            float y = (labelValue - displaySpeed) * SpeedPxPerKnot;

            Text label = speedLabelPool[i];
            if (labelValue < 0f || y > speedTapeHeight * 0.5f + SpeedStep * SpeedPxPerKnot || y < -(speedTapeHeight * 0.5f + SpeedStep * SpeedPxPerKnot))
            {
                if (!string.IsNullOrEmpty(label.text))
                    label.text = "";
                continue;
            }

            string valueStr = labelValue.ToString("0");
            if (label.text != valueStr)
                label.text = valueStr;

            label.rectTransform.anchoredPosition = new Vector2(label.rectTransform.anchoredPosition.x, y);
        }

        int roundedSpeed = Mathf.RoundToInt(displaySpeed);
        string boxStr = roundedSpeed.ToString();
        if (speedBoxText.text != boxStr)
            speedBoxText.text = boxStr;
    }

    private void UpdateAltitudeTape()
    {
        // Directly target the 'altitude' variable, offset by roll reaction if enabled
        float rollOffset = altitudeReactsToRoll ? GetRollFactor() * altitudeRollDeviation : 0f;
        float altitudeTarget = Mathf.Max(0f, altitude + rollOffset);
        displayAltitude = Mathf.SmoothDamp(displayAltitude, altitudeTarget, ref altVelocity, smoothDuration);

        float nearestBase = Mathf.Round(displayAltitude / AltStep) * AltStep;
        int half = altLabelPool.Length / 2;

        for (int i = 0; i < altLabelPool.Length; i++)
        {
            float labelValue = nearestBase + (i - half) * AltStep;
            float y = (labelValue - displayAltitude) * AltPxPerFoot;

            Text label = altLabelPool[i];
            if (y > altTapeHeight * 0.5f + AltStep * AltPxPerFoot || y < -(altTapeHeight * 0.5f + AltStep * AltPxPerFoot))
            {
                if (!string.IsNullOrEmpty(label.text))
                    label.text = "";
                continue;
            }

            string valueStr = labelValue.ToString("0");
            if (label.text != valueStr)
                label.text = valueStr;

            label.rectTransform.anchoredPosition = new Vector2(label.rectTransform.anchoredPosition.x, y);
        }

        int roundedAlt = Mathf.RoundToInt(displayAltitude);
        string boxStr = roundedAlt.ToString();
        if (altBoxText.text != boxStr)
            altBoxText.text = boxStr;
    }

    private void UpdateHeadingTape()
    {
        displayHeading = Mathf.SmoothDampAngle(displayHeading, heading, ref headingVelocity, smoothDuration);
        float wrappedDisplay = Wrap360(displayHeading);

        float nearestBase = Mathf.Round(wrappedDisplay / HeadingStep) * HeadingStep;
        int half = headingLabelPool.Length / 2;

        for (int i = 0; i < headingLabelPool.Length; i++)
        {
            float labelValue = Wrap360(nearestBase + (i - half) * HeadingStep);
            float diff = ShortestAngleDiff(wrappedDisplay, labelValue);
            float x = diff * headingPxPerDegree;

            Text label = headingLabelPool[i];
            if (x > headingTapeWidth * 0.5f + HeadingStep * headingPxPerDegree || x < -(headingTapeWidth * 0.5f + HeadingStep * headingPxPerDegree))
            {
                if (!string.IsNullOrEmpty(label.text))
                    label.text = "";
                continue;
            }

            int roundedLabelValue = Mathf.RoundToInt(labelValue) % 360;
            string valueStr = roundedLabelValue + "°";
            if (label.text != valueStr)
                label.text = valueStr;

            label.rectTransform.anchoredPosition = new Vector2(x, label.rectTransform.anchoredPosition.y);
        }

        int roundedHeading = Mathf.RoundToInt(wrappedDisplay) % 360;
        string boxStr = roundedHeading + "°";
        if (headingBoxText.text != boxStr)
            headingBoxText.text = boxStr;
    }

    private void UpdateILSAndFD()
    {
        float locClamped = Mathf.Clamp(localizer, -1f, 1f);
        locPointer.anchoredPosition = new Vector2(locClamped * locHalfRange, locPointer.anchoredPosition.y);

        float gsClamped = Mathf.Clamp(glideslope, -1f, 1f);
        gsPointer.anchoredPosition = new Vector2(gsPointer.anchoredPosition.x, gsClamped * gsHalfHeight + AttitudeCenterYOffset);

        fdVerticalBar.anchoredPosition = new Vector2(locClamped * attitudeRadius * 0.5f, 0f);
        fdHorizontalBar.anchoredPosition = new Vector2(0f, gsClamped * attitudeRadius * 0.5f);
    }

    // ==================================================
    // Helpers
    // ==================================================
    private static float Wrap360(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f)
            wrapped += 360f;
        return wrapped;
    }

    private static float ShortestAngleDiff(float from, float to)
    {
        return (to - from + 540f) % 360f - 180f;
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RegisterCreatedObjectForUndo(go);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetRect(rt, size, anchoredPos);
        return rt;
    }

    private void RegisterCreatedObjectForUndo(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Generate A320 PFD");
#endif
    }

    private void SetRect(RectTransform rt, Vector2 size, Vector2 anchoredPos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RegisterCreatedObjectForUndo(go);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = GetDefaultFont();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Sprite cachedCircleSprite;
    private static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null)
            return cachedCircleSprite;

        const int diameter = 256;
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float radius = diameter * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        return cachedCircleSprite;
    }

    private static Sprite cachedTriangleSprite;
    private static Sprite GetTriangleSprite()
    {
        if (cachedTriangleSprite != null)
            return cachedTriangleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float t = (float)y / (size - 1);
            float halfWidth = (size * 0.5f) * t;

            for (int x = 0; x < size; x++)
            {
                float distOutsideEdge = Mathf.Abs(x + 0.5f - size * 0.5f) - halfWidth;
                float alpha = Mathf.Clamp01(0.5f - distOutsideEdge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedTriangleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return cachedTriangleSprite;
    }

    private static Font GetDefaultFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (cachedFont == null)
            cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        return cachedFont;
    }
}