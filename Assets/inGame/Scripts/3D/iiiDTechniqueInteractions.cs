using Meta.WitAi.Dictation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;
using static HandRayInteraction;
using static Oculus.Interaction.Context;

public class iiiDTechniqueInteractions : MonoBehaviour
{

    public static iiiDTechniqueInteractions Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            return;
        }
    }
    // --- Public References ---
    public bool WantFilter = false;
    public bool wantxyz = false;

    [Header("Public References")]
    public OVRHand hand;
    public OVRSkeleton skeleton;
    public Transform palm;
    public LayerMask targetLayer;

    [Header("Gaze Interaction")]
    [SerializeField] private OVREyeGaze LeyeGaze;
    [SerializeField] private OVREyeGaze ReyeGaze;

    // --- OneEuroFilter ---
    [Header("One Euro Filter")]
    public float filterMinCutoff = 0.01f; // Increase for less delay
    public float filterBeta = 1f; // Higher means less lag
    public float filterDCutoff = 1.0f;
    private OneEuroFilter[] positionFilters;
    private OneEuroFilter[] directionFilters;

    [Header("Cursor Settings")]
    public GameObject cursorPrefab;
    public float cursorSize = 0.02f;
    public float offset = 0.01f;
    public Color cursorNormalColor = Color.white;
    public Color cursorHoverColor = Color.cyan;
    public Color cursorPinchColor = Color.green;

    [Header("Calibration")]
    [Tooltip("Offset to apply to X axis (negative moves left, positive moves right)")]
    public float calibrationOffsetX = -0.01f;
    [Tooltip("Offset to apply to Y axis (negative moves down, positive moves up)")]
    public float calibrationOffsetY = 0f;
    [Tooltip("Weight bias between left and right eye (0.5 is equal, <0.5 favors left eye, >0.5 favors right eye)")]
    [Range(0.0f, 1.0f)]
    public float eyeWeightBias = 0.4f;


    // --- Public Configuration ---
    [Header("Configuration")]
    public float rayDistance = 10f;
    public Color pinchColor = Color.green;

    // --- Private State Variables ---
    private Transform palmTransform;
    private LineRenderer lineRenderer;
    private GameObject selectedObject = null;
    private GameObject lastHitObject = null;
    private GameObject maskObject = null;
    private GameObject currentlyAdjustingObject = null;

    // --- Private Rendering & Color Management ---
    private Renderer lastHitRenderer = null;
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

    // --- Private Raycasting & Movement ---
    private Vector3 lastRayPosition;

    // --- Private Interaction States ---
    private bool isPinching = false;
    private bool block = false;
    private bool pinch = false;
    private bool release = false;
    private bool isPinchingActive = false;
    private bool adjusting = false;
    private bool rot = false;

    // --- Other Variables ---
    private int count = 2;

    [Header("Translation")]
    public float smoothSpeed = 20f; // Adjust speed as needed
    public float smoothFactor = 0.01f;
    public float handMovementMultiplier = 2.5f; // Adjust this value to control speed
    public float LNDMovementMultiplier = 2.5f; // Adjust this value to control speed
    public float GPMovementMultiplier = 2.5f; // Adjust this value to control speed
    private Vector3 initialPalmPosition;
    private LineRenderer handToObjectLine;
    private Vector3 initialRayEndPoint;
    private float initialPinchZDistance;
    private Vector3 initialPinchPosition;
    private Vector3 initialFingerTipPosition;
    private Transform indexTip;
    private float initialIndexTipZ;
    private GameObject selectedTarget = null;
    private bool isMovingTarget = false;
    private Vector3 initialHitPoint1;
    private Vector3 initialTargetPosition;
    private Vector3 targetPosition;

    [Header("Rescaling")]
    public float adjustscaleFactor = 0.02f; // Adjust scale factor as needed
    public float scaleFactor = 0.5f;
    public float childScaleFactor = 1.0f;
    public float AdjusthandMovementMultiplier = 1.5f; // Adjust this value to control speed
    public float minScaleThreshold = 0.05f; // Smaller minimum scale threshold
    private float handMovementThreshold = 0.01f; // Ignore very small hand movements
    public float xAxisScaleSensitivity = 0.1f;  // Decrease this value to slow down X scaling
    public float yAxisScaleSensitivity = 0.1f;  // Decrease this value to slow down Y scaling
    public float zAxisScaleSensitivity = 0.1f;  // Decrease this value to slow down Z scaling
    public float transparencyValue = 0.5f; // Alpha value when object is translucent
    private bool isTransparentMode = false;
    private float doublePinchTimeThreshold = 0.3f; // Time window for double pinch detection
    private float lastPinchTime = 0f;
    private Material[] originalMaterials;
    private bool[] originalCollidersState;
    private Collider[] objectColliders;
    private int pinchCount = 0;
    private float pinchCountResetTime = 0.5f;
    private GameObject lastHighlightedObject = null;
    private bool waitingForSecondPinch = false;
    private float lastPinchReleaseTime = 0f;
    private bool wasLastPinchOnTarget = false;
    private GameObject lastPinchedObject;
    private bool isPinchHoldActive = false;
    private GameObject pinnedHandleObject = null;

    [Header("Rotation")]
    public float rotationSpeed = 2.0f;
    public float rotationSmoothFactor = 0.1f;
    public GameObject rotationAngleText;
    public TextMeshProUGUI xAngleText;
    public TextMeshProUGUI yAngleText;
    public TextMeshProUGUI zAngleText;
    public float colliderThickness = 0.05f; // Added: thickness for mesh colliders
    private bool isRotating = false;
    private GameObject currentRotationHandle = null;
    private Vector3 initialHandPosition;
    private Quaternion initialRotation;
    private Vector3 initialRayPosition;
    private Vector3 currentEulerAngles;
    private Transform indexTipForRotation;
    private float initialIndexTipPosition;
    private Vector3 angularVelocity = Vector3.zero;
    private string currentRotationAxis = "";
    private bool isRotationActive = false;
    private GameObject rotHandleObject = null;
    private Vector3 pinchStartPoint; // NEW: Store initial pinch point
    private Vector3 pinchEndPoint;   // NEW: Store end pinch point
    private Quaternion[] originalHandleRotations;
    private GameObject[] rotationHandles;


    bool rotpinch = false;
    private GameObject selectedRotObject = null; // Store the Rot object

    private Vector3 initialRayDirection; // Store initial ray direction when pinching starts
    private bool isFirstMove = true; // Track first movement

    private GameObject parent = null;

    private Vector3 initialObjectPosition;
    private Quaternion initialObjectRotation;
    private float initialAngle = 0f;

    private Vector3 lineStartPoint;
    private LineRenderer currentLineRenderer;
    private GameObject currentLine;
    private bool isDrawingLine = false;
    private bool canDraw = false;

    private GameObject draggedCircle = null;
    private bool isDragging = false;
    private GameObject fixedCircle = null; // Track which circle is fixed


    [Header("Object Spawn")]
    public GameObject squarePrefab;    // Assign Square prefab in Inspector
    public GameObject circlePrefab;    // Assign Circle prefab in Inspector
    public GameObject trianglePrefab;  // Assign Triangle prefab in Inspector
    public GameObject capsulePrefab;   // Assign Capsule prefab in Inspector
    public GameObject hexagonPrefab;   // Assign Capsule prefab in Inspector
    private GameObject selectedPrefabType = null; // Variable to track which prefab to spawn
    private bool canSpawnPrefab = false;
    private bool isSpawningPrefab = false;
    private GameObject currentPrefab = null;
    private Vector3 prefabStartPoint;
    private GameObject previewSphere; // Sphere at start point
    private GameObject endSphere; // Sphere at end point
    private GameObject previewMask; // Mask that follows the ray
    private bool isOverShapeSelector = false; // Track if we're over a shape selector

    [Header("Line Drawing")]
    private GameObject selectedLine = null;
    private bool isMovingLine = false;
    private Vector3 initialHitPoint;
    private Vector3 initialParentPosition;
    private Vector3[] initialLinePositions;
    private bool wasPinchingLastFrame = false;
    private bool pinchingOnLine = false;
    private GameObject selectedLineForCircles = null;
    private Dictionary<GameObject, GameObject> circleToAnchorMap = new Dictionary<GameObject, GameObject>();// List to track which lines are attached to which anchors
    private Dictionary<GameObject, Vector3> anchorToOffsetMap = new Dictionary<GameObject, Vector3>();

    [Header("Free Hand Drawing")]
    public List<GameObject> handlePrefabs = new List<GameObject>(); // The prefabs you want to use as handles
    public float handleDistance = 0.5f; // Distance from object where handles will be placed
    public GameObject rotationHandlePrefab; // The prefab for rotation handle
    public float rotationHandleOffset = 0.2f; // Extra distance for rotation handle above Y handle 
    public List<GameObject> anchorPrefabs = new List<GameObject>();
    public float anchorDistance = 2.0f;
    private List<GameObject> activeHandles = new List<GameObject>(); // Keeps track of created handles
    private bool freeHandDrawingMode = false;
    private List<GameObject> drawingPoints = new List<GameObject>();
    private LineRenderer drawingLine;
    private GameObject currentDrawing;
    private float minDistanceBetweenPoints = 0.05f; // Minimum distance required between points
    private float closeShapeThreshold = 0.1f; // Distance threshold for closing a shape
    private Vector3 lastPointPosition; // To track the last point's position


    [Header("Multi-Select")]
    private bool multiSelectMode = false;
    private GameObject selectionMask;
    private Vector3 selectionStartPosition;
    private List<GameObject> selectedObjects = new List<GameObject>();
    private bool istarget = false;
    private bool isSelectionMaskActive = false;

    [Header("Destroy Object")]
    private GameObject DestroyObject = null;
    private bool isDestroy = false;
    private bool DoDestroy = false;
    private bool ButtonPressed = false;

    [Header("Panels")]
    public GameObject colorPanels; // Reference to the color object
    public GameObject ShapesPanels;

    [Header("Color Picker")]
    private bool isColorCopyMode = false;
    private Color copiedColor = Color.white;
    private Color FinalColor = Color.white;
    private Color InitialColor = Color.white;
    private GameObject selectcolObject;
    private bool selectCol = false;

    [Header("UI Scaling Settings")]
    [SerializeField] private float scaleModifier = 0.05f; // Adjust this in inspector
    public GameObject BGcollider;
    private GameObject selectedUIElement = null;
    private bool isScalingUI = false;
    private Vector3 initialHitPointUI;
    private Vector3 initialParentScale;
    private Vector3 initialRayDirection1;

    [Header("Mask")]
    private GameObject CurentObj = null;
    private bool selectobj = false;

    // Cursor references
    [Header("Cursor")]
    private GameObject cursor;
    private MeshRenderer cursorRenderer;

    //Update references
    private enum InteractionMode { HandRay, EyeGaze, GazePinch }
    private InteractionMode currentMode = InteractionMode.HandRay;

    [Header("Speech to Text")]
    public DictationService dictationService;
    [SerializeField] private bool appendText = true; // Set to false if you want to replace text instead of append
    [SerializeField] private bool resetTextOnPinch = true; // New option to control text reset behavior
    [SerializeField] private float pinchThresholdActivation = 0.8f; // Threshold for pinch detection (0-1)
    [SerializeField] private float pinchThresholdDeactivation = 0.6f; // Threshold for unpinch detection (0-1)
    public TMP_InputField textInputField;
    private GameObject selectedTextObject;
    private bool dictationActive = false;
    private string lastFullTranscription = "";
    private string currentTextBase = "";
    private bool isProcessingFullTranscription = false;
    private bool isMicActive = false;  // Track if mic is currently active
    private bool wasPinching = false;  // Track the previous pinch state

    [HideInInspector] public Vector3 filteredRayOrigin, filteredRayDirection;
    [HideInInspector] public bool isHoldingNow;
    //---------------------- initialize functions ----------------------

    private void SetupLineRenderer()
    {
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.002f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.yellow;
        lineRenderer.sortingOrder = 10;
    }

    private void InitializePalm()
    {
        if (skeleton != null)
        {
            palmTransform = palm;
        }
    }
   
    private void InitializeFilters()
    {
        positionFilters = new OneEuroFilter[3];
        directionFilters = new OneEuroFilter[3];

        for (int i = 0; i < 3; i++)
        {
            positionFilters[i] = new OneEuroFilter(filterMinCutoff, filterBeta, filterDCutoff);
            directionFilters[i] = new OneEuroFilter(filterMinCutoff, filterBeta, filterDCutoff);
        }
    }

    private void CreateCursor()
    {
        if (cursorPrefab != null)
        {
            cursor = Instantiate(cursorPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            // Create a simple sphere cursor if no prefab is provided
            cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider collider = cursor.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider); // Remove collider to avoid physics interactions
            }
        }

        if (cursor == null)
        {
            Debug.LogError("EyeGazeCursor: Failed to create cursor object!");
            enabled = false;
            return;
        }

        // Set initial size and appearance
        cursor.transform.localScale = new Vector3(cursorSize, cursorSize, cursorSize);
        cursorRenderer = cursor.GetComponent<MeshRenderer>();
        if (cursorRenderer == null)
        {
            Debug.LogError("EyeGazeCursor: Cursor object does not have a MeshRenderer component!");
            enabled = false;
            return;
        }

        // Create and assign material
        Material cursorMaterial = null;

        // Try Universal Render Pipeline first
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader != null)
        {
            cursorMaterial = new Material(urpShader);
        }
        else
        {
            // Fallback to standard shader
            cursorMaterial = new Material(Shader.Find("Standard"));

            // If standard shader is not available, try sprites
            if (cursorMaterial == null)
            {
                cursorMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        // Final fallback
        if (cursorMaterial == null)
        {
            cursorMaterial = new Material(Shader.Find("Mobile/Diffuse"));
        }

        // Assign material and color
        if (cursorMaterial != null)
        {
            cursorRenderer.material = cursorMaterial;
            cursorRenderer.material.color = cursorNormalColor;
        }
        else
        {
            Debug.LogError("EyeGazeCursor: Failed to create cursor material!");
        }
    }


    //---------------------- Unity Functions ----------------------
    void Start()
    {
        SetupRotationUI();
        SetupLineRenderer();
        InitializeFilters();
        InitializePalm();
        CreateCursor();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {

            cursor.SetActive(false);
            if (lineRenderer != null) lineRenderer.enabled = true;
            currentMode = InteractionMode.HandRay;
        }
        if (Input.GetKeyDown(KeyCode.W))
        {

            cursor.SetActive(true);
            if (lineRenderer != null) lineRenderer.enabled = false;
            currentMode = InteractionMode.GazePinch;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            cursor.SetActive(true);
            if (lineRenderer != null) lineRenderer.enabled = false;
            currentMode = InteractionMode.EyeGaze;
        }

        //if (HandRayInteraction.Instance.isPerspectiveActive == true) return;

        if (cursor == null || cursorRenderer == null || hand == null || palmTransform == null) return;

        // Check if eye tracking is available when needed
        if (cursor == null || cursorRenderer == null ||
                   LeyeGaze == null || ReyeGaze == null || hand == null ||
                   !LeyeGaze.EyeTrackingEnabled || !ReyeGaze.EyeTrackingEnabled) return;


        // Common variables
        float currentTime = Time.time;
        isHoldingNow = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);


        //currentMode = (InteractionMode)HandRayInteraction.Instance.currentMode;


        // Process the active interaction method
        if (currentMode == InteractionMode.EyeGaze)
        {
            Vector3 leftEyePos = LeyeGaze.transform.position;
            Vector3 rightEyePos = ReyeGaze.transform.position;
            Quaternion leftEyeRot = LeyeGaze.transform.rotation;
            Quaternion rightEyeRot = ReyeGaze.transform.rotation;

            // Use weighted average between eyes
            Vector3 rayOrigin = Vector3.Lerp(leftEyePos, rightEyePos, eyeWeightBias);
            Quaternion weightedRotation = Quaternion.Slerp(leftEyeRot, rightEyeRot, eyeWeightBias);
            Vector3 rawRayDirection = weightedRotation * Vector3.forward;

            if (!WantFilter)
            {
                filteredRayOrigin = rayOrigin;
                filteredRayDirection = rawRayDirection;
            }
            else
            {
                filteredRayOrigin = new Vector3(
                    positionFilters[0].Filter(rayOrigin.x, currentTime),
                    positionFilters[1].Filter(rayOrigin.y, currentTime),
                    positionFilters[2].Filter(rayOrigin.z, currentTime)
                );

                filteredRayDirection = new Vector3(
                    directionFilters[0].Filter(rawRayDirection.x, currentTime),
                    directionFilters[1].Filter(rawRayDirection.y, currentTime),
                    directionFilters[2].Filter(rawRayDirection.z, currentTime)
                ).normalized;
            }

            // Apply One Euro Filter to smooth eye gaze tracking

            // Apply automatic calibration during calibration mode or use stored values
            filteredRayDirection = ApplyCalibrationToDirection(filteredRayDirection);
            UpdateCursor(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        }
        else if (currentMode == InteractionMode.GazePinch)
        {
            Vector3 leftEyePos = LeyeGaze.transform.position;
            Vector3 rightEyePos = ReyeGaze.transform.position;
            Quaternion leftEyeRot = LeyeGaze.transform.rotation;
            Quaternion rightEyeRot = ReyeGaze.transform.rotation;

            // Use weighted average between eyes
            Vector3 rayOrigin = Vector3.Lerp(leftEyePos, rightEyePos, eyeWeightBias);
            Quaternion weightedRotation = Quaternion.Slerp(leftEyeRot, rightEyeRot, eyeWeightBias);
            Vector3 rawRayDirection = weightedRotation * Vector3.forward;

            if (!WantFilter)
            {
                filteredRayOrigin = rayOrigin;
                filteredRayDirection = rawRayDirection;
            }
            else
            {
                filteredRayOrigin = new Vector3(
                    positionFilters[0].Filter(rayOrigin.x, currentTime),
                    positionFilters[1].Filter(rayOrigin.y, currentTime),
                    positionFilters[2].Filter(rayOrigin.z, currentTime)
                );

                filteredRayDirection = new Vector3(
                    directionFilters[0].Filter(rawRayDirection.x, currentTime),
                    directionFilters[1].Filter(rawRayDirection.y, currentTime),
                    directionFilters[2].Filter(rawRayDirection.z, currentTime)
                ).normalized;
            }
            // Apply automatic calibration during calibration mode or use stored values
            filteredRayDirection = ApplyCalibrationToDirection(filteredRayDirection);
            UpdateCursor(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        }
        else
        {
            // Hand ra9y tracking
            Vector3 rayOrigin = palmTransform.position;
            Vector3 rawRayDirection = palmTransform.forward;

            // Apply One Euro Filter
            filteredRayOrigin = rayOrigin;

            filteredRayDirection = new Vector3(
                directionFilters[0].Filter(rawRayDirection.x, currentTime),
                directionFilters[1].Filter(rawRayDirection.y, currentTime),
                directionFilters[2].Filter(rawRayDirection.z, currentTime)
            ).normalized;

            UpdateLineRenderer(rayOrigin, filteredRayDirection);
        }
        CheckForModeSwitching(filteredRayOrigin, filteredRayDirection, isHoldingNow);

        TargetDrag(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        AdjustParentSizeOnPinch3D(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        HandleObjectRotation(filteredRayOrigin, filteredRayDirection, isHoldingNow);

        PrefabSpawn(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        ButtonFeedback(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        DestroyObj(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        ChangeColor2(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        ScaleParentOnDrag(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        MultiSelectDetection(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        LineDraw(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        // Reset states when gaze interaction stops
        if (!isHoldingNow)
        {
            selectedObject = null;
            lastHitObject = null;
            if (lastHitRenderer != null)
            {
                lastHitRenderer.material.color = copiedColor;
            }
            lastHitRenderer = null;
            isPinching = false;
            block = false;
            release = false;
            adjusting = false;
            isPinchingActive = false;
            adjusting = false;
            rot = false;
            isMovingTarget = false;
        }
    }

    //---------------------- calibration ----------------------

    private void CheckForModeSwitching(Vector3 origin, Vector3 direction, bool isHoldingNow)
    {
        if (block || rot || adjusting || isMovingLine || isColorCopyMode || multiSelectMode || isSelectionMaskActive) return;
        ;
        RaycastHit2D hit2D = getRaycast2d(origin, direction);

        if (hit2D.collider != null)
        {
            // Switch to eye gaze when hitting a LooknDrop object while in HandRay mode
            if ((currentMode == InteractionMode.HandRay || currentMode == InteractionMode.EyeGaze) && hit2D.collider.CompareTag("GazePinch"))
            {
                if (isHoldingNow)
                {
                    currentMode = InteractionMode.GazePinch;
                    cursor.SetActive(true);
                    lineRenderer.enabled = false;
                    Debug.Log("Switched to Eye Gaze mode");
                }
            }
            // Switch to eye gaze when hitting a LooknDrop object while in HandRay mode
            if ((currentMode == InteractionMode.HandRay || currentMode == InteractionMode.GazePinch) && hit2D.collider.CompareTag("LooknDrop"))
            {
                if (isHoldingNow)
                {
                    currentMode = InteractionMode.EyeGaze;
                    cursor.SetActive(true);
                    lineRenderer.enabled = false;
                    Debug.Log("Switched to Eye Gaze mode");
                }
            }
            // Switch to hand ray when hitting a HandRay object while in EyeGaze mode
            else if ((currentMode == InteractionMode.EyeGaze || currentMode == InteractionMode.GazePinch) && hit2D.collider.CompareTag("HandRay"))
            {
                if (isHoldingNow)
                {
                    currentMode = InteractionMode.HandRay;
                    cursor.SetActive(false);
                    lineRenderer.enabled = true;
                    Debug.Log("Switched to Hand Ray mode");
                }
            }
        }
    }

    private Vector3 ApplyCalibrationToDirection(Vector3 direction)
    {
        // Create a rotation to adjust the direction slightly
        // For small angles, we can approximate using direct vector addition
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(right, direction).normalized;

        // Add the calibration offsets (scaled by some factor to make them subtle)
        Vector3 adjustedDirection = direction + (right * calibrationOffsetX) + (up * calibrationOffsetY);

        // Ensure we maintain a unit vector
        return adjustedDirection.normalized;
    }

    //---------------------- Cursor ----------------------

    private void UpdateCursor(Vector3 origin, Vector3 direction, bool isPinching)
    {
        if (origin == Vector3.zero || direction == Vector3.zero) return;

        // Check for 3D object hit
        RaycastHit hit3D;
        bool hit3DObject = Physics.Raycast(origin, direction, out hit3D, Mathf.Infinity);

        // Check for 2D object hit
        Vector3 endPoint = origin + (direction * rayDistance);
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, 0.01f, Vector2.zero, 0);
        RaycastHit2D hit2D1 = Physics2D.CircleCast(endPoint, 0.01f, Vector2.zero, 0, targetLayer);
        Vector3 hitPoint = new Vector3(hit3D.point.x, hit3D.point.y, hit3D.point.z - 0.01f);
        // Position the cursor
        if (hit3DObject)
        {
            // Hit a 3D object
            cursor.transform.position = hitPoint;
            rayDistance = hit3D.distance;

            // Update cursor color based on interaction state
            if (isPinching)
            {
                cursorRenderer.material.color = cursorPinchColor;
            }
            else
            {
                cursorRenderer.material.color = cursorHoverColor;
            }
        }
        else if (hit2D.collider != null)
        {
            // Hit a 2D object
            cursor.transform.position = hit2D.point;

            // Update cursor color based on interaction state
            if (isPinching)
            {
                cursorRenderer.material.color = cursorPinchColor;
            }
            else
            {
                cursorRenderer.material.color = cursorHoverColor;
            }
        }
        else if (hit2D1.collider != null)
        {
            // Hit a 2D object
            cursor.transform.position = hitPoint;

            // Update cursor color based on interaction state
            if (isPinching)
            {
                cursorRenderer.material.color = cursorPinchColor;
            }
            else
            {
                cursorRenderer.material.color = cursorHoverColor;
            }
        }
        else
        {
            // No hit, position cursor at end of ray
            cursor.transform.position = endPoint;
            cursorRenderer.material.color = cursorNormalColor;
        }

        // Face cursor toward the camera
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cursor.transform.LookAt(mainCamera.transform);
        }
    }

    //---------------------- Line Renderer ----------------------

    private void UpdateLineRenderer(Vector3 origin, Vector3 direction)
    {
        if (origin == Vector3.zero || direction == Vector3.zero) return;

        RaycastHit hit3D;
        if (Physics.Raycast(origin, direction, out hit3D, Mathf.Infinity) &&
            (hit3D.collider.CompareTag("Target") || 
            hit3D.collider.CompareTag("Canvas") || 
            hit3D.collider.CompareTag("Finish") ||
             IsScalingHandle(hit3D.collider.gameObject) ||
            hit3D.collider.CompareTag("XRot") ||
            hit3D.collider.CompareTag("YRot") ||
            hit3D.collider.CompareTag("ZRot") ||
            hit3D.collider.CompareTag("Rot")))
        {
            Vector3 endPoint = hit3D.point;
            rayDistance = hit3D.distance;
            lineRenderer.startColor = Color.green;
            lineRenderer.endColor = Color.yellow;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }
        else
        {
            rayDistance = 10;
            Vector3 endPoint = origin + (direction * rayDistance);
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.yellow;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }
    }

    //---------------------- Ray ----------------------

    private bool GetRaycastPoint(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 hitPoint)
    {
        hitPoint = rayOrigin;
        RaycastHit hit3D;

        if (Physics.Raycast(rayOrigin, rayDirection, out hit3D, rayDistance))
        {
            hitPoint = hit3D.point;
            return true;
        }
        return false;
    }

    private RaycastHit2D getRaycast2d(Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) ;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);

        return hit2D;
    }

    //---------------------- Traslation ----------------------

    private void TargetDrag(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin) && !isMovingTarget) return;


        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        RaycastHit hit;
        bool hasHit = Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance);
        // Handle EyeGaze or HandRay modes
        Vector3 moveVelocity = Vector3.zero; // Add this to your class-level variables

        if (currentMode == InteractionMode.HandRay)
        {
            // Step 1: Detect and initiate drag
            if (hasHit && hit.collider.CompareTag("Target") && !isDragging)
            {
                GameObject hitTarget = hit.collider.gameObject;
                GameObject parentTarget = hitTarget.transform.parent != null ? hitTarget.transform.parent.gameObject : hitTarget;

                if (isHoldingNow && !isMovingTarget)
                {
                    selectedTarget = parentTarget;
                    isMovingTarget = true;
                    block = true;

                    // Store initial positions
                    initialPalmPosition = palm.position;
                    initialTargetPosition = selectedTarget.transform.position;

                    Debug.Log("Started dragging target: " + selectedTarget.name);
                }
            }

            // Step 2: While dragging
            if (isMovingTarget && selectedTarget != null && isHoldingNow)
            {
                // Only continue moving if still hitting the selected target
                if (hasHit && hit.collider != null)
                {
                    GameObject currentHit = hit.collider.transform.root.gameObject;

                    if (currentHit != selectedTarget)
                        return;
                }
                else
                {
                    return; // Not hitting anything anymore
                }

                // Use ray endpoint for X/Y anchoring
                Vector3 rayBasedPosition = endPoint;
                // Calculate hand movement delta
                Vector3 handMovementDelta = palm.position - initialPalmPosition;

                // Apply movement multiplier
                handMovementDelta *= handMovementMultiplier;
                // Z offset based on palm movement
                float zDelta = (palm.position.z - initialPalmPosition.z) * handMovementMultiplier;

                Vector3 targetPosition = new Vector3(
                    rayBasedPosition.x,
                    rayBasedPosition.y,
                    initialTargetPosition.z + zDelta
                );

                // Apply smooth movement
                selectedTarget.transform.position = Vector3.SmoothDamp(
                    selectedTarget.transform.position,
                    targetPosition,
                    ref moveVelocity,
                    smoothFactor // Tweak this for snappiness/smoothness
                );
            }

            // Step 3: End dragging
            if (!isHoldingNow && isMovingTarget)
            {
                initialPalmPosition = palm.position;
                initialTargetPosition = selectedTarget.transform.position;

                isMovingTarget = false;
                selectedTarget = null;

                if (maskObject != null)
                    maskObject.SetActive(false);

                Debug.Log("Stopped dragging target");
            }
        }
        if (currentMode == InteractionMode.EyeGaze)
        {
            // Step 1: Detect object under gaze to begin dragging
            if (!isMovingTarget && hasHit && hit.collider.CompareTag("Target") && !isDragging)
            {
                GameObject hitTarget = hit.collider.gameObject;
                GameObject parentTarget = hitTarget.transform.parent != null ? hitTarget.transform.parent.gameObject : hitTarget;
                if (isHoldingNow) // This likely checks if pinch is active
                {
                    selectedTarget = parentTarget;
                    block = true;
                    // Find index fingertip bone
                    foreach (var bone in skeleton.Bones)
                    {
                        if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                        {
                            indexTip = bone.Transform;
                            break;
                        }
                    }
                    if (indexTip != null)
                        initialIndexTipZ = indexTip.position.z;
                    initialTargetPosition = selectedTarget.transform.position;
                    initialRayEndPoint = rayOrigin + (rayDirection * rayDistance); // Save initial ray position
                    isMovingTarget = true;
                    Debug.Log("Started dragging target: " + selectedTarget.name);
                }
            }
            // Step 2: While dragging - UPDATED to continue as long as pinch is held
            else if (isMovingTarget && selectedTarget != null)
            {
                // Check if still pinching
                if (isHoldingNow)
                {
                    if (indexTip == null) return;

                    // Continue updating ray position based on gaze
                    Vector3 currentRayEndPoint = rayOrigin + (rayDirection * rayDistance);

                    // Use updated ray endpoint for X & Y positioning
                    Vector3 rayBasedPosition = endPoint;

                    // Smooth out zDelta with finger tip motion
                    float zDelta = (indexTip.position.z - initialIndexTipZ) * LNDMovementMultiplier;

                    // Final target position
                    Vector3 targetPosition = new Vector3(
                        rayBasedPosition.x,
                        rayBasedPosition.y,
                        initialTargetPosition.z + zDelta
                    );

                    // Smooth move using SmoothDamp
                    selectedTarget.transform.position = Vector3.SmoothDamp(
                        selectedTarget.transform.position,
                        targetPosition,
                        ref moveVelocity,
                        smoothFactor
                    );
                }
                else
                {
                    // Pinch was released, stop dragging
                    isMovingTarget = false;
                    selectedTarget = null;
                    block = false;
                    Debug.Log("Stopped dragging target");
                }
            }
        }
        if (currentMode == InteractionMode.GazePinch)
        {
            // STEP 1: Start dragging if ray is on target and pinch starts
            if (!isMovingTarget && hasHit && hit.collider.CompareTag("Target") && isHoldingNow && !isDragging)
            {
                GameObject hitTarget = hit.collider.gameObject;
                GameObject parentTarget = hitTarget.transform.parent != null ? hitTarget.transform.parent.gameObject : hitTarget;

                selectedTarget = parentTarget;
                isMovingTarget = true;
                block = true;

                cursor.SetActive(false);

                initialPalmPosition = palm.position;
                initialTargetPosition = selectedTarget.transform.position;

                // Line renderer
                if (handToObjectLine == null)
                {
                    GameObject lineObj = new GameObject("HandToObjectLine");
                    handToObjectLine = lineObj.AddComponent<LineRenderer>();
                    handToObjectLine.startWidth = 0.005f;
                    handToObjectLine.endWidth = 0.005f;
                    handToObjectLine.material = new Material(Shader.Find("Sprites/Default"));
                    handToObjectLine.startColor = Color.cyan;
                    handToObjectLine.endColor = Color.cyan;
                    handToObjectLine.positionCount = 2;
                    handToObjectLine.sortingOrder = 20;
                }

                handToObjectLine.gameObject.SetActive(true);

                Debug.Log("Started dragging with GazePinch: " + selectedTarget.name);
            }

            // STEP 2: Continue dragging — NO ray check here
            if (isMovingTarget && selectedTarget != null && isHoldingNow)
            {
                Vector3 handDelta = palm.position - initialPalmPosition;
                handDelta *= GPMovementMultiplier;

                Vector3 newTargetPos = initialTargetPosition + handDelta;

                selectedTarget.transform.position = Vector3.Lerp(
                    selectedTarget.transform.position,
                    newTargetPos,
                    Time.deltaTime * smoothSpeed
                );

                if (handToObjectLine != null)
                {
                    handToObjectLine.SetPosition(0, palm.position);
                    handToObjectLine.SetPosition(1, selectedTarget.transform.position);
                }
            }

            // STEP 3: Stop dragging when pinch ends
            if (!isHoldingNow && isMovingTarget)
            {
                isMovingTarget = false;
                selectedTarget = null;
                cursor.SetActive(true);

                if (handToObjectLine != null)
                    handToObjectLine.gameObject.SetActive(false);

                if (maskObject != null)
                    maskObject.SetActive(false);

                Debug.Log("Stopped dragging with GazePinch");
            }
        }

    }

    //---------------------- Rescaling ----------------------

    // Add this variable to track the currently active transparent target
    private GameObject currentTransparentTarget = null;

    // Updated method to handle double pinch detection
    private void HandleDoublePinch(GameObject targetObject, bool isPinching)
    {
        // Only process if the object has the "Target" tag
        if (!targetObject.CompareTag("Target"))
            return;

        if (isPinching && !isPinchingActive)
        {
            isPinchingActive = true;
            wasLastPinchOnTarget = true;

            // Check if this is a different target than the current transparent one
            if (isTransparentMode && currentTransparentTarget != null && targetObject != currentTransparentTarget)
            {
                // If pinching a new target while another is already in transparent mode,
                // simply deactivate the children of the current transparent target
                SetChildrenActive(currentTransparentTarget, false);
                Debug.Log("Deactivated children of previous target: " + currentTransparentTarget.name);

                // Exit transparency mode for the previous target
                if (isTransparentMode)
                {
                    // Restore original materials for previous target
                    Renderer[] renderers = currentTransparentTarget.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < renderers.Length && i < originalMaterials.Length; i++)
                    {
                        if (originalMaterials[i] != null)
                            renderers[i].material = originalMaterials[i];
                    }

                    // Restore original collider states for previous target
                    for (int i = 0; i < objectColliders.Length && i < originalCollidersState.Length; i++)
                    {
                        objectColliders[i].enabled = originalCollidersState[i];
                    }

                    if (currentTransparentTarget.gameObject.CompareTag("XRot") ||
                        currentTransparentTarget.gameObject.CompareTag("YRot") ||
                        currentTransparentTarget.gameObject.CompareTag("ZRot"))
                    {
                        currentTransparentTarget.gameObject.SetActive(false);
                    }

                    Debug.Log("Exited transparency mode for " + currentTransparentTarget.name);
                    isTransparentMode = false;
                    isRotationActive = false;
                    currentTransparentTarget = null;
                }
            }
            else if (waitingForSecondPinch && targetObject == lastPinchedObject)
            {
                float timeSinceLastPinch = Time.time - lastPinchReleaseTime;

                if (timeSinceLastPinch <= doublePinchTimeThreshold)
                {
                    print("double starts");
                    SwitchToNewTarget(targetObject);
                    waitingForSecondPinch = false;
                }
                else
                {
                    waitingForSecondPinch = false;
                }
            }

            lastPinchedObject = targetObject;
        }
        else if (!isPinching && isPinchingActive && wasLastPinchOnTarget)
        {
            isPinchingActive = false;
            // Record time of pinch release
            lastPinchReleaseTime = Time.time;

            // Start waiting for second pinch
            waitingForSecondPinch = true;
            wasLastPinchOnTarget = false;
        }
    }

    // Improved transparency toggle method
    private void ToggleTransparencyMode(GameObject targetObject)
    {
        // Target object should be the one with the "Target" tag
        if (!targetObject.CompareTag("Target"))
        {
            Debug.LogWarning("Attempted to toggle transparency on non-Target object");
            return;
        }

        if (!isTransparentMode)
        {
            // If there was a previous transparent target, turn off its children first
            if (currentTransparentTarget != null && currentTransparentTarget != targetObject)
            {
                SetChildrenActive(currentTransparentTarget, false);
                Debug.Log("Deactivated children of previous target: " + currentTransparentTarget.name);
            }

            // Set this as the new current transparent target
            currentTransparentTarget = targetObject;

            // Store original materials and make translucent
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                // Skip handles - we want them to remain visible
                if (IsScalingHandle(renderers[i].gameObject))
                    continue;
                if (renderers[i].gameObject.CompareTag("Rot") || renderers[i].gameObject.CompareTag("XRot") ||
                    renderers[i].gameObject.CompareTag("YRot") || renderers[i].gameObject.CompareTag("ZRot")) continue;

                originalMaterials[i] = renderers[i].material;
                Material transparentMaterial = new Material(renderers[i].material);

                // Make sure the shader supports transparency
                transparentMaterial.shader = Shader.Find("Standard");
                transparentMaterial.SetFloat("_Mode", 3); // Transparent mode
                transparentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                transparentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                transparentMaterial.SetInt("_ZWrite", 0);
                transparentMaterial.DisableKeyword("_ALPHATEST_ON");
                transparentMaterial.EnableKeyword("_ALPHABLEND_ON");
                transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");

                // Adjust the alpha
                Color color = transparentMaterial.color;
                color.a = transparencyValue;
                transparentMaterial.color = color;
                transparentMaterial.renderQueue = 3000;
                SetChildrenActive(targetObject, true);
                renderers[i].material = transparentMaterial;
            }

            // Disable colliders except for handles
            objectColliders = targetObject.GetComponentsInChildren<Collider>(true);
            originalCollidersState = new bool[objectColliders.Length];

            for (int i = 0; i < objectColliders.Length; i++)
            {
                originalCollidersState[i] = objectColliders[i].enabled;

                if (objectColliders[i].gameObject.CompareTag("Rot") || objectColliders[i].gameObject.CompareTag("XRot") ||
                    objectColliders[i].gameObject.CompareTag("YRot") || objectColliders[i].gameObject.CompareTag("ZRot")) continue;
                // Check if this is a scaling handle
                bool isHandle = IsScalingHandle(objectColliders[i].gameObject);

                // Only handles should have active colliders
                objectColliders[i].enabled = isHandle;
            }

            Debug.Log("Entered transparency mode for " + targetObject.name);
            isTransparentMode = true;
        }
        else
        {
            // Restore original materials
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length && i < originalMaterials.Length; i++)
            {
                if (originalMaterials[i] != null)
                    renderers[i].material = originalMaterials[i];
            }

            // Restore original collider states
            for (int i = 0; i < objectColliders.Length && i < originalCollidersState.Length; i++)
            {
                objectColliders[i].enabled = originalCollidersState[i];
            }

            SetChildrenActive(targetObject, false);
            if (targetObject.gameObject.CompareTag("XRot") ||
                targetObject.gameObject.CompareTag("YRot") ||
                targetObject.gameObject.CompareTag("ZRot"))
            {
                targetObject.gameObject.SetActive(false);
            }

            Debug.Log("Exited transparency mode for " + targetObject.name);
            isTransparentMode = false;
            isRotationActive = false;

            // Clear the current transparent target reference
            currentTransparentTarget = null;
        }
    }

    // Modified CheckForOutsidePinch method to handle multiple targets
    private void CheckForOutsidePinch(GameObject hitObject, bool isPinching)
    {
        // If in transparent mode and pinching something that's not the target or a handle
        if (isTransparentMode && isPinching && hitObject != null &&
            !hitObject.CompareTag("Target") && !IsScalingHandle(hitObject) && !hitObject.CompareTag("Finish")
            && !hitObject.CompareTag("Rot") && !hitObject.CompareTag("XRot") && !hitObject.CompareTag("YRot")
            && !hitObject.CompareTag("ZRot"))
        {
            // Find all objects with "Target" tag
            GameObject[] targetObjects = GameObject.FindGameObjectsWithTag("Target");

            // Only toggle the current transparent target
            if (currentTransparentTarget != null && isTransparentMode)
            {
                ToggleTransparencyMode(currentTransparentTarget);
            }
        }
    }

    // Add this helper method to manage target activation/deactivation when switching between targets
    private void SwitchToNewTarget(GameObject newTarget)
    {
        // If we already have an active target and it's different from the new one
        if (currentTransparentTarget != null && currentTransparentTarget != newTarget)
        {
            // If current target is in transparent mode, toggle it off first
            if (isTransparentMode)
            {
                ToggleTransparencyMode(currentTransparentTarget);
            }

            // Now toggle the new target on
            ToggleTransparencyMode(newTarget);
        }
        else
        {
            // If no current target or same target, just toggle normally
            ToggleTransparencyMode(newTarget);
        }
    }
    // Helper method to check if an object is a scaling handle
    private bool IsScalingHandle(GameObject obj)
    {
        // Check if the object has any of the scaling handle tags
        string[] handleTags = new string[] {
        "X", "Y", "Z", "NX", "NY", "NZ",
        "XY", "XZ", "YZ", "XNY", "XNZ", "YNZ",
        "NXY", "NXZ", "NYZ", "NXNY", "NXNZ", "NYNZ",
        "XYZ", "XYNZ", "XNYZ", "NXYZ", "XNYNZ", "NXYNZ", "NXNYZ", "NXNYNZ"
    };

        foreach (string tag in handleTags)
        {
            if (obj.CompareTag(tag))
                return true;
        }

        return false;
    }

    // Modify AdjustParentSizeOnPinch3D to maintain the interaction
    private void AdjustParentSizeOnPinch3D(Vector3 rayOrigin, Vector3 rayDirection, bool isPinching)
    {

        // Calculate ray endpoint
        Vector3 rayEndPoint = rayOrigin + (rayDirection * rayDistance);

        // Raycast only if we're not already holding an object
        GameObject hitObject = null;
        if (!isPinchHoldActive)
        {
            // Raycast against 3D colliders
            RaycastHit hit;
            bool didHit = Physics.Raycast(rayEndPoint, rayDirection, out hit, rayDistance);//, targetLayer);
            float sphereRadius = 0.01f; // Radius of 5cm provides more generous hit detection

            RaycastHit hits;
            bool didHit1 = Physics.SphereCast(rayOrigin, sphereRadius, rayDirection, out hits, rayDistance);

            if (didHit1)
            {
                hitObject = hits.collider.gameObject;

                // Process double pinch for target objects
                if (hitObject.CompareTag("Target"))
                {
                    HandleDoublePinch(hitObject, isPinching);
                }
            }

            // Check for pinches outside the target object
            CheckForOutsidePinch(hitObject, isPinching);

            // Handle highlighting
            Handle3DHighlighting(hits);
        }
        else
        {
            // If we're in pinch-hold mode, use the pinned handle
            hitObject = pinnedHandleObject;
        }

        // Choose scaling method based on the current mode
        if (currentMode == InteractionMode.GazePinch)
        {
            ProcessGazePinchScaling3D(hitObject, rayOrigin, isPinching);
        }
        else if (currentMode == InteractionMode.EyeGaze)
        {
            ProcessEyeGazeScaling3D(hitObject, rayOrigin, rayEndPoint, isPinching);
        }
        else if (currentMode == InteractionMode.HandRay)
        {
            ProcessHandRayScaling3D(hitObject, rayOrigin, rayEndPoint, isPinching);
        }

        // Reset pinch state if no longer pinching
        if (!isPinching && (isPinchingActive || isPinchHoldActive))
        {
            if (currentlyAdjustingObject != null)
            {
                ResetPinchState(currentlyAdjustingObject, currentlyAdjustingObject.transform.parent);

                // Reset pinch hold state
                isPinchHoldActive = false;
                pinnedHandleObject = null;
            }
        }
    }
    // Modify ProcessHandRayScaling3D to maintain handle reference when pinching
    private void ProcessHandRayScaling3D(GameObject hitObject, Vector3 raycastOrigin, Vector3 rayEndPoint, bool isPinching)
    {
        if (hitObject == null) return;
        if (!IsScalingHandle(hitObject)) return;

        Transform parentTransform = hitObject.transform.parent;
        if (parentTransform == null) return;

        Vector3 moveVelocity = Vector3.zero;

        if (isPinching)
        {
            if (!isPinchingActive && !isPinchHoldActive)
            {
                // Initialize scaling (same as before)
                isPinchingActive = true;
                isPinchHoldActive = true;
                pinnedHandleObject = hitObject;
                initialPalmPosition = palm.position;
                initialRayEndPoint = rayEndPoint;
                currentlyAdjustingObject = hitObject;
                initialParentScale = parentTransform.localScale;
                initialObjectPosition = hitObject.transform.position;

                StoreChildrenWorldScales(parentTransform);
                SetChildrenActive(parentTransform.gameObject, false);
                hitObject.SetActive(true);
                return;
            }

            adjusting = true;

            // Calculate movement with reduced sensitivity
            Vector3 rayDelta = rayEndPoint - initialRayEndPoint;
            rayDelta *= 0.05f; // Reduce ray sensitivity to slow down X/Y scaling

            float zDelta = (palm.position.z - initialPalmPosition.z) * handMovementMultiplier * 0.05f; // Reduce Z sensitivity

            // Combine movement
            Vector3 movementDelta = new Vector3(rayDelta.x, rayDelta.y, zDelta);

            // Convert to local space for scaling
            Vector3 localDelta = parentTransform.InverseTransformDirection(movementDelta);

            // Calculate scale changes
            float scaleChangeX = 0, scaleChangeY = 0, scaleChangeZ = 0;
            Vector3 localPositionOffset = Vector3.zero;

            Apply3DScalingBasedOnTag(hitObject, localDelta, ref scaleChangeX, ref scaleChangeY, ref scaleChangeZ, ref localPositionOffset);

            // Apply scale changes with minimum limits
            Vector3 currentScale = parentTransform.localScale;
            currentScale.x = Mathf.Max(currentScale.x + scaleChangeX, minScaleThreshold);
            currentScale.y = Mathf.Max(currentScale.y + scaleChangeY, minScaleThreshold);
            currentScale.z = Mathf.Max(currentScale.z + scaleChangeZ, minScaleThreshold);

            // Calculate position offset
            Vector3 currentPosition = parentTransform.position;
            Vector3 worldPositionOffset = parentTransform.TransformDirection(localPositionOffset);
            currentPosition += worldPositionOffset;

            // Use enhanced smooth factor for slower transitions
            float enhancedSmoothFactor = smoothFactor * 3.0f;

            parentTransform.localScale = Vector3.SmoothDamp(
                parentTransform.localScale,
                currentScale,
                ref scaleVelocity,
                enhancedSmoothFactor);

            parentTransform.position = Vector3.SmoothDamp(
                parentTransform.position,
                currentPosition,
                ref moveVelocity,
                enhancedSmoothFactor);

            // Apply inverse scaling to children (except handles)
            RestoreChildrenWorldScales(parentTransform);
            // Visual feedback
            Renderer renderer = hitObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.2f);
            }

            // Keep updating the line visualizer
            UpdateHandToObjectLine(hitObject);
        }
    }
    // Make similar modifications to ProcessGazePinchScaling3D method
    private void ProcessGazePinchScaling3D(GameObject hitObject, Vector3 raycastOrigin, bool isPinching)
    {
        if (hitObject == null) return;
        if (!IsScalingHandle(hitObject)) return;

        Vector3 moveVelocity = Vector3.zero;
        Transform parentTransform = hitObject.transform.parent;
        if (parentTransform == null) return;

        if (isPinching)
        {
            if (!isPinchingActive && !isPinchHoldActive)
            {
                // Initialize scaling
                isPinchingActive = true;
                isPinchHoldActive = true;
                pinnedHandleObject = hitObject;
                initialPalmPosition = palm.position;
                currentlyAdjustingObject = hitObject;

                StoreChildrenWorldScales(parentTransform);
                cursor.SetActive(false);
                SetChildrenActive(parentTransform.gameObject, false);
                hitObject.SetActive(true);

                // Create hand-to-object line
                CreateHandToObjectLine(hitObject);
                return;
            }

            // Continue scaling
            adjusting = true;
            Vector3 handMovementDelta = palm.position - initialPalmPosition;

            // Ignore tiny movements
            if (handMovementDelta.magnitude < handMovementThreshold)
                return;

            // Apply movement multiplier (similar to GPMovementMultiplier in translation)
            handMovementDelta *= AdjusthandMovementMultiplier;

            // Get scaling changes based on handle tag
            Vector3 localPinchDelta = parentTransform.InverseTransformDirection(handMovementDelta);
            float scaleChangeX = 0, scaleChangeY = 0, scaleChangeZ = 0;
            Vector3 localPositionOffset = Vector3.zero;

            Apply3DScalingBasedOnTag(hitObject, localPinchDelta, ref scaleChangeX, ref scaleChangeY, ref scaleChangeZ, ref localPositionOffset);

            // Apply scale changes with minimum limits
            Vector3 currentScale = parentTransform.localScale;
            currentScale.x = Mathf.Max(currentScale.x + scaleChangeX, minScaleThreshold);
            currentScale.y = Mathf.Max(currentScale.y + scaleChangeY, minScaleThreshold);
            currentScale.z = Mathf.Max(currentScale.z + scaleChangeZ, minScaleThreshold);

            // Calculate position offset
            Vector3 currentPosition = parentTransform.position;
            Vector3 worldPositionOffset = parentTransform.TransformDirection(localPositionOffset);
            currentPosition += worldPositionOffset;

            parentTransform.localScale = Vector3.SmoothDamp(
                    parentTransform.localScale,
                    currentScale,
                    ref scaleVelocity,
                        smoothFactor);

            parentTransform.position = Vector3.SmoothDamp(
                parentTransform.position,
                currentPosition,
                ref moveVelocity,
                smoothFactor);
            RestoreChildrenWorldScales(parentTransform);

            // Update line renderer
            UpdateHandToObjectLine(hitObject);

            // Visual feedback
            Renderer renderer = hitObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.2f);
            }
        }
    }

    // And modify ProcessEyeGazeScaling3D in the same way
    private void ProcessEyeGazeScaling3D(GameObject hitObject, Vector3 raycastOrigin, Vector3 rayEndPoint, bool isPinching)
    {
        if (hitObject == null) return;
        if (!IsScalingHandle(hitObject)) return;

        Transform parentTransform = hitObject.transform.parent;
        if (parentTransform == null) return;

        Vector3 moveVelocity = Vector3.zero;

        if (isPinching)
        {
            if (!isPinchingActive && !isPinchHoldActive)
            {
                // Initialize scaling
                isPinchingActive = true;
                isPinchHoldActive = true;
                pinnedHandleObject = hitObject;
                currentlyAdjustingObject = hitObject;

                // Find index fingertip bone for Z control (same as your EyeGaze translation)
                foreach (var bone in skeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                    {
                        indexTip = bone.Transform;
                        break;
                    }
                }

                if (indexTip != null)
                    initialIndexTipZ = indexTip.position.z;

                initialRayEndPoint = rayEndPoint;
                initialObjectPosition = hitObject.transform.position;
                initialParentScale = parentTransform.localScale;

                StoreChildrenWorldScales(parentTransform);
                SetChildrenActive(parentTransform.gameObject, false);
                hitObject.SetActive(true);
                return;
            }

            // Continue scaling if we still have the index tip
            if (indexTip == null) return;

            adjusting = true;

            // Calculate scale delta based on ray endpoint for X/Y and finger tip for Z
            Vector3 rayDelta = rayEndPoint - initialRayEndPoint;
            float zDelta = (indexTip.position.z - initialIndexTipZ) * LNDMovementMultiplier;

            // Combine movement
            Vector3 movementDelta = new Vector3(rayDelta.x, rayDelta.y, zDelta);

            // Convert to local space for scaling
            Vector3 localDelta = parentTransform.InverseTransformDirection(movementDelta);

            // Calculate scale changes
            float scaleChangeX = 0, scaleChangeY = 0, scaleChangeZ = 0;
            Vector3 localPositionOffset = Vector3.zero;

            Apply3DScalingBasedOnTag(hitObject, localDelta, ref scaleChangeX, ref scaleChangeY, ref scaleChangeZ, ref localPositionOffset);

            // Apply scale changes with minimum limits
            Vector3 currentScale = parentTransform.localScale;
            currentScale.x = Mathf.Max(currentScale.x + scaleChangeX, minScaleThreshold);
            currentScale.y = Mathf.Max(currentScale.y + scaleChangeY, minScaleThreshold);
            currentScale.z = Mathf.Max(currentScale.z + scaleChangeZ, minScaleThreshold);

            // Calculate position offset
            Vector3 currentPosition = parentTransform.position;
            Vector3 worldPositionOffset = parentTransform.TransformDirection(localPositionOffset);
            currentPosition += worldPositionOffset;

            // Use SmoothDamp for smoother transitions (like in your translation code)
            parentTransform.localScale = Vector3.SmoothDamp(
                parentTransform.localScale,
                currentScale,
                ref scaleVelocity,
                smoothFactor);

            parentTransform.position = Vector3.SmoothDamp(
                parentTransform.position,
                currentPosition,
                ref moveVelocity,
                smoothFactor);


            RestoreChildrenWorldScales(parentTransform);

            // Visual feedback
            Renderer renderer = hitObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.2f);
            }
        }
    }

    // Update ResetPinchState to also handle the new pinch hold state
    private void ResetPinchState(GameObject hitObject, Transform parentTransform)
    {
        isPinchingActive = false;
        isPinchHoldActive = false;
        pinnedHandleObject = null;

        cursor.SetActive(true);
        if (handToObjectLine != null)
            handToObjectLine.gameObject.SetActive(false);

        SetChildrenActive(parentTransform.gameObject, true);

        foreach (Transform child in parentTransform)
        {
            if (child.gameObject.CompareTag("XRot") ||
                child.gameObject.CompareTag("YRot") ||
                child.gameObject.CompareTag("ZRot"))
            {
                child.gameObject.SetActive(false);
            }
        }

        if (currentlyAdjustingObject == hitObject)
        {
            currentlyAdjustingObject = null;
        }

        // Reset color when not pinching
        Renderer renderer = hitObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.white;
        }
    }


    private Vector3 scaleVelocity;
    // Updated highlighting for 3D objects
    private void Handle3DHighlighting(RaycastHit hit)
    {
        if (hit.collider != null)
        {
            GameObject hitObj = hit.collider.gameObject;

            if (IsScalingHandle(hitObj) && !isPinchingActive)
            {
                Renderer renderer = hitObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.green;
                    if (lastHighlightedObject != null && lastHighlightedObject != hitObj)
                    {
                        Renderer lastRenderer = lastHighlightedObject.GetComponent<Renderer>();
                        if (lastRenderer != null) lastRenderer.material.color = Color.blue;
                    }
                    lastHighlightedObject = hitObj;
                }
            }
            else if (!IsScalingHandle(hitObj))
            {
                // Reset color if not hovering a handle
                if (lastHighlightedObject != null)
                {
                    Renderer lastRenderer = lastHighlightedObject.GetComponent<Renderer>();
                    if (lastRenderer != null) lastRenderer.material.color = Color.blue;
                    lastHighlightedObject = null;
                }
            }
        }
        else
        {
            // Nothing hit, reset highlighting
            if (lastHighlightedObject != null)
            {
                Renderer lastRenderer = lastHighlightedObject.GetComponent<Renderer>();
                if (lastRenderer != null) lastRenderer.material.color = Color.blue;
                lastHighlightedObject = null;
            }
        }
    }

    private void CreateHandToObjectLine(GameObject hitObject)
    {
        if (handToObjectLine == null)
        {
            GameObject lineObj = new GameObject("HandToObjectLine");
            handToObjectLine = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(handToObjectLine);
        }

        // Ensure line renderer is active and positioned correctly
        handToObjectLine.gameObject.SetActive(true);
        handToObjectLine.SetPosition(0, palm.position);
        handToObjectLine.SetPosition(1, hitObject.transform.position);
    }

    private void ConfigureLineRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.005f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.cyan;
        lineRenderer.endColor = Color.cyan;
        lineRenderer.positionCount = 2;
        lineRenderer.sortingOrder = 20;
    }
   
    private void UpdateHandToObjectLine(GameObject hitObject)
    {
        if (handToObjectLine != null && handToObjectLine.gameObject.activeSelf)
        {
            hitObject.SetActive(true);
            // First point is always the palm position
            handToObjectLine.SetPosition(0, palm.position);

            // Second point is the position of the specific handle being pinched
            handToObjectLine.SetPosition(1, hitObject.transform.position);

            // Optional: Adjust line color or width based on interaction
            handToObjectLine.startColor = Color.cyan;
            handToObjectLine.endColor = Color.red;
        }
    }

    // Expanded version of ApplyScalingBasedOnTag for 3D
    private void Apply3DScalingBasedOnTag(GameObject hitObject, Vector3 localPinchDelta,
                                          ref float scaleChangeX, ref float scaleChangeY, ref float scaleChangeZ,
                                          ref Vector3 localPositionOffset)
    {
        Vector3 adjustedDelta = new Vector3(
                                    localPinchDelta.x * xAxisScaleSensitivity,
                                    localPinchDelta.y * yAxisScaleSensitivity,
                                    localPinchDelta.z * zAxisScaleSensitivity
                                );
        // Handle single-axis scaling
        if (hitObject.CompareTag("X"))
        {
            scaleChangeX = adjustedDelta.x;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, 0, 0);
        }
        else if (hitObject.CompareTag("NX"))
        {
            scaleChangeX = -adjustedDelta.x;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, 0, 0);
        }
        else if (hitObject.CompareTag("Y"))
        {
            scaleChangeY = adjustedDelta.y;
            localPositionOffset = new Vector3(0, scaleChangeY * 0.5f, 0);
        }
        else if (hitObject.CompareTag("NY"))
        {
            scaleChangeY = -adjustedDelta.y;
            localPositionOffset = new Vector3(0, -scaleChangeY * 0.5f, 0);
        }
        else if (hitObject.CompareTag("Z"))
        {
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(0, 0, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NZ"))
        {
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(0, 0, -scaleChangeZ * 0.5f);
        }


        // Handle two-axis scaling
        else if (hitObject.CompareTag("XY"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeY = adjustedDelta.y;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, scaleChangeY * 0.5f, 0);
        }
        else if (hitObject.CompareTag("XZ"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, 0, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("YZ"))
        {
            scaleChangeY = adjustedDelta.y;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(0, scaleChangeY * 0.5f, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("XNY"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeY = -adjustedDelta.y;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, -scaleChangeY * 0.5f, 0);
        }
        else if (hitObject.CompareTag("XNZ"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, 0, -scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("YNZ"))
        {
            scaleChangeY = adjustedDelta.y;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(0, scaleChangeY * 0.5f, -scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NXY"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeY = adjustedDelta.y;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, scaleChangeY * 0.5f, 0);
        }
        else if (hitObject.CompareTag("NXZ"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, 0, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NYZ"))
        {
            scaleChangeY = -adjustedDelta.y;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(0, -scaleChangeY * 0.5f, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NXNY"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeY = -adjustedDelta.y;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, -scaleChangeY * 0.5f, 0);
        }
        else if (hitObject.CompareTag("NXNZ"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, 0, -scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NYNZ"))
        {
            scaleChangeY = -adjustedDelta.y;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(0, -scaleChangeY * 0.5f, -scaleChangeZ * 0.5f);
        }

        // Handle three-axis scaling (corners)
        else if (hitObject.CompareTag("XYZ"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeY = adjustedDelta.y;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, scaleChangeY * 0.5f, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("XYNZ"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeY = adjustedDelta.y;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, scaleChangeY * 0.5f, -scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("XNYZ"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeY = -adjustedDelta.y;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, -scaleChangeY * 0.5f, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NXYZ"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeY = adjustedDelta.y;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, scaleChangeY * 0.5f, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("XNYNZ"))
        {
            scaleChangeX = adjustedDelta.x;
            scaleChangeY = -adjustedDelta.y;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(scaleChangeX * 0.5f, -scaleChangeY * 0.5f, -scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NXYNZ"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeY = adjustedDelta.y;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, scaleChangeY * 0.5f, -scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NXNYZ"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeY = -adjustedDelta.y;
            scaleChangeZ = adjustedDelta.z;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, -scaleChangeY * 0.5f, scaleChangeZ * 0.5f);
        }
        else if (hitObject.CompareTag("NXNYNZ"))
        {
            scaleChangeX = -adjustedDelta.x;
            scaleChangeY = -adjustedDelta.y;
            scaleChangeZ = -adjustedDelta.z;
            localPositionOffset = new Vector3(-scaleChangeX * 0.5f, -scaleChangeY * 0.5f, -scaleChangeZ * 0.5f);
        }
    }


    public Vector3 targetHandleSize = new Vector3(0.1280569f, 0.1280569f, 0.1280569f);
    public float RotHandleSize = 2;
    private Dictionary<Transform, Vector3> originalHandleLocalScales = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalHandleLocalRotations = new Dictionary<Transform, Quaternion>();


    private void StoreChildrenWorldScales(Transform parentTransform)
    {
        originalHandleLocalScales.Clear();
        originalHandleLocalRotations.Clear();

        foreach (Transform child in parentTransform)
        {
            if (child.gameObject.name == "MaskObject" || child.gameObject.name == "HoverMask")
                continue;

            // Store original local scales and rotations for rotation handles
            if (child.CompareTag("XRot") || child.CompareTag("YRot") || child.CompareTag("ZRot"))
            {
                // Store the original local scale
                originalHandleLocalScales[child] = child.localScale;

            }
        }
    }

    private void RestoreChildrenWorldScales(Transform parentTransform)
    {
        Vector3 inverseParentScale = new Vector3(
            1f / parentTransform.lossyScale.x,
            1f / parentTransform.lossyScale.y,
            1f / parentTransform.lossyScale.z
        );

        // Find max scale for uniform rotation handle scaling
        float maxParentScale = Mathf.Max(parentTransform.lossyScale.x, parentTransform.lossyScale.y, parentTransform.lossyScale.z);

        foreach (Transform child in parentTransform)
        {
            if (child.gameObject.name == "MaskObject" || child.gameObject.name == "HoverMask")
                continue;

            // Rotation handles ("XRot", "YRot", "ZRot")
            if (child.CompareTag("XRot") || child.CompareTag("YRot") || child.CompareTag("ZRot"))
            {
                if (originalHandleLocalScales.TryGetValue(child, out Vector3 originalLocalScale))
                {
                    // Multiply original local scale uniformly by maxParentScale
                    child.localScale = originalLocalScale * maxParentScale * RotHandleSize;
                }
            }
            else
            {
                // For other scaling handles, keep fixed size
                child.localScale = new Vector3(
                    targetHandleSize.x * inverseParentScale.x,
                    targetHandleSize.y * inverseParentScale.y,
                    targetHandleSize.z * inverseParentScale.z
                );
            }
        }
    }


    //---------------------- Rotation -----------------------

    // Helper method to deactivate rotation handles
    private void DeactivateRotationHandles()
    {
        if (rotHandleObject != null)
        {
            Transform parentTransform = rotHandleObject.transform.parent;
            if (parentTransform != null)
            {
                // Find and deactivate rotation handles by tags instead of names
                foreach (Transform child in parentTransform)
                {
                    if (child.gameObject.CompareTag("XRot") ||
                        child.gameObject.CompareTag("YRot") ||
                        child.gameObject.CompareTag("ZRot"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        isRotationActive = false;
        rotHandleObject = null;
    }

    // Add this method to initialize and store the handles and their original rotations
    private void InitializeRotationHandles(Transform parentTransform)
    {
        if (parentTransform == null) return;

        // Find all rotation handles by tags
        List<GameObject> handlesList = new List<GameObject>();
        foreach (Transform child in parentTransform)
        {
            if (child.gameObject.CompareTag("XRot") ||
                child.gameObject.CompareTag("YRot") ||
                child.gameObject.CompareTag("ZRot"))
            {
                handlesList.Add(child.gameObject);
            }
        }

        rotationHandles = handlesList.ToArray();
        originalHandleRotations = new Quaternion[rotationHandles.Length];

        // Store original rotations
        for (int i = 0; i < rotationHandles.Length; i++)
        {
            originalHandleRotations[i] = rotationHandles[i].transform.rotation;
        }
    }

    // Add this method to reset handle rotations to their original orientation
    private void ResetHandleRotations()
    {
        if (rotationHandles == null || originalHandleRotations == null) return;

        for (int i = 0; i < rotationHandles.Length; i++)
        {
            if (rotationHandles[i] != null)
            {
                rotationHandles[i].transform.rotation = originalHandleRotations[i];
            }
        }
    }

    private void HandleObjectRotation(Vector3 rayOrigin, Vector3 rayDirection, bool isPinching)
    {
        // Early exit conditions
        if (adjusting || isDragging || multiSelectMode || isSelectionMaskActive || block || isMovingTarget)
            return;

        // Calculate ray endpoint
        Vector3 rayEndPoint = rayOrigin + (rayDirection * rayDistance);

        float sphereRadius = 0.05f; // Radius of 5cm provides more generous hit detection

        RaycastHit hit;
        bool didHit = Physics.SphereCast(rayOrigin, sphereRadius, rayDirection, out hit, rayDistance);
        GameObject hitObject = null;

        if (didHit)
        {
            hitObject = hit.collider.gameObject;

            // Check if we hit a rotation control object
            if (hitObject.CompareTag("Rot"))
            {
                if (isPinching && !isRotationActive)
                {
                    // Activate rotation mode
                    isRotationActive = true;
                    rotHandleObject = hitObject;

                    // Get the parent object to find rotation handles
                    Transform parentTransform = hitObject.transform.parent;
                    if (parentTransform != null)
                    {
                        SetChildrenActive(parentTransform.gameObject, false);
                        // Initially activate all rotation handles
                        foreach (Transform child in parentTransform)
                        {
                            if (child.gameObject.CompareTag("XRot") ||
                                child.gameObject.CompareTag("YRot") ||
                                child.gameObject.CompareTag("ZRot"))
                            {
                                child.gameObject.SetActive(true);
                            }
                        }

                        // Store all handles for later reference
                        InitializeRotationHandles(parentTransform);
                    }
                }
            }

            if (didHit)
            {
                hitObject = hit.collider.gameObject;

                // Handle highlighting for rotation rings
                HandleRotationRingHighlighting(hit);

                // If we're currently rotating (pinching), deactivate all handles except the current one
                if (isRotating && currentRotationHandle != null)
                {
                    DeactivateUnusedRotationHandles(currentRotationHandle);
                }

                // Process rotation based on the current mode
                if (currentMode == InteractionMode.GazePinch)
                {
                    ProcessGazePinchRotation(hitObject, rayOrigin, isPinching);
                }
                else if (currentMode == InteractionMode.EyeGaze)
                {
                    ProcessEyeGazeRotation(hitObject, rayOrigin, rayEndPoint, isPinching);
                }
                else if (currentMode == InteractionMode.HandRay)
                {
                    ProcessHandRayRotation(hitObject, rayOrigin, rayEndPoint, isPinching);
                }
            }
            else
            {
                // Reset highlighting when not pointing at anything
                ResetRotationRingHighlighting();
            }

            // If no longer pinching, reset rotation state and reactivate all handles
            if (!isPinching && isRotating)
            {
                ResetRotationState();
                ReactivateAllRotationHandles();
            }

            // Update rotation angle text if rotating
            if (isRotating && rotationAngleText != null)
            {
                UpdateRotationAngleText();
            }
        }
    }

    // Add this new method to deactivate all handles except the one currently being used
    private void DeactivateUnusedRotationHandles(GameObject currentHandle)
    {
        if (currentHandle == null || rotationHandles == null) return;

        string currentAxis = DetermineRotationAxis(currentHandle);

        foreach (GameObject handle in rotationHandles)
        {
            if (handle != null && handle != currentHandle)
            {
                // Deactivate all handles except the one we're currently using
                handle.SetActive(false);
            }
        }
    }

    // Add this method to reactivate all handles when pinch is released
    private void ReactivateAllRotationHandles()
    {
        if (rotationHandles == null) return;

        foreach (GameObject handle in rotationHandles)
        {
            if (handle != null)
            {
                handle.SetActive(true);
            }
        }
    }

    // Modify the ResetRotationState method to include handle reactivation
    private void ResetRotationState()
    {
        isRotating = false;

        if (currentRotationHandle != null)
        {
            // Reset color
            Renderer renderer = currentRotationHandle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = GetAxisColor(DetermineRotationAxis(currentRotationHandle));
            }
        }

        // Reactivate all rotation handles
        ReactivateAllRotationHandles();

        // Hide visual feedback
        if (handToObjectLine != null)
            handToObjectLine.gameObject.SetActive(false);

        // Hide rotation angle text with a short delay
        if (rotationAngleText != null)
            Invoke("HideRotationAngleText", 1.5f);

        currentRotationHandle = null;
        rot = false;
    }
    // Handle visual feedback for rotation rings
    private void HandleRotationRingHighlighting(RaycastHit hit)
    {
        if (hit.collider != null)
        {
            GameObject hitObj = hit.collider.gameObject;

            // Check if we hit a rotation ring
            if (IsRotationRing(hitObj) && !isRotating)
            {
                // Highlight the ring
                Renderer renderer = hitObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.green;

                    // Reset color of previously highlighted ring if different
                    if (lastHighlightedObject != null && lastHighlightedObject != hitObj)
                    {
                        Renderer lastRenderer = lastHighlightedObject.GetComponent<Renderer>();
                        if (lastRenderer != null) lastRenderer.material.color = GetAxisColor(DetermineRotationAxis(lastHighlightedObject));
                    }
                    lastHighlightedObject = hitObj;
                }
            }
            else if (!IsRotationRing(hitObj))
            {
                ResetRotationRingHighlighting();
            }
        }
    }

    // Reset highlighting on rotation rings
    private void ResetRotationRingHighlighting()
    {
        if (lastHighlightedObject != null && IsRotationRing(lastHighlightedObject))
        {
            Renderer lastRenderer = lastHighlightedObject.GetComponent<Renderer>();
            if (lastRenderer != null) lastRenderer.material.color = GetAxisColor(DetermineRotationAxis(lastHighlightedObject));
            lastHighlightedObject = null;
        }
    }

    // Get color based on axis
    private Color GetAxisColor(string ringAxis)
    {
        if (ringAxis == "X") return new Color(0.9f, 0.2f, 0.2f); // Red for X
        if (ringAxis == "Y") return new Color(0.2f, 0.9f, 0.2f); // Green for Y
        if (ringAxis == "Z") return new Color(0.2f, 0.2f, 0.9f); // Blue for Z
        return Color.white;
    }

    // Check if object is a rotation ring using tags instead of names
    private bool IsRotationRing(GameObject obj)
    {
        return obj != null && (
            obj.CompareTag("XRot") ||
            obj.CompareTag("YRot") ||
            obj.CompareTag("ZRot")
        );
    }

    private void ProcessGazePinchRotation(GameObject hitObject, Vector3 raycastOrigin, bool isPinching)
    {
        if (hitObject == null) return;
        if (!IsRotationRing(hitObject)) return;

        Transform parentTransform = hitObject.transform.parent;
        if (parentTransform == null) return;

        if (isPinching)
        {
            if (!isRotating)
            {
                // Initialize rotation
                isRotating = true;
                currentRotationHandle = hitObject;
                initialHandPosition = palm.position;
                pinchStartPoint = palm.position; // Use palm position as starting point
                initialRotation = parentTransform.rotation;
                currentEulerAngles = parentTransform.eulerAngles;
                currentRotationAxis = DetermineRotationAxis(hitObject);

                // Initialize handle rotations
                InitializeRotationHandles(parentTransform);

                // Show rotation angle text
                if (rotationAngleText != null)
                    rotationAngleText.SetActive(true);

                // Create visual feedback line
                CreateHandToObjectLine(hitObject);

                // Record starting time for rotation
                rot = true;
                return;
            }

            // Store current pinch point
            pinchEndPoint = palm.position;

            // Calculate target rotation based on absolute position
            Quaternion targetRotation = CalculateAbsoluteRotation(pinchEndPoint, parentTransform.position, currentRotationAxis);

            // Apply rotation with smooth damping for better control
            parentTransform.rotation = SmoothDampQuaternion(
                parentTransform.rotation,
                targetRotation,
                ref angularVelocity,
                rotationSmoothFactor
            );

            // Reset handle rotations to keep them aligned with global axes
            ResetHandleRotations();

            // Update visual feedback
            UpdateHandToObjectLine(currentRotationHandle);

            // Update rotation handle color for feedback
            Renderer renderer = currentRotationHandle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.2f);
            }
        }
    }

    // Process rotation in EyeGaze mode with angle-based rotation
    private void ProcessEyeGazeRotation(GameObject hitObject, Vector3 raycastOrigin, Vector3 rayEndPoint, bool isPinching)
    {
        if (hitObject == null) return;
        if (!IsRotationRing(hitObject)) return;

        Transform parentTransform = hitObject.transform.parent;
        if (parentTransform == null) return;

        if (isPinching)
        {
            if (!isRotating)
            {
                // Initialize rotation
                isRotating = true;
                currentRotationHandle = hitObject;
                pinchStartPoint = rayEndPoint; // Store initial pinch point
                initialRotation = parentTransform.rotation; // Store initial rotation
                currentEulerAngles = parentTransform.eulerAngles;
                currentRotationAxis = DetermineRotationAxis(hitObject);

                // Store current pinch point as end point too, to avoid immediate rotation
                pinchEndPoint = pinchStartPoint;

                InitializeRotationHandles(parentTransform);
                // Find index fingertip bone for additional control
                foreach (var bone in skeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                    {
                        indexTipForRotation = bone.Transform;
                        initialIndexTipPosition = indexTipForRotation.position.z;
                        break;
                    }
                }

                // Show rotation angle text
                if (rotationAngleText != null)
                    rotationAngleText.SetActive(true);

                rot = true;
                return;
            }

            // Skip if we don't have the index tip reference
            if (indexTipForRotation == null) return;

            // Store current pinch point
            pinchEndPoint = rayEndPoint;

            // Calculate target rotation based on where the ray is pointing
            Quaternion targetRotation = CalculateAbsoluteRotation(rayEndPoint, parentTransform.position, currentRotationAxis);

            // Apply rotation with smooth damping for better control
            parentTransform.rotation = SmoothDampQuaternion(
                parentTransform.rotation,
                targetRotation,
                ref angularVelocity,
                rotationSmoothFactor
            );

            // Reset handle rotations to keep them aligned with global axes
            ResetHandleRotations();
            // Update rotation handle color for feedback
            Renderer renderer = currentRotationHandle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.2f);
            }
        }
    }

    // Process rotation in HandRay mode with angle-based rotation
    private void ProcessHandRayRotation(GameObject hitObject, Vector3 raycastOrigin, Vector3 rayEndPoint, bool isPinching)
    {
        if (hitObject == null) return;
        if (!IsRotationRing(hitObject)) return;

        Transform parentTransform = hitObject.transform.parent;
        if (parentTransform == null) return;

        if (isPinching)
        {
            if (!isRotating)
            {
                // Initialize rotation
                isRotating = true;
                currentRotationHandle = hitObject;
                pinchStartPoint = rayEndPoint; // Store initial pinch point
                initialRotation = parentTransform.rotation; // Store initial rotation
                currentEulerAngles = parentTransform.eulerAngles;
                currentRotationAxis = DetermineRotationAxis(hitObject);

                // Store current pinch point as end point too, to avoid immediate rotation
                pinchEndPoint = pinchStartPoint;

                InitializeRotationHandles(parentTransform);
                // Show rotation angle text
                if (rotationAngleText != null)
                    rotationAngleText.SetActive(true);

                rot = true;
                return;
            }

            // Store current pinch point
            pinchEndPoint = rayEndPoint;

            // Calculate target rotation based on where the ray is pointing
            Quaternion targetRotation = CalculateAbsoluteRotation(rayEndPoint, parentTransform.position, currentRotationAxis);

            // Apply rotation with smooth damping for better control
            parentTransform.rotation = SmoothDampQuaternion(
                parentTransform.rotation,
                targetRotation,
                ref angularVelocity,
                rotationSmoothFactor
            );


            ResetHandleRotations();
            // Update rotation handle color for feedback
            Renderer renderer = currentRotationHandle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.2f);
            }
        }
    }

    // NEW: Calculate rotation angle based on the start and end pinch points
    private float CalculateRotationAngle(Vector3 startPoint, Vector3 endPoint, Vector3 pivotPoint, string axis)
    {
        Vector3 startDirection, endDirection;

        switch (axis)
        {
            case "X":
                // For X rotation, use the Y-Z plane
                startDirection = new Vector3(0, startPoint.y - pivotPoint.y, startPoint.z - pivotPoint.z).normalized;
                endDirection = new Vector3(0, endPoint.y - pivotPoint.y, endPoint.z - pivotPoint.z).normalized;
                break;

            case "Y":
                // For Y rotation, use the X-Z plane
                startDirection = new Vector3(startPoint.x - pivotPoint.x, 0, startPoint.z - pivotPoint.z).normalized;
                endDirection = new Vector3(endPoint.x - pivotPoint.x, 0, endPoint.z - pivotPoint.z).normalized;
                break;

            case "Z":
                // For Z rotation, use the X-Y plane
                startDirection = new Vector3(startPoint.x - pivotPoint.x, startPoint.y - pivotPoint.y, 0).normalized;
                endDirection = new Vector3(endPoint.x - pivotPoint.x, endPoint.y - pivotPoint.y, 0).normalized;
                break;

            default:
                return 0f;
        }

        // Calculate angle between the two directions
        float angle = Vector3.SignedAngle(startDirection, endDirection, GetRotationAxis(axis));

        // Apply rotation speed modifier
        return angle * rotationSpeed * 0.1f;
    }

    private Quaternion CalculateAbsoluteRotation(Vector3 rayPoint, Vector3 objectPosition, string axis)
    {
        // Calculate the direction from object to ray point
        Vector3 direction = rayPoint - objectPosition;

        // Store the object's current rotation before applying new rotation
        Quaternion currentRotation = initialRotation;

        // Get the current world-space rotation angles
        Vector3 currentAngles = currentEulerAngles;

        // Use pinch start and end points to calculate rotation angle
        float rotationAngle = 0f;
        if (Vector3.Distance(pinchStartPoint, pinchEndPoint) > 0.01f) // Only rotate if the hand has moved
        {
            Vector3 startDir = pinchStartPoint - objectPosition;
            Vector3 endDir = rayPoint - objectPosition;

            // Calculate angle based on axis
            if (axis == "X")
            {
                // Project onto YZ plane
                Vector3 startYZ = new Vector3(0, startDir.y, startDir.z).normalized;
                Vector3 endYZ = new Vector3(0, endDir.y, endDir.z).normalized;
                rotationAngle = Vector3.SignedAngle(startYZ, endYZ, Vector3.right) * rotationSpeed;
                return Quaternion.AngleAxis(rotationAngle, Vector3.right) * currentRotation;
            }
            else if (axis == "Y")
            {
                // Project onto XZ plane
                Vector3 startXZ = new Vector3(startDir.x, 0, startDir.z).normalized;
                Vector3 endXZ = new Vector3(endDir.x, 0, endDir.z).normalized;
                rotationAngle = Vector3.SignedAngle(startXZ, endXZ, Vector3.up) * rotationSpeed;
                return Quaternion.AngleAxis(rotationAngle, Vector3.up) * currentRotation;
            }
            else if (axis == "Z")
            {
                // Project onto XY plane
                Vector3 startXY = new Vector3(startDir.x, startDir.y, 0).normalized;
                Vector3 endXY = new Vector3(endDir.x, endDir.y, 0).normalized;
                rotationAngle = Vector3.SignedAngle(startXY, endXY, Vector3.forward) * rotationSpeed;
                return Quaternion.AngleAxis(rotationAngle, Vector3.forward) * currentRotation;
            }
        }

        // If no significant movement or unknown axis, return the current rotation
        return currentRotation;
    }

    // Helper method to get rotation axis vector
    private Vector3 GetRotationAxis(string axis)
    {
        switch (axis)
        {
            case "X": return Vector3.right;
            case "Y": return Vector3.up;
            case "Z": return Vector3.forward;
            default: return Vector3.up;
        }
    }

    // Determine which axis this rotation handle controls based on tag
    private string DetermineRotationAxis(GameObject handle)
    {
        if (handle.CompareTag("XRot")) return "X";
        if (handle.CompareTag("YRot")) return "Y";
        if (handle.CompareTag("ZRot")) return "Z";
        return "Y"; // Default to Y if unknown
    }

    // Add this to your existing variables
    private Space rotationSpace = Space.World; // Use Space.World for global rotation

    // Modify the ApplyRotation method to use world space
    private void ApplyRotation(Transform targetTransform, float rotationAmount)
    {
        // Create a rotation in world space based on the current axis
        Quaternion rotationDelta = Quaternion.identity;

        switch (currentRotationAxis)
        {
            case "X":
                rotationDelta = Quaternion.AngleAxis(rotationAmount, Vector3.right);
                break;
            case "Y":
                rotationDelta = Quaternion.AngleAxis(rotationAmount, Vector3.up);
                break;
            case "Z":
                rotationDelta = Quaternion.AngleAxis(rotationAmount, Vector3.forward);
                break;
        }

        // Apply the world space rotation to the target
        targetTransform.rotation = rotationDelta * targetTransform.rotation;

        // Update current euler angles for UI display
        currentEulerAngles = targetTransform.eulerAngles;

        // Reset handle rotations to maintain global orientation
        ResetHandleRotations();
    }

    // Replace CalculateTargetRotation with this version for world space rotation
    private Quaternion CalculateTargetRotation(Transform targetTransform, float amount)
    {
        // Create a rotation in world space
        Quaternion rotationDelta = Quaternion.identity;

        switch (currentRotationAxis)
        {
            case "X":
                rotationDelta = Quaternion.AngleAxis(amount, Vector3.right);
                break;
            case "Y":
                rotationDelta = Quaternion.AngleAxis(amount, Vector3.up);
                break;
            case "Z":
                rotationDelta = Quaternion.AngleAxis(amount, Vector3.forward);
                break;
        }

        // Return the target rotation in world space
        return rotationDelta * targetTransform.rotation;
    }

    // Modify the SetupRotationHandles method to position handles in world space
    private void SetupRotationHandles(Transform targetTransform)
    {
        if (targetTransform == null) return;

        Vector3 objectCenter = targetTransform.position;
        float handleSize = 0.3f; // Adjust as needed

        foreach (Transform child in targetTransform)
        {
            if (child.CompareTag("XRot"))
            {
                // Position the X rotation handle
                child.position = objectCenter;
                child.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.right);
                child.localScale = new Vector3(handleSize, handleSize, handleSize);
            }
            else if (child.CompareTag("YRot"))
            {
                // Position the Y rotation handle
                child.position = objectCenter;
                child.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                child.localScale = new Vector3(handleSize, handleSize, handleSize);
            }
            else if (child.CompareTag("ZRot"))
            {
                // Position the Z rotation handle
                child.position = objectCenter;
                child.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                child.localScale = new Vector3(handleSize, handleSize, handleSize);
            }
        }
    }

    // Call this method whenever you activate rotation handles
    private void ActivateRotationHandles(Transform parentTransform)
    {
        if (parentTransform == null) return;

        // Find and activate rotation handles by tags
        foreach (Transform child in parentTransform)
        {
            if (child.gameObject.CompareTag("XRot") ||
                child.gameObject.CompareTag("YRot") ||
                child.gameObject.CompareTag("ZRot"))
            {
                child.gameObject.SetActive(true);
            }
        }

        // Setup handles in world space
        SetupRotationHandles(parentTransform);

        // Initialize handle rotations
        InitializeRotationHandles(parentTransform);
    }

    // Helper function to smooth damp between quaternions (similar to Vector3.SmoothDamp but for rotations)
    private Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, ref Vector3 angVelocity, float smoothTime)
    {
        if (smoothTime < 0.0001f) return target;

        // Convert to euler angles for smooth damping
        Vector3 currentEuler = current.eulerAngles;
        Vector3 targetEuler = target.eulerAngles;

        // Fix angles to avoid weird behavior when crossing 360 degrees
        for (int i = 0; i < 3; i++)
        {
            while (targetEuler[i] - currentEuler[i] > 180f) targetEuler[i] -= 360f;
            while (targetEuler[i] - currentEuler[i] < -180f) targetEuler[i] += 360f;
        }

        // Apply smooth damp to each component
        Vector3 result = new Vector3(
            Mathf.SmoothDamp(currentEuler.x, targetEuler.x, ref angVelocity.x, smoothTime),
            Mathf.SmoothDamp(currentEuler.y, targetEuler.y, ref angVelocity.y, smoothTime),
            Mathf.SmoothDamp(currentEuler.z, targetEuler.z, ref angVelocity.z, smoothTime)
        );

        return Quaternion.Euler(result);
    }


    // Hide rotation angle text after delay
    private void HideRotationAngleText()
    {
        if (rotationAngleText != null)
            rotationAngleText.SetActive(false);
    }

    // Update the text displaying rotation angles
    private void UpdateRotationAngleText()
    {
        if (currentRotationHandle == null || currentRotationHandle.transform.parent == null)
            return;

        // Get current rotation in degrees
        Vector3 angles = currentRotationHandle.transform.parent.eulerAngles;

        // Normalize angles to -180 to 180 range for more intuitive display
        if (angles.x > 180) angles.x -= 360;
        if (angles.y > 180) angles.y -= 360;
        if (angles.z > 180) angles.z -= 360;

        // Update text components
        if (xAngleText != null)
            xAngleText.text = "X: " + angles.x.ToString("F1") + "°";
        if (yAngleText != null)
            yAngleText.text = "Y: " + angles.y.ToString("F1") + "°";
        if (zAngleText != null)
            zAngleText.text = "Z: " + angles.z.ToString("F1") + "°";

        // Highlight the active axis text
        Color highlightColor = GetAxisColor(currentRotationAxis);
        Color defaultColor = new Color(0.8f, 0.8f, 0.8f);

        if (xAngleText != null) xAngleText.color = (currentRotationAxis == "X") ? highlightColor : defaultColor;
        if (yAngleText != null) yAngleText.color = (currentRotationAxis == "Y") ? highlightColor : defaultColor;
        if (zAngleText != null) zAngleText.color = (currentRotationAxis == "Z") ? highlightColor : defaultColor;
    }

    private void SetupRotationUI()
    {
        // Create parent panel if it doesn't exist
        if (rotationAngleText == null)
        {
            // Create Canvas if it doesn't exist in scene
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();

                // Position the canvas in front of the camera
                canvas.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;
                canvas.transform.rotation = Camera.main.transform.rotation;
                canvas.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            }

            // Create rotation panel
            GameObject panelObj = new GameObject("RotationPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(160, 100);
            panelRect.localPosition = new Vector3(0, 100, 0);

            // Add background image
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Create text objects for each axis
            CreateAxisText(panelObj, "XAngleText", new Vector3(0, 30, 0), ref xAngleText);
            CreateAxisText(panelObj, "YAngleText", new Vector3(0, 0, 0), ref yAngleText);
            CreateAxisText(panelObj, "ZAngleText", new Vector3(0, -30, 0), ref zAngleText);

            // Save reference to the panel
            rotationAngleText = panelObj;
            rotationAngleText.SetActive(false);
        }
    }

    // Helper method to create text elements
    private void CreateAxisText(GameObject parent, string name, Vector3 localPos, ref TextMeshProUGUI textRef)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);
        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(150, 30);
        rectTransform.localPosition = localPos;

        textRef = textObj.AddComponent<TextMeshProUGUI>();
        textRef.alignment = TextAlignmentOptions.Center;
        textRef.fontSize = 18;
        textRef.text = name.Replace("AngleText", ": 0.0°");
        textRef.color = Color.white;
    }


    //--------------------- Object Spawn ------------------------



    private void PrefabSpawn(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting) return;

        // First check if we're over a shape selector
        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);

        if (hit2D.collider != null)
        {
            // Check if we're hovering over any of the shape selectors
            isOverShapeSelector = hit2D.collider.CompareTag("Squares") ||
                                 hit2D.collider.CompareTag("Circles") ||
                                 hit2D.collider.CompareTag("Triangles") ||
                                 hit2D.collider.CompareTag("Capsules") ||
                                 hit2D.collider.CompareTag("Hexagon");

            // Handle selecting a shape type when pinching on a shape selector
            if (!canSpawnPrefab && isHoldingNow && isOverShapeSelector)
            {
                // Determine which prefab to spawn based on the tag
                if (hit2D.collider.CompareTag("Squares"))
                {
                    selectedPrefabType = squarePrefab;
                    canSpawnPrefab = true;
                    print("Can spawn square prefab");
                }
                else if (hit2D.collider.CompareTag("Circles"))
                {
                    selectedPrefabType = circlePrefab;
                    canSpawnPrefab = true;
                    print("Can spawn circle prefab");
                }
                else if (hit2D.collider.CompareTag("Triangles"))
                {
                    selectedPrefabType = trianglePrefab;
                    canSpawnPrefab = true;
                    print("Can spawn triangle prefab");
                }
                else if (hit2D.collider.CompareTag("Capsules"))
                {
                    selectedPrefabType = capsulePrefab;
                    canSpawnPrefab = true;
                    print("Can spawn capsule prefab");
                }
                else if (hit2D.collider.CompareTag("Hexagon"))
                {
                    selectedPrefabType = hexagonPrefab;
                    canSpawnPrefab = true;
                    SetChildrenActive(selectedPrefabType, false);
                    print("Can spawn hexagon prefab");
                }

                if (canSpawnPrefab)
                {
                    BGcollider.SetActive(true);
                }
            }
        }
        else
        {
            isOverShapeSelector = false;
        }

        // Only allow spawning if we're not over a shape selector
        if (isOverShapeSelector && canSpawnPrefab)
        {
            // If we're still over a shape selector, don't allow spawning to begin yet
            return;
        }

        // Handle prefab spawning in 3D space with Z locked at -87
        RaycastHit hit;
        if (Physics.SphereCast(rayOrigin, 0.1f, rayDirection, out hit, rayDistance))
        {
            // Use the hit point's X and Y, but force Z to be -87
            Vector3 hitPoint = new Vector3(hit.point.x, hit.point.y, -87.5f);

            if (canSpawnPrefab && !isMovingLine)
            {
                if (isHoldingNow)
                {
                    if (!isSpawningPrefab)
                    {
                        // Start spawning with preview sphere and mask
                        CreatePreviewElements(hitPoint);
                        BGcollider.SetActive(false);
                        // Store start point but don't create actual prefab yet
                        prefabStartPoint = hitPoint;
                        isSpawningPrefab = true;
                    }

                    if (isSpawningPrefab)
                    {
                        // Update the preview mask and end sphere to follow the ray
                        UpdatePreviewElements(prefabStartPoint, hitPoint);
                    }
                }

                if (!isHoldingNow && isSpawningPrefab)
                {
                    // Only now create the actual prefab
                    PrefabCreation(prefabStartPoint);
                    ScalePrefabBetweenPoints(prefabStartPoint, hitPoint);

                    print("Prefab spawn complete");

                    // Clean up preview elements
                    CleanupPreviewElements();

                    // Reset tracking variables
                    currentPrefab = null;
                    canSpawnPrefab = false;
                    isSpawningPrefab = false;
                    selectCol = false;
                    selectedPrefabType = null; // Reset the selected prefab type
                }
            }
        }
    }

    private void CreatePreviewElements(Vector3 hitPoint)
    {
        // Create a sphere at the start point
        previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        previewSphere.transform.position = hitPoint;
        previewSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Set a distinct material for the preview sphere
        Renderer sphereRenderer = previewSphere.GetComponent<Renderer>();
        sphereRenderer.material.color = Color.yellow;

        // Create end sphere (initially at the same position)
        endSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        endSphere.transform.position = hitPoint;
        endSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Set a distinct material for the end sphere
        Renderer endSphereRenderer = endSphere.GetComponent<Renderer>();
        endSphereRenderer.material.color = Color.green;

        // Create a preview mask object that will follow the ray
        previewMask = new GameObject("PreviewMask");
        previewMask.transform.position = hitPoint;

        // Add a semi-transparent quad or other visual indicator
        // This is just a placeholder - you can customize this based on the shape you're creating
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.SetParent(previewMask.transform);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = Quaternion.Euler(0, 0, 90); // Face upward

        Renderer quadRenderer = quad.GetComponent<Renderer>();
        Material previewMaterial = new Material(Shader.Find("Transparent/Diffuse"));
        previewMaterial.color = new Color(0.5f, 0.5f, 1f, 0.3f); // Semi-transparent blue
        quadRenderer.material = previewMaterial;
    }

    private void UpdatePreviewElements(Vector3 startPoint, Vector3 endPoint)
    {
        if (previewMask == null || endSphere == null) return;

        // Ensure Z coordinate is consistent
        endPoint.z = -87.5f;

        // Update end sphere position
        endSphere.transform.position = endPoint;

        // Calculate the distance between start and end points
        float distance = Vector2.Distance(
            new Vector2(startPoint.x, startPoint.y),
            new Vector2(endPoint.x, endPoint.y)
        );

        // Calculate the midpoint
        Vector3 midPoint = (startPoint + endPoint) / 2;
        midPoint.z = -87.5f;

        // Position the preview mask at the midpoint
        previewMask.transform.position = midPoint;

        // Scale the preview mask based on the shape type and distance
        // For simplicity, we're just scaling a quad here
        GameObject previewShape = previewMask.transform.GetChild(0).gameObject;
        previewShape.transform.localScale = new Vector3(distance, distance, 1);
    }

    private void CleanupPreviewElements()
    {
        // Destroy the preview elements
        if (previewSphere != null)
        {
            Destroy(previewSphere);
            previewSphere = null;
        }

        if (endSphere != null)
        {
            Destroy(endSphere);
            endSphere = null;
        }

        if (previewMask != null)
        {
            Destroy(previewMask);
            previewMask = null;
        }
    }

    private void PrefabCreation(Vector3 hitPoint)
    {
        // Instantiate the selected prefab at the hit point
        if (selectedPrefabType != null)
        {
            currentPrefab = Instantiate(selectedPrefabType, hitPoint, Quaternion.identity);

            // Set initial scale to a default value
            currentPrefab.transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else
        {
            Debug.LogError("No prefab selected to spawn!");
        }
    }

    private void ScalePrefabBetweenPoints(Vector3 startPoint, Vector3 endPoint)
    {
        if (currentPrefab == null) return;

        // Ensure both points are at Z = -87
        startPoint.z = -87f;
        endPoint.z = -87f;

        // Calculate the distance between the two points on the X-Y plane
        float distance = Vector2.Distance(
            new Vector2(startPoint.x, startPoint.y),
            new Vector2(endPoint.x, endPoint.y)
        );

        // Scale the prefab based on the distance
        float scaleValue = Mathf.Max(0.1f, distance);
        currentPrefab.transform.localScale = new Vector3(scaleValue, scaleValue, scaleValue);

        // Calculate the midpoint between start and end points
        Vector3 midPoint = (startPoint + endPoint) / 2;
        midPoint.z = -87f;  // Force Z to -87

        // Position the prefab at the midpoint
        currentPrefab.transform.position = midPoint;

        // Keep rotation fixed at (0,0,0)
        currentPrefab.transform.rotation = Quaternion.Euler(0, 0, 0);
    }



    //---------------------- Line Drawing ------------------------


    // Declare dictionaries for 3D sphere-to-anchor mapping
    private Dictionary<GameObject, GameObject> sphereToAnchorMap = new Dictionary<GameObject, GameObject>();

    // 3D sphere references for dragging and manipulation
    private GameObject draggedSphere = null;
    private GameObject fixedSphere = null;
    private GameObject selectedLineForSpheres = null;//---------------------- 3D Line Drawing ------------------------

    [SerializeField] private List<string> excludedFinishObjects = new List<string>();
    // Add the names of Finish objects you don't want to draw on in the Inspector

    private void LineDraw(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (rot || block || adjusting)
            return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);

        // Replace 2D CircleCast with 3D SphereCast
        RaycastHit hit3D;
        bool hitSomething = Physics.SphereCast(rayOrigin, hoverOffset, rayDirection, out hit3D, rayDistance);

        // If the user just started pinching (isHoldingNow became true this frame)
        if (isHoldingNow && !wasPinchingLastFrame)
        {
            // Initially hide all spheres
            HideAllSpheres();
            // Check if we're pinching on a line
            if (hitSomething && hit3D.collider.CompareTag("DrawnLine"))
            {
                GameObject hitLine = hit3D.collider.gameObject;
                GameObject parentLine = (hitLine.transform.parent != null && hitLine.transform.parent.CompareTag("DrawnLine"))
                    ? hitLine.transform.parent.gameObject
                    : hitLine; // Fallback to hitLine if parent is null

                // Show spheres for this line when pinching on it
                ShowSpheresForLine(parentLine);
                pinchingOnLine = true;
                selectedLineForSpheres = parentLine;
            }

            if (hitSomething && hit3D.collider.CompareTag("Sphere"))
            {
                // If pinching on a sphere, keep it visible
                GameObject hitSphere = hit3D.collider.gameObject;
                GameObject parentLine = hitSphere.transform.parent.gameObject;
                ShowSpheresForLine(parentLine);
                pinchingOnLine = true;
                selectedLineForSpheres = parentLine;
            }
            else
            {
                // Pinching elsewhere, hide all spheres
                pinchingOnLine = false;
                selectedLineForSpheres = null;
            }
        }

        // Always keep spheres visible for the line being manipulated
        if (selectedLineForSpheres != null && pinchingOnLine)
        {
            ShowSpheresForLine(selectedLineForSpheres);
        }

        // Check if we hit a DrawnLine and handle line movement
        if (hitSomething)
        {
            if (hit3D.collider.CompareTag("DrawnLine") && !canDraw && !isDragging)
            {
                GameObject hitLine = hit3D.collider.gameObject;
                GameObject parentLine = (hitLine.transform.parent != null && hitLine.transform.parent.CompareTag("DrawnLine"))
                    ? hitLine.transform.parent.gameObject
                    : hitLine; // Fallback to hitLine if parent is null

                if (isHoldingNow && !isMovingLine)
                {
                    // Start moving the line
                    selectedLine = parentLine;
                    isMovingLine = true;
                    initialHitPoint = hit3D.point;

                    if (selectedLine != null)
                    {
                        LineRenderer lr = selectedLine.GetComponent<LineRenderer>();
                        if (lr != null)
                        {
                            // Store the initial positions of the line points
                            initialLinePositions = new Vector3[lr.positionCount];
                            for (int i = 0; i < lr.positionCount; i++)
                            {
                                initialLinePositions[i] = lr.GetPosition(i);
                            }
                        }

                        // Show spheres for the line being moved
                        ShowSpheresForLine(selectedLine);
                    }
                    else
                    {
                        Debug.LogWarning("Selected line is null!");
                    }
                }
            }
            if (hit2D.collider != null && hit2D.collider.CompareTag("Line") && !canDraw)
            {

                if (isHoldingNow)
                {

                    BGcollider.SetActive(true);
                    canDraw = true;
                    print("can draw");
                }
            }
            if (hit3D.collider.CompareTag("Sphere") && !canDraw)
            {
                if (isHoldingNow)
                {
                    GameObject hitSphere = hit3D.collider.gameObject;

                    // If no sphere is currently fixed, or the hit sphere is not the fixed sphere
                    if (fixedSphere == null || hitSphere != fixedSphere)
                    {
                        draggedSphere = hitSphere;
                        isDragging = true;

                        // If no sphere is fixed, set the other sphere as fixed
                        if (fixedSphere == null)
                        {
                            LineRenderer lr = hitSphere.transform.parent.GetComponent<LineRenderer>();
                            if (lr != null)
                            {
                                // Determine which sphere is the other one
                                fixedSphere = (hitSphere.name == "StartPointSphere")
                                    ? lr.transform.Find("EndPointSphere").gameObject
                                    : lr.transform.Find("StartPointSphere").gameObject;
                            }
                        }
                    }
                }
            }
        }

        // Handle moving the entire line
        if (isMovingLine && selectedLine != null && isHoldingNow)
        {
            // Calculate movement delta
            Vector3 currentPoint = rayOrigin + (rayDirection * hit3D.distance);
            Vector3 movementDelta = currentPoint - initialHitPoint;

            // Move the line transform
            selectedLine.transform.position += movementDelta;

            // Update the LineRenderer points manually
            LineRenderer lr = selectedLine.GetComponent<LineRenderer>();
            if (lr != null && initialLinePositions != null)
            {
                for (int i = 0; i < lr.positionCount && i < initialLinePositions.Length; i++)
                {
                    // Apply the same movement to each point
                    lr.SetPosition(i, initialLinePositions[i] + (currentPoint - initialHitPoint));
                }

                // Update the initialLinePositions for continuous movement
                for (int i = 0; i < initialLinePositions.Length; i++)
                {
                    initialLinePositions[i] += movementDelta;
                }
            }

            // Update the initialHitPoint for the next frame
            initialHitPoint = currentPoint;
        }
        else if (isDragging && draggedSphere != null && isHoldingNow)
        {
            // Calculate new position based on ray intersection with a plane
            Plane draggingPlane = new Plane(Camera.main.transform.forward, draggedSphere.transform.position);
            float distance;
            if (draggingPlane.Raycast(new Ray(rayOrigin, rayDirection), out distance))
            {
                Vector3 newPosition = rayOrigin + rayDirection * distance;
                // Move the dragged sphere
                draggedSphere.transform.position = newPosition;
                UpdateLineFromSphere(draggedSphere);
                HandleSphereAnchorAttachment();
            }
        }

        // Reset states when releasing
        if (!isHoldingNow)
        {
            isDragging = false;
            draggedSphere = null;
            fixedSphere = null;

            if (isMovingLine)
            {
                isMovingLine = false;
                selectedLine = null;
                initialLinePositions = null; // Clear stored positions
            }
        }

        // Handle line drawing
        RaycastHit hit;
        if (!Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance, targetLayer)) return;

        // We'll keep the "Finish" tag check but filter specific objects
        if (hit.collider != null && hit.collider.CompareTag("Finish") && canDraw && !isMovingLine)
        {
            GameObject canvasObject = hit.collider.gameObject;

            // Skip drawing on excluded Finish objects
            if (excludedFinishObjects.Contains(canvasObject.name))
            {
                return; // Skip drawing on this object
            }

            Vector3 hitPoint = hit.point;

            if (isHoldingNow)
            {
                if (!isDrawingLine)
                {
                    LineCreation(hitPoint, canvasObject);
                    isDrawingLine = true;

                    // Show spheres while actively drawing a new line
                    if (currentLine != null)
                    {
                        ShowSpheresForLine(currentLine);
                    }
                }

                if (currentLineRenderer != null)
                {
                    currentLineRenderer.SetPosition(1, hitPoint);

                    // Continue showing spheres while drawing
                    if (currentLine != null)
                    {
                        ShowSpheresForLine(currentLine);
                    }
                }
            }

            if (!isHoldingNow && isDrawingLine && canDraw)
            {
                print("draw ends");
                if (currentLineRenderer != null)
                {
                    currentLineRenderer.SetPosition(1, hitPoint);

                    // Complete the line
                    GameObject completedLine = currentLine;
                    AddColliderToLine(completedLine);

                    // Hide the spheres immediately when drawing completes
                    HideSpheresForLine(completedLine);

                    currentLineRenderer = null;
                    currentLine = null;

                    // Reset fixed sphere tracking
                    fixedSphere = null;
                }

                canDraw = false;
                isDrawingLine = false;

                BGcollider.SetActive(false);
            }
        }

        // Store pinch state for next frame
        wasPinchingLastFrame = isHoldingNow;
    }
    private void HideAllSpheres()
    {
        // Find all objects with "Sphere" tag and disable them
        GameObject[] allSpheres = GameObject.FindGameObjectsWithTag("Sphere");
        foreach (GameObject sphere in allSpheres)
        {
            sphere.gameObject.SetActive(false);
        }
    }

    private void ShowSpheresForLine(GameObject line)
    {
        if (line == null) return;

        // Find spheres that are children of this line
        Transform startSphere = line.transform.Find("StartPointSphere");
        Transform endSphere = line.transform.Find("EndPointSphere");

        if (startSphere != null)
        {
            startSphere.gameObject.SetActive(true);
        }

        if (endSphere != null)
        {
            endSphere.gameObject.SetActive(true);
        }

        // Enable their renderers
        if (startSphere != null)
        {
            MeshRenderer renderer = startSphere.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = true;
        }

        if (endSphere != null)
        {
            MeshRenderer renderer = endSphere.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    private void HideSpheresForLine(GameObject line)
    {
        if (line == null) return;

        // Find spheres that are children of this line
        Transform startSphere = line.transform.Find("StartPointSphere");
        Transform endSphere = line.transform.Find("EndPointSphere");

        if (startSphere != null)
        {
            startSphere.gameObject.SetActive(false);
        }

        if (endSphere != null)
        {
            endSphere.gameObject.SetActive(false);
        }

        // Disable their renderers
        if (startSphere != null)
        {
            MeshRenderer renderer = startSphere.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        if (endSphere != null)
        {
            MeshRenderer renderer = endSphere.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    private void UpdateLineFromSphere(GameObject sphere)
    {
        LineRenderer lr = sphere.transform.parent.GetComponent<LineRenderer>();
        if (lr == null) return;

        if (sphere.name == "StartPointSphere")
        {
            lr.SetPosition(0, sphere.transform.position);
        }
        else
        {
            lr.SetPosition(1, sphere.transform.position);
        }

        // Update the collider to match the new line position
        UpdateLineCollider(lr.gameObject);
    }

    private void AddColliderToLine(GameObject line)
    {
        LineRenderer lr = line.GetComponent<LineRenderer>();
        if (lr == null || lr.positionCount < 2) return;

        // Get world positions of line start and end
        Vector3 startWorld = lr.GetPosition(0);
        Vector3 endWorld = lr.GetPosition(1);

        // Create a capsule collider for the line
        CreateLineCapsuleCollider(line, startWorld, endWorld);

        // Create sphere at the end point
        CreateSphereAtPoint(endWorld, line, false);

        // Ensure the line stays on top by adjusting Z position slightly
        Vector3 linePos = line.transform.position;
        line.transform.position = new Vector3(linePos.x, linePos.y, linePos.z - 0.01f);
    }
    private void CreateSphereAtPoint(Vector3 point, GameObject parentLine, bool isStartPoint)
    {
        // Create a sphere using Unity's primitive
        GameObject sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObject.name = isStartPoint ? "StartPointSphere" : "EndPointSphere";
        sphereObject.tag = "Sphere";

        // Get the renderer to adjust appearance
        MeshRenderer meshRenderer = sphereObject.GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));
        meshRenderer.material.color = Color.white;

        // Make sphere invisible by default
        meshRenderer.enabled = false;

        float sphereSize = 0.04f;
        sphereObject.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);

        // Parent it first, THEN set local position
        sphereObject.transform.SetParent(parentLine.transform);

        // Convert world position to local space relative to the line
        Vector3 localPosition = parentLine.transform.InverseTransformPoint(point);
        sphereObject.transform.localPosition = localPosition;

        // The primitive already comes with a collider, so we just adjust its radius
        SphereCollider sphereCollider = sphereObject.GetComponent<SphereCollider>();
        sphereCollider.radius = 0.4f;
    }
    private void LineCreation(Vector3 hitPoint, GameObject canvasObject)
    {
        // Create a new line GameObject
        currentLine = new GameObject("DrawnLine");

        // Add the LineRenderer component
        currentLineRenderer = currentLine.AddComponent<LineRenderer>();

        // Configure LineRenderer
        currentLineRenderer.startWidth = 0.02f;
        currentLineRenderer.endWidth = 0.02f;
        currentLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        currentLineRenderer.startColor = Color.black;
        currentLineRenderer.endColor = Color.black;
        currentLineRenderer.positionCount = 2;
        currentLineRenderer.useWorldSpace = true;
        currentLineRenderer.sortingOrder = 1;
        currentLine.tag = "DrawnLine";
        currentLineRenderer.alignment = LineAlignment.TransformZ;

        // Store the hit point as the start point of the line
        lineStartPoint = hitPoint;
        currentLineRenderer.SetPosition(0, hitPoint);
        currentLineRenderer.SetPosition(1, hitPoint);

        // Create sphere at the start point (hidden by default)
        CreateSphereAtPoint(hitPoint, currentLine, true);

        // Don't set the parent to the canvas
        // Instead, keep it as a top-level object in the scene
        // currentLine.transform.SetParent(canvasObject.transform); - REMOVED
    }

    private void CreateLineCapsuleCollider(GameObject line, Vector3 start, Vector3 end)
    {
        // Create a child GameObject for the collider if it doesn't exist
        GameObject colliderObject = line.transform.Find("LineCollider")?.gameObject;
        if (colliderObject == null)
        {
            colliderObject = new GameObject("LineCollider");
            colliderObject.transform.SetParent(line.transform);
        }
        colliderObject.tag = "DrawnLine";

        // Reset position and rotation
        colliderObject.transform.localPosition = Vector3.zero;
        colliderObject.transform.localRotation = Quaternion.identity;

        // Add or get CapsuleCollider component
        CapsuleCollider capsuleCollider = colliderObject.GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            capsuleCollider = colliderObject.AddComponent<CapsuleCollider>();
        }

        // Convert points to local space
        Vector3 startLocal = line.transform.InverseTransformPoint(start);
        Vector3 endLocal = line.transform.InverseTransformPoint(end);

        // Calculate direction and length
        Vector3 direction = endLocal - startLocal;
        float length = direction.magnitude;
        direction.Normalize();

        // Set collider properties
        float lineWidth = line.GetComponent<LineRenderer>().startWidth;

        // Set capsule parameters
        capsuleCollider.radius = lineWidth / 2;
        capsuleCollider.height = length + lineWidth; // Add radius*2 to account for the rounded ends

        // Position the collider at the midpoint of the line
        colliderObject.transform.localPosition = (startLocal + endLocal) / 2;

        // Orient the capsule along the line
        // The direction index determines which local axis to align with the capsule's height
        // 0 = X, 1 = Y, 2 = Z (Unity's default is Y)
        capsuleCollider.direction = 2; // Using Z-axis for height

        // Rotate the collider to align with the line direction
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
        colliderObject.transform.localRotation = rotation;
    }

    private void UpdateLineCollider(GameObject line)
    {
        LineRenderer lr = line.GetComponent<LineRenderer>();
        if (lr == null || lr.positionCount < 2) return;

        Vector3 startWorld = lr.GetPosition(0);
        Vector3 endWorld = lr.GetPosition(1);

        // Update capsule collider
        GameObject colliderObj = line.transform.Find("LineCollider")?.gameObject;
        if (colliderObj != null)
        {
            CapsuleCollider capsuleCollider = colliderObj.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                // Convert points to local space
                Vector3 startLocal = line.transform.InverseTransformPoint(startWorld);
                Vector3 endLocal = line.transform.InverseTransformPoint(endWorld);

                // Calculate direction and length
                Vector3 direction = endLocal - startLocal;
                float length = direction.magnitude;
                direction.Normalize();

                // Update collider properties
                capsuleCollider.height = length + lr.startWidth;

                // Update position to midpoint
                colliderObj.transform.localPosition = (startLocal + endLocal) / 2;

                // Update rotation to align with line direction
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                colliderObj.transform.localRotation = rotation;
            }
        }
    }

    private void HandleSphereAnchorAttachment()
    {
        // Only check for anchor attachment if we're dragging a sphere
        if (draggedSphere != null)
        {
            // Using 3D OverlapSphere instead of 2D OverlapCircleAll
            Collider[] hitColliders = Physics.OverlapSphere(draggedSphere.transform.position, 0.1f);

            GameObject nearestAnchor = null;
            float minDistance = 0.5f; // Minimum distance threshold for attachment

            foreach (Collider collider in hitColliders)
            {
                if (collider.CompareTag("Anchor"))
                {
                    float distance = Vector3.Distance(draggedSphere.transform.position, collider.transform.position);
                    if (distance < minDistance)
                    {
                        nearestAnchor = collider.gameObject;
                        minDistance = distance;
                    }
                }
            }

            // If we found an anchor and we're not already attached to it
            if (nearestAnchor != null && (!sphereToAnchorMap.ContainsKey(draggedSphere) || sphereToAnchorMap[draggedSphere] != nearestAnchor))
            {
                // Attach to the anchor
                AttachSphereToAnchor(draggedSphere, nearestAnchor);
            }

            // If we're not near any anchor but we were previously attached, detach
            if (nearestAnchor == null && sphereToAnchorMap.ContainsKey(draggedSphere))
            {
                DetachSphereFromAnchor(draggedSphere);
            }
        }
    }

    private void AttachSphereToAnchor(GameObject sphere, GameObject anchor)
    {
        // Store the attachment in our mapping
        sphereToAnchorMap[sphere] = anchor;

        // Calculate and store the offset (for when the anchor moves)
        Vector3 offset = sphere.transform.position - anchor.transform.position;
        anchorToOffsetMap[sphere] = Vector3.zero; // No offset when snapping directly

        // Snap the sphere to the anchor position
        sphere.transform.position = anchor.transform.position;

        // Update the line to match the new sphere position
        UpdateLineFromSphere(sphere);

        // Visual feedback (optional)
        MeshRenderer sphereRenderer = sphere.GetComponent<MeshRenderer>();
        if (sphereRenderer != null)
        {
            // Store original color if needed
            // sphereRenderer.material.color = Color.green; // Change color to indicate attachment
        }

        Debug.Log($"Attached {sphere.name} to anchor {anchor.name}");
    }

    private void DetachSphereFromAnchor(GameObject sphere)
    {
        // Remove the attachment from our mapping
        if (sphereToAnchorMap.ContainsKey(sphere))
        {
            sphereToAnchorMap.Remove(sphere);
            anchorToOffsetMap.Remove(sphere);

            // Reset visual feedback
            MeshRenderer sphereRenderer = sphere.GetComponent<MeshRenderer>();
            if (sphereRenderer != null)
            {
                // Reset to original color
                // sphereRenderer.material.color = Color.white;
            }

            Debug.Log($"Detached {sphere.name} from anchor");
        }
    }

    private void UpdateAttachedLines()
    {
        // For each attached sphere
        foreach (var kvp in sphereToAnchorMap.ToList()) // ToList to avoid collection modification during iteration
        {
            GameObject sphere = kvp.Key;
            GameObject anchor = kvp.Value;

            // If either the sphere or anchor has been destroyed, remove the mapping
            if (sphere == null || anchor == null)
            {
                sphereToAnchorMap.Remove(sphere);
                continue;
            }

            // Update the sphere position to follow the anchor
            sphere.transform.position = anchor.transform.position;

            // Update the line
            UpdateLineFromSphere(sphere);
        }
    }

    private void DetectSpherePinch(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting || canDraw) return;

        // Using 3D raycasting instead of the 2D getRaycast2d function
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance);

        if (hitSomething)
        {
            // Check if the collider is tagged as a "Sphere"
            if (hit.collider.CompareTag("Sphere"))
            {
                // If the user is holding the pinch gesture and it's not already in pinch mode
                if (isHoldingNow && !isPinching)
                {
                    isPinching = true;
                    ActivateAllAnchorsInScene(true); // Activate anchors when the pinch starts
                }
            }
        }

        // When the pinch gesture is released
        if (!isHoldingNow && isPinching)
        {
            isPinching = false;
            ActivateAllAnchorsInScene(false);
        }
    }

    private void ActivateAllAnchorsInScene(bool anchoractive)
    {
        // Find all game objects in the scene, including inactive ones
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);

        // Iterate over all objects in the scene
        foreach (GameObject obj in allObjects)
        {
            // Check if the object has the "Anchor" tag
            if (obj.CompareTag("Anchor"))
            {
                // Check if the anchor has a parent (i.e., is a child of the Target object)
                if (obj.transform.parent != null)
                {
                    obj.SetActive(anchoractive);
                }
            }
        }
    }

    //---------------------- Button Functions ----------------------

    private void ButtonFeedback(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (rot || adjusting || block || multiSelectMode || isSelectionMaskActive)
        {
            if (maskObject != null)
            {
                maskObject.SetActive(false);
            }
            return;
        }

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.GetRayIntersection(new Ray(rayOrigin, rayDirection), Mathf.Infinity);
        if (hit2D.collider != null)
        {
            if (hit2D.collider.CompareTag("Destroy") ||
                hit2D.collider.CompareTag("Line") ||
                hit2D.collider.CompareTag("ColorPicker") ||
                hit2D.collider.CompareTag("Shape") ||
                hit2D.collider.CompareTag("Color") ||
                hit2D.collider.CompareTag("Finish") ||
                hit2D.collider.CompareTag("Squares") ||
                hit2D.collider.CompareTag("Circles") ||
                hit2D.collider.CompareTag("Triangles") ||
                hit2D.collider.CompareTag("Capsules") ||
                hit2D.collider.CompareTag("DrawnLine") ||
                hit2D.collider.CompareTag("UI") ||
                hit2D.collider.CompareTag("Hexagon") ||
                hit2D.collider.CompareTag("Pen") ||
                hit2D.collider.CompareTag("MultiSelect") ||
                hit2D.collider.CompareTag("HandRay") ||
                hit2D.collider.CompareTag("GazePinch") ||
                hit2D.collider.CompareTag("LooknDrop") ||
                hit2D.collider.CompareTag("Voice") ||
                hit2D.collider.CompareTag("3D2D"))
            {
                CreateMask(hit2D.collider.gameObject);

            }

            if (hit2D.collider.CompareTag("ColorPicker"))
            {
                // If pinching inside ColorPicker, activate the color object
                if (isHoldingNow && !ButtonPressed)
                {
                    print(hit2D.collider.gameObject.name);
                    ButtonPressed = true;
                    if (colorPanels != null)
                    {
                        colorPanels.SetActive(true);
                    }
                }
            }

            if (hit2D.collider.CompareTag("Shape"))
            {
                // If pinching inside ColorPicker, activate the color object
                if (isHoldingNow && !ButtonPressed)
                {
                    print(hit2D.collider.gameObject.name);
                    ButtonPressed = true;
                    if (ShapesPanels != null)
                    {
                        ShapesPanels.SetActive(true);
                    }
                }
            }
        }
        else
        {
            // If pinching outside any valid object, deactivate the color object
            if (isHoldingNow && ButtonPressed)
            {
                ButtonPressed = false;
                if (colorPanels != null)
                {
                    colorPanels.SetActive(false);
                }
                if (ShapesPanels != null)
                {
                    ShapesPanels.SetActive(false);
                }
            }

            if (maskObject != null)
            {
                maskObject.SetActive(false);
            }
        }
    }

    private Color originalcolor;
    private void DestroyObj(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if ( rot || adjusting) return;

        // Keep using 2D raycast for the Destroy UI button
        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);
        if (hit2D.collider != null)
        {
            if (hit2D.collider.CompareTag("Destroy"))
            {
                if (isHoldingNow)
                {
                    if (DoDestroy && DestroyObject != null)
                    {
                        // Destroy selected object mode
                        DestroyObject.SetActive(false);
                        Debug.Log("Destroyed object: " + DestroyObject.name);
                        DoDestroy = false;
                        DestroyObject = null;
                        isDestroy = false;
                        isHoldingNow = false;
                        return;
                    }
                }
            }
        }

        // Use SphereCast for 3D target selection
        RaycastHit hit;
        if (Physics.SphereCast(rayOrigin, 0.01f, rayDirection, out hit, rayDistance))
        {
            GameObject hitObject = hit.collider.gameObject.transform.parent != null
                ? hit.collider.gameObject.transform.parent.gameObject
                : hit.collider.gameObject;

            if (hit.collider.CompareTag("Target") || hit.collider.CompareTag("DrawnLine"))
            {
                if (isHoldingNow)
                {
                    if (!DoDestroy)
                    {
                        DoDestroy = true;
                        DestroyObject = hitObject;
                        // Optional: Add visual feedback that the object is selected for destruction
                        // For example, you could change its color temporarily
                        Renderer renderer = hitObject.GetComponent<Renderer>();
                        if (renderer != null)
                        {

                            originalcolor = renderer.material.color;
                                // Store original color if needed later
                                // originalColor = renderer.material.color;
                            //renderer.material.color = Color.red; // Highlight it red to indicate destruction
                        }

                        Debug.Log("Object selected for destruction: " + DestroyObject.name);
                    }
                }
            }
        }
    }

    private void ChangeColor2(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (isTransparentMode || rot || adjusting || canSpawnPrefab || multiselectiondone) return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);
        RaycastHit2D hit2DLayer = getRaycast2d(rayOrigin, rayDirection);

        bool isHoveringColor = false;  // Track if we are on a Color-tagged object

        // Check if we hit any collider
        if (hit2D.collider != null)
        {
            GameObject hitObject = hit2D.collider.gameObject;

            // Step 1: Copy color from Color-tagged object
            if (hit2D.collider.CompareTag("Color"))
            {
                isHoveringColor = true;

                // Sample the color at the hit point
                copiedColor = SampleExactSpriteColor(hit2D.point, hitObject);

                if (selectcolObject != null && selectcolObject.tag == "Target")
                {
                    Renderer renderer = selectcolObject.GetComponent<Renderer>();
                    if (renderer != null && selectCol)
                    {
                        for (int i = 0; i < renderer.materials.Length; i++)
                        {
                            renderer.materials[i].color = copiedColor;

                            if (isHoldingNow)
                            {
                                FinalColor = copiedColor;
                                InitialColor = FinalColor;
                                renderer.materials[i].color = FinalColor;
                                selectCol = false;
                                return;
                            }
                        }
                        Debug.Log("Applied color to: " + hitObject.name);
                    }
                }

                if (selectcolObject != null && selectcolObject.name == "Drawing" && selectCol)
                {
                    UpdateShapeFillColor(selectcolObject, copiedColor);
                }

                if (selectcolObject != null && selectcolObject.CompareTag("DrawnLine") && selectCol)
                {
                    selectCol = false;
                    GameObject parentObject = selectcolObject;
                    if (parentObject != null && isHoldingNow)
                    {
                        LineRenderer lineRenderer = parentObject.GetComponent<LineRenderer>();
                        if (lineRenderer != null)
                        {
                            lineRenderer.startColor = copiedColor;
                            lineRenderer.endColor = copiedColor;
                            Debug.Log("Applied color to LineRenderer: " + parentObject.name);
                            isColorCopyMode = false;
                        }
                    }
                }
            }
        }

        // ?? Reset color if not on "Color"
        if (!isHoveringColor && selectcolObject != null)
        {

            if (selectcolObject.CompareTag("Target"))
            {
                Renderer renderer = selectcolObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    for (int i = 0; i < renderer.materials.Length; i++)
                    {
                        renderer.materials[i].color = InitialColor;
                    }
                }
            }
            else if (selectcolObject.name == "Drawing")
            {
                UpdateShapeFillColor(selectcolObject, InitialColor);
            }
            else if (selectcolObject.CompareTag("DrawnLine"))
            {
                LineRenderer lineRenderer = selectcolObject.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    lineRenderer.startColor = InitialColor;
                    lineRenderer.endColor = InitialColor;
                }
            }
        }

        RaycastHit hit;
        // Select new target when hovering over Target or DrawnLine
        if (Physics.SphereCast(rayOrigin, 0.01f, rayDirection, out hit, rayDistance))
        {
            if (hit.collider.CompareTag("Target") || hit.collider.CompareTag("DrawnLine"))
            {
                GameObject targetObject = hit.collider.gameObject.transform.parent != null
                    ? hit.collider.gameObject.transform.parent.gameObject
                    : hit.collider.gameObject;

                if (isHoldingNow)
                {
                    selectcolObject = targetObject;
                    InitialColor = targetObject.GetComponent<Renderer>()?.material.color ?? Color.white;
                    print("------------selected--------" + targetObject);
                    selectCol = true;
                }
            }
        }
    }

    public void UpdateShapeFillColor(GameObject shapeObject, Color newColor)
    {
        if (shapeObject == null || !shapeObject.CompareTag("Target"))
        {
            Debug.LogWarning("Invalid shape object provided for color update.");
            return;
        }

        Renderer renderer = shapeObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = newColor; // Highlight it red to indicate destruction
        }
        else
        {
            Debug.LogWarning("Shape object does not have a valid SpriteRenderer or Sprite.");
        }
    }

    private Color SampleExactSpriteColor(Vector2 worldPoint, GameObject spriteObject)
    {
        // First try SpriteRenderer for regular sprites
        SpriteRenderer spriteRenderer = spriteObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            try
            {
                // Get sprite texture
                Sprite sprite = spriteRenderer.sprite;
                Texture2D texture = sprite.texture;

                // Convert world position to local space
                Vector3 localPoint = spriteRenderer.transform.InverseTransformPoint(worldPoint);

                // Calculate texture coordinates with higher precision
                // Need to account for:
                // 1. Sprite rect within the texture
                // 2. Pivot point
                // 3. Pixels per unit

                // Get sprite rect within the texture
                Rect spriteRect = sprite.rect;

                // Convert local position to normalized position within the sprite (0-1 range)
                // The sprite's size in world units = texture size in pixels / pixels per unit
                float normalizedX = (localPoint.x + (sprite.bounds.size.x / 2)) / sprite.bounds.size.x;
                float normalizedY = (localPoint.y + (sprite.bounds.size.y / 2)) / sprite.bounds.size.y;

                // Apply sprite flipping if needed
                if (spriteRenderer.flipX) normalizedX = 1 - normalizedX;
                if (spriteRenderer.flipY) normalizedY = 1 - normalizedY;

                // Map the normalized position to the sprite's rect within texture
                int pixelX = Mathf.RoundToInt(spriteRect.x + (normalizedX * spriteRect.width));
                int pixelY = Mathf.RoundToInt(spriteRect.y + (normalizedY * spriteRect.height));

                // Debug the coordinates
                Debug.Log($"World Point: {worldPoint}, Local Point: {localPoint}");
                Debug.Log($"Normalized: ({normalizedX:F3}, {normalizedY:F3})");
                Debug.Log($"Pixel Coordinates: ({pixelX}, {pixelY}) in texture size {texture.width}x{texture.height}");

                // Safety clamp
                pixelX = Mathf.Clamp(pixelX, 0, texture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, texture.height - 1);

                // Direct texture sampling approach
                Color pixelColor;

                // First try direct sampling (requires read/write enabled texture)
                try
                {
                    pixelColor = texture.GetPixel(pixelX, pixelY);
                    // If we got a valid color and not clear/white, return it
                    if (pixelColor.a > 0.1f && !(pixelColor.r > 0.95f && pixelColor.g > 0.95f && pixelColor.b > 0.95f))
                    {
                        return pixelColor * spriteRenderer.color; // Apply the sprite's tint
                    }
                }
                catch
                {
                    // Direct sampling failed, fallback to RenderTexture approach
                }

                // Create a readable copy of the texture using RenderTexture
                RenderTexture tempRT = RenderTexture.GetTemporary(
                    texture.width, texture.height, 0, RenderTextureFormat.ARGB32);

                // Copy the texture to the temporary render texture
                Graphics.Blit(texture, tempRT);

                // Store the currently active render texture
                RenderTexture prevRT = RenderTexture.active;
                RenderTexture.active = tempRT;

                // Create a readable texture and read the pixels
                Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readableTexture.Apply();

                // Restore previous render texture
                RenderTexture.active = prevRT;
                RenderTexture.ReleaseTemporary(tempRT);

                // Get the color from the readable texture
                pixelColor = readableTexture.GetPixel(pixelX, pixelY);

                // Apply the sprite's color/tint
                pixelColor = pixelColor * spriteRenderer.color;

                // Clean up
                Destroy(readableTexture);

                return pixelColor;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error sampling sprite: " + e.Message);
                // Fallback to renderer's color
                return spriteRenderer.color;
            }
        }

        // Try UI Image if sprite renderer not found
        UnityEngine.UI.Image image = spriteObject.GetComponent<UnityEngine.UI.Image>();
        if (image != null && image.sprite != null)
        {
            try
            {
                // Get the local position in the rect transform
                Vector2 localPoint;
                RectTransform rectTransform = image.rectTransform;

                // Convert screen point to local point in rectangle
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    Camera.main.WorldToScreenPoint(worldPoint),
                    image.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                    out localPoint))
                {
                    return image.color; // Conversion failed, use image tint
                }

                // Convert to normalized coordinates (0-1)
                // Need to account for the rect transform size
                Vector2 pivot = rectTransform.pivot;
                Vector2 normalizedPosition = new Vector2(
                    (localPoint.x + rectTransform.rect.width * pivot.x) / rectTransform.rect.width,
                    (localPoint.y + rectTransform.rect.height * pivot.y) / rectTransform.rect.height
                );

                // Get the sprite and its texture
                Sprite sprite = image.sprite;
                Texture2D texture = sprite.texture;
                Rect spriteRect = sprite.rect;

                // Calculate pixel coordinates in the texture
                int pixelX = Mathf.RoundToInt(spriteRect.x + (normalizedPosition.x * spriteRect.width));
                int pixelY = Mathf.RoundToInt(spriteRect.y + (normalizedPosition.y * spriteRect.height));

                // Debug info
                Debug.Log($"UI Image - Normalized: ({normalizedPosition.x:F3}, {normalizedPosition.y:F3})");
                Debug.Log($"UI Image - Pixel Coords: ({pixelX}, {pixelY})");

                // Safety clamp
                pixelX = Mathf.Clamp(pixelX, 0, texture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, texture.height - 1);

                // Try direct texture sampling first
                Color pixelColor;
                try
                {
                    pixelColor = texture.GetPixel(pixelX, pixelY);
                    // If we got a valid color and not clear/white, return it
                    if (pixelColor.a > 0.1f && !(pixelColor.r > 0.95f && pixelColor.g > 0.95f && pixelColor.b > 0.95f))
                    {
                        return pixelColor * image.color; // Apply the image's tint
                    }
                }
                catch
                {
                    // Direct sampling failed, fallback to RenderTexture approach
                }

                // Create a readable copy of the texture using RenderTexture
                RenderTexture tempRT = RenderTexture.GetTemporary(
                    texture.width, texture.height, 0, RenderTextureFormat.ARGB32);

                Graphics.Blit(texture, tempRT);
                RenderTexture prevRT = RenderTexture.active;
                RenderTexture.active = tempRT;

                Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readableTexture.Apply();

                RenderTexture.active = prevRT;
                RenderTexture.ReleaseTemporary(tempRT);

                pixelColor = readableTexture.GetPixel(pixelX, pixelY);
                pixelColor = pixelColor * image.color; // Apply the image's tint

                Destroy(readableTexture);
                return pixelColor;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error sampling UI Image: " + e.Message);
                return image.color; // Fallback to image color
            }
        }

        // Alternative approach for color wheels - check if the object name contains "color wheel"
        if (spriteObject.name.ToLower().Contains("color") && spriteObject.name.ToLower().Contains("wheel"))
        {
            // This is a specialized sampling for color wheels
            try
            {
                // Get transform position
                Vector3 objectCenter = spriteObject.transform.position;

                // Get vector from center to hit point
                Vector2 dirFromCenter = (Vector2)worldPoint - (Vector2)objectCenter;

                // Calculate angle and radius
                float angle = Mathf.Atan2(dirFromCenter.y, dirFromCenter.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360;

                float radius = dirFromCenter.magnitude / (spriteObject.transform.localScale.x * 0.5f);
                radius = Mathf.Clamp01(radius); // Normalize to 0-1

                // Convert to HSV color
                // Hue is based on angle around the wheel
                float hue = angle / 360f;

                // Saturation increases with radius (from center to edge)
                float saturation = radius;

                // Value (brightness) is always 1 for bright colors
                float value = 1f;

                // Create color from HSV
                Color hsvColor = Color.HSVToRGB(hue, saturation, value);

                Debug.Log($"Color wheel sampling - Angle: {angle:F1}°, Radius: {radius:F2}, HSV: ({hue:F3}, {saturation:F3}, {value:F3})");

                return hsvColor;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error in color wheel sampling: " + e.Message);
            }
        }

        // Fallback - couldn't get a valid color
        return Color.white;
    }

    private void ScaleParentOnDrag(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting || isMovingTarget) return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);

        // Check if we hit a UI-tagged object
        if (hit2D.collider != null && hit2D.collider.CompareTag("UI"))
        {
            GameObject hitObject = hit2D.collider.gameObject;
            Transform parentTransform = hitObject.transform.parent;
            if (parentTransform == null) return;

            if (isHoldingNow && !isScalingUI)
            {
                // Start dragging the UI element
                selectedUIElement = hitObject;
                isScalingUI = true;
                initialHitPointUI = endPoint;
                initialParentScale = parentTransform.localScale;
                initialRayDirection1 = rayDirection.normalized;
                Debug.Log("Started scaling UI parent: " + parentTransform.name + " with initial scale: " + initialParentScale);
            }
        }

        // Handle scaling while holding
        if (isScalingUI && selectedUIElement != null && isHoldingNow)
        {
            BGcollider.SetActive(true);
            Transform parentTransform = selectedUIElement.transform.parent;

            // Calculate distance between current point and initial point
            float distanceMoved = Vector3.Distance(endPoint, initialHitPointUI);

            // Determine direction (moving toward or away from the UI)
            float directionFactor = Vector3.Dot(rayDirection, endPoint - initialHitPointUI) > 0 ? 1 : -1;

            // Apply direction to distance
            float scaledDistance = distanceMoved * directionFactor;

            // Calculate scale factor (use logarithmic scaling for small values)
            // This provides more control for very small scale values like 0.017
            float scaleFactor = 1f + (scaledDistance * scaleModifier);

            // Limit extreme scaling for small initial values
            scaleFactor = Mathf.Clamp(scaleFactor, 0.5f, 2.0f);

            // Apply uniform scaling from initial scale
            Vector3 newScale = new Vector3(
                initialParentScale.x * scaleFactor,
                initialParentScale.y * scaleFactor,
                initialParentScale.z * scaleFactor
            );

            // Ensure minimum scale doesn't go too small
            float minScale = initialParentScale.x * 0.25f; // Allow scaling to 25% of original at minimum
            newScale.x = Mathf.Max(newScale.x, minScale);
            newScale.y = Mathf.Max(newScale.y, minScale);
            newScale.z = Mathf.Max(newScale.z, minScale);

            // Apply smooth scaling
            parentTransform.localScale = Vector3.Lerp(
                parentTransform.localScale,
                newScale,
                Time.deltaTime * smoothSpeed
            );

            // Debug scaling
            if (Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
            {
                Debug.Log("Scaling: Factor=" + scaleFactor + ", New Scale=" + newScale);
            }
        }

        // Reset when releasing
        if (!isHoldingNow && isScalingUI)
        {
            BGcollider.SetActive(false);
            isScalingUI = false;
            selectedUIElement = null;
            Debug.Log("Stopped scaling UI");
        }
    }

    //---------------------- MultiSelect ----------------------
    // Add these variables at class level
    private bool lastIsHolding = false;
    private bool wasDragging = false;
    private bool multiselectiondone = false;
    private void MultiSelectDetection(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (rot || block || adjusting)
            return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin))
        {
            return;
        }

        // Check if we're starting a selection or releasing
        // If we just released and weren't dragging, we need to handle that
        if (!isHoldingNow && lastIsHolding && !wasDragging && multiSelectMode)
        {
            // User clicked and released without dragging while in multiselect mode
            // Reset multiselect mode (optional - remove if you want it to stay in multiselect mode)
            Debug.Log("Released without dragging in multiselect mode");
        }

        // Track if we're dragging a selection
        if (isHoldingNow && selectionMask != null)
        {
            wasDragging = true;
        }
        else if (!isHoldingNow)
        {
            wasDragging = false;
        }

        // Save current holding state for next frame
        lastIsHolding = isHoldingNow;

        float hoverOffset = 0.01f;

        // Keep the original 2D check for the MultiSelect button
        RaycastHit2D hit2D = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0);

        // Check for activating multiselect mode
        if (!multiSelectMode)
        {
            if (hit2D.collider != null && hit2D.collider.CompareTag("MultiSelect"))
            {
                if (isHoldingNow)
                {
                    BGcollider.SetActive(true);
                    // Activate multiselect mode
                    multiSelectMode = true;
                    Debug.Log("Multi-Select Mode Activated");
                    return;
                }
            }
        }
        else // In multiselect mode
        {
            if (isHoldingNow)
            {
                // Get the 3D point in world space where the ray hits
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance))
                {
                    // If we're starting a new selection (not continuing an existing one)
                    if (selectionMask == null)
                    {
                        // Only start selection if we're NOT clicking on the MultiSelect button
                        // This allows user to first activate multiselect mode, then start drawing elsewhere
                        if (!(hit2D.collider != null && hit2D.collider.CompareTag("MultiSelect")))
                        {
                            selectionStartPosition = hit.point;
                            Create3DSelectionMask(selectionStartPosition);
                        }
                    }
                    else
                    {
                        // Continue updating existing selection
                        Update3DSelectionMask(hit.point);
                    }
                }
            }
            else // Not holding anymore
            {
                if (selectionMask != null)
                {
                    multiselectiondone = true;
                    BGcollider.SetActive(false);
                    // Finalize selection
                    Finalize3DSelection();
                }
            }
        }

        // Check if we're hovering over a 3D target
        RaycastHit targetHit;
        if (Physics.Raycast(rayOrigin, rayDirection, out targetHit, rayDistance))
        {
            istarget = targetHit.collider.CompareTag("Target");
        }
        else
        {
            istarget = false;
        }

        // Check for deselecting by pinching elsewhere
        if (isHoldingNow && !multiSelectMode && selectedObjects.Count > 0 && !istarget)
        {
            Debug.Log("---------------------deselecting -------------------");
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance))
            {
                // If pinch is not on the selection container, deselect
                if (hit.collider != null &&
                    hit.collider.gameObject != selectionMask &&
                    !selectedObjects.Contains(hit.collider.gameObject))
                {
                    multiselectiondone=false;
                    DeselectAll();
                }
            }
        }
    }

    private void Create3DSelectionMask(Vector3 position)
    {
        // Create a new game object for the selection mask
        selectionMask = new GameObject("3DSelectionMask");

        // Create a cube primitive to represent the selection volume visually
        GameObject visualCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualCube.name = "MaskVisual";
        visualCube.transform.SetParent(selectionMask.transform);

        // Remove the collider from the visual cube - we'll add our own later
        Destroy(visualCube.GetComponent<Collider>());

        // Set material with transparency
        Renderer renderer = visualCube.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = new Color(0.3f, 0.6f, 1f, 0.3f);

        // Make sure the material is transparent
        renderer.material.SetFloat("_Mode", 3); // Transparent mode
        renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        renderer.material.SetInt("_ZWrite", 0);
        renderer.material.DisableKeyword("_ALPHATEST_ON");
        renderer.material.EnableKeyword("_ALPHABLEND_ON");
        renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        renderer.material.renderQueue = 3000;

        // Store the start position
        selectionStartPosition = position;

        // Initialize with zero size at the start position
        selectionMask.transform.position = position;
        visualCube.transform.localScale = Vector3.zero;
    }
    public float maskDepth = 1f; // Default depth in the +Z direction

    private void Update3DSelectionMask(Vector3 currentPosition)
    {
        if (selectionMask == null) return;

        // Calculate the corner positions of our selection volume
        Vector3 min = new Vector3(
            Mathf.Min(selectionStartPosition.x, currentPosition.x),
            Mathf.Min(selectionStartPosition.y, currentPosition.y),
            Mathf.Min(selectionStartPosition.z, currentPosition.z)
        );

        Vector3 max = new Vector3(
            Mathf.Max(selectionStartPosition.x, currentPosition.x),
            Mathf.Max(selectionStartPosition.y, currentPosition.y),
            Mathf.Max(selectionStartPosition.z, currentPosition.z)
        );

        // Calculate dimensions
        Vector3 size = max - min;

        // Ensure minimum size to prevent zero dimensions
        size.x = Mathf.Max(size.x, 0.01f);
        size.y = Mathf.Max(size.y, 0.01f);
        size.z = Mathf.Max(size.z, 0.01f);

        // Calculate center
        Vector3 center = min + size * 0.5f;

        // Update visual representation
        Transform visualCube = selectionMask.transform.Find("MaskVisual");
        if (visualCube != null)
        {
            selectionMask.transform.position = center;
            visualCube.localScale = new Vector3(size.x, size.y, maskDepth);
            visualCube.localPosition = new Vector3(0f, 0f, maskDepth / 2f);
        }
    }

    private void Finalize3DSelection()
    {
        if (selectionMask == null) return;

        // Get the visual cube to determine bounds
        Transform visualCube = selectionMask.transform.Find("MaskVisual");
        if (visualCube == null) return;

        // Get the world space bounds of our selection volume
        Vector3 center = selectionMask.transform.position;
        Vector3 extents = visualCube.localScale * 0.5f;

        // Find all colliders within the bounds
        Collider[] colliders = Physics.OverlapBox(center, extents, Quaternion.identity, targetLayer);

        bool foundSelectableObjects = false;

        // Add objects to the selection
        foreach (Collider collider in colliders)
        {
            // Check if it's a selectable object and not already in our selection
            if (collider.gameObject != selectionMask &&
                collider.CompareTag("Target") &&
                !selectedObjects.Contains(collider.gameObject))
            {
                // Add to selection
                selectedObjects.Add(collider.gameObject);

                // Record original position before parenting (for relative positioning)
                Vector3 originalLocalPosition = collider.transform.localPosition;
                Quaternion originalLocalRotation = collider.transform.localRotation;
                Transform originalParent = collider.transform.parent;

                // Parent to selection mask
                collider.transform.SetParent(selectionMask.transform);

                foundSelectableObjects = true;
            }
        }

        // If nothing was selected, destroy the mask
        if (!foundSelectableObjects)
        {
            Destroy(selectionMask);
            selectionMask = null;
        }
        else
        {
            // Add box collider to the mask for interaction
            BoxCollider maskCollider = selectionMask.AddComponent<BoxCollider>();
            maskCollider.size = visualCube.localScale;
            maskCollider.center = Vector3.zero;

            // Set the mask's tag to "Target" so it can be selected/moved
            selectionMask.tag = "Target";

            // Make the visual somewhat transparent
            Renderer renderer = visualCube.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = renderer.material.color;
                renderer.material.color = new Color(color.r, color.g, color.b, 0.2f);
            }

            // Add a reference to the MaskVisual object on the selection mask for easier access
            //selectionMask.AddComponent<MaskVisualReference>().visualObject = visualCube.gameObject;
        }

        // Deactivate multiselect mode after finalizing selection
        multiSelectMode = false;
        Debug.Log("Multi-Select Mode Deactivated - 3D Selection Volume Created");
    }

    private void DeselectAll()
    {
        if (selectionMask != null)
        {
            // Get a copy of the direct children to avoid issues while modifying the hierarchy
            List<Transform> directChildren = new List<Transform>();
            for (int i = 0; i < selectionMask.transform.childCount; i++)
            {
                Transform child = selectionMask.transform.GetChild(i);
                // Skip the visual mask object
                if (child.name == "MaskVisual") continue;
                directChildren.Add(child);
            }

            // Unparent all objects
            foreach (Transform child in directChildren)
            {
                // Make sure the object stays active
                child.gameObject.SetActive(true);

                // Unparent from the selection mask
                child.SetParent(null);
            }

            // Destroy the selection mask
            Destroy(selectionMask);
            selectionMask = null;
        }

        // Clear selection list
        selectedObjects.Clear();

        // Reset multiselect mode
        multiSelectMode = false;
        Debug.Log("3D Selection Deactivated - All Objects Deselected");
    }

    //---------------------- free hand -----------------------------

    // Modified FreeHandDrawingDetection for 3D drawing
    private void FreeHandDrawingDetection(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (rot || isDragging || multiSelectMode || isSelectionMaskActive || block)
            return;

        // Check for activating drawing mode with Pen object (3D)
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance))
        {
            if (hit.collider.CompareTag("Pen"))
            {
                if (isHoldingNow && !freeHandDrawingMode)
                {
                    // Activate free hand drawing mode
                    Activate3DDrawingMode();
                    return;
                }
            }
        }

        // If not in drawing mode, exit early
        if (!freeHandDrawingMode) return;

        // If we're in drawing mode and pinching
        if (isHoldingNow)
        {
            // First, check if we're clicking on an existing drawing point
            float hoverOffset = 0.1f; // Increase detection radius for 3D space
            Collider[] hitColliders = Physics.OverlapSphere(palm.position, hoverOffset);

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("DrawingPoint") && drawingPoints.Count >= 3)
                {
                    // Find which point was hit
                    GameObject hitPoint = hitCollider.gameObject;
                    int pointIndex = drawingPoints.IndexOf(hitPoint);

                    if (pointIndex != -1 && pointIndex != drawingPoints.Count - 1) // Make sure we're not clicking the most recent point
                    {
                        // Close the shape by connecting current point to the selected point
                        Finalize3DShape(pointIndex);
                        return;
                    }
                }
            }

            // Use palm position for placing new points in 3D space
            Vector3 drawPoint = palm.position;

            // If not closing shape by clicking a point, check if we should add a new point
            if (drawingPoints.Count == 0 ||
                Vector3.Distance(drawPoint, lastPointPosition) >= minDistanceBetweenPoints)
            {
                Create3DDrawingPoint(drawPoint);
                UpdateDrawingLine();
                lastPointPosition = drawPoint;
            }
        }

        // Add a gesture to finalize the shape (e.g., double pinch or specific gesture)
        // For now, if user stops holding for 3 seconds, finalize the shape
        if (!isHoldingNow && drawingPoints.Count > 2)
        {
            if (!isWaitingToFinalize)
            {
                isWaitingToFinalize = true;
                Invoke("Finalize3DShape", 3.0f);
            }
        }
        else if (isHoldingNow && isWaitingToFinalize)
        {
            // Cancel finalization if user starts pinching again
            isWaitingToFinalize = false;
            CancelInvoke("Finalize3DShape");
        }
    }


    private void UpdateDrawingLine()
    {
        // Update line renderer with all points
        drawingLine.positionCount = drawingPoints.Count;

        for (int i = 0; i < drawingPoints.Count; i++)
        {
            drawingLine.SetPosition(i, drawingPoints[i].transform.position);
        }
    }

    private bool isWaitingToFinalize = false;

    private void Activate3DDrawingMode()
    {
        freeHandDrawingMode = true;
        isMovingTarget = false; // Prevent conflicts with target dragging
        Debug.Log("3D Free Hand Drawing Mode Activated");

        // Clear any previous points
        ClearDrawingPoints();

        // Reset last point position
        lastPointPosition = Vector3.zero;

        // Create new drawing object
        currentDrawing = new GameObject("Drawing3D");

        // Add line renderer for connecting points
        drawingLine = currentDrawing.AddComponent<LineRenderer>();
        drawingLine.startWidth = 0.01f;
        drawingLine.endWidth = 0.01f;
        drawingLine.material = new Material(Shader.Find("Sprites/Default"));
        drawingLine.startColor = Color.black;
        drawingLine.endColor = Color.black;
        drawingLine.positionCount = 0;

        // Add a mesh filter and renderer for the eventual 3D shape
        currentDrawing.AddComponent<MeshFilter>();
        currentDrawing.AddComponent<MeshRenderer>();
        currentDrawing.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        currentDrawing.GetComponent<MeshRenderer>().material.color = Color.white;
        currentDrawing.GetComponent<MeshRenderer>().enabled = false; // Will enable when shape is finalized
    }

    private void ClearDrawingPoints()
    {    // Disable the LineRenderer without destroying it
        if (drawingLine != null)
        {
            drawingLine.enabled = false;
        }
        foreach (GameObject point in drawingPoints)
        {
            Destroy(point);
        }

        drawingPoints.Clear();

        if (currentDrawing != null)
        {
            Destroy(currentDrawing);
        }
    }

    private void Create3DDrawingPoint(Vector3 position)
    {
        // Create a new GameObject for the point
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = "DrawingPoint3D";
        point.transform.position = position;

        // Set appropriate scale for visibility
        point.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        // Set material
        point.GetComponent<Renderer>().material.color = Color.red;

        // Add a tag
        point.tag = "DrawingPoint";

        // Add to our list
        drawingPoints.Add(point);
        point.transform.SetParent(currentDrawing.transform);
    }

    private void Finalize3DShape(int closeToPointIndex = 0)
    {
        if (!freeHandDrawingMode || drawingPoints.Count < 3) return;

        freeHandDrawingMode = false;
        isWaitingToFinalize = false;
        Debug.Log("3D Drawing complete - creating object");

        // Add final point to line to close shape
        drawingLine.positionCount = drawingPoints.Count + 1;
        drawingLine.SetPosition(drawingPoints.Count, drawingPoints[closeToPointIndex].transform.position);

        // Generate 3D mesh from the points
        Generate3DMeshFromPoints();

        // Add MeshCollider for interaction
        MeshCollider meshCollider = currentDrawing.AddComponent<MeshCollider>();
        meshCollider.convex = true;

        // Make drawing points invisible but keep them for reference
        foreach (GameObject point in drawingPoints)
        {
            point.GetComponent<Renderer>().enabled = false;
        }

        // Deactivate the LineRenderer
        drawingLine.enabled = false;

        // Place handles around the created object using your prefabs
        PlaceHandlesAroundObject(currentDrawing);

        // Reset for next drawing
        GameObject lastDrawing = currentDrawing;
        currentDrawing = null;
        drawingLine = null;
        drawingPoints.Clear();

        // Set tag for interaction
        lastDrawing.tag = "Target";
    }

    private void PlaceHandlesAroundObject(GameObject targetObject)
    {
        // Clear any existing handles
        ClearHandles();

        // Make sure we have handle prefabs
        if (handlePrefabs.Count == 0)
        {
            Debug.LogWarning("No handle prefabs assigned. Please assign prefabs in the inspector.");
            return;
        }

        // Get the bounds of the object from the PolygonCollider2D
        PolygonCollider2D collider = targetObject.GetComponent<PolygonCollider2D>();
        if (collider == null) return;

        Bounds bounds = collider.bounds;
        Vector3 center = bounds.center;

        // Define positions for all 8 handles
        Vector3[] handlePositions = new Vector3[8] {
        center + new Vector3(-handleDistance, 0, 0),     // 0: Left (X-)
        center + new Vector3(handleDistance, 0, 0),      // 1: Right (X+)
        center + new Vector3(0, -handleDistance, 0),     // 2: Bottom (Y-)
        center + new Vector3(0, handleDistance, 0),      // 3: Top (Y+)
        center + new Vector3(-handleDistance, handleDistance, 0),   // 4: Top-Left (X-Y+)
        center + new Vector3(handleDistance, handleDistance, 0),    // 5: Top-Right (X+Y+)
        center + new Vector3(-handleDistance, -handleDistance, 0),  // 6: Bottom-Left (X-Y-)
        center + new Vector3(handleDistance, -handleDistance, 0)    // 7: Bottom-Right (X+Y-)
    };

        string[] handleTypes = new string[8] {
        "X-", "X+", "Y-", "Y+", "X-Y+", "X+Y+", "X-Y-", "X+Y-"
    };

        // Place each handle from its specific prefab
        for (int i = 0; i < 8; i++)
        {
            // Make sure we have enough prefabs
            if (i < handlePrefabs.Count)
            {
                PlaceHandle(handlePrefabs[i], handlePositions[i], handleTypes[i], targetObject);
            }
            else
            {
                Debug.LogWarning("Not enough handle prefabs assigned. Expected 8 prefabs, found " + handlePrefabs.Count);
                break;
            }
        }

        // Place the rotation handle above the Y+ handle
        if (rotationHandlePrefab != null)
        {
            Vector3 rotationHandlePosition = center + new Vector3(0, handleDistance + rotationHandleOffset, 0);
            PlaceHandle(rotationHandlePrefab, rotationHandlePosition, "ROT", targetObject);
        }
        else
        {
            Debug.LogWarning("Rotation handle prefab not assigned. Please assign it in the inspector.");
        }

        // Place anchors if anchor prefabs are assigned
        PlaceAnchorsAroundObject(targetObject, center);
    }

    private void PlaceAnchorsAroundObject(GameObject targetObject, Vector3 center)
    {
        // Check if we have enough anchor prefabs
        if (anchorPrefabs.Count < 4)
        {
            Debug.LogWarning("Not enough anchor prefabs assigned. Expected 4 prefabs, found " + anchorPrefabs.Count);
            return;
        }

        // Define positions for the 4 anchors on the main axes
        Vector3[] anchorPositions = new Vector3[4] {
        center + new Vector3(-anchorDistance, 0, 0),     // 0: Left (X-)
        center + new Vector3(anchorDistance, 0, 0),      // 1: Right (X+)
        center + new Vector3(0, -anchorDistance, 0),     // 2: Bottom (Y-)
        center + new Vector3(0, anchorDistance, 0)       // 3: Top (Y+)
        };

        string[] anchorTypes = new string[4] {
        "ANCHOR_X-", "ANCHOR_X+", "ANCHOR_Y-", "ANCHOR_Y+"
        };

        for (int i = 0; i < 4; i++)
        {
            anchorPrefabs[i].SetActive(false);
        }
        // Place each anchor
        for (int i = 0; i < 4; i++)
        {
            PlaceHandle(anchorPrefabs[i], anchorPositions[i], anchorTypes[i], targetObject);
        }
    }

    private void PlaceHandle(GameObject prefab, Vector3 position, string handleType, GameObject parent)
    {
        // Instantiate the handle prefab at the desired position
        GameObject handle = Instantiate(prefab, position, Quaternion.identity);

        // Set the parent to the target object
        handle.transform.SetParent(parent.transform);

        // Add handle type information if you need to identify them later
        HandleIdentifier identifier = handle.GetComponent<HandleIdentifier>();
        if (identifier == null)
        {
            identifier = handle.AddComponent<HandleIdentifier>();
        }
        identifier.handleType = handleType;

        // Add to our list of active handles
        activeHandles.Add(handle);
    }

    private void ClearHandles()
    {
        foreach (GameObject handle in activeHandles)
        {
            if (handle != null)
            {
                Destroy(handle);
            }
        }

        activeHandles.Clear();
    }

    private void Generate3DMeshFromPoints()
    {
        if (drawingPoints.Count < 3) return;

        // Get the MeshFilter and enable the renderer
        MeshFilter meshFilter = currentDrawing.GetComponent<MeshFilter>();
        currentDrawing.GetComponent<MeshRenderer>().enabled = true;

        // Create lists to hold vertices and triangles
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Calculate center point of all drawing points for extrusion direction
        Vector3 center = Vector3.zero;
        foreach (GameObject point in drawingPoints)
        {
            center += point.transform.position;
        }
        center /= drawingPoints.Count;

        // Find normal vector for the shape (assuming points are roughly planar)
        // This uses cross product of two edges to estimate the normal
        Vector3 edge1 = drawingPoints[1].transform.position - drawingPoints[0].transform.position;
        Vector3 edge2 = drawingPoints[2].transform.position - drawingPoints[0].transform.position;
        Vector3 normal = Vector3.Cross(edge1, edge2).normalized;

        // If normal is zero (points are collinear), use a default
        if (normal.magnitude < 0.001f)
        {
            normal = Vector3.up;
        }

        // Extrusion depth
        float depth = 0.1f;

        // Generate frontface vertices
        foreach (GameObject point in drawingPoints)
        {
            // Add vertex in world space, then convert to local for the mesh
            Vector3 worldPos = point.transform.position;
            Vector3 localPos = currentDrawing.transform.InverseTransformPoint(worldPos);
            vertices.Add(localPos);
        }

        // Generate backface vertices (extruded along normal)
        foreach (GameObject point in drawingPoints)
        {
            Vector3 worldPos = point.transform.position + normal * depth;
            Vector3 localPos = currentDrawing.transform.InverseTransformPoint(worldPos);
            vertices.Add(localPos);
        }

        // Create triangles for front face using simple triangulation
        // This is a simple fan triangulation which works for convex shapes
        for (int i = 1; i < drawingPoints.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }

        // Create triangles for back face (reversed winding)
        int backOffset = drawingPoints.Count;
        for (int i = 1; i < drawingPoints.Count - 1; i++)
        {
            triangles.Add(backOffset);
            triangles.Add(backOffset + i + 1);
            triangles.Add(backOffset + i);
        }

        // Create side faces
        for (int i = 0; i < drawingPoints.Count; i++)
        {
            int next = (i + 1) % drawingPoints.Count;

            // First triangle
            triangles.Add(i);
            triangles.Add(next);
            triangles.Add(backOffset + i);

            // Second triangle
            triangles.Add(next);
            triangles.Add(backOffset + next);
            triangles.Add(backOffset + i);
        }

        // Create the mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        // Recalculate normals for proper lighting
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Assign mesh to the MeshFilter
        meshFilter.mesh = mesh;
    }

    // Improved triangulation for potentially concave shapes
    private void TriangulateShape(List<Vector3> points, List<int> triangles)
    {
        // This is a simplified ear-clipping triangulation
        // For more complex shapes, consider using a package or more sophisticated algorithm

        List<int> remaining = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            remaining.Add(i);
        }

        while (remaining.Count > 3)
        {
            bool earFound = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = (i + remaining.Count - 1) % remaining.Count;
                int curr = i;
                int next = (i + 1) % remaining.Count;

                int prevIndex = remaining[prev];
                int currIndex = remaining[curr];
                int nextIndex = remaining[next];

                Vector3 a = points[prevIndex];
                Vector3 b = points[currIndex];
                Vector3 c = points[nextIndex];

                // Check if this vertex forms an ear
                if (IsEar(a, b, c, points, remaining))
                {
                    // Add triangle
                    triangles.Add(prevIndex);
                    triangles.Add(currIndex);
                    triangles.Add(nextIndex);

                    // Remove current vertex
                    remaining.RemoveAt(curr);
                    earFound = true;
                    break;
                }
            }

            // If no ear is found, we might have a self-intersecting polygon
            // For simplicity, break and use a fan triangulation as fallback
            if (!earFound)
            {
                Debug.LogWarning("Complex shape detected, using fallback triangulation.");
                TriangulateFan(points, triangles);
                return;
            }
        }

        // Add final triangle
        if (remaining.Count == 3)
        {
            triangles.Add(remaining[0]);
            triangles.Add(remaining[1]);
            triangles.Add(remaining[2]);
        }
    }

    private bool IsEar(Vector3 a, Vector3 b, Vector3 c, List<Vector3> points, List<int> indices)
    {
        // Check if the triangle abc is an ear

        // First check if this is a convex angle
        Vector3 ab = b - a;
        Vector3 bc = c - b;
        Vector3 normal = Vector3.Cross(ab, bc).normalized;

        // Assuming the points are roughly on a plane - project to 2D
        Vector3 up = normal;
        Vector3 right = Vector3.Cross(up, ab).normalized;
        Vector3 forward = Vector3.Cross(right, up).normalized;

        Vector2 a2d = new Vector2(Vector3.Dot(a, right), Vector3.Dot(a, forward));
        Vector2 b2d = new Vector2(Vector3.Dot(b, right), Vector3.Dot(b, forward));
        Vector2 c2d = new Vector2(Vector3.Dot(c, right), Vector3.Dot(c, forward));

        // Check if convex
        Vector2 ab2d = b2d - a2d;
        Vector2 bc2d = c2d - b2d;
        float cross = ab2d.x * bc2d.y - ab2d.y * bc2d.x;

        if (cross <= 0)
            return false;

        // Check if any other point is inside this triangle
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];

            // Skip vertices that form the triangle
            if (points[idx] == a || points[idx] == b || points[idx] == c)
                continue;

            Vector3 p = points[idx];
            Vector2 p2d = new Vector2(Vector3.Dot(p, right), Vector3.Dot(p, forward));

            if (IsPointInTriangle(p2d, a2d, b2d, c2d))
                return false;
        }

        return true;
    }

    private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // Barycentric coordinates check
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private void TriangulateFan(List<Vector3> points, List<int> triangles)
    {
        // Simple fan triangulation from first vertex
        for (int i = 1; i < points.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }
    }

    //---------------------- Masks --------------------

    private void HoverAndPinchDetection(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {

        if (rot || isDragging || adjusting || canDraw || isMovingLine || multiSelectMode || isSelectionMaskActive) return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin))
        {
            return;
        }

        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0, targetLayer);

        if (hit2D.collider != null)
        {
            GameObject hitObject = hit2D.collider.gameObject;
            if (IsValidTarget(hitObject)) return;
            // **Pinching on a "Target" activates its children**
            if (hitObject.CompareTag("Target"))
            {
                if (isHoldingNow && (!selectobj || CurentObj != hitObject))
                {
                    // **Deactivate previous selection if switching objects**
                    if (CurentObj != null && CurentObj != hitObject)
                    {
                        SetChildrenActive(CurentObj, false);
                    }

                    // **Activate new object and update current selection**
                    CurentObj = hitObject;
                    SetChildrenActive(CurentObj, true);
                    selectobj = true;
                    Debug.Log("Activated children of: " + CurentObj.name);
                }
            }
            else
            {
                // **Pinching on another object ? Deactivate children**
                if (isHoldingNow && selectobj)
                {
                    SetChildrenActive(CurentObj, false);
                    selectobj = false;
                    CurentObj = null;
                    Debug.Log("Deactivated children as another object was pinched.");
                }
            }
        }
        else
        {
            // **Pinching in empty space ? Deactivate children**
            if (isHoldingNow && selectobj)
            {
                SetChildrenActive(CurentObj, false);
                selectobj = false;
                CurentObj = null;
                Debug.Log("Deactivated children as empty space was pinched.");
            }
        }
    }

    private bool IsValidTarget(GameObject obj)
    {
        return obj.CompareTag("X") || obj.CompareTag("Y") ||
               obj.CompareTag("NX") || obj.CompareTag("NY") || obj.CompareTag("XY") ||
               obj.CompareTag("NXNY") || obj.CompareTag("XNY") || obj.CompareTag("NXY");
    }

    private void SetChildrenActive(GameObject parent, bool active)
    {
        if (parent != null)
        {
            foreach (Transform child in parent.transform)
            {
                if (block && child.gameObject.name == "MaskObject") continue;
                if (adjusting && child.gameObject.name == "HoverMask") continue;

                if (child.gameObject.name == "Canvas") continue;
                if (child.gameObject.name == "inputbox") continue;
                if (child.tag == "Anchor") continue;
                if (!isTransparentMode &&( child.tag == "XRot"||child.tag == "YRot"|| child.tag == "ZRot")) continue;
                child.gameObject.SetActive(active);
            }
        }
    }

    private void CreateMask(GameObject targetObject)
    {
        if (targetObject == null) return;

        Transform existingMask = targetObject.transform.Find("MaskObject");
        if (existingMask != null)
        {
            maskObject = existingMask.gameObject;
            maskObject.SetActive(true);
            return;
        }

        maskObject = new GameObject("MaskObject");
        maskObject.transform.SetParent(targetObject.transform, false);
        maskObject.transform.localPosition = Vector3.zero;
        maskObject.transform.localScale = Vector3.one * 1.5f;


        SpriteRenderer originalRenderer = targetObject.GetComponent<SpriteRenderer>();
        if (originalRenderer != null)
        {
            SpriteRenderer maskRenderer = maskObject.AddComponent<SpriteRenderer>();
            maskRenderer.sprite = originalRenderer.sprite;
            maskRenderer.color = new Color(0, 0, 0, 0.3f);
            maskRenderer.sortingOrder = originalRenderer.sortingOrder - 1;
        }

        maskObject.SetActive(true);
    }
}
