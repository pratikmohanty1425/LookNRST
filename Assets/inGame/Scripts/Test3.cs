using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test3 : MonoBehaviour
{
    private RaycastHit2D hitInfo;
    public Sprite newSprite;
    public GameObject centercamera;
    private Dictionary<GameObject, Sprite> originalSprites = new Dictionary<GameObject, Sprite>();
    private GameObject lastHitObject = null;
    private LineRenderer lineRenderer;

    private Vector3 smoothedStartPosition;
    private Vector3 smoothedDirection;
    private float smoothFactor = 0.1f; // Adjust this for more/less smoothing

    void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;

        // Initialize smoothing variables
        smoothedStartPosition = centercamera.transform.position;
        smoothedDirection = centercamera.transform.forward;
    }

    void FixedUpdate() // Use FixedUpdate for smoother updates
    {
        Vector3 targetStartPosition = centercamera.transform.position;
        Vector3 targetDirection = centercamera.transform.forward;

        // Apply smoothing using Lerp
        smoothedStartPosition = Vector3.Lerp(smoothedStartPosition, targetStartPosition, smoothFactor);
        smoothedDirection = Vector3.Lerp(smoothedDirection, targetDirection, smoothFactor).normalized;

        float rayDistance = 100.0f;

        // Update LineRenderer positions
        lineRenderer.SetPosition(0, smoothedStartPosition);
        lineRenderer.SetPosition(1, smoothedStartPosition + smoothedDirection * rayDistance);

        // Perform a 2D Raycast
        hitInfo = Physics2D.Raycast(smoothedStartPosition, smoothedDirection, rayDistance);

        if (hitInfo.collider != null && hitInfo.collider.CompareTag("IntObject"))
        {
            GameObject hitObject = hitInfo.collider.gameObject;
            SpriteRenderer spriteRenderer = hitObject.GetComponent<SpriteRenderer>();

            print(hitInfo.collider.tag);
            if (spriteRenderer != null && newSprite != null)
            {
                if (!originalSprites.ContainsKey(hitObject))
                {
                    originalSprites[hitObject] = spriteRenderer.sprite;
                }

                spriteRenderer.sprite = newSprite;
                lastHitObject = hitObject;
            }
        }
        else
        {
            if (lastHitObject != null && originalSprites.ContainsKey(lastHitObject))
            {
                SpriteRenderer spriteRenderer = lastHitObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = originalSprites[lastHitObject];
                }
                lastHitObject = null;
            }
        }
    }
}
