using Meta.XR.MRUtilityKit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.HID;
using UnityEngine.UIElements;

public class HandRayInteraction1 : MonoBehaviour
{
    // --- Public References ---
    public OVRHand hand;
    public OVRSkeleton skeleton;
    public Transform palm;
    public LayerMask targetLayer;

    // --- Public Configuration ---
    public float rayDistance = 10f;
    public float moveSpeed = 5f;
    public float smoothFactor = 0.1f;
    public float smoothTime = 0.1f;
    public Color pinchColor = Color.green;
    public float scaleFactor = 0.5f;
    public float childScaleFactor = 1.0f;

    // --- Private State Variables ---
    private Transform palmTransform;
    private LineRenderer lineRenderer;
    private GameObject selectedObject = null;
    private GameObject lastHitObject = null;
    private GameObject hit = null;
    private GameObject maskObject = null;
    private GameObject currentlyAdjustingObject = null;

    // --- Private Rendering & Color Management ---
    private Renderer lastHitRenderer = null;
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

    // --- Private Raycasting & Movement ---
    private Vector3 smoothedRayOrigin;
    private Vector3 smoothedRayDirection;
    private Vector3 smoothedStartPosition;
    private Vector3 smoothedDirection;
    private Vector3 velocity = Vector3.zero;
    private Vector3 lastRayPosition;

    // --- Private Interaction States ---
    private bool isPinching = false;
    private bool block = false;
    private bool pinch = false;
    private bool release = false;
    private bool isPinchingActive = false;
    private bool adjusting = false;
    private bool rot = false;
    private bool lineactive = false;

    // --- Other Variables ---
    private int count = 2;

    // --- OneEuroFilter ---
    private OneEuroFilter[] positionFilters;
    private OneEuroFilter[] directionFilters;
    public float filterMinCutoff = 0.01f; // Increase for less delay
    public float filterBeta = 1f; // Higher means less lag
    public float filterDCutoff = 1.0f;

    private LineRenderer circularPathRenderer;
    private const int CIRCLE_SEGMENTS = 50;
    private Vector3 rotationCenter;
    private float rotationRadius;


    bool rotpinch = false;
    private GameObject selectedRotObject = null; // Store the Rot object

    private Vector3 initialRayDirection; // Store initial ray direction when pinching starts
    private bool isFirstMove = true; // Track first movement

    private GameObject parent = null;
    private float hoverStartTime = -1f;
    private float hoverEndTime = -1f;
    private bool isShowingChildren = false;
    private const float HOVER_DELAY = 0f; // 5 second delay

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

    void Start()
    {
        InitializePalm();
        SetupLineRenderer();
        InitializeFilters();
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

    void Update()
    {
        if (palmTransform == null) return;

        bool isHoldingNow = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        Vector3 rayOrigin = palmTransform.position;
        Vector3 rawRayDirection = palmTransform.forward;

        // Apply One Euro Filter to each component of the position and direction
        float currentTime = Time.time;
        Vector3 filteredRayOrigin = new Vector3(
            positionFilters[0].Filter(rayOrigin.x, currentTime),
            positionFilters[1].Filter(rayOrigin.y, currentTime),
            positionFilters[2].Filter(rayOrigin.z, currentTime)
        );
        //print(filteredRayOrigin);
        //print(rayOrigin);
        Vector3 filteredRayDirection = new Vector3(
            directionFilters[0].Filter(rawRayDirection.x, currentTime),
            directionFilters[1].Filter(rawRayDirection.y, currentTime),
            directionFilters[2].Filter(rawRayDirection.z, currentTime)
        ).normalized;


        UpdateLineRenderer(rayOrigin, filteredRayDirection);
        //DetectAndSelectObject(rayOrigin, filteredRayDirection, isHoldingNow);
        //MoveSelectedObject(rayOrigin, filteredRayDirection);
        //HoverOverObject(rayOrigin, filteredRayDirection, isHoldingNow);
        AdjustParentSizeOnPinch2(rayOrigin, filteredRayDirection, isHoldingNow);
        RotateAndDragParentObject(rayOrigin, filteredRayDirection, isHoldingNow);
        LineDraw(rayOrigin, filteredRayDirection, isHoldingNow);
        TargetDrag(rayOrigin, filteredRayDirection, isHoldingNow);
        //ChangeColor(rayOrigin, filteredRayDirection, isHoldingNow);
        DestroyObj(rayOrigin, filteredRayDirection, isHoldingNow);
        ScaleParentOnDrag(rayOrigin, filteredRayDirection, isHoldingNow);
        ChangeColor2(rayOrigin, filteredRayDirection, isHoldingNow);
        ButtonFeedback(rayOrigin, filteredRayDirection, isHoldingNow);
        UpdateAttachedLines();
        DetectCirclePinch(rayOrigin, filteredRayDirection, isHoldingNow);
        PrefabSpawn(rayOrigin, filteredRayDirection, isHoldingNow);
        HoverAndPinchDetection(rayOrigin, filteredRayDirection, isHoldingNow);
        FreeHandDrawingDetection(rayOrigin, filteredRayDirection, isHoldingNow);
        MultiSelectDetection(rayOrigin, filteredRayDirection, isHoldingNow);
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

    private void InitializePalm()
    {
        if (skeleton != null)
        {
            palmTransform = palm;
        }
    }

    //---------------------- Line Renderer & Ray ----------------------

    private RaycastHit2D getRaycast2d(Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) ;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);

        return hit2D;
    }

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

    private void UpdateLineRenderer(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit3D;
        // Perform a 3D Raycast specifically for Canvas
        if (Physics.Raycast(origin, direction, out hit3D, Mathf.Infinity, targetLayer) &&
            (hit3D.collider.CompareTag("Canvas") || hit3D.collider.CompareTag("Finish")))
        {
            // Set the end point to the exact hit point on the Canvas
            Vector3 endPoint = hit3D.point;

            // Update rayDistance to the distance of the Canvas hit
            rayDistance = hit3D.distance;

            // Set the line renderer positions
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }
        else
        {
            // If no Canvas is hit, use the original ray direction
            Vector3 endPoint = origin + (direction * rayDistance);

            // Set the line renderer positions
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }
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

    //---------------------- Line Drawing ------------------------
    private GameObject selectedLine = null;
    private bool isMovingLine = false;
    private Vector3 initialHitPoint;
    private Vector3 initialParentPosition;
    private Vector3[] initialLinePositions;

    private void LineDraw(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting) return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);

        // If the user just started pinching (isHoldingNow became true this frame)
        if (isHoldingNow && !wasPinchingLastFrame)
        {
            // Initially hide all circles
            HideAllCircles();

            // Check if we're pinching on a line
            if (hit2D.collider != null && hit2D.collider.CompareTag("DrawnLine"))
            {
                GameObject hitLine = hit2D.collider.gameObject;
                GameObject parentLine = (hitLine.transform.parent != null && hitLine.transform.parent.CompareTag("DrawnLine"))
                    ? hitLine.transform.parent.gameObject
                    : hitLine; // Fallback to hitLine if parent is null

                // Show circles for this line when pinching on it
                ShowCirclesForLine(parentLine);
                pinchingOnLine = true;
                selectedLineForCircles = parentLine;
            }
            else if (hit2D.collider != null && hit2D.collider.CompareTag("Circle"))
            {
                // If pinching on a circle, keep it visible
                GameObject hitCircle = hit2D.collider.gameObject;
                GameObject parentLine = hitCircle.transform.parent.gameObject;
                ShowCirclesForLine(parentLine);
                pinchingOnLine = true;
                selectedLineForCircles = parentLine;
            }
            else
            {
                // Pinching elsewhere, hide all circles
                pinchingOnLine = false;
                selectedLineForCircles = null;
            }
        }

        // Always keep circles visible for the line being manipulated
        if (selectedLineForCircles != null && pinchingOnLine)
        {
            ShowCirclesForLine(selectedLineForCircles);
        }

        // Check if we hit a DrawnLine and handle line movement
        if (hit2D.collider != null)
        {
            if (hit2D.collider.CompareTag("DrawnLine") && !canDraw && !isDragging)
            {
                GameObject hitLine = hit2D.collider.gameObject;
                GameObject parentLine = (hitLine.transform.parent != null && hitLine.transform.parent.CompareTag("DrawnLine"))
                    ? hitLine.transform.parent.gameObject
                    : hitLine; // Fallback to hitLine if parent is null

                if (isHoldingNow && !isMovingLine)
                {
                    // Start moving the line
                    selectedLine = parentLine;
                    isMovingLine = true;
                    initialHitPoint = endPoint;

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

                        // Show circles for the line being moved
                        ShowCirclesForLine(selectedLine);
                        //Debug.Log("Starting to move line: " + selectedLine.name);
                    }
                    else
                    {
                        Debug.LogWarning("Selected line is null!");
                    }
                }
            }

            else if (hit2D.collider.CompareTag("Line") && !canDraw)
            {
                if (isHoldingNow)
                {
                    canDraw = true;
                    print("can draw");
                }
            }
            else if (hit2D.collider.CompareTag("Circle") && !canDraw && !isMovingLine)
            {
                if (isHoldingNow)
                {
                    GameObject hitCircle = hit2D.collider.gameObject;
                    //print(hitCircle);

                    // If no circle is currently fixed, or the hit circle is not the fixed circle
                    if (fixedCircle == null || hitCircle != fixedCircle)
                    {
                        draggedCircle = hitCircle;
                        isDragging = true;

                        // If no circle is fixed, set the other circle as fixed
                        if (fixedCircle == null)
                        {
                            LineRenderer lr = hitCircle.transform.parent.GetComponent<LineRenderer>();
                            if (lr != null)
                            {
                                // Determine which circle is the other one
                                fixedCircle = (hitCircle.name == "StartPointCircle")
                                    ? lr.transform.Find("EndPointCircle").gameObject
                                    : lr.transform.Find("StartPointCircle").gameObject;
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
            Vector3 movementDelta = endPoint - initialHitPoint;

            // Move the line transform
            selectedLine.transform.position += movementDelta;

            // IMPORTANT: Update the LineRenderer points manually
            // This is necessary because LineRenderer points are in world space
            LineRenderer lr = selectedLine.GetComponent<LineRenderer>();
            if (lr != null && initialLinePositions != null)
            {
                for (int i = 0; i < lr.positionCount && i < initialLinePositions.Length; i++)
                {
                    // Apply the same movement to each point
                    lr.SetPosition(i, initialLinePositions[i] + (endPoint - initialHitPoint));
                }

                // Update the initialLinePositions for continuous movement
                for (int i = 0; i < initialLinePositions.Length; i++)
                {
                    initialLinePositions[i] += movementDelta;
                }
            }

            // Update the initialHitPoint for the next frame
            initialHitPoint = endPoint;
        }
        else if (isDragging && draggedCircle != null && isHoldingNow)
        {
            // Move the dragged circle
            draggedCircle.transform.position = endPoint;
            UpdateLineFromCircle(draggedCircle);
            HandleCircleAnchorAttachment();
        }

        // Reset states when releasing
        if (!isHoldingNow)
        {
            isDragging = false;
            draggedCircle = null;
            fixedCircle = null;

            if (isMovingLine)
            {
                isMovingLine = false;
                selectedLine = null;
                initialLinePositions = null; // Clear stored positions
                //Debug.Log("Stopped moving line");
            }
        }

        // Handle line drawing
        RaycastHit hit;
        if (!Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance, targetLayer)) return;

        if (hit.collider != null && hit.collider.CompareTag("Canvas") && canDraw && !isMovingLine)
        {
            GameObject canvasObject = hit.collider.gameObject;
            Vector3 hitPoint = hit.point;

            if (isHoldingNow)
            {
                if (!isDrawingLine)
                {
                    LineCreation(hitPoint, canvasObject);
                    isDrawingLine = true;

                    // Show circles while actively drawing a new line
                    if (currentLine != null)
                    {
                        ShowCirclesForLine(currentLine);
                    }
                }

                if (currentLineRenderer != null)
                {
                    currentLineRenderer.SetPosition(1, hitPoint);

                    // Continue showing circles while drawing
                    if (currentLine != null)
                    {
                        ShowCirclesForLine(currentLine);
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

                    // Hide the circles immediately when drawing completes
                    HideCirclesForLine(completedLine);

                    currentLineRenderer = null;
                    currentLine = null;

                    // Reset fixed circle tracking
                    fixedCircle = null;
                }

                canDraw = false;
                isDrawingLine = false;
            }
        }

        // Store pinch state for next frame
        wasPinchingLastFrame = isHoldingNow;
    }

    private bool wasPinchingLastFrame = false;
    private bool pinchingOnLine = false;
    private GameObject selectedLineForCircles = null;

    private void HideAllCircles()
    {
        // Find all objects with "Circle" tag and disable their renderers
        GameObject[] allCircles = GameObject.FindGameObjectsWithTag("Circle");
        foreach (GameObject circle in allCircles)
        {
            circle.gameObject.SetActive(false);
        }
    }

    private void ShowCirclesForLine(GameObject line)
    {
        if (line == null) return;

        // Find circles that are children of this line
        Transform startCircle = line.transform.Find("StartPointCircle");
        Transform endCircle = line.transform.Find("EndPointCircle");

        if (startCircle != null)
        {
            startCircle.gameObject.SetActive(true);
        }

        if (endCircle != null)
        {
            endCircle.gameObject.SetActive(true);
        }
        // Enable their renderers
        if (startCircle != null)
        {
            SpriteRenderer renderer = startCircle.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = true;
        }

        if (endCircle != null)
        {
            SpriteRenderer renderer = endCircle.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    private void HideCirclesForLine(GameObject line)
    {
        if (line == null) return;

        // Find circles that are children of this line
        Transform startCircle = line.transform.Find("StartPointCircle");
        Transform endCircle = line.transform.Find("EndPointCircle");
        if (startCircle != null)
        {
            startCircle.gameObject.SetActive(false);
        }

        if (endCircle != null)
        {
            endCircle.gameObject.SetActive(false);
        }
        // Disable their renderers
        if (startCircle != null)
        {
            SpriteRenderer renderer = startCircle.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        if (endCircle != null)
        {
            SpriteRenderer renderer = endCircle.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
        }
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

        // Create circle at the start point (hidden by default)
        CreateCircleAtPoint(hitPoint, canvasObject, currentLine, true);
        currentLine.AddComponent<ObjectToCanvas>();
        // Set the parent to the canvas
        currentLine.transform.SetParent(canvasObject.transform);
    }

    private void CreateCircleAtPoint(Vector3 point, GameObject parentCanvas, GameObject parentLine, bool isStartPoint)
    {
        GameObject circleObject = new GameObject(isStartPoint ? "StartPointCircle" : "EndPointCircle");
        circleObject.tag = "Circle";
        SpriteRenderer circleRenderer = circleObject.AddComponent<SpriteRenderer>();

        Texture2D circleTexture = CreateCircleTexture(64, Color.white);
        Sprite circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, circleTexture.width, circleTexture.height), new Vector2(0.4f, 0.4f));

        circleRenderer.sprite = circleSprite;

        // Make circle invisible by default
        circleRenderer.enabled = false;

        float circleSize = 0.08f;
        circleObject.transform.localScale = new Vector3(circleSize, circleSize, 1);

        // Parent it first, THEN set local position
        circleObject.transform.SetParent(parentLine.transform);

        // Convert world position to local space relative to the line
        Vector3 localPosition = parentLine.transform.InverseTransformPoint(point);
        localPosition.z = 0; // Ensure Z-axis remains 0
        circleObject.transform.localPosition = localPosition;

        circleRenderer.sortingOrder = 2;

        CircleCollider2D circleCollider = circleObject.AddComponent<CircleCollider2D>();
        circleCollider.radius = 0.4f;
    }

    private void UpdateLineFromCircle(GameObject circle)
    {
        LineRenderer lr = circle.transform.parent.GetComponent<LineRenderer>();
        if (lr == null) return;

        if (circle.name == "StartPointCircle")
        {
            lr.SetPosition(0, circle.transform.position);
        }
        else
        {
            lr.SetPosition(1, circle.transform.position);
        }

        // Update the EdgeCollider2D to match the new line position
        EdgeCollider2D edgeCollider = lr.gameObject.GetComponent<EdgeCollider2D>();
        if (edgeCollider != null)
        {
            Vector3 startLocal = lr.transform.InverseTransformPoint(lr.GetPosition(0));
            Vector3 endLocal = lr.transform.InverseTransformPoint(lr.GetPosition(1));

            edgeCollider.points = new Vector2[] { new Vector2(startLocal.x, startLocal.y), new Vector2(endLocal.x, endLocal.y) };
        }

        // Update the BoxCollider2D
        GameObject boxColliderObj = lr.transform.Find("BoxCollider")?.gameObject;
        if (boxColliderObj != null)
        {
            BoxCollider2D boxCollider = boxColliderObj.GetComponent<BoxCollider2D>();

            if (boxCollider != null)
            {
                // Calculate the new line direction and length
                Vector2 startLocal = lr.transform.InverseTransformPoint(lr.GetPosition(0));
                Vector2 endLocal = lr.transform.InverseTransformPoint(lr.GetPosition(1));
                Vector2 lineDirection = endLocal - startLocal;
                float lineLength = lineDirection.magnitude;
                lineDirection.Normalize();

                // Update the BoxCollider2D size and position
                float lineWidth = lr.startWidth; // Use line width as thickness
                boxCollider.size = new Vector2(lineLength, lineWidth);

                // Set the collider position to the midpoint of the line
                Vector2 midpoint = (startLocal + endLocal) / 2f;
                boxColliderObj.transform.localPosition = midpoint;

                // Reset rotation to match the line's direction
                boxColliderObj.transform.localRotation = Quaternion.identity;
                float angle = Mathf.Atan2(lineDirection.y, lineDirection.x) * Mathf.Rad2Deg;
                boxColliderObj.transform.Rotate(0, 0, angle);
            }
        }
    }

    private void AddColliderToLine(GameObject line)
    {
        LineRenderer lr = line.GetComponent<LineRenderer>();
        if (lr == null || lr.positionCount < 2) return;

        // Get world positions of line start and end
        Vector3 startWorld = lr.GetPosition(0);
        Vector3 endWorld = lr.GetPosition(1);

        // Convert world positions to local
        Vector3 startLocal = line.transform.InverseTransformPoint(startWorld);
        Vector3 endLocal = line.transform.InverseTransformPoint(endWorld);

        // Ensure EdgeCollider2D exists
        EdgeCollider2D edgeCollider = line.GetComponent<EdgeCollider2D>();
        if (edgeCollider == null) edgeCollider = line.AddComponent<EdgeCollider2D>();

        edgeCollider.points = new Vector2[] { new Vector2(startLocal.x, startLocal.y), new Vector2(endLocal.x, endLocal.y) };

        // Ensure BoxCollider2D exists in a separate child object
        GameObject boxColliderObj = line.transform.Find("BoxCollider")?.gameObject;
        if (boxColliderObj == null)
        {
            boxColliderObj = new GameObject("BoxCollider");
            boxColliderObj.transform.SetParent(line.transform);
            boxColliderObj.AddComponent<BoxCollider2D>();
        }
        boxColliderObj.tag = "DrawnLine";
        BoxCollider2D boxCollider = boxColliderObj.GetComponent<BoxCollider2D>();

        // Reset scale to match parent (prevents unwanted scaling issues)
        boxColliderObj.transform.localScale = Vector3.one;

        // Calculate line properties
        Vector2 lineDirection = (Vector2)(endLocal - startLocal);
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();

        float lineWidth = lr.startWidth; // Use line width as thickness

        // Set collider size
        boxCollider.size = new Vector2(lineLength, lineWidth);

        // Position collider at the midpoint
        Vector2 midpoint = (startLocal + endLocal) / 2f;
        boxColliderObj.transform.localPosition = midpoint;

        // Reset rotation to match parent first, then apply line rotation
        boxColliderObj.transform.localRotation = Quaternion.identity; // Match parent
        float angle = Mathf.Atan2(lineDirection.y, lineDirection.x) * Mathf.Rad2Deg;
        boxColliderObj.transform.Rotate(0, 0, angle);

        // Create circle at the end point
        CreateCircleAtPoint(endWorld, line.transform.parent.gameObject, line, false);

        // Ensure the line stays on top
        if (line.transform.parent != null)
        {
            Vector3 linePos = line.transform.localPosition;
            line.transform.localPosition = new Vector3(linePos.x, linePos.y, -0.01f);
        }
    }

    private Texture2D CreateCircleTexture(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // Calculate the center and radius
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        // Fill the texture with a circle
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Calculate distance from center
                float distance = Vector2.Distance(new Vector2(x, y), center);

                // Set pixel color based on distance from center
                if (distance <= radius)
                {
                    // Inside the circle - solid color
                    texture.SetPixel(x, y, color);
                }
                else
                {
                    // Outside the circle - transparent
                    Color transparentColor = color;
                    transparentColor.a = 0;
                    texture.SetPixel(x, y, transparentColor);
                }
            }
        }

        texture.Apply();
        return texture;
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

    // List to track which lines are attached to which anchors
    private Dictionary<GameObject, GameObject> circleToAnchorMap = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, Vector3> anchorToOffsetMap = new Dictionary<GameObject, Vector3>();

    // Add this to your existing LineDraw function, inside the part where you handle dragging circles
    private void HandleCircleAnchorAttachment()
    {
        // Only check for anchor attachment if we're dragging a circle
        if (isDragging && draggedCircle != null)
        {
            // Check if the dragged circle is overlapping with any anchor
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(draggedCircle.transform.position, 0.1f);

            GameObject nearestAnchor = null;
            float minDistance = 0.1f; // Minimum distance threshold for attachment

            foreach (Collider2D collider in hitColliders)
            {
                if (collider.CompareTag("Anchor"))
                {
                    float distance = Vector2.Distance(draggedCircle.transform.position, collider.transform.position);
                    if (distance < minDistance)
                    {
                        nearestAnchor = collider.gameObject;
                        minDistance = distance;
                    }
                }
            }

            // If we found an anchor and we're not already attached to it
            if (nearestAnchor != null && (!circleToAnchorMap.ContainsKey(draggedCircle) || circleToAnchorMap[draggedCircle] != nearestAnchor))
            {
                // Attach to the anchor
                AttachCircleToAnchor(draggedCircle, nearestAnchor);
            }

            // If we're not near any anchor but we were previously attached, detach
            if (nearestAnchor == null && circleToAnchorMap.ContainsKey(draggedCircle))
            {
                DetachCircleFromAnchor(draggedCircle);
            }
        }
    }

    private void AttachCircleToAnchor(GameObject circle, GameObject anchor)
    {
        // Store the attachment in our mapping
        circleToAnchorMap[circle] = anchor;

        // Calculate and store the offset (for when the anchor moves)
        Vector3 offset = circle.transform.position - anchor.transform.position;
        anchorToOffsetMap[circle] = Vector3.zero; // No offset when snapping directly

        // Snap the circle to the anchor position
        circle.transform.position = anchor.transform.position;

        // Update the line to match the new circle position
        UpdateLineFromCircle(circle);

        // Visual feedback (optional)
        SpriteRenderer circleRenderer = circle.GetComponent<SpriteRenderer>();
        if (circleRenderer != null)
        {
            // Store original color if needed
            // circleRenderer.color = Color.green; // Change color to indicate attachment
        }

        Debug.Log($"Attached {circle.name} to anchor {anchor.name}");
    }

    private void DetachCircleFromAnchor(GameObject circle)
    {
        // Remove the attachment from our mapping
        if (circleToAnchorMap.ContainsKey(circle))
        {
            circleToAnchorMap.Remove(circle);
            anchorToOffsetMap.Remove(circle);

            // Reset visual feedback
            SpriteRenderer circleRenderer = circle.GetComponent<SpriteRenderer>();
            if (circleRenderer != null)
            {
                // Reset to original color
                // circleRenderer.color = Color.red;
            }

            Debug.Log($"Detached {circle.name} from anchor");
        }
    }

    // Call this in Update() to handle moving anchors
    private void UpdateAttachedLines()
    {
        // For each attached circle
        foreach (var kvp in circleToAnchorMap.ToList()) // ToList to avoid collection modification during iteration
        {
            GameObject circle = kvp.Key;
            GameObject anchor = kvp.Value;

            // If either the circle or anchor has been destroyed, remove the mapping
            if (circle == null || anchor == null)
            {
                circleToAnchorMap.Remove(circle);
                continue;
            }

            // Update the circle position to follow the anchor
            circle.transform.position = anchor.transform.position;

            // Update the line
            UpdateLineFromCircle(circle);
        }
    }

    private void DetectCirclePinch(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting || canDraw) return;

        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);

        if (hit2D.collider != null)
        {
            // Check if the collider is tagged as a "Circle"
            if (hit2D.collider.CompareTag("Circle"))
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

    //--------------------- Object Spawn ------------------------

    // Add these variables to your class
    private bool canSpawnPrefab = false;
    private bool isSpawningPrefab = false;
    private GameObject currentPrefab = null;
    private Vector3 prefabStartPoint;

    // Add these prefab variables to your class
    public GameObject squarePrefab;    // Assign Square prefab in Inspector
    public GameObject circlePrefab;    // Assign Circle prefab in Inspector
    public GameObject trianglePrefab;  // Assign Triangle prefab in Inspector
    public GameObject capsulePrefab;   // Assign Capsule prefab in Inspector
    public GameObject hexagonPrefab;   // Assign Capsule prefab in Inspector

    // Variable to track which prefab to spawn
    private GameObject selectedPrefabType = null;

    private void PrefabSpawn(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting) return;

        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);

        // Check if we hit any of the shape objects to activate their specific prefab spawning mode
        if (hit2D.collider != null && !canSpawnPrefab && isHoldingNow)
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
        }

        // Handle prefab spawning
        RaycastHit hit;
        if (!Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance, targetLayer)) return;

        if (hit.collider != null && hit.collider.CompareTag("Canvas") && canSpawnPrefab && !isMovingLine)
        {
            GameObject canvasObject = hit.collider.gameObject;
            Vector3 hitPoint = hit.point;

            if (isHoldingNow)
            {
                if (!isSpawningPrefab)
                {
                    // Start spawning the selected prefab
                    PrefabCreation(hitPoint, canvasObject);
                    isSpawningPrefab = true;
                }

                if (currentPrefab != null)
                {
                    // Scale the prefab based on the distance between start point and current point
                    ScalePrefabBetweenPoints(prefabStartPoint, hitPoint);
                }
            }

            if (!isHoldingNow && isSpawningPrefab && canSpawnPrefab)
            {
                print("Prefab spawn complete");

                // Finalize the prefab
                if (currentPrefab != null)
                {
                    // Final scale adjustment
                    ScalePrefabBetweenPoints(prefabStartPoint, hitPoint);

                    // Reset tracking variables
                    currentPrefab = null;
                }

                canSpawnPrefab = false;
                isSpawningPrefab = false; selectCol = false;
                selectedPrefabType = null; // Reset the selected prefab type
            }
        }
    }

    private void PrefabCreation(Vector3 hitPoint, GameObject canvasObject)
    {
        // Store the initial hit point
        prefabStartPoint = new Vector3(hitPoint.x, hitPoint.y, hitPoint.z - 0.1f);

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

        // Calculate the distance between the two points
        float distance = Vector3.Distance(startPoint, endPoint);

        // Scale the prefab based on the distance
        float scaleValue = Mathf.Max(0.1f, distance);
        currentPrefab.transform.localScale = new Vector3(scaleValue, scaleValue, 1.0f);

        // Calculate the midpoint between start and end points
        Vector3 midPoint = (startPoint + endPoint) / 2;

        // Position the prefab at the midpoint
        currentPrefab.transform.position = midPoint;

        // Keep rotation fixed at (0,0,0)
        currentPrefab.transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    //---------------------- free hand -----------------------------
    private bool freeHandDrawingMode = false;
    private List<GameObject> drawingPoints = new List<GameObject>();
    private LineRenderer drawingLine;
    private GameObject currentDrawing;
    private float minDistanceBetweenPoints = 0.05f; // Minimum distance required between points
    private float closeShapeThreshold = 0.1f; // Distance threshold for closing a shape
    private Vector3 lastPointPosition; // To track the last point's position

    private void FreeHandDrawingDetection(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin))
        {
            return;
        }

        // Check for activating drawing mode with Pen object (2D)
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0);

        if (hit2D.collider != null && hit2D.collider.CompareTag("Pen"))
        {
            if (isHoldingNow && !freeHandDrawingMode)
            {
                // Activate free hand drawing mode
                ActivateDrawingMode();
                return;
            }
        }

        // If not in drawing mode, exit early
        if (!freeHandDrawingMode) return;

        // Check for drawing on Canvas (3D)
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance))
        {
            if (hit.collider.CompareTag("Canvas"))
            {
                if (isHoldingNow)
                {
                    // Convert hit point to canvas local space if needed
                    Vector3 drawPoint = hit.point;

                    // First, check if we're clicking on an existing drawing point
                    RaycastHit2D pointHit = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0);

                    if (pointHit.collider != null && pointHit.collider.CompareTag("DrawingPoint") && drawingPoints.Count >= 3)
                    {
                        // Find which point was hit
                        GameObject hitPoint = pointHit.collider.gameObject;
                        int pointIndex = drawingPoints.IndexOf(hitPoint);

                        if (pointIndex != -1 && pointIndex != drawingPoints.Count - 1) // Make sure we're not clicking the most recent point
                        {
                            // Close the shape by connecting current point to the selected point
                            FinalizeShape(pointIndex);
                            return;
                        }
                    }

                    // If not closing shape by clicking a point, check if we should add a new point
                    if (drawingPoints.Count == 0 ||
                        Vector3.Distance(drawPoint, lastPointPosition) >= minDistanceBetweenPoints)
                    {
                        CreateDrawingPoint(drawPoint);
                        UpdateDrawingLine();
                        lastPointPosition = drawPoint;
                    }
                }
            }
        }
    }

    private void ActivateDrawingMode()
    {
        freeHandDrawingMode = true;
        Debug.Log("Free Hand Drawing Mode Activated");

        // Clear any previous points
        ClearDrawingPoints();

        // Reset last point position
        lastPointPosition = Vector3.zero;

        // Create new drawing object
        currentDrawing = new GameObject("Drawing");

        // Add line renderer for connecting points
        drawingLine = currentDrawing.AddComponent<LineRenderer>();
        drawingLine.startWidth = 0.01f;
        drawingLine.endWidth = 0.01f;
        drawingLine.material = new Material(Shader.Find("Sprites/Default"));
        drawingLine.startColor = Color.black;
        drawingLine.endColor = Color.black;
        drawingLine.positionCount = 0;
    }

    private void CreateDrawingPoint(Vector3 position)
    {
        // Create a new GameObject for the point
        GameObject point = new GameObject("DrawingPoint");
        point.transform.position = position;

        // Add a SpriteRenderer component
        SpriteRenderer spriteRenderer = point.AddComponent<SpriteRenderer>();

        // Create a circle sprite
        spriteRenderer.sprite = CreateCircleSprite();
        spriteRenderer.color = Color.red;

        // Set appropriate scale for visibility
        point.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Add a 2D circle collider
        CircleCollider2D collider = point.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f; // This works with the sprite which has a size of 1 unit

        // Add a tag
        point.tag = "DrawingPoint";

        // Add to our list
        drawingPoints.Add(point);
        point.transform.SetParent(currentDrawing.transform);
    }

    private Sprite CreateCircleSprite()
    {
        // Create a texture for the circle
        int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize);

        // Calculate center and radius
        Vector2 center = new Vector2(textureSize / 2, textureSize / 2);
        float radius = textureSize / 2;

        // Fill the texture with transparent pixels
        Color[] colors = new Color[textureSize * textureSize];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }

        // Draw the circle
        for (int x = 0; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    colors[y * textureSize + x] = Color.white;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        // Create a sprite from the texture
        return Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f));
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

    private void FinalizeShape(int closeToPointIndex = 0)
    {
        freeHandDrawingMode = false;
        Debug.Log("Drawing complete - creating object");

        // Add final point to line to close shape
        drawingLine.positionCount = drawingPoints.Count + 1;
        drawingLine.SetPosition(drawingPoints.Count, drawingPoints[closeToPointIndex].transform.position);

        // Create a polygon collider to match the shape
        PolygonCollider2D polygonCollider = currentDrawing.AddComponent<PolygonCollider2D>();
        // Convert 3D points to 2D points for the collider
        Vector2[] colliderPoints = new Vector2[drawingPoints.Count];
        for (int i = 0; i < drawingPoints.Count; i++)
        {
            colliderPoints[i] = new Vector2(
                drawingPoints[i].transform.position.x,
                drawingPoints[i].transform.position.y
            );
        }

        // Set the points in the collider
        polygonCollider.points = colliderPoints;

        // Generate a sprite instead of mesh
        CreateSpriteForShape(colliderPoints);

        currentDrawing.transform.position -= new Vector3(0, 0, 88.01f);
        polygonCollider.offset = new Vector2(currentDrawing.transform.position.x * (-1), currentDrawing.transform.position.y * (-1)); // Offset collider in 2D space

        // Make drawing points invisible but keep them for reference
        foreach (GameObject point in drawingPoints)
        {
            point.GetComponent<Renderer>().enabled = false;
        }

        // Deactivate the LineRenderer
        drawingLine.enabled = false;

        // Reset for next drawing
        currentDrawing = null;
        drawingLine = null;
        drawingPoints.Clear();
    }

    private void CreateSpriteForShape(Vector2[] points)
    {
        // Find the bounds of the shape
        Vector2 min = points[0];
        Vector2 max = points[0];

        foreach (Vector2 point in points)
        {
            min.x = Mathf.Min(min.x, point.x);
            min.y = Mathf.Min(min.y, point.y);
            max.x = Mathf.Max(max.x, point.x);
            max.y = Mathf.Max(max.y, point.y);
        }

        // Add slight padding for better visual appearance
        Vector2 padding = new Vector2(0.1f, 0.1f);
        min -= padding;
        max += padding;

        // Calculate dimensions
        Vector2 size = max - min;

        // Set pixels per unit dynamically based on the shape's size
        float pixelsPerUnit = Mathf.Min(500f / size.x, 500f / size.y);

        // Create texture with high resolution for smoother edges
        int textureWidth = Mathf.CeilToInt(size.x * pixelsPerUnit);
        int textureHeight = Mathf.CeilToInt(size.y * pixelsPerUnit);

        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = "DrawingTexture";  // Give a name to the texture

        // Initialize texture pixels as transparent
        Color[] colors = new Color[textureWidth * textureHeight];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.clear;

        // Convert world space points to texture space
        Vector2[] texturePoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            texturePoints[i] = new Vector2(
                ((points[i].x - min.x) / size.x) * textureWidth,
                ((points[i].y - min.y) / size.y) * textureHeight
            );
        }

        // Fill polygon in texture
        Color fillColor = Color.white; // Semi-transparent blue
        FillPolygonAntiAliased(colors, textureWidth, textureHeight, texturePoints, fillColor);

        texture.SetPixels(colors);
        texture.Apply(true); // Generate mipmaps

        // Create a sprite from the texture and give it a name
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.name = "DrawingSprite";  // Give a name to the sprite

        // Add SpriteRenderer to the drawing object
        SpriteRenderer spriteRenderer = currentDrawing.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
        spriteRenderer.material.name = "DrawingMaterial";  // Give a name to the material

        // Ensure sprite and collider align perfectly
        currentDrawing.transform.position = new Vector3(min.x + size.x / 2, min.y + size.y / 2, currentDrawing.transform.position.z);
        currentDrawing.transform.localScale = Vector3.one; // Ensures no distortion in shape

        // Ensure PolygonCollider2D matches the shape
        PolygonCollider2D polygonCollider = currentDrawing.GetComponent<PolygonCollider2D>();
        if (polygonCollider != null)
        {
            polygonCollider.points = points;
        }
        currentDrawing.layer = 5;
        // Set tag for interaction
        currentDrawing.tag = "Target";
    }

    private void FillPolygonAntiAliased(Color[] colors, int width, int height, Vector2[] polygon, Color fillColor)
    {
        // First pass: Fill the solid interior using standard point-in-polygon
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsPointInPolygon(new Vector2(x, y), polygon))
                {
                    colors[y * width + x] = fillColor;
                }
            }
        }

        // Second pass: Advanced anti-aliasing for edges
        // Using distance-based anti-aliasing
        float aaRadius = 2.0f; // Anti-aliasing radius in pixels

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = y * width + x;

                // Skip if already fully filled or fully empty
                if (colors[idx].a == fillColor.a || colors[idx].a == 0f)
                {
                    // Check if this is near the edge by sampling nearby pixels
                    bool nearEdge = false;
                    float minDistance = float.MaxValue;

                    // Check pixels in a small radius
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                int neighborIdx = ny * width + nx;
                                // If we find a pixel with different fill state
                                if ((colors[idx].a > 0 && colors[neighborIdx].a == 0) ||
                                    (colors[idx].a == 0 && colors[neighborIdx].a > 0))
                                {
                                    nearEdge = true;
                                    minDistance = Mathf.Min(minDistance, Mathf.Sqrt(dx * dx + dy * dy));
                                }
                            }
                        }
                    }

                    // If we're near an edge, apply anti-aliasing
                    if (nearEdge)
                    {
                        // Calculate edge distance more precisely using polygon segments
                        float edgeDistance = DistanceToPolygonEdge(new Vector2(x, y), polygon);

                        // Apply anti-aliasing based on edge distance
                        if (edgeDistance < aaRadius)
                        {
                            // Blend factor based on distance (closer to edge = more transparent)
                            float blend = Mathf.Clamp01(edgeDistance / aaRadius);

                            if (colors[idx].a > 0)
                            {
                                // Inside edge - fade out
                                colors[idx] = new Color(
                                    fillColor.r,
                                    fillColor.g,
                                    fillColor.b,
                                    fillColor.a * blend
                                );
                            }
                            else
                            {
                                // Outside edge - fade in
                                colors[idx] = new Color(
                                    fillColor.r,
                                    fillColor.g,
                                    fillColor.b,
                                    fillColor.a * (1f - blend)
                                );
                            }
                        }
                    }
                }
            }
        }
    }

    private float DistanceToPolygonEdge(Vector2 point, Vector2[] polygon)
    {
        float minDistance = float.MaxValue;

        // Check distance to each edge segment
        for (int i = 0; i < polygon.Length; i++)
        {
            int j = (i + 1) % polygon.Length;

            // Get the line segment
            Vector2 start = polygon[i];
            Vector2 end = polygon[j];

            // Calculate distance from point to line segment
            float dist = DistanceToLineSegment(point, start, end);
            minDistance = Mathf.Min(minDistance, dist);
        }

        return minDistance;
    }

    private float DistanceToLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        // Calculate squared length of line segment
        float lengthSquared = (lineEnd - lineStart).sqrMagnitude;

        // If segment is a point, return distance to that point
        if (lengthSquared == 0f)
            return Vector2.Distance(point, lineStart);

        // Calculate projection of point onto line
        float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, lineEnd - lineStart) / lengthSquared);

        // Calculate closest point on line segment
        Vector2 projection = lineStart + t * (lineEnd - lineStart);

        // Return distance to closest point
        return Vector2.Distance(point, projection);
    }

    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        // Point-in-polygon algorithm (ray casting)
        bool inside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                inside = !inside;
            }
        }

        return inside;
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

    //---------------------- MultiSelect ----------------------

    private bool multiSelectMode = false;
    private GameObject selectionMask;
    private Vector3 selectionStartPosition;
    private List<GameObject> selectedObjects = new List<GameObject>();
    private bool istarget = false;
    private bool isSelectionMaskActive = false;
    private void MultiSelectDetection(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin))
        {
            return;
        }
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0);

        // Check for activating multiselect mode
        if (!multiSelectMode)
        {

            if (hit2D.collider != null && hit2D.collider.CompareTag("MultiSelect"))
            {
                if (isHoldingNow)
                {
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
                // Check for canvas hit
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance))
                {
                    if (hit.collider.CompareTag("Canvas"))
                    {
                        // If first touch, create selection mask
                        if (selectionMask == null)
                        {
                            selectionStartPosition = hit.point;
                            CreateSelectionMask(selectionStartPosition);
                        }
                        else
                        {
                            // Update mask size based on drag
                            UpdateSelectionMask(hit.point);
                        }
                    }
                }
            }
            else // Not holding anymore
            {
                if (selectionMask != null)
                {
                    // Finalize selection
                    FinalizeSelection();
                }
            }
        }
        RaycastHit2D hit2D1 = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0, targetLayer);

        if (hit2D1.collider != null && hit2D1.collider.tag == "Target")
        {
            istarget = true;
        }
        else
        {
            istarget = false;
        }

        // Check for deselecting by pinching elsewhere
        if (isHoldingNow && !multiSelectMode && selectedObjects.Count > 0 && !istarget)
        {
            print("---------------------deselecting -------------------");
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance))
            {
                // If pinch is not on the selection container or canvas, deselect
                if (hit.collider != null &&
                    hit.collider.CompareTag("Canvas"))
                {
                    DeselectAll();
                }
            }
        }
    }

    private void CreateSelectionMask(Vector3 position)
    {
        // Create a new game object for the selection mask
        selectionMask = new GameObject("SelectionMask");

        // Add Line Renderer component instead of a sprite
        LineRenderer lineRenderer = selectionMask.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 5; // 5 points to create a closed rectangle (last point connects back to first)
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(0.3f, 0.6f, 1f, 0.3f);
        lineRenderer.endColor = new Color(0.3f, 0.6f, 1f, 0.3f);

        // Store the start position
        selectionStartPosition = position;

        // Initialize with all points at the start position
        for (int i = 0; i < 5; i++)
        {
            lineRenderer.SetPosition(i, position);
        }

        // Add a MeshFilter and MeshRenderer for the fill area
        MeshFilter meshFilter = selectionMask.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = selectionMask.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material.color = new Color(0.3f, 0.6f, 1f, 0.2f);
    }

    private void UpdateSelectionMask(Vector3 currentPosition)
    {
        if (selectionMask == null) return;

        LineRenderer lineRenderer = selectionMask.GetComponent<LineRenderer>();
        if (lineRenderer == null) return;

        // Update the line renderer to draw a rectangle
        // Point 0: Start position
        lineRenderer.SetPosition(0, selectionStartPosition);
        // Point 1: Top-right corner
        lineRenderer.SetPosition(1, new Vector3(currentPosition.x, selectionStartPosition.y, selectionStartPosition.z));
        // Point 2: Current position (bottom-right corner)
        lineRenderer.SetPosition(2, currentPosition);
        // Point 3: Bottom-left corner
        lineRenderer.SetPosition(3, new Vector3(selectionStartPosition.x, currentPosition.y, selectionStartPosition.z));
        // Point 4: Back to start position (to close the rectangle)
        lineRenderer.SetPosition(4, selectionStartPosition);

        // Update the fill mesh
        MeshFilter meshFilter = selectionMask.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[4];
            vertices[0] = selectionMask.transform.InverseTransformPoint(selectionStartPosition);
            vertices[1] = selectionMask.transform.InverseTransformPoint(new Vector3(currentPosition.x, selectionStartPosition.y, selectionStartPosition.z));
            vertices[2] = selectionMask.transform.InverseTransformPoint(currentPosition);
            vertices[3] = selectionMask.transform.InverseTransformPoint(new Vector3(selectionStartPosition.x, currentPosition.y, selectionStartPosition.z));

            mesh.vertices = vertices;
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;
        }
    }

    private void FinalizeSelection()
    {
        // Create bounds from the two corners
        Vector3 min = new Vector3(
            Mathf.Min(selectionStartPosition.x, selectionMask.GetComponent<LineRenderer>().GetPosition(2).x),
            Mathf.Min(selectionStartPosition.y, selectionMask.GetComponent<LineRenderer>().GetPosition(2).y),
            selectionStartPosition.z
        );

        Vector3 max = new Vector3(
            Mathf.Max(selectionStartPosition.x, selectionMask.GetComponent<LineRenderer>().GetPosition(2).x),
            Mathf.Max(selectionStartPosition.y, selectionMask.GetComponent<LineRenderer>().GetPosition(2).y),
            selectionStartPosition.z
        );

        // Calculate width and height of the selection
        float width = max.x - min.x;
        float height = max.y - min.y;

        // Calculate center point of the selection
        Vector3 center = new Vector3(
            min.x + width / 2,
            min.y + height / 2,
            min.z
        );

        // Find all objects under the mask
        Collider2D[] colliders = Physics2D.OverlapAreaAll(min, max);

        bool foundSelectableObjects = false;

        // Add objects to the selection
        foreach (Collider2D collider in colliders)
        {
            // Check if it's a selectable object
            if (collider.gameObject != selectionMask && collider.CompareTag("Target"))
            {
                // Add to selection
                selectedObjects.Add(collider.gameObject);

                // Parent directly to selection mask
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
            // Disable ONLY the LineRenderer to hide the selection outline
            LineRenderer lineRenderer = selectionMask.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            // Keep the MeshRenderer active to show the filled area
            MeshRenderer meshRenderer = selectionMask.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                // Make sure it's enabled
                meshRenderer.enabled = true;

                // You can optionally adjust the opacity if needed
                Color color = meshRenderer.material.color;
                meshRenderer.material.color = new Color(color.r, color.g, color.b, 0.3f); // Adjust the alpha as needed
            }

            // Add box collider to the mask - make sure it matches exact dimensions
            BoxCollider2D maskCollider = selectionMask.GetComponent<BoxCollider2D>();
            if (maskCollider == null)
            {
                maskCollider = selectionMask.AddComponent<BoxCollider2D>();
            }

            // Update the collider's position and size to match the selection exactly
            maskCollider.offset = selectionMask.transform.InverseTransformPoint(center);
            maskCollider.size = new Vector2(width, height);

            // Set the mask's tag to "Target"
            selectionMask.tag = "Target";

            // Set the mask's layer to UI layer
            selectionMask.layer = LayerMask.NameToLayer("UI");
        }

        // Deactivate multiselect mode after finalizing selection
        multiSelectMode = false;
        Debug.Log("Multi-Select Mode Deactivated - Selection Mask Remains Visible");
    }

    private void DeselectAll()
    {
        if (selectionMask != null)
        {
            // Get a copy of the direct children to avoid issues while modifying the hierarchy
            List<Transform> directChildren = new List<Transform>();
            for (int i = 0; i < selectionMask.transform.childCount; i++)
            {
                directChildren.Add(selectionMask.transform.GetChild(i));
            }

            // Unparent only the direct children of the selection mask
            // This preserves their own child hierarchies
            foreach (Transform child in directChildren)
            {
                // Make sure the object stays active
                if (child.gameObject.name == "MaskObject") continue;
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
        Debug.Log("Multi-Select Mode Deactivated");
    }

    //---------------------- Button Functions ----------------------

    private bool isDestroy = false;

    private bool DoDestroy = false;
    private bool ButtonPressed = false;
    public GameObject colorObject; // Reference to the color object
    public GameObject ShapesObject;

    private void ButtonFeedback(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (rot || adjusting || block || multiSelectMode || isSelectionMaskActive) return;
        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);
        if (hit2D.collider != null)
        {
            if (hit2D.collider.CompareTag("Destroy") ||
                hit2D.collider.CompareTag("Line") ||
                hit2D.collider.CompareTag("ColorPicker") ||
                hit2D.collider.CompareTag("Shape") ||
                hit2D.collider.CompareTag("Color") ||
                hit2D.collider.CompareTag("Target") ||
                hit2D.collider.CompareTag("Finish") ||
                hit2D.collider.CompareTag("Squares") ||
                hit2D.collider.CompareTag("Circles") ||
                hit2D.collider.CompareTag("Triangles") ||
                hit2D.collider.CompareTag("Capsules") ||
                hit2D.collider.CompareTag("DrawnLine") ||
                hit2D.collider.CompareTag("UI") ||
                hit2D.collider.CompareTag("Hexagon") ||
                hit2D.collider.CompareTag("Pen") ||
                hit2D.collider.CompareTag("MultiSelect"))
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
                    if (colorObject != null)
                    {
                        colorObject.SetActive(true);
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
                    if (ShapesObject != null)
                    {
                        ShapesObject.SetActive(true);
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
                if (colorObject != null)
                {
                    colorObject.SetActive(false);
                }
                if (ShapesObject != null)
                {
                    ShapesObject.SetActive(false);
                }
            }

            if (maskObject != null)
            {
                maskObject.SetActive(false);
            }
        }
    }

    private GameObject DestroyObject = null;

    private void DestroyObj(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting) return;

        RaycastHit2D hit2D = getRaycast2d(rayOrigin, rayDirection);

        if (hit2D.collider != null)
        {
            GameObject hitObject = hit2D.collider.gameObject.transform.parent != null
                ? hit2D.collider.gameObject.transform.parent.gameObject
                : hit2D.collider.gameObject;

            if (hit2D.collider.CompareTag("Destroy"))
            {
                if (isHoldingNow && !isPinching)
                {
                    isPinching = true;
                    if (!isDestroy)
                    {
                        isDestroy = true;
                        Debug.Log("Destruction mode activated.");
                    }
                }
                if (!isHoldingNow && isPinching)
                {

                    if (DoDestroy && DestroyObject != null)
                    {
                        isPinching = false;
                        DestroyObject.SetActive(false);
                        Debug.Log("Destroyed object: " + DestroyObject.name);

                        DoDestroy = false;
                        DestroyObject = null;
                        isDestroy = false;
                        return;
                    }
                }
            }

            if (hit2D.collider.CompareTag("Target") || hit2D.collider.CompareTag("DrawnLine"))
            {
                if (isHoldingNow)
                {
                    if (isDestroy)
                    {
                        hitObject.SetActive(false);
                        Debug.Log("Destroyed object: " + hitObject.name);
                        isDestroy = false;
                        DestroyObject = null;
                        DoDestroy = false;
                        return;
                    }

                    if (!DoDestroy)
                    {
                        DoDestroy = true;
                        DestroyObject = hitObject;
                        Debug.Log("Object selected for destruction: " + DestroyObject.name);
                    }
                }
            }
        }
    }

    private bool isColorCopyMode = false;
    private Color copiedColor = Color.white;
    private bool hasColorCopied = false;
    private GameObject selectcolObject = null;
    private bool selectCol = false;
    private void ChangeColor2(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting || canSpawnPrefab) return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0);

        // Check if we hit any collider
        if (hit2D.collider != null)
        {
            GameObject hitObject = hit2D.collider.gameObject;

            // Step 1: Copy color from Color-tagged object
            if (hit2D.collider.CompareTag("Color"))
            {

                if (isHoldingNow && !isPinching)
                {// Get exact pixel color at hit point
                    copiedColor = SampleExactSpriteColor(hit2D.point, hitObject);
                    if (!isColorCopyMode)
                    {
                        isPinching = true;
                        hasColorCopied = true;
                        isColorCopyMode = true;
                    }

                    // Debug the sampled color
                    Debug.Log($"Sampled Color: R:{copiedColor.r:F3}, G:{copiedColor.g:F3}, B:{copiedColor.b:F3}, A:{copiedColor.a:F3}");
                    Debug.Log("Color copy mode activated. Copied pixel color: " + copiedColor.ToString());
                }

                if (!isHoldingNow && isPinching)
                {
                    isPinching = false;
                    // Get exact pixel color at hit point
                    copiedColor = SampleExactSpriteColor(hit2D.point, hitObject);


                    if (selectcolObject != null && selectcolObject.tag == "Target" && selectCol)
                    {
                        selectCol = false;
                        Renderer renderer = selectcolObject.GetComponent<Renderer>();
                        if (selectcolObject != null)
                        {
                            if (renderer != null)
                            {
                                // Apply to all materials if there are multiple
                                for (int i = 0; i < renderer.materials.Length; i++)
                                {
                                    renderer.materials[i].color = copiedColor;
                                }
                                Debug.Log("Applied color to: " + hit2D.collider.gameObject.name);
                                selectcolObject = null;
                                return;
                            }

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
                                selectcolObject = null;
                                return;
                            }
                        }
                    }
                }
                // Only activate on pinch (when holding starts)

            }



            if (hit2D.collider.CompareTag("Target") || hit2D.collider.CompareTag("DrawnLine"))
            {
                GameObject targetObject = hit2D.collider.gameObject.transform.parent != null
                        ? hit2D.collider.gameObject.transform.parent.gameObject
                        : hit2D.collider.gameObject;
                if (isHoldingNow && !selectCol)
                {
                    selectcolObject = targetObject;
                    print("------------selceted--------" + targetObject);
                    selectCol = true;
                }


                if (isHoldingNow && isColorCopyMode && hasColorCopied)
                {
                    // Skip certain tags that shouldn't receive color changes
                    if (targetObject.CompareTag("Canvas") ||
                        targetObject.CompareTag("Destroy") ||
                        targetObject.CompareTag("Line") ||
                        targetObject.CompareTag("Color") ||
                        targetObject.name == "GalleryWindow" ||
                        targetObject.name == "UtilPanel" ||
                        targetObject.name == "ShapesPanel")
                    {
                        return;
                    }

                    // **Handle LineRenderer for DrawnLine**
                    if (targetObject.CompareTag("DrawnLine"))
                    {
                        GameObject parentObject = targetObject;
                        if (parentObject != null && isHoldingNow)
                        {
                            LineRenderer lineRenderer = parentObject.GetComponent<LineRenderer>();
                            if (lineRenderer != null)
                            {
                                lineRenderer.startColor = copiedColor;
                                lineRenderer.endColor = copiedColor;
                                Debug.Log("Applied color to LineRenderer: " + parentObject.name);
                                isColorCopyMode = false;
                                return;
                            }
                        }
                    }

                    if (targetObject.name == "Drawing")
                    {
                        UpdateShapeFillColor(targetObject,copiedColor);
                    }

                    if (targetObject.CompareTag("Target"))
                    {
                        Renderer renderer = targetObject.gameObject.GetComponent<Renderer>();
                        if (targetObject.gameObject != null && isHoldingNow)
                        {
                            if (renderer != null)
                            {
                                // Apply to all materials if there are multiple
                                for (int i = 0; i < renderer.materials.Length; i++)
                                {
                                    renderer.materials[i].color = copiedColor;
                                }
                                isColorCopyMode = false;
                                Debug.Log("Applied color to: " + targetObject.gameObject.name);
                                return;
                            }
                        }
                    }

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

        SpriteRenderer spriteRenderer = shapeObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // Option 1: Simply change the sprite color - fastest but affects the whole sprite uniformly
            spriteRenderer.color = newColor;

            // Option 2: If you need to regenerate the texture with the exact same shape but new fill color
            // Uncomment and use this method instead for precise color control
            /*
            Texture2D originalTexture = spriteRenderer.sprite.texture;
            Texture2D newTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGBA32, false);

            // Get the original pixels
            Color[] pixels = originalTexture.GetPixels();
            Color[] newPixels = new Color[pixels.Length];

            // Replace colors while preserving alpha values
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                {
                    // Keep the original alpha but use the new color
                    newPixels[i] = new Color(newColor.r, newColor.g, newColor.b, pixels[i].a);
                }
                else
                {
                    newPixels[i] = Color.clear;
                }
            }

            // Apply the new pixels
            newTexture.SetPixels(newPixels);
            newTexture.Apply(true);

            // Create a new sprite with the updated texture
            Sprite newSprite = Sprite.Create(
                newTexture,
                spriteRenderer.sprite.rect,
                spriteRenderer.sprite.pivot,
                spriteRenderer.sprite.pixelsPerUnit
            );

            // Assign the new sprite
            spriteRenderer.sprite = newSprite;
            */
        }
        else
        {
            Debug.LogWarning("Shape object does not have a valid SpriteRenderer or Sprite.");
        }
    }
    // Improved color sampling method specifically designed for accurate sprite color detection
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

    private GameObject selectedUIElement = null;
    private bool isScalingUI = false;
    private Vector3 initialHitPointUI;
    private Vector3 initialParentScale;
    private Vector3 initialRayDirection1;

    // Add a scaling sensitivity modifier that works well with small scale values
    [SerializeField] private float scaleModifier = 0.05f; // Adjust this in inspector

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
                initialParentScale.z
            );

            // Ensure minimum scale doesn't go too small
            float minScale = initialParentScale.x * 0.25f; // Allow scaling to 25% of original at minimum
            newScale.x = Mathf.Max(newScale.x, minScale);
            newScale.y = Mathf.Max(newScale.y, minScale);

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
            isScalingUI = false;
            selectedUIElement = null;
            Debug.Log("Stopped scaling UI");
        }
    }

    //---------------------- Helper Functions ----------------------

    private void ResetPinchState()
    {
        isFirstMove = true;
    }

    //---------------------- Rotation ----------------------

    private void InitializeCircularPath()
    {
        if (circularPathRenderer != null) return; // Prevent multiple initializations

        GameObject pathObject = new GameObject("CircularPath");
        circularPathRenderer = pathObject.AddComponent<LineRenderer>();

        circularPathRenderer.material = new Material(Shader.Find("Sprites/Default"));
        circularPathRenderer.startColor = new Color(1, 1, 1, 0.5f); // Slightly more visible
        circularPathRenderer.endColor = new Color(1, 1, 1, 0.5f);
        circularPathRenderer.startWidth = 0.05f;
        circularPathRenderer.endWidth = 0.05f;
        circularPathRenderer.positionCount = CIRCLE_SEGMENTS + 1;
        circularPathRenderer.useWorldSpace = true;
        circularPathRenderer.enabled = false;
        circularPathRenderer.sortingOrder = 1;
        circularPathRenderer.alignment = LineAlignment.TransformZ;
    }

    private void UpdateCircularPath(Vector3 center, float radius)
    {
        if (circularPathRenderer == null) return;

        // **Prevent invisible or zero-radius circles**
        if (radius <= 0.01f) return;

        Vector3[] positions = new Vector3[CIRCLE_SEGMENTS + 1];
        for (int i = 0; i <= CIRCLE_SEGMENTS; i++)
        {
            float angle = i * (2 * Mathf.PI / CIRCLE_SEGMENTS);
            float x = center.x + radius * Mathf.Cos(angle);
            float y = center.y + radius * Mathf.Sin(angle);
            positions[i] = new Vector3(x, y, center.z);
        }
        circularPathRenderer.SetPositions(positions);
        circularPathRenderer.enabled = true; // Ensure it is visible
    }

    private void RotateAndDragParentObject(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (adjusting) return;
        if (block || isMovingTarget) return;

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        float hoverOffset = 0.01f;
        RaycastHit2D hitObject = Physics2D.CircleCast(raycastOrigin, hoverOffset, Vector2.zero, 0, targetLayer);

        if (isHoldingNow)
        {
            if (!rotpinch)
            {
                // Start pinching
                if (hitObject.collider != null && hitObject.collider.CompareTag("Rot"))
                {
                    selectedRotObject = hitObject.collider.gameObject;
                    Transform parentTransform = selectedRotObject.transform.parent;
                    Renderer hitObjectRenderer = selectedRotObject.GetComponent<Renderer>();

                    if (parentTransform != null)
                    {
                        rotpinch = true;

                        // Store initial states
                        isFirstMove = true;
                        initialObjectPosition = selectedRotObject.transform.position;
                        initialObjectRotation = parentTransform.rotation;

                        rotationCenter = parentTransform.position;
                        rotationRadius = Vector2.Distance(
                            new Vector2(selectedRotObject.transform.position.x, selectedRotObject.transform.position.y),
                            new Vector2(parentTransform.position.x, parentTransform.position.y)
                        );

                        // Calculate initial angle from parent to rotation object
                        Vector2 initialDirection = (Vector2)selectedRotObject.transform.position - (Vector2)parentTransform.position;
                        initialAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;

                        // Store initial ray for reference
                        initialRayDirection = rayDirection;

                        // Ensure the path always initializes
                        InitializeCircularPath();
                        UpdateCircularPath(rotationCenter, rotationRadius);

                        // Change color while pinching
                        if (hitObjectRenderer != null)
                        {
                            hitObjectRenderer.material.color = Color.red;
                        }
                    }
                }
            }

            if (rotpinch && selectedRotObject != null)
            {
                // Pass the parent transform directly to ensure we're working with the right object
                Transform parentTransform = selectedRotObject.transform.parent;
                if (parentTransform != null)
                {
                    RotateWithRay(selectedRotObject, parentTransform, rayDirection);
                }
                rot = true;
            }
        }

        if (!isHoldingNow && rotpinch)
        {
            rotpinch = false;
            isFirstMove = true;
            ResetPinchState();

            if (selectedRotObject != null)
            {
                Renderer hitObjectRenderer = selectedRotObject.GetComponent<Renderer>();
                if (hitObjectRenderer != null)
                {
                    hitObjectRenderer.material.color = Color.white;
                }

                // Ensure path hides properly
                if (circularPathRenderer != null && circularPathRenderer.enabled)
                {
                    circularPathRenderer.enabled = false;
                }

                selectedRotObject = null;
            }
        }
    }

    private void RotateWithRay(GameObject rotObject, Transform parentTransform, Vector3 rayDirection)
    {
        if (rotObject == null || parentTransform == null) return;

        // On first move, just set up initial state and return
        if (isFirstMove)
        {
            isFirstMove = false;
            return;
        }

        // Project rays onto the XY plane to ensure consistent movement
        Vector3 initialRayFlat = Vector3.ProjectOnPlane(initialRayDirection, Vector3.forward).normalized;
        Vector3 currentRayFlat = Vector3.ProjectOnPlane(rayDirection, Vector3.forward).normalized;

        // Calculate angle between the initial ray and current ray
        float rayAngleDelta = Vector3.SignedAngle(initialRayFlat, currentRayFlat, Vector3.forward);

        // Apply rotation to parent based on ray angle change
        Quaternion targetRotation = initialObjectRotation * Quaternion.Euler(0, 0, rayAngleDelta);

        // Apply smooth rotation
        float rotationSpeed = 0.25f;
        parentTransform.rotation = Quaternion.Slerp(parentTransform.rotation, targetRotation, rotationSpeed);

        // Move rotation handle along circle
        float angleRad = (initialAngle + rayAngleDelta) * Mathf.Deg2Rad;
        Vector3 newPosition = rotationCenter + new Vector3(
            Mathf.Cos(angleRad),
            Mathf.Sin(angleRad),
            0
        ) * rotationRadius;

        float moveSpeed = 0.25f;
        rotObject.transform.position = Vector3.Lerp(rotObject.transform.position, newPosition, moveSpeed);

        // Update the circular path visualization if needed
        if (circularPathRenderer != null && circularPathRenderer.enabled)
        {
            UpdateCircularPath(rotationCenter, rotationRadius);
        }
    }

    //---------------------- Traslation ----------------------

    private GameObject selectedTarget = null;
    private bool isMovingTarget = false;
    private Vector3 initialHitPoint1;
    private Vector3 initialTargetPosition;
    private Vector3 targetPosition;
    public float smoothSpeed = 20f; // Adjust speed as needed

    private void TargetDrag(Vector3 rayOrigin, Vector3 rayDirection, bool isHoldingNow)
    {
        if (block || rot || adjusting || isMovingLine || isColorCopyMode || multiSelectMode || isSelectionMaskActive) return;
        //if(!isMovingTarget) maskObject.SetActive(false);

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin)) return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);
        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0, targetLayer);

        // Check if we hit a Target object
        if (hit2D.collider != null && hit2D.collider.CompareTag("Target") && !isDragging)
        {
            GameObject hitTarget = hit2D.collider.gameObject;

            // Get the parent object if available
            GameObject parentTarget = hitTarget.transform.parent != null ? hitTarget.transform.parent.gameObject : hitTarget;

            if (isHoldingNow && !isMovingTarget)
            {
                // Start moving the target
                selectedTarget = parentTarget;
                isMovingTarget = true;
                initialHitPoint1 = endPoint;
                initialTargetPosition = selectedTarget.transform.position;
                targetPosition = initialTargetPosition; // Initialize smooth target position
                CreateMask(selectedTarget);
                Debug.Log("Starting to move target: " + selectedTarget.name);
            }
        }

        // Handle moving the target with smooth transition
        if (isMovingTarget && selectedTarget != null && isHoldingNow)
        {
            // Calculate movement delta
            Vector3 movementDelta = endPoint - initialHitPoint1;

            // Compute new target position
            targetPosition = initialTargetPosition + movementDelta;

            // Apply smooth movement
            selectedTarget.transform.position = Vector3.Lerp(
                selectedTarget.transform.position,
                targetPosition,
                Time.deltaTime * smoothSpeed
            );
        }

        // Reset states when releasing
        if (!isHoldingNow && isMovingTarget)
        {
            isMovingTarget = false;
            selectedTarget = null;
            if (maskObject != null)
            {
                maskObject.SetActive(false);
            }
            Debug.Log("Stopped moving target");
        }
    }

    //---------------------- Rescaling ----------------------

    private void AdjustParentSizeOnPinch2(Vector3 rayOrigin, Vector3 rayDirection, bool isPinching)
    {
        if (rot || isDragging || multiSelectMode || isSelectionMaskActive) return;
        if (block || isMovingTarget) SetChildrenActive(parent, false);

        Vector3 raycastOrigin;
        if (!GetRaycastPoint(rayOrigin, rayDirection, out raycastOrigin))
            return;

        Vector3 endPoint = rayOrigin + (rayDirection * rayDistance);

        float hoverOffset = 0.01f;
        RaycastHit2D hit2D = Physics2D.CircleCast(endPoint, hoverOffset, Vector2.zero, 0, targetLayer);

        if (hit2D.collider != null && isPinchingActive && (currentlyAdjustingObject != null && currentlyAdjustingObject != hit2D.collider.gameObject))
        {
            return;
        }

        if (hit2D.collider != null || currentlyAdjustingObject != null)
        {
            GameObject hitObject = hit2D.collider != null ? hit2D.collider.gameObject : currentlyAdjustingObject;

            Transform parentTransform = hitObject.transform.parent;
            if (parentTransform == null) return;

            if (isPinching)
            {
                if (!isPinchingActive)
                {
                    isPinchingActive = true;
                    lastRayPosition = raycastOrigin;
                    currentlyAdjustingObject = hitObject;

                    SetChildrenActive(parentTransform.gameObject, false);
                    hitObject.SetActive(true);
                    return;
                }

                adjusting = true;

                // Get the pinch movement in world space
                Vector3 pinchDelta = raycastOrigin - lastRayPosition;
                pinchDelta *= scaleFactor;

                // Convert world space delta to parent's local space
                Vector3 localPinchDelta = parentTransform.InverseTransformDirection(pinchDelta);

                Vector3 currentScale = parentTransform.localScale;
                Vector3 currentPosition = parentTransform.position;

                float scaleChangeX = 0, scaleChangeY = 0;
                Vector3 localPositionOffset = Vector3.zero;

                // Apply correct scaling directions using local coordinates
                if (hitObject.CompareTag("X"))
                {
                    scaleChangeX = localPinchDelta.x;
                    localPositionOffset = new Vector3(scaleChangeX * 0.5f, 0, 0);
                }
                else if (hitObject.CompareTag("NX"))
                {
                    scaleChangeX = -localPinchDelta.x;
                    localPositionOffset = new Vector3(-scaleChangeX * 0.5f, 0, 0);
                }
                else if (hitObject.CompareTag("Y"))
                {
                    scaleChangeY = localPinchDelta.y;
                    localPositionOffset = new Vector3(0, scaleChangeY * 0.5f, 0);
                }
                else if (hitObject.CompareTag("NY"))
                {
                    scaleChangeY = -localPinchDelta.y;
                    localPositionOffset = new Vector3(0, -scaleChangeY * 0.5f, 0);
                }
                else if (hitObject.CompareTag("XY"))
                {
                    scaleChangeX = localPinchDelta.x;
                    scaleChangeY = localPinchDelta.y;
                    localPositionOffset = new Vector3(scaleChangeX * 0.5f, scaleChangeY * 0.5f, 0);
                }
                else if (hitObject.CompareTag("XNY"))
                {
                    scaleChangeX = localPinchDelta.x;
                    scaleChangeY = -localPinchDelta.y;
                    localPositionOffset = new Vector3(scaleChangeX * 0.5f, -scaleChangeY * 0.5f, 0);
                }
                else if (hitObject.CompareTag("NXY"))
                {
                    scaleChangeX = -localPinchDelta.x;
                    scaleChangeY = localPinchDelta.y;
                    localPositionOffset = new Vector3(-scaleChangeX * 0.5f, scaleChangeY * 0.5f, 0);
                }
                else if (hitObject.CompareTag("NXNY"))
                {
                    scaleChangeX = -localPinchDelta.x;
                    scaleChangeY = -localPinchDelta.y;
                    localPositionOffset = new Vector3(-scaleChangeX * 0.5f, -scaleChangeY * 0.5f, 0);
                }

                // Apply scale changes with minimum size limit
                currentScale.x = Mathf.Max(currentScale.x + scaleChangeX, 0.1f);
                currentScale.y = Mathf.Max(currentScale.y + scaleChangeY, 0.1f);

                // Convert local position offset to world space and apply
                Vector3 worldPositionOffset = parentTransform.TransformDirection(localPositionOffset);
                currentPosition += worldPositionOffset;

                // Apply the changes
                rescaleChildObject(currentScale, parentTransform, currentPosition, hitObject);

                // Update last position with smoothing
                lastRayPosition = Vector3.Lerp(lastRayPosition, raycastOrigin, 0.2f);

                // Update color feedback
                SpriteRenderer spriteRenderer = hitObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(spriteRenderer.color, Color.red, 0.2f);
                }
            }
            else
            {
                isPinchingActive = false;

                SetChildrenActive(parentTransform.gameObject, true);

                if (currentlyAdjustingObject == hitObject)
                {
                    currentlyAdjustingObject = null;
                }

                // Reset color when not pinching
                SpriteRenderer spriteRenderer = hitObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.white;
                }
            }
        }

        if (!isPinching)
        {
            currentlyAdjustingObject = null;
        }
    }

    private void rescaleChildObject(Vector3 currentScale, Transform parentTransform, Vector3 currentPosition, GameObject hitObject)
    {
        // Store inverse scale with a reduction factor to keep children slightly smaller
        Vector3 adjustedScale = new Vector3((1f / currentScale.x) * childScaleFactor, (1f / currentScale.y) * childScaleFactor, 1f);

        // Apply scaling to parent
        parentTransform.localScale = Vector3.Lerp(parentTransform.localScale, currentScale, 0.2f);
        parentTransform.localPosition = Vector3.Lerp(parentTransform.localPosition, currentPosition, 0.2f);

        // Apply reduced inverse scaling to children
        foreach (Transform child in parentTransform)
        {
            // Skip MaskObject and HoverMask, and the currently pinched object
            if (child.gameObject.name == "MaskObject" || child.gameObject.name == "HoverMask" || child.gameObject == hitObject) continue;
            hitObject.gameObject.transform.localScale = adjustedScale;
            child.localScale = adjustedScale;

        }
    }

    //---------------------- Masks --------------------

    private GameObject CurentObj = null;
    private bool selectobj = false;

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

    // **Helper function to check if an object has a valid tag**
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
                if (child.tag == "Anchor") continue;
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
