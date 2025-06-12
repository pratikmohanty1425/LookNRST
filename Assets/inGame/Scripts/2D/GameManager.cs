using Meta.WitAi.Dictation;
using System.Collections;
using System.Collections.Generic;
using Unity.Android.Gradle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Oculus.Interaction.Context;
using static OVRPlugin;
using static UnityEditor.U2D.ScriptablePacker;

public class GameManager : MonoBehaviour
{

    public bool WantFilter = false;

    [Header("Public References")]
    public OVRHand hand;
    public OVRSkeleton skeleton;
    public Transform palm;
    public LayerMask targetLayer;

    // --- Public Configuration ---
    [Header("Configuration")]
    public float rayDistance = 10f;
    public float moveSpeed = 5f;
    public float smoothFactor = 0.1f;
    public float smoothTime = 0.1f;
    public Color pinchColor = Color.green;

    // --- OneEuroFilter ---
    [Header("One Euro Filter")]
    public float filterMinCutoff = 0.01f; // Increase for less delay
    public float filterBeta = 1f; // Higher means less lag
    public float filterDCutoff = 1.0f;
    private OneEuroFilter[] positionFilters;
    private OneEuroFilter[] directionFilters;

    [Header("Gaze Interaction")]
    [SerializeField] private OVREyeGaze LeyeGaze;
    [SerializeField] private OVREyeGaze ReyeGaze;

    [Header("Cursor Settings")]
    public GameObject cursorPrefab;
    public float cursorSize = 0.02f;
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

    // Cursor references
    [Header("Cursor")]
    private GameObject cursor;
    private MeshRenderer cursorRenderer;

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

    //Update references
    private enum InteractionMode { HandRay, EyeGaze, GazePinch }
    private InteractionMode currentMode = InteractionMode.HandRay;



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
        SetupLineRenderer();
        InitializeFilters();
        CreateCursor();
        InitializePalm();
    }

    void Update()
    {

        if (cursor == null || cursorRenderer == null || hand == null || palmTransform == null) return;

        // Check if eye tracking is available when needed
        if (cursor == null || cursorRenderer == null ||
                   LeyeGaze == null || ReyeGaze == null || hand == null ||
                   !LeyeGaze.EyeTrackingEnabled || !ReyeGaze.EyeTrackingEnabled) return;


        // Common variables
        float currentTime = Time.time;
        bool isHoldingNow = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        Vector3 filteredRayOrigin, filteredRayDirection;

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
            // Hand ray tracking
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

        perspectivechange(filteredRayOrigin, filteredRayDirection, isHoldingNow);
        // Reset states when gaze interaction stops
        if (!isHoldingNow)
        {
            selectedObject = null;
            lastHitObject = null;
            lastHitRenderer = null;
            isPinching = false;
            block = false;
            release = false;
            adjusting = false;
            isPinchingActive = false;
            adjusting = false;
            rot = false;
        }
    }

    //---------------------- calibration ----------------------

    private void CheckForModeSwitching(Vector3 origin, Vector3 direction, bool isHoldingNow)
    {
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
        bool hit3DObject = Physics.Raycast(origin, direction, out hit3D, Mathf.Infinity, targetLayer);

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
        if (Physics.Raycast(origin, direction, out hit3D, Mathf.Infinity, targetLayer) &&
            (hit3D.collider.CompareTag("Canvas") || hit3D.collider.CompareTag("Finish")))
        {
            Vector3 endPoint = hit3D.point;
            rayDistance = hit3D.distance;

            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }
        else
        {
            Vector3 endPoint = origin + (direction * rayDistance);
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }
    }

    //---------------------- Ray ----------------------
    // **Validate gaze vectors to prevent NaN/Infinity values**
    private Vector3 ValidateVector(Vector3 vector)
    {
        if (float.IsNaN(vector.x) || float.IsNaN(vector.y) || float.IsNaN(vector.z) ||
            float.IsInfinity(vector.x) || float.IsInfinity(vector.y) || float.IsInfinity(vector.z))
        {
            Debug.LogError("Invalid eye tracking data detected!");
            return Vector3.zero; // Reset to avoid errors
        }
        return vector;
    }

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
    private RaycastHit2D getRaycast2dLayer(Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) ;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0, targetLayer);

        return hit2D;
    }


    //---------------------- 3D<>2D --------------------------

    private bool isPerspectiveActive = false;  // Track the perspective state
    private bool wasPerspectivePinching = false;  // Track the previous pinch state
    private bool isTransitioning = false;  // Track if we're in the middle of a transition
    private float transitionDuration = 1.0f;  // Transition duration in seconds
    private float transitionTimer = 0f;
    private Dictionary<string, GameObject> created3DObjects = new Dictionary<string, GameObject>();



    [Header("3D<>2D")]
    public List<Sprite> sprites = new List<Sprite>();
    public GameObject canvasboard;
    public GameObject DimentionalSpace;
    public Material defaultMaterial; // Default material for 3D objects

    [Header("3D Model Settings")]
    public float modelScaleFactor = 1.0f; // Scale adjustment for 3D models

    [Header("Prefab Settings")]
    public List<GameObject> shapePrefabs = new List<GameObject>(); // Initialized list
    public bool createDefaultShapesIfNoMatch = true; // Create default shapes if no prefab match is found

    [Header("Debug Settings")]
    public bool debugMode = true; // Enable extensive debugging

    // Required to be called from external controller
    public void OnPerspectiveChange(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        perspectivechange(rayOrigin, rayDirection, isHoldingNow);
    }

    // Add this for manual testing in the Editor
    public void TestPerspectiveChange()
    {
        // Toggle the perspective state directly for testing
        isPerspectiveActive = !isPerspectiveActive;

        if (debugMode) Debug.Log("TEST: Perspective changing to " + (isPerspectiveActive ? "2D" : "3D"));

        if (isPerspectiveActive)
        {
            // Clean up any existing 3D objects
            CleanUp3DObjects();
        }
        else
        {
            // Create 3D objects from 2D objects
            Create3DObjectsFromPrefabs();
        }

        // Start the transition
        StartTransition();
    }

    private void perspectivechange(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        // Update transition if one is in progress
        if (isTransitioning)
        {
            UpdateTransition();
            return;  // Skip the rest of the function while transitioning
        }

        if (debugMode) Debug.Log("Attempting perspective change. isHoldingNow: " + isHoldingNow);

        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);
        if (hit2D.collider != null)
        {
            if (debugMode) Debug.Log("Hit object: " + hit2D.collider.gameObject.name + " with tag: " + hit2D.collider.tag);

            // Check if the hit object has the "3D2D" tag
            if (hit2D.collider.CompareTag("3D2D"))
            {
                // Detect new pinch (pinch began)
                if (isHoldingNow && !wasPerspectivePinching)
                {
                    if (debugMode) Debug.Log("Pinch detected on 3D2D object");

                    foreach (Transform child in hit2D.collider.gameObject.transform)
                    {
                        if (child.gameObject.name == "MaskObject") Destroy(child.gameObject);
                    }

                    // Get the SpriteRenderer component to change the sprite
                    SpriteRenderer spriteRenderer = hit2D.collider.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null && sprites.Count >= 2)
                    {
                        // Toggle the perspective state
                        isPerspectiveActive = !isPerspectiveActive;

                        // Change sprite based on perspective state
                        if (isPerspectiveActive)
                        {
                            // Change to sprite[1] when toggled to 2D
                            spriteRenderer.sprite = sprites[1];

                            // Clean up any existing 3D objects
                            CleanUp3DObjects();
                        }
                        else
                        {
                            // Change back to sprite[0] when toggled to 3D
                            spriteRenderer.sprite = sprites[0];

                            // Create 3D objects from 2D objects
                            Create3DObjectsFromPrefabs();
                        }

                        // Start the transition
                        StartTransition();
                    }
                    else
                    {
                        if (debugMode) Debug.LogWarning("SpriteRenderer not found or sprites list too short");
                    }

                    Debug.Log("Perspective changing to " + (isPerspectiveActive ? "2D" : "3D"));
                }

                // Update previous pinch state
                wasPerspectivePinching = isHoldingNow;
            }
        }
        else
        {
            if (debugMode && isHoldingNow) Debug.Log("No collider hit by raycast");

            // Update pinch state even when not hitting anything
            wasPerspectivePinching = isHoldingNow;
        }
    }

    private void Create3DObjectsFromPrefabs()
    {
        // Find all GameObjects with the "Target" tag
        GameObject[] targetObjects = GameObject.FindGameObjectsWithTag("Target");

        if (debugMode) Debug.Log("Found " + targetObjects.Length + " objects with 'Target' tag to convert to 3D");

        foreach (GameObject targetObject in targetObjects)
        {
            // Ensure the object has a renderer and is active
            Renderer renderer = targetObject.GetComponent<Renderer>();
            if (renderer != null && targetObject.activeInHierarchy)
            {
                // Get the object's name for prefab matching
                string objectName = targetObject.name;
                targetObject.SetActive(false);
                Debug.Log("Converting target object to 3D: " + objectName);

                // Find matching prefab or create default
                GameObject prefabMatch = FindMatchingPrefab(objectName);
                if (prefabMatch != null)
                {
                    // Instantiate the matching prefab
                    InstantiatePrefabFor2DObject(prefabMatch, targetObject);
                }
                else if (createDefaultShapesIfNoMatch)
                {
                    // Create a default shape based on the collider
                    CreateDefaultShapeFor2DObject(objectName, targetObject);
                }
                else
                {
                    if (debugMode) Debug.LogWarning("No matching prefab found for " + objectName + " and default creation is disabled");
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning("Target object " + targetObject.name + " has no renderer or is inactive");
            }
        }
    }

    private GameObject FindMatchingPrefab(string objectName)
    {
        if (shapePrefabs == null || shapePrefabs.Count == 0)
        {
            if (debugMode) Debug.LogWarning("No prefabs assigned to shapePrefabs list");
            return null;
        }

        // Normalize the object name for comparison
        string normalizedName = objectName.ToLower();

        // First try to find an exact name match
        foreach (GameObject prefab in shapePrefabs)
        {
            if (prefab == null) continue;

            if (prefab.name.ToLower() == normalizedName)
            {
                if (debugMode) Debug.Log("Found exact name match prefab: " + prefab.name);
                return prefab;
            }
        }

        // If no exact match, try to find a prefab that contains the object name
        foreach (GameObject prefab in shapePrefabs)
        {
            if (prefab == null) continue;

            if (prefab.name.ToLower().Contains(normalizedName) ||
                normalizedName.Contains(prefab.name.ToLower()))
            {
                if (debugMode) Debug.Log("Found partial name match prefab: " + prefab.name);
                return prefab;
            }
        }

        if (debugMode) Debug.Log("No matching prefab found for: " + objectName);
        return null;
    }

    private void InstantiatePrefabFor2DObject(GameObject prefab, GameObject sourceObject)
    {
        // Instantiate the prefab
        GameObject newObject = Instantiate(prefab);

        // Set object name
        newObject.name = "Prefab_" + sourceObject.name;

        // Position the 3D object at the same location as the 2D object
        newObject.transform.position = new Vector3(
            sourceObject.transform.position.x,
            sourceObject.transform.position.y,
            DimentionalSpace.transform.position.z
        );

        // Set the rotation - inherit Z rotation from source
        newObject.transform.rotation = Quaternion.Euler(0, 0, sourceObject.transform.rotation.eulerAngles.z);

        // Set scale based on the 2D object's scale and the model scale factor
        newObject.transform.localScale = new Vector3(
            sourceObject.transform.localScale.x * modelScaleFactor,
            sourceObject.transform.localScale.y * modelScaleFactor,
            sourceObject.transform.localScale.x * modelScaleFactor // Use x for z to maintain proportions
        );

        // Set parent
        newObject.transform.SetParent(DimentionalSpace.transform);

        // Try to copy color from source if possible
        CopyColor(sourceObject, newObject);

        // Store for cleanup later
        string key = sourceObject.GetInstanceID().ToString();
        if (created3DObjects.ContainsKey(key))
        {
            Destroy(created3DObjects[key]);
        }
        created3DObjects[key] = newObject;

        if (debugMode) Debug.Log("Created 3D object from prefab for: " + sourceObject.name);
    }

    private void CreateDefaultShapeFor2DObject(string objectName, GameObject sourceObject)
    {
        // Normalize the object name to lowercase for easier comparison
        string normalizedName = objectName.ToLower();

        // Determine what kind of collider the object has
        string colliderType = "unknown";
        Collider2D collider = sourceObject.GetComponent<Collider2D>();
        if (collider != null)
        {
            if (collider is BoxCollider2D) colliderType = "box";
            else if (collider is CircleCollider2D) colliderType = "circle";
            else if (collider is CapsuleCollider2D) colliderType = "capsule";
        }

        // Create primitive based on name and collider type
        PrimitiveType primitiveType = DeterminePrimitiveType(normalizedName, colliderType);
        GameObject newObject = GameObject.CreatePrimitive(primitiveType);

        // Set object name
        newObject.name = "Default_" + sourceObject.name;

        // Position the 3D object at the same location as the 2D object
        newObject.transform.position = new Vector3(
            sourceObject.transform.position.x,
            sourceObject.transform.position.y,
            DimentionalSpace.transform.position.z
        );

        // Set the rotation - inherit Z rotation from source
        newObject.transform.rotation = Quaternion.Euler(0, 0, sourceObject.transform.rotation.eulerAngles.z);

        // Set scale based on the 2D object's scale
        newObject.transform.localScale = new Vector3(
            sourceObject.transform.localScale.x * modelScaleFactor,
            sourceObject.transform.localScale.y * modelScaleFactor,
            sourceObject.transform.localScale.x * modelScaleFactor // Use x for z to maintain proportions
        );

        // Set parent
        newObject.transform.SetParent(DimentionalSpace.transform);

        // Apply material
        if (defaultMaterial != null)
        {
            Renderer renderer = newObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(defaultMaterial);
            }
        }

        // Copy color from source
        CopyColor(sourceObject, newObject);

        // Store for cleanup later
        string key = sourceObject.GetInstanceID().ToString();
        if (created3DObjects.ContainsKey(key))
        {
            Destroy(created3DObjects[key]);
        }
        created3DObjects[key] = newObject;

        if (debugMode) Debug.Log($"Created default 3D {primitiveType} for: {sourceObject.name}");
    }

    private PrimitiveType DeterminePrimitiveType(string objectName, string colliderType)
    {
        // First check collider type
        if (colliderType == "circle")
        {
            return PrimitiveType.Sphere;
        }
        else if (colliderType == "capsule")
        {
            return PrimitiveType.Capsule;
        }
        // Then check name if collider type doesn't give enough info
        else if (objectName.Contains("sphere") || objectName.Contains("ball") || objectName.Contains("circle"))
        {
            return PrimitiveType.Sphere;
        }
        else if (objectName.Contains("capsule") || objectName.Contains("pill"))
        {
            return PrimitiveType.Capsule;
        }
        else if (objectName.Contains("cylinder") || objectName.Contains("tube"))
        {
            return PrimitiveType.Cylinder;
        }
        else if (objectName.Contains("plane") || objectName.Contains("floor") || objectName.Contains("ground"))
        {
            return PrimitiveType.Plane;
        }
        else if (objectName.Contains("quad") || objectName.Contains("rectangle") || objectName.Contains("square"))
        {
            return PrimitiveType.Quad;
        }

        // Default to cube
        return PrimitiveType.Cube;
    }

    private void CopyColor(GameObject sourceObject, GameObject targetObject)
    {
        // Copy color from source to target if possible
        SpriteRenderer spriteRenderer = sourceObject.GetComponent<SpriteRenderer>();
        Renderer targetRenderer = targetObject.GetComponent<Renderer>();

        if (spriteRenderer != null && targetRenderer != null)
        {
            // Create a new material if needed
            if (targetRenderer.material == null)
            {
                targetRenderer.material = new Material(Shader.Find("Standard"));
            }

            // Copy the color
            targetRenderer.material.color = spriteRenderer.color;
        }
    }

    private void CleanUp3DObjects()
    {
        Debug.Log("Cleaning up " + created3DObjects.Count + " 3D objects");

        foreach (var obj in created3DObjects.Values)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        created3DObjects.Clear();
    }

    private void StartTransition()
    {
        Debug.Log("Starting transition to " + (isPerspectiveActive ? "2D" : "3D"));

        // Initialize transition
        isTransitioning = true;
        transitionTimer = 0f;

        // Make sure both objects are active for the transition
        canvasboard.SetActive(true);
        DimentionalSpace.SetActive(true);

        // Set initial alpha/scale values
        if (isPerspectiveActive)
        {
            // Transitioning to 2D (canvas)
            SetCanvasAlpha(0f);
        }
        else
        {
            // Transitioning to 3D (dimensional space)
            SetCanvasAlpha(1f);
        }
    }

    private void UpdateTransition()
    {
        // Increase timer
        transitionTimer += Time.deltaTime;

        // Calculate progress (0 to 1)
        float progress = Mathf.Clamp01(transitionTimer / transitionDuration);

        if (isPerspectiveActive)
        {
            // Transitioning to 2D (canvas)
            SetCanvasAlpha(progress);
        }
        else
        {
            // Transitioning to 3D (dimensional space)
            SetCanvasAlpha(1f - progress);
        }

        // Check if transition is complete
        if (progress >= 1.0f)
        {
            FinishTransition();
        }
    }

    private void FinishTransition()
    {
        Debug.Log("Finishing transition");

        isTransitioning = false;

        // Finalize object states
        if (isPerspectiveActive)
        {
            // Finalize 2D state
            SetCanvasAlpha(1f);
            DimentionalSpace.SetActive(false);
        }
        else
        {
            // Finalize 3D state
            canvasboard.SetActive(false);
        }
    }

    private void SetCanvasAlpha(float alpha)
    {
        // Find CanvasGroup component or use other method to adjust alpha
        CanvasGroup canvasGroup = canvasboard.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
        else
        {
            // Alternative if no CanvasGroup exists
            CanvasRenderer[] renderers = canvasboard.GetComponentsInChildren<CanvasRenderer>(true);
            foreach (CanvasRenderer renderer in renderers)
            {
                renderer.SetAlpha(alpha);
            }
        }
    }

    // For manual testing via inspector or for debugging
    void OnGUI()
    {
        if (debugMode)
        {
            // Create a simple debug button at the top of the screen
            if (GUI.Button(new Rect(10, 10, 200, 40), "Toggle Perspective"))
            {
                TestPerspectiveChange();
            }
        }
    }

}
