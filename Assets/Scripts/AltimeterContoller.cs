// A320PFD.cs
//
// Generates a complete A320-style Primary Flight Display entirely at runtime - no manually
// created UI objects required. Attach to any empty GameObject and press Play.
//
// Everything (Canvas, attitude indicator with sky/ground/horizon/pitch ladder, roll scale,
// fixed aircraft symbol, speed tape, altitude tape, heading tape, vertical speed indicator,
// flight director bars, localizer/glideslope diamonds, and A320-style labels) is built once and
// then driven every frame purely by updating cached RectTransform/Text references - no
// hierarchy searches, no per-frame instantiation.
//
// PERSISTING THE GENERATED UI: pressing Play and letting Awake() generate it is a temporary
// PREVIEW only - Unity always discards anything created during Play Mode the moment Play stops,
// and no script can override that. To make the generated PFD a permanent part of the scene that
// you can hand-edit afterward, use this component's context menu (gear icon in the Inspector, or
// right-click the component header) -> "Bake PFD To Scene", while NOT in Play Mode. That builds
// the exact same hierarchy directly into the Edit Mode scene (fully Undo-able), marks the scene
// dirty, and sets Is Baked so this component will never regenerate or touch it again - from then
// on the generated Canvas/Text/Image objects are ordinary scene objects you can reposition,
// resize, or restyle by hand like anything else.
//
// ROLL CONVENTION (per spec): Roll Input +1 -> Aircraft Roll +30 deg (rolling right) ->
// Horizon Rotation -30 deg (horizon visually tilts left under the fixed aircraft symbol).
// Aircraft Roll and Horizon Rotation are always exact negatives of each other; only the
// AIRCRAFT ROLL value is smoothed (via Mathf.SmoothDampAngle), and Horizon Rotation is derived
// from it every frame, so both stay perfectly in sync with the same smoothing curve.

using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class A320PFD : MonoBehaviour
{
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
    [Tooltip("Baseline/commanded airspeed. This value is never modified by the script - what the tape actually shows is this plus a bounded roll-reaction offset (see Speed Dynamics below), so this number always stays exactly what you typed.")]
    public float speed = 140f;
    [Tooltip("Baseline/commanded altitude. This value is never modified by the script - what the tape actually shows is this plus Vertical Speed's effect (if enabled) plus a bounded roll-reaction offset (see Altitude Dynamics below), so this number always stays exactly what you typed instead of drifting on its own.")]
    public float altitude = 5000f;
    [Range(0f, 359.99f)] public float heading = 270f;
    [Tooltip("Defaults to 0 so Altitude holds still until you actively command a climb/descent - set this away from 0 (with Auto Altitude From VS on below) to make the aircraft actually climb or descend, exactly like a real aircraft: altitude only changes because vertical speed is nonzero, not on its own.")]
    public float verticalSpeed = 0f;

    [Tooltip("When on, Heading is no longer a free value you set directly - holding a bank angle continuously turns the aircraft, exactly like a real coordinated turn (rate of turn depends on both bank angle and airspeed). Turn off to control Heading manually again, e.g. for testing the heading tape in isolation.")]
    public bool autoTurnWithRoll = true;

    [Header("Speed Dynamics")]
    [Tooltip("When on, the displayed speed is Speed above plus a bounded offset that follows Roll Input's sign and magnitude directly: rolling toward +1 pushes speed up toward Speed + Speed Roll Deviation, rolling toward -1 pushes it down toward Speed - Speed Roll Deviation, and it settles back to exactly Speed at Roll Input 0. The offset can never exceed +/- Speed Roll Deviation, however long you hold the roll.")]
    public bool speedReactsToRoll = true;

    [Tooltip("Maximum speed deviation (knots) in either direction, reached at full +/-1 Roll Input.")]
    [Min(0f)] public float speedRollDeviation = 15f;

    [Header("Altitude Dynamics")]
    [Tooltip("When on, the displayed altitude is the running baseline (Altitude above, advanced over time by Vertical Speed if Auto Altitude From VS is on) plus a bounded offset that follows Roll Input's sign and magnitude directly, the same way Speed Dynamics works above.")]
    public bool altitudeReactsToRoll = true;

    [Tooltip("Maximum altitude deviation (feet) in either direction, reached at full +/-1 Roll Input.")]
    [Min(0f)] public float altitudeRollDeviation = 10f;

    [Tooltip("When on, the altitude baseline continuously integrates Vertical Speed over time, exactly like a real aircraft (altitude IS the running total of climb/descent rate, not an independent number). With Vertical Speed at 0 (the default) the baseline simply holds still - it only moves once you actively dial in a climb/descent rate. Turn off to keep the baseline pinned exactly to the Altitude field above (e.g. for testing the altitude tape/roll deviation in isolation).")]
    public bool autoAltitudeFromVS = true;

    [Header("Pitch Dynamics")]
    [Tooltip("When on, the attitude indicator's horizon visibly moves up/down (a real pitch-up/pitch-down movement, not just the roll rotation) driven by the altitude tape's own actual current climb/descent RATE - whatever is causing altitude to move (roll reaction, Vertical Speed integration, or you typing a new Altitude directly) makes the horizon react, since pitch reads altitude's behavior rather than reading Roll Input directly.")]
    public bool pitchReactsToAltitude = true;

    [Tooltip("Maximum visible pitch (degrees), reached once altitude is changing at Pitch Rate Reference (ft/min) or faster.")]
    [Min(0f)] public float maxPitchDegrees = 15f;

    [Tooltip("Climb/descent rate (ft/min) that produces the full Max Pitch Degrees. Smaller = pitch reaches its max sooner (more sensitive); larger = a faster climb/descent is needed before pitch maxes out.")]
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
    [Tooltip("Leave empty to generate a full-screen overlay Canvas (default, previous behavior). Assign a RectTransform (e.g. a panel already under an existing Canvas) to generate the PFD as a child of it instead - no new Canvas is created, and everything is clipped strictly to its own bounds so it can never draw outside that parent.")]
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
    [Tooltip("SPD/ALT/HDG/V-S/LOC/GS/FD/AP/CAT3/DUAL/QNH header labels.")]
    [Min(1)] public int headerLabelFontSize = 15;
    [Tooltip("Scrolling numbers on the speed/altitude/heading tapes.")]
    [Min(1)] public int tapeLabelFontSize = 20;
    [Tooltip("The highlighted current-value box on the speed/altitude/heading tapes.")]
    [Min(1)] public int valueBoxFontSize = 24;
    [Tooltip("Pitch ladder numbers (10/20/30) inside the attitude indicator.")]
    [Min(1)] public int pitchLadderFontSize = 16;
    [Tooltip("Roll scale numbers (10/20/30/45/60) above the attitude indicator.")]
    [Min(1)] public int rollScaleFontSize = 13;
    [Tooltip("Numbers on the vertical speed indicator's fixed scale.")]
    [Min(1)] public int vsiScaleFontSize = 13;
    [Tooltip("The numeric vertical speed readout (e.g. +500) below the VSI.")]
    [Min(1)] public int vsiValueFontSize = 16;

    // ==================================================
    // Bake state
    // ==================================================
    [Header("Bake")]
    [Tooltip("True once this PFD has been baked into permanent scene objects (via the 'Bake PFD To Scene' context menu action, in Edit Mode). While true, this component will never regenerate/rebuild the hierarchy - it only drives the existing baked objects using the reference fields below, which Unity serializes normally like any other Inspector reference. Runtime-only Play Mode testing (no bake) still works exactly as before and is discarded on Stop, same as any other Unity behavior - that discard-on-stop is a Unity engine rule with no script-level workaround, which is exactly why baking has to happen in Edit Mode.")]
    [SerializeField] private bool isBaked = false;

    // ==================================================
    // Runtime-generated references (cached, never searched for). [SerializeField] so that once
    // baked, these keep pointing at the correct scene objects across domain reloads/Play Mode
    // transitions without ever needing to re-search the hierarchy.
    // ==================================================
    [HideInInspector] [SerializeField] private Canvas canvas;
    [HideInInspector] [SerializeField] private RectTransform pfdRoot;

    [HideInInspector] [SerializeField] private RectTransform attitudeContainer;
    [HideInInspector] [SerializeField] private RectTransform horizonPivot;
    [HideInInspector] [SerializeField] private RectTransform rollPointer;
    [HideInInspector] [SerializeField] private float attitudeRadius;
    [HideInInspector] [SerializeField] private float rollScaleRadius;
    [HideInInspector] [SerializeField] private float pitchPxPerDegree;

    // The attitude indicator (and everything anchored to it: roll scale, LOC, GS) is shifted
    // down from pfdRoot's center by this much, leaving clear headroom above the sphere for the
    // roll scale arc so it never overlaps/crowds the top of the pitch ladder.
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

    // Reserved horizontal space to the right of the attitude sphere for the glideslope scale,
    // so the altitude tape/VSI never overlap it.
    private const float GsAreaWidth = 70f;

    // Heading tape
    [HideInInspector] [SerializeField] private RectTransform headingTapeArea;
    [HideInInspector] [SerializeField] private Text[] headingLabelPool;
    [HideInInspector] [SerializeField] private Text headingBoxText;
    [HideInInspector] [SerializeField] private float headingTapeWidth;
    [HideInInspector] [SerializeField] private float headingPxPerDegree;
    private const float HeadingStep = 10f;

    // VSI
    [HideInInspector] [SerializeField] private RectTransform vsiPointer;
    [HideInInspector] [SerializeField] private Text vsiValueText;
    [HideInInspector] [SerializeField] private float vsiScaleHalfHeight;
    private const float VsiMaxRange = 3000f;

    // Turn dynamics - standard aviation rate-of-turn approximation:
    // deg/sec = (1091 * tan(bank)) / TAS(knots). This is the same relationship real aircraft
    // (and real PFDs) follow: a steeper bank turns faster, but the SAME bank turns slower at
    // higher airspeed - using the existing Speed field as TAS gives physically-consistent
    // behavior "for free" instead of a made-up constant turn rate.
    private const float TurnRateConstant = 1091f;
    private const float MinSpeedForTurnRate = 20f; // avoids a divide-by-near-zero blowing up the turn rate at very low/zero speed

    // FD / LOC / GS
    [HideInInspector] [SerializeField] private RectTransform fdVerticalBar;
    [HideInInspector] [SerializeField] private RectTransform fdHorizontalBar;
    [HideInInspector] [SerializeField] private RectTransform locPointer;
    [HideInInspector] [SerializeField] private RectTransform gsPointer;
    [HideInInspector] [SerializeField] private float locHalfRange;
    [HideInInspector] [SerializeField] private float gsHalfHeight;

    /// <summary>The actual, already-smoothed aircraft roll angle (degrees) this frame - the same value the horizon's rotation and roll pointer are driven from. Exposed so other scripts (e.g. a 3D aircraft model rotator) can match the PFD's attitude exactly instead of independently re-deriving their own roll from the gyro with different scaling/smoothing, which would drift out of sync with what the instrument shows.</summary>
    public float CurrentAircraftRoll => currentAircraftRoll;

    // Smoothing state - serialized too, so a baked PFD's tapes/roll don't reset to 0 and jump
    // on the next domain reload; harmless to serialize even for non-baked runtime-only use.
    [HideInInspector] [SerializeField] private float currentAircraftRoll;
    [HideInInspector] [SerializeField] private float rollVelocity;
    [HideInInspector] [SerializeField] private float currentPitch;
    [HideInInspector] [SerializeField] private float pitchVelocity;
    [HideInInspector] [SerializeField] private float previousDisplayAltitude;
    [HideInInspector] [SerializeField] private bool hasPreviousDisplayAltitude;
    [HideInInspector] [SerializeField] private float actualSpeed; // Speed + the current bounded roll-reaction offset
    [HideInInspector] [SerializeField] private float displaySpeed;
    [HideInInspector] [SerializeField] private float speedVelocity;
    [HideInInspector] [SerializeField] private float altitudeBaseline; // advances via Vertical Speed; the public Altitude field itself is never touched
    [HideInInspector] [SerializeField] private float displayAltitude;
    [HideInInspector] [SerializeField] private float altVelocity;
    [HideInInspector] [SerializeField] private float displayHeading;
    [HideInInspector] [SerializeField] private float headingVelocity;
    [HideInInspector] [SerializeField] private float displayVS;
    [HideInInspector] [SerializeField] private float vsVelocity;

    private static Font cachedFont;

    // ==================================================
    // Lifecycle
    // ==================================================
    private void Awake()
    {
        if (isBaked)
        {
            // Already baked (in Edit Mode, or a previous Play session) - pfdRoot and every other
            // reference field above already point at the existing, permanent scene hierarchy via
            // normal Unity serialization. Nothing to (re)generate - Update() drives it directly.
            return;
        }

        actualSpeed = speed;
        displaySpeed = speed;
        altitudeBaseline = altitude;
        displayAltitude = altitude;
        displayHeading = heading;
        displayVS = verticalSpeed;

        BuildPFD();

        // NOTE: this only marks the in-memory Play Mode instance as built, so repeated Awake()
        // calls within the same session don't double-generate. It does NOT persist past Stop -
        // Unity always discards anything created during Play when Play Mode ends, with no
        // script-level way around that. Real persistence requires baking in Edit Mode - see
        // BakeToScene() below.
        isBaked = true;
    }

    private void Update()
    {
        if (pfdRoot == null)
            return;

        UpdateRoll();
        UpdateHeadingFromRoll();
        UpdateSpeedFromRoll();
        UpdateAltitudeFromVS();
        UpdateSpeedTape();
        UpdateAltitudeTape();
        UpdatePitchFromAltitude();
        UpdateHeadingTape();
        UpdateVSI();
        UpdateILSAndFD();
    }

#if UNITY_EDITOR
    [ContextMenu("Bake PFD To Scene")]
    private void BakeToScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[A320PFD] Bake must be run in Edit Mode, not Play Mode - Unity always discards anything created during Play when you stop, with no way around that from a script. Exit Play Mode, then use 'Bake PFD To Scene' again from this component's context menu.", this);
            return;
        }

        if (isBaked)
        {
            Debug.LogWarning("[A320PFD] Already baked - this GameObject's PFD is already a permanent part of the scene. If you want to regenerate from scratch, delete the generated hierarchy under it by hand first, then untick Is Baked before baking again.", this);
            return;
        }

        actualSpeed = speed;
        displaySpeed = speed;
        altitudeBaseline = altitude;
        displayAltitude = altitude;
        displayHeading = heading;
        displayVS = verticalSpeed;

        BuildPFD();
        isBaked = true;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log("[A320PFD] Baked - the generated PFD is now a permanent part of the scene (Ctrl+S to save it). You can freely edit/reposition/resize any of the generated objects by hand from now on; this component will not regenerate or touch them again.", this);
    }
#endif

    private void OnDestroy()
    {
        if (isBaked)
            return; // baked objects are a permanent, independent part of the scene now - this
                     // component no longer owns their lifecycle, even if it's removed/deleted.

        // Own-Canvas mode: destroying the Canvas takes pfdRoot with it. Parented mode: no Canvas
        // was created, so pfdRoot itself is the thing to clean up (without touching the parent).
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
        BuildVSI();
        BuildFlightDirectorAndILS();
        BuildLabels();
    }

    private void BuildCanvas()
    {
        // If a parent container was assigned, the PFD builds as a child of it instead - no new
        // Canvas is created (the parent is assumed to already live under one), and canvas stays
        // null so OnDestroy() knows to clean up pfdRoot directly instead.
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

        // Guarantees nothing generated below can ever render outside pfdRoot's own rect,
        // regardless of whether it ended up under a full-screen Canvas or a small parent panel -
        // this is what makes "only generate inside the parent" actually enforced rather than
        // just a matter of where things happen to be positioned.
        pfdRoot.gameObject.AddComponent<RectMask2D>();
    }

    // -------------------- Attitude Indicator --------------------
    private void BuildAttitudeIndicator()
    {
        attitudeRadius = Mathf.Min(pfdSize.x, pfdSize.y) * 0.30f;
        Vector2 size = new Vector2(attitudeRadius * 2f, attitudeRadius * 2f);

        attitudeContainer = CreateRect("Attitude_Container", pfdRoot, size, new Vector2(0f, AttitudeCenterYOffset));

        // RectMask2D only clips to a RECTANGLE - a genuinely round instrument face needs an
        // alpha-shaped Mask instead: an Image using a procedurally generated circle sprite,
        // combined with a Mask component that clips children to wherever that sprite is opaque.
        // showMaskGraphic = true so this same circle also doubles as the visible black bezel.
        Image frame = attitudeContainer.gameObject.AddComponent<Image>();
        frame.sprite = GetCircleSprite();
        frame.color = new Color(0f, 0f, 0f, 1f);
        frame.raycastTarget = false;
        Mask mask = attitudeContainer.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // Oversized so rotation never reveals an edge.
        float overSize = size.x * 2.2f;

        horizonPivot = CreateRect("Horizon_Pivot", attitudeContainer, new Vector2(overSize, overSize), Vector2.zero);

        // Sky's bottom edge and Ground's top edge both sit exactly at y=0 (the horizon) with no
        // overlap - each panel is full oversized height, offset by exactly half its own height,
        // so they meet edge-to-edge instead of one covering the other.
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

        // Fixed aircraft reference symbol - child of the container (NOT the pivot), so it never
        // rotates and always stays centered regardless of roll.
        BuildAircraftSymbol();
    }

    private void BuildPitchLadder()
    {
        // attitudeRadius/25 previously put the 30 deg rung at 1.2x the container's half-height -
        // outside the RectMask2D clip area, so it (and its label) never actually rendered.
        // attitudeRadius/45 keeps the 30 deg rung comfortably inside the visible window. Stored
        // as a field (not just a local) so Update() can move horizonPivot using this exact same
        // scale - a given pitch angle shifts the horizon by exactly as many pixels as its own
        // ladder rung sits at, so the ladder rung and the fixed aircraft symbol line up correctly
        // once actual pitch reaches that angle.
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

            // Mirrored on the right side too, matching a real attitude indicator where both ends
            // of every pitch rung carry a number, not just the left.
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
        // 1.12x so the arc sits clearly above the sphere's top edge without crowding it, and
        // (combined with the narrower +10 label offset below, instead of +20) without the far
        // "60" labels reaching out sideways far enough to collide with the speed tape / GS
        // scale / altitude group next to them.
        rollScaleRadius = attitudeRadius * 1.12f;
        float[] majorAngles = { -60f, -45f, -30f, -20f, -10f, 0f, 10f, 20f, 30f, 45f, 60f };

        foreach (float angle in majorAngles)
        {
            bool major = Mathf.Abs(angle) == 0f || Mathf.Abs(angle) == 30f || Mathf.Abs(angle) == 60f || Mathf.Abs(angle) == 45f;
            float tickLength = major ? 16f : 10f;

            float rad = angle * Mathf.Deg2Rad;
            Vector2 outerPos = new Vector2(rollScaleRadius * Mathf.Sin(rad), rollScaleRadius * Mathf.Cos(rad) + AttitudeCenterYOffset);

            // Parented to pfdRoot (NOT attitudeContainer) - the attitude indicator's RectMask2D
            // is a SQUARE clip, so ticks/labels near the top of the round-looking arc were
            // getting cut off right at the mask edge. Y is offset by AttitudeCenterYOffset to
            // stay aligned with the attitude indicator's actual (shifted-down) center.
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

        // 16x14 (wider than tall) so the triangle reads as a clear wedge rather than looking
        // squashed - and positioned a bit further out (-4 instead of -8) so the apex actually
        // touches down near the tick marks instead of floating above them.
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
        // 35 (was 8) - the roll scale's "60" labels reach out sideways past the sphere, so a
        // small gap here left them crowded right up against the tape with no breathing room.
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

        // The reference box is wider than the tape (size.x + 14) but the same height range fits
        // inside the tape's own height, so the exact union bounding box of tape + box is
        // (tape width + 14, tape height). This group is a single empty parent sized to exactly
        // that bound, with its own background - the tape and box are children of it (at local
        // zero, since the group itself is centered at the same point they used to share as
        // independent siblings), so the whole altitude meter is one grouped unit instead of two
        // coincidentally-overlapping objects.
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

    // -------------------- Vertical Speed Indicator --------------------
    private void BuildVSI()
    {
        Vector2 size = new Vector2(pfdSize.x * 0.06f, pfdSize.y * 0.5f);
        Vector2 pos = new Vector2(attitudeRadius + GsAreaWidth + pfdSize.x * 0.13f + size.x * 0.5f + 12f, 0f);
        vsiScaleHalfHeight = size.y * 0.5f;

        RectTransform vsiArea = CreateRect("VSI_Area", pfdRoot, size, pos);
        Image bg = vsiArea.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        bg.raycastTarget = false;

        float[] scaleValues = { -3000f, -2000f, -1000f, 0f, 1000f, 2000f, 3000f };
        foreach (float v in scaleValues)
        {
            float y = (v / VsiMaxRange) * vsiScaleHalfHeight;
            RectTransform tick = CreateRect($"VSI_Tick_{v}", vsiArea, new Vector2(size.x * 0.4f, 2f), new Vector2(-size.x * 0.15f, y));
            Image ti = tick.gameObject.AddComponent<Image>();
            ti.color = primaryColor;
            ti.raycastTarget = false;

            Text label = CreateText($"VSI_Label_{v}", vsiArea, (Mathf.Abs(v) / 1000f).ToString("0"), vsiScaleFontSize, primaryColor, TextAnchor.MiddleRight);
            SetRect(label.rectTransform, new Vector2(size.x * 0.55f, 16f), new Vector2(size.x * 0.28f, y));
        }

        vsiPointer = CreateRect("VSI_Pointer", vsiArea, new Vector2(size.x * 0.5f, 4f), new Vector2(0f, 0f));
        Image pImg = vsiPointer.gameObject.AddComponent<Image>();
        pImg.color = greenColor;
        pImg.raycastTarget = false;

        RectTransform box = CreateRect("VSI_Box", pfdRoot, new Vector2(size.x + 10f, 26f), new Vector2(pos.x, -(size.y * 0.5f + 20f)));
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0f, 0f, 0f, 0.85f);
        boxImg.raycastTarget = false;

        vsiValueText = CreateText("VSI_ValueText", box, "+000", vsiValueFontSize, greenColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(vsiValueText.rectTransform, new Vector2(size.x + 6f, 22f), Vector2.zero);
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

        // Localizer scale (horizontal, below attitude indicator). Y offset by
        // AttitudeCenterYOffset to stay aligned under the (shifted-down) attitude indicator.
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

        // Glideslope scale (vertical, right of attitude indicator, left of altitude tape).
        // Kept within GsAreaWidth of the attitude sphere so it never collides with the alt tape.
        // Y offset by AttitudeCenterYOffset to stay vertically centered on the attitude indicator.
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
        CreateHeaderLabel("V/S", new Vector2(attitudeRadius + GsAreaWidth + pfdSize.x * 0.13f + pfdSize.x * 0.06f + 12f, pfdSize.y * 0.25f + 20f), greenColor);
        CreateHeaderLabel("LOC", new Vector2(-(locHalfRange + 30f), AttitudeCenterYOffset - (attitudeRadius + 40f)), whiteOrMagentaForLoc());
        // Narrow (32px, was 80px) and centered directly ABOVE the GS scale rather than beside
        // it - the wide default label box was extending past the GS scale's own footprint and
        // bleeding into the altitude group's left edge.
        CreateHeaderLabel("GS", new Vector2(attitudeRadius + GsAreaWidth * 0.5f, gsHalfHeight + 16f + AttitudeCenterYOffset), whiteOrMagentaForLoc(), 32f);

        // Top status row - FD / AP / CAT3 / DUAL / QNH.
        float topY = pfdSize.y * 0.5f - 20f;
        CreateHeaderLabel("FD", new Vector2(-pfdSize.x * 0.30f, topY), magentaColor);
        CreateHeaderLabel("AP", new Vector2(-pfdSize.x * 0.15f, topY), greenColor);
        CreateHeaderLabel("CAT3", new Vector2(0f, topY), greenColor);
        CreateHeaderLabel("DUAL", new Vector2(pfdSize.x * 0.15f, topY), primaryColor);
        CreateHeaderLabel("QNH", new Vector2(pfdSize.x * 0.30f, topY), primaryColor);
    }

    private Color whiteOrMagentaForLoc()
    {
        return magentaColor;
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

    // Continuously turns the aircraft while banked, using the actual smoothed bank angle (so the
    // turn ramps in/out exactly as the roll animation does) - holding +30 deg keeps heading
    // climbing every frame in real time, exactly like a real coordinated turn, until the bank is
    // released back toward level.
    private void UpdateHeadingFromRoll()
    {
        if (!autoTurnWithRoll)
            return;

        float safeSpeed = Mathf.Max(speed, MinSpeedForTurnRate);
        float turnRateDegPerSec = (TurnRateConstant * Mathf.Tan(currentAircraftRoll * Mathf.Deg2Rad)) / safeSpeed;
        heading = Wrap360(heading + turnRateDegPerSec * Time.deltaTime);
    }

    // Normalized -1..+1 roll direction/magnitude, based on the actual smoothed bank (so it ramps
    // in/out exactly as the visible roll animation does) rather than the raw Roll Input slider -
    // shared by both the speed and altitude roll-reaction below so they move together.
    private float GetRollFactor()
    {
        return Mathf.Clamp(currentAircraftRoll / Mathf.Max(maxRoll, 0.0001f), -1f, 1f);
    }

    // Moves the horizon vertically (a genuine pitch-up/pitch-down, distinct from horizonPivot's
    // roll ROTATION) driven by the altitude tape's own actual current rate of change - NOT by
    // reading Roll Input/currentAircraftRoll directly. Must run AFTER UpdateAltitudeTape() each
    // frame so displayAltitude already reflects this frame's value. Measuring the real frame-to-
    // frame delta of displayAltitude (rather than re-deriving it from roll) means pitch responds
    // correctly no matter WHAT is currently moving altitude - the roll-reaction offset, Vertical
    // Speed integration, or even a manual Altitude edit - exactly like a real aircraft's pitch
    // reflects its actual climb/descent rate, not a specific control input.
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

    // Speed is Speed (the baseline, never modified) plus a bounded offset that follows roll
    // direction directly: +roll -> speed rises toward Speed + Speed Roll Deviation, -roll ->
    // speed falls toward Speed - Speed Roll Deviation, wings level -> settles back to exactly
    // Speed. The offset can never exceed +/- Speed Roll Deviation no matter how long the roll is
    // held, since it's computed fresh from the current roll factor every frame rather than
    // accumulated over time.
    private void UpdateSpeedFromRoll()
    {
        float offset = speedReactsToRoll ? GetRollFactor() * speedRollDeviation : 0f;
        actualSpeed = Mathf.Max(0f, speed + offset);
    }

    // Altitude has two independent, additive pieces, neither of which ever touches the public
    // Altitude field itself:
    // 1. altitudeBaseline - the running integral of Vertical Speed over time (real climb/descent,
    //    unbounded by design - that's genuinely how altitude works). At Vertical Speed 0 (the
    //    default) this simply never changes.
    // 2. A bounded roll-reaction offset, same shape as speed's above: +roll -> altitude rises
    //    toward baseline + Altitude Roll Deviation, -roll -> falls toward baseline - Altitude
    //    Roll Deviation, wings level -> settles back to exactly the baseline. Capped at
    //    +/- Altitude Roll Deviation regardless of how long the roll is held.
    private void UpdateAltitudeFromVS()
    {
        if (autoAltitudeFromVS)
            altitudeBaseline = Mathf.Max(0f, altitudeBaseline + (verticalSpeed / 60f) * Time.deltaTime);
        else
            altitudeBaseline = altitude; // manual mode - baseline tracks whatever you type into Altitude
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
        float rollOffset = altitudeReactsToRoll ? GetRollFactor() * altitudeRollDeviation : 0f;
        float altitudeTarget = Mathf.Max(0f, altitudeBaseline + rollOffset);
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

            // Natural digit count (no 3-digit zero-padding) plus a degree symbol - "30" not
            // "030", "5" not "005", but "300" still shows all three digits since that's its
            // actual value, not padding.
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

    private void UpdateVSI()
    {
        displayVS = Mathf.SmoothDamp(displayVS, verticalSpeed, ref vsVelocity, smoothDuration);

        float clamped = Mathf.Clamp(displayVS, -VsiMaxRange, VsiMaxRange);
        float y = (clamped / VsiMaxRange) * vsiScaleHalfHeight;
        vsiPointer.anchoredPosition = new Vector2(vsiPointer.anchoredPosition.x, y);

        int rounded = Mathf.RoundToInt(displayVS);
        string sign = rounded >= 0 ? "+" : "-";
        string valueStr = sign + Mathf.Abs(rounded).ToString("000");
        if (vsiValueText.text != valueStr)
            vsiValueText.text = valueStr;
    }

    private void UpdateILSAndFD()
    {
        float locClamped = Mathf.Clamp(localizer, -1f, 1f);
        locPointer.anchoredPosition = new Vector2(locClamped * locHalfRange, locPointer.anchoredPosition.y);

        float gsClamped = Mathf.Clamp(glideslope, -1f, 1f);
        gsPointer.anchoredPosition = new Vector2(gsPointer.anchoredPosition.x, gsClamped * gsHalfHeight + AttitudeCenterYOffset);

        // Flight director bars driven from the same ILS deviation signals - fully wireable to
        // real FD command values later by replacing these two lines.
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
        float diff = (to - from + 540f) % 360f - 180f;
        return diff;
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

    // In Edit Mode (i.e. during an Editor Bake), every generated object is registered with the
    // Undo system as it's created, so the entire bake is a single Ctrl+Z-able action instead of
    // leaving behind objects Undo doesn't know about. No-op during Play Mode (Undo doesn't apply
    // there, and UnityEditor isn't compiled into device builds at all).
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

    // Procedurally generates a single filled circle sprite (once, cached/reused for any size via
    // RectTransform scaling - Image stretches it to fill whatever rect it's on). A ~1.5px soft
    // alpha edge keeps it from looking jagged/pixelated when scaled up to the instrument's size.
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

    // Procedurally generates a downward-pointing triangle/wedge sprite for the roll pointer,
    // matching a real bank-angle pointer instead of a plain square. Texture2D.SetPixel treats
    // y=0 as the BOTTOM of the image, and a UI Image's sprite renders with V=0 at the bottom of
    // its RectTransform - so putting the apex at y=0 here makes it point down (toward the roll
    // scale ticks below the pointer) once rendered.
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
            // t=0 at the bottom (apex, zero width), t=1 at the top (full width base).
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