using UnityEngine;

public class ObjectToCanvas : MonoBehaviour
{
    [SerializeField] private string canvasTag = "Canvas";
    public float rayDistance = 10f;
    public float smoothSpeed = 0.1f;
    public float returnSpeed = 0.05f; // Separate speed for returning

    private Vector3 lastValidCanvasPosition;
    private bool isOnCanvas = false;

    void Start()
    {
        lastValidCanvasPosition = transform.position;
    }

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // Perform the raycast with layer filtering first
        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            // Then verify the tag explicitly
            if (hit.collider.CompareTag(canvasTag))
            {
                // Only update position if it's actually a Canvas
                lastValidCanvasPosition = hit.point;
                transform.position = Vector3.Lerp(transform.position, lastValidCanvasPosition, smoothSpeed);
                isOnCanvas = true;
                return; // Exit early since we found our canvas
            }
        }

        // If we get here, we're not hitting a Canvas
        if (isOnCanvas)
        {
            // Smoothly return to last canvas position
            transform.position = Vector3.Lerp(transform.position, lastValidCanvasPosition, returnSpeed);

            // Check if we've basically returned
            if (Vector3.Distance(transform.position, lastValidCanvasPosition) < 0.01f)
            {
                isOnCanvas = false;
            }
        }
    }
}