using UnityEngine;
using System.IO;

public class ScreenshotTaker : MonoBehaviour
{
    [Header("Screenshot Settings")]
    public string screenshotPath = @"C:\Users\Lenovo\Desktop\All\2_AUClasses\Thesis\Photos";
    public bool use1080p = true; // Force 1080p resolution
    public int screenshotScale = 1; // Only used if use1080p is false

    void Update()
    {
        // Check if spacebar is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeScreenshot();
        }
    }

    void TakeScreenshot()
    {
        // Create the directory if it doesn't exist
        if (!Directory.Exists(screenshotPath))
        {
            Directory.CreateDirectory(screenshotPath);
        }

        // Generate filename with timestamp
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"Screenshot_{timestamp}.png";
        string fullPath = Path.Combine(screenshotPath, filename);

        if (use1080p)
        {
            // Take 1080p screenshot using RenderTexture
            Take1080pScreenshot(fullPath);
        }
        else
        {
            // Use standard screenshot method
            ScreenCapture.CaptureScreenshot(fullPath, screenshotScale);
        }

        // Optional: Log the screenshot location
        Debug.Log($"Screenshot saved to: {fullPath}");

        // Optional: Show a brief message
        StartCoroutine(ShowScreenshotMessage());
    }

    void Take1080pScreenshot(string path)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = FindObjectOfType<Camera>();
        }

        if (cam == null)
        {
            Debug.LogError("No camera found for screenshot!");
            return;
        }

        // Create a RenderTexture with 1080p resolution
        RenderTexture renderTexture = new RenderTexture(1920, 1080, 24);
        RenderTexture currentRT = RenderTexture.active;

        // Set the camera to render to our RenderTexture
        cam.targetTexture = renderTexture;
        cam.Render();

        // Read the pixels from the RenderTexture
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenshot.Apply();

        // Clean up
        cam.targetTexture = null;
        RenderTexture.active = currentRT;
        DestroyImmediate(renderTexture);

        // Save the screenshot
        byte[] bytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        DestroyImmediate(screenshot);
    }

    // Optional: Brief visual feedback
    System.Collections.IEnumerator ShowScreenshotMessage()
    {
        // You can add UI feedback here if needed
        Debug.Log("Screenshot captured!");
        yield return new WaitForSeconds(0.1f);
    }
}