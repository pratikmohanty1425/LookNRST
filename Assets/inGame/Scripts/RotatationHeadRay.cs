using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotatationHeadRay : MonoBehaviour
{
    public OVRHand hand;
    public Color pinchColor = Color.green;
    public float moveSpeed = 5f;
    public float rotationSpeed = 100f; // Add this field for rotation speed control
    private RaycastHit hitInfo;
    private Vector3 smoothedStartPosition;
    private Vector3 smoothedDirection;
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();
    private GameObject selectedObject = null;
    private float smoothFactor = 0.1f;
    private bool isPinching = false;
    private float rayDistance = 300.0f;
    private GameObject lastHitObject = null;
    private GameObject parentObject = null;
    private Renderer lastHitRenderer = null;
    private Vector3 lastHitPoint; // Store the last hit point for reference
    private Quaternion targetRotation; // Store the target rotation for smooth interpolation

    void Update()
    {
        UpdateRay();
        HandleObjectSelection();
        RotateSelectedObject();
    }

    private void UpdateRay()
    {
        Vector3 targetStartPosition = Camera.main.transform.position;
        Vector3 targetDirection = Camera.main.transform.forward;
        smoothedStartPosition = Vector3.Lerp(smoothedStartPosition, targetStartPosition, smoothFactor);
        smoothedDirection = Vector3.Lerp(smoothedDirection, targetDirection, smoothFactor).normalized;
    }

    private void HandleObjectSelection()
    {
        bool isHoldingNow = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        if (Physics.Raycast(smoothedStartPosition, smoothedDirection, out hitInfo, rayDistance))
        {
            if (hitInfo.collider.CompareTag("Rot"))
            {
                GameObject hitObject = hitInfo.collider.gameObject;
                parentObject = hitObject.transform.parent?.gameObject;
                Renderer objectRenderer = hitObject.GetComponent<Renderer>();
                lastHitObject = hitObject;
                lastHitRenderer = objectRenderer;
                if (objectRenderer != null)
                {
                    if (!originalColors.ContainsKey(hitObject))
                    {
                        originalColors[hitObject] = objectRenderer.material.color;
                    }
                }
                if (isHoldingNow && !isPinching)
                {
                    print("pinch " + (selectedObject == null ? "start" : "again"));
                    selectedObject = parentObject;  // Select the parent object
                    lastHitPoint = hitInfo.point; // Store initial hit point
                    if (objectRenderer != null)
                    {
                        objectRenderer.material.color = Color.red;
                    }
                    isPinching = true;
                }
            }
        }

        if (!isHoldingNow && isPinching)
        {
            print("pinch out");
            if (lastHitRenderer != null)
            {
                lastHitRenderer.material.color = Color.white;
            }
            if (parentObject != null)
            {
                parentObject = null;
                selectedObject = null;
            }
            isPinching = false;
        }
    }

    private void RotateSelectedObject()
    {
        if (isPinching && selectedObject != null)
        {
            // Get the direction vector from the head ray
            Vector3 targetDirection = smoothedDirection;

            // Calculate the angle between the forward vector and the target direction
            float angle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;

            // Create a rotation that only affects the Z axis
            Quaternion targetRotation = Quaternion.Euler(
                selectedObject.transform.rotation.eulerAngles.x,
                selectedObject.transform.rotation.eulerAngles.y,
                angle
            );

            // Smoothly rotate to the target rotation
            selectedObject.transform.rotation = Quaternion.Slerp(
                selectedObject.transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}