using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class DraggableObject : MonoBehaviour
{
    [System.Serializable]
    public class SnapElement
    {
        public int index;

        public bool unlocknavigationOnSnap = true;

        public GameObject highlightObject;

        public bool restoreToSnapWhenConditionActive = true;

        public UnityEvent OnSnapCompleted;

        [Header("Display Options")]
        [Tooltip("If enabled, first time this index is reached, interaction will be ignored.")]
        public bool enableFirstIgnore = false;

        [HideInInspector]
        public bool hasVisitedOnce = false;

        [HideInInspector]
        public Collider highlightCollider;

        [Header("Debugging Only")]
        [Tooltip("True once snapping is completed. Dragging will be disabled.")]
        public bool snapped;
    }

    [Header("Snap Elements")]
    [SerializeField]
    private List<SnapElement> elements = new List<SnapElement>();

    [Header("Movement")]
    [SerializeField] private float snapSpeed = 8f;
    [SerializeField] private float returnSpeed = 6f;
    [SerializeField] private float snapDistance = 0.01f;

    [Header("Rotation")]
    [SerializeField] private bool snapRotation = false;
    [SerializeField] private float snapRotationThreshold = 0.5f;

    [Header("Mode")]
    [SerializeField] private bool triggerEventOnly = false;

    [Header("Animator Control")]
    [SerializeField] private Animator animator;

    [Header("Drag Events")]
    [SerializeField] private UnityEvent OnDragStart;

    private PageNavigationController pageNavigationController;
    private PersistentAssetController persistentAssetController;

    private Camera mainCam;
    private Collider objectCollider;

    private bool isDragging;
    private bool snapping;
    private bool returning;
    private bool canDrag;
    private bool interactionLocked;

    private int activeElementIndex = -1;
    private int lastSnappedElementIndex = -1;

    private Vector3 offset;
    private float objectScreenZ;

    private Vector3 currentStartPos;
    private Quaternion currentStartRot;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        pageNavigationController =
            FindFirstObjectByType<PageNavigationController>();

        persistentAssetController =
            GetComponent<PersistentAssetController>();

        mainCam = Camera.main;

        if (mainCam == null)
            mainCam = FindFirstObjectByType<Camera>();

        objectCollider = GetComponent<Collider>();

        foreach (var element in elements)
        {
            if (element.highlightObject != null)
            {
                element.highlightCollider =
                    element.highlightObject.GetComponent<Collider>();

                element.highlightObject.SetActive(false);
            }
        }
    }


    // ============================================================
    // ENABLE
    // ============================================================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;

        // IMPORTANT:
        // The object may be enabled AFTER the page has already
        // changed.
        //
        // Therefore, directly check the current page here.
        if (PageNavigationController.CurrentIndex >= 0)
        {
            HandlePageChanged(PageNavigationController.CurrentIndex);
        }
    }


    // ============================================================
    // DISABLE
    // ============================================================

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;

        isDragging = false;
        snapping = false;
        returning = false;
        canDrag = false;
        interactionLocked = true;
    }


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        // Extra safety check.
        //
        // If this object was enabled before the page controller
        // finished initializing, check the page again here.

        if (PageNavigationController.CurrentIndex >= 0)
        {
            HandlePageChanged(PageNavigationController.CurrentIndex);
        }
    }


    // ============================================================
    // PAGE CHANGED
    // ============================================================

    private void HandlePageChanged(int pageIndex)
    {
        ResetState();

        activeElementIndex = -1;

        // --------------------------------------------------------
        // Find the Snap Element matching the current page index
        // --------------------------------------------------------

        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].index == pageIndex)
            {
                ActivateElement(i);
                return;
            }
        }

        // --------------------------------------------------------
        // No matching index
        // --------------------------------------------------------

        canDrag = false;
        interactionLocked = true;
    }


    // ============================================================
    // RESET STATE
    // ============================================================

    private void ResetState()
    {
        isDragging = false;
        snapping = false;
        returning = false;
    }


    // ============================================================
    // ACTIVATE ELEMENT
    // ============================================================

    private void ActivateElement(int index)
    {
        if (index < 0 || index >= elements.Count)
            return;

        activeElementIndex = index;

        interactionLocked = false;

        SnapElement element = elements[index];


        // ========================================================
        // IMPORTANT INDEX CHECK
        // ========================================================
        //
        // The GameObject itself can be enabled on page 38.
        //
        // When OnEnable() runs, the current page is checked.
        //
        // If:
        //
        // element.index = 38
        //
        // and:
        //
        // CurrentIndex = 38
        //
        // this element becomes draggable.
        //
        // ========================================================

        if (element.index != PageNavigationController.CurrentIndex)
        {
            canDrag = false;
            interactionLocked = true;
            return;
        }


        // ========================================================
        // FIRST IGNORE
        // ========================================================

        if (element.enableFirstIgnore &&
            !element.hasVisitedOnce)
        {
            element.hasVisitedOnce = true;

            canDrag = false;
            interactionLocked = true;

            return;
        }


        // Mark this index as visited

        element.hasVisitedOnce = true;


        // ========================================================
        // CHECK SNAPPED STATE
        // ========================================================

        if (element.snapped)
        {
            canDrag = false;
            interactionLocked = true;
        }
        else
        {
            // ====================================================
            // OBJECT IS DRAGGABLE
            // ====================================================

            canDrag = true;
            interactionLocked = false;
        }


        // ========================================================
        // RESTORE SNAPPED POSITION
        // ========================================================

        if (element.restoreToSnapWhenConditionActive &&
            element.snapped &&
            element.highlightObject != null)
        {
            Transform target =
                element.highlightObject.transform;

            transform.position =
                target.position;

            if (snapRotation)
            {
                transform.rotation =
                    target.rotation;
            }
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // Return
        // --------------------------------------------------------

        if (returning)
        {
            ReturnToLastValidPosition();
            return;
        }


        // --------------------------------------------------------
        // Snapping
        // --------------------------------------------------------

        if (!triggerEventOnly && snapping)
        {
            SnapToHighlight();
            return;
        }


        // --------------------------------------------------------
        // Drag Check
        // --------------------------------------------------------

        if (!canDrag || interactionLocked)
            return;


        HandleInput();
    }


    // ============================================================
    // HANDLE INPUT
    // ============================================================

    private void HandleInput()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }


        // Mouse Down

        if (Input.GetMouseButtonDown(0))
        {
            TryStartDrag(Input.mousePosition);
        }


        // Drag

        if (isDragging &&
            Input.GetMouseButton(0))
        {
            Drag(Input.mousePosition);
        }


        // Mouse Up

        if (isDragging &&
            Input.GetMouseButtonUp(0))
        {
            Release();
        }
    }


    // ============================================================
    // TRY START DRAG
    // ============================================================

    private void TryStartDrag(Vector3 inputPos)
    {
        if (activeElementIndex < 0)
            return;


        SnapElement element =
            elements[activeElementIndex];


        if (element.snapped)
            return;


        // --------------------------------------------------------
        // Double-check current page
        // --------------------------------------------------------

        if (element.index != PageNavigationController.CurrentIndex)
            return;


        Ray ray =
            mainCam.ScreenPointToRay(inputPos);

        RaycastHit hit;


        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider == objectCollider)
            {
                isDragging = true;

                currentStartPos =
                    transform.position;

                currentStartRot =
                    transform.rotation;


                // ------------------------------------------------
                // Drag Start Event
                // ------------------------------------------------

                OnDragStart?.Invoke();


                // ------------------------------------------------
                // Disable Animator
                // ------------------------------------------------

                if (animator != null &&
                    animator.enabled)
                {
                    animator.enabled = false;
                }


                // ------------------------------------------------
                // Screen Z
                // ------------------------------------------------

                objectScreenZ =
                    mainCam.WorldToScreenPoint(
                        transform.position
                    ).z;


                // ------------------------------------------------
                // Offset
                // ------------------------------------------------

                offset =
                    transform.position -
                    GetWorldPosition(inputPos);


                // ------------------------------------------------
                // Show Highlight
                // ------------------------------------------------

                if (element.highlightObject != null)
                {
                    element.highlightObject.SetActive(true);
                }
            }
        }
    }


    // ============================================================
    // DRAG
    // ============================================================

    private void Drag(Vector3 inputPos)
    {
        transform.position =
            GetWorldPosition(inputPos) + offset;
    }


    // ============================================================
    // RELEASE
    // ============================================================

    private void Release()
    {
        if (!isDragging)
            return;

        isDragging = false;


        if (activeElementIndex < 0)
        {
            StartReturn();
            EnableAnimator();
            return;
        }


        SnapElement element =
            elements[activeElementIndex];


        if (element.highlightCollider == null)
        {
            StartReturn();
            EnableAnimator();
            return;
        }


        // --------------------------------------------------------
        // Check overlap
        // --------------------------------------------------------

        bool inside =
            objectCollider.bounds.Intersects(
                element.highlightCollider.bounds
            );


        // ========================================================
        // TRIGGER EVENT ONLY
        // ========================================================

        if (triggerEventOnly)
        {
            if (inside)
            {
                if (!element.snapped)
                {
                    FinalizeSnap(element);
                }

                EnableAnimator();
            }
            else
            {
                StartReturn();
            }

            return;
        }


        // ========================================================
        // NORMAL SNAP
        // ========================================================

        if (inside && !element.snapped)
        {
            snapping = true;
        }
        else
        {
            StartReturn();
        }
    }


    // ============================================================
    // SNAP TO HIGHLIGHT
    // ============================================================

    private void SnapToHighlight()
    {
        if (activeElementIndex < 0)
        {
            StartReturn();
            return;
        }


        SnapElement element =
            elements[activeElementIndex];


        if (element.highlightObject == null)
        {
            StartReturn();
            return;
        }


        Transform target =
            element.highlightObject.transform;


        // --------------------------------------------------------
        // Position
        // --------------------------------------------------------

        transform.position =
            Vector3.Lerp(
                transform.position,
                target.position,
                snapSpeed * Time.deltaTime
            );


        // --------------------------------------------------------
        // Rotation
        // --------------------------------------------------------

        if (snapRotation)
        {
            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    target.rotation,
                    snapSpeed * Time.deltaTime
                );
        }


        // --------------------------------------------------------
        // Snap complete
        // --------------------------------------------------------

        if (Vector3.Distance(
                transform.position,
                target.position
            ) < snapDistance)
        {
            transform.position =
                target.position;


            if (snapRotation)
            {
                transform.rotation =
                    target.rotation;
            }


            snapping = false;

            FinalizeSnap(element);

            EnableAnimator();
        }
    }


    // ============================================================
    // FINALIZE SNAP
    // ============================================================

    private void FinalizeSnap(SnapElement element)
    {
        element.snapped = true;

        lastSnappedElementIndex =
            activeElementIndex;


        // --------------------------------------------------------
        // Disable dragging
        // --------------------------------------------------------

        canDrag = false;
        interactionLocked = true;


        // --------------------------------------------------------
        // Hide highlight
        // --------------------------------------------------------

        if (element.highlightObject != null)
        {
            element.highlightObject.SetActive(false);
        }


        // ========================================================
        // UPDATE PERSISTENT TRANSFORM
        // ========================================================

        if (persistentAssetController != null)
        {
            persistentAssetController.UpdatePageTransform(
                element.index,
                transform.localPosition,
                transform.localEulerAngles,
                transform.localScale
            );
        }


        // ========================================================
        // SNAP COMPLETED EVENT
        // ========================================================

        element.OnSnapCompleted?.Invoke();


        // ========================================================
        // ENABLE NAVIGATION
        // ========================================================

        if (pageNavigationController != null &&
            element.unlocknavigationOnSnap)
        {
            pageNavigationController.EnableNavigationButtons();
        }
    }


    // ============================================================
    // START RETURN
    // ============================================================

    private void StartReturn()
    {
        returning = true;


        if (activeElementIndex >= 0)
        {
            SnapElement element =
                elements[activeElementIndex];


            if (element.highlightObject != null)
            {
                element.highlightObject.SetActive(false);
            }
        }
    }


    // ============================================================
    // RETURN TO LAST VALID POSITION
    // ============================================================

    private void ReturnToLastValidPosition()
    {
        Vector3 targetPos =
            currentStartPos;


        // --------------------------------------------------------
        // Position
        // --------------------------------------------------------

        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPos,
                returnSpeed * Time.deltaTime
            );


        // --------------------------------------------------------
        // Rotation
        // --------------------------------------------------------

        if (snapRotation)
        {
            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    currentStartRot,
                    returnSpeed * Time.deltaTime
                );
        }


        // --------------------------------------------------------
        // Return complete
        // --------------------------------------------------------

        if (Vector3.Distance(
                transform.position,
                targetPos
            ) < snapDistance)
        {
            transform.position =
                targetPos;


            if (snapRotation)
            {
                transform.rotation =
                    currentStartRot;
            }


            returning = false;

            EnableAnimator();
        }
    }


    // ============================================================
    // ENABLE ANIMATOR
    // ============================================================

    private void EnableAnimator()
    {
        if (animator != null &&
            !animator.enabled)
        {
            animator.enabled = true;
        }
    }


    // ============================================================
    // GET WORLD POSITION
    // ============================================================

    private Vector3 GetWorldPosition(Vector3 inputPos)
    {
        inputPos.z = objectScreenZ;

        return mainCam.ScreenToWorldPoint(inputPos);
    }
}