using Oculus.Interaction.HandGrab;
using System.Collections.Generic;
using UnityEngine;

public class TranslationHeadRay : MonoBehaviour
{
    public OVRHand hand;
    public Color pinchColor = Color.green;
    public float moveSpeed = 5f;

    private RaycastHit hitInfo;
    private Vector3 smoothedStartPosition;
    private Vector3 smoothedDirection;
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();
    private GameObject selectedObject = null;
    private LineRenderer lineRenderer;
    private float smoothFactor = 0.1f;
    private bool isPinching = false;
    private float rayDistance = 300.0f;
    private bool wasHoldingLastFrame = false;

    void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;

        smoothedStartPosition = Camera.main.transform.position;
        smoothedDirection = Camera.main.transform.forward;
    }

    void Update()
    {
        UpdateRay();
        HandleObjectSelection();
        MoveSelectedObject();
    }

    private void UpdateRay()
    {
        Vector3 targetStartPosition = Camera.main.transform.position;
        Vector3 targetDirection = Camera.main.transform.forward;

        smoothedStartPosition = Vector3.Lerp(smoothedStartPosition, targetStartPosition, smoothFactor);
        smoothedDirection = Vector3.Lerp(smoothedDirection, targetDirection, smoothFactor).normalized;

        lineRenderer.SetPosition(0, smoothedStartPosition);
        lineRenderer.SetPosition(1, smoothedStartPosition + smoothedDirection * rayDistance);
    }

    private void HandleObjectSelection()
    {
        bool isHoldingNow = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        // Handle pinch release first
        //if (!isHoldingNow && wasHoldingLastFrame)
        //{
        //    if (selectedObject != null)
        //    {
        //        selectedObject.GetComponent<Renderer>().material.color = originalColors[selectedObject];
         //       selectedObject = null;
        //    }
        //    isPinching = false;
        //    print("pinch out");
        //}

        // Then check for ray hits and new pinches
        if (Physics.Raycast(smoothedStartPosition, smoothedDirection, out hitInfo, rayDistance))
        {
            
            if (hitInfo.collider.CompareTag("Target"))
            {
                GameObject hitObject = hitInfo.collider.gameObject;
                Renderer objectRenderer = hitObject.GetComponent<Renderer>();

                if (objectRenderer != null)
                {
                    // Store original color if not already stored
                    if (!originalColors.ContainsKey(hitObject))
                    {
                        originalColors[hitObject] = objectRenderer.material.color;
                    }
                    
                }
                // Handle both initial pinch and re-pinch
                if (isHoldingNow && !isPinching)
                {
                    print("pinch " + (selectedObject == null ? "start" : "again"));
                    selectedObject = hitObject;
                    objectRenderer.material.color = pinchColor;
                    isPinching = true;
                }
                if (!isHoldingNow && isPinching)
                {
                    print("pinch out");
                    selectedObject = hitObject;
                    objectRenderer.material.color = Color.white; 
                    isPinching = false;
                }
            }
        }

        wasHoldingLastFrame = isHoldingNow;
    }

    private void MoveSelectedObject()
    {
        if (isPinching && selectedObject != null)
        {
            // Calculate point along ray at current distance
            float currentDistance = Vector3.Distance(smoothedStartPosition, selectedObject.transform.position);
            Vector3 targetPosition = smoothedStartPosition + smoothedDirection * currentDistance;

            // Maintain original Z position
            targetPosition.z = selectedObject.transform.position.z;

            // Move object to target position
            selectedObject.transform.position = Vector3.Lerp(
                selectedObject.transform.position,
                targetPosition,
                Time.deltaTime * moveSpeed
            );
        }
    }
}
