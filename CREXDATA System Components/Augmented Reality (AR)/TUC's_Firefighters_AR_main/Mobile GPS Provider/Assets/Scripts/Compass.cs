using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Compass : MonoBehaviour
{
    private bool isCompassReady = false;
    // Store the last heading that was used to update the UI
    private float lastHeading = -1f;

    public RawImage CompassImage;
    public Text CompassDirectionText;
    public float headingDebug;
    
    // Controls the speed of the lerp for the UV coordinate
    public float uvLerpFactor = 0.5f;

    void Start()
    {
        StartCoroutine(InitializeCompass());
        StartCoroutine(UpdateCompassUI());
    }

    IEnumerator InitializeCompass()
    {
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait < 1)
        {
            Debug.Log("Location service timed out");
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("Unable to determine device location");
            yield break;
        }

        Input.compass.enabled = true;
        isCompassReady = true;
    }

    IEnumerator UpdateCompassUI()
    {
        while (true)
        {
            if (isCompassReady && Input.compass.trueHeading >= 0)
            {
                float heading = Input.compass.trueHeading;
                UpdateCompass(heading);
            }
            yield return new WaitForSeconds(0.03f);
        }
    }

    void UpdateCompass(float heading)
    {
        // On the very first update, set lastHeading so we always update at least once.
        if (lastHeading < 0f)
        {
            lastHeading = heading;
        }
        
        // If the difference between the current and last heading is less than 1 degree, do not update.
        if (Mathf.Abs(Mathf.DeltaAngle(heading, lastHeading)) < 1f)
        {
            return;
        }
        
        // Update the stored heading
        lastHeading = heading;

        // Calculate target UV x-coordinate (0 to 1 based on heading)
        float targetX = heading / 360f;
        float currentX = CompassImage.uvRect.x;

        // Calculate the delta ensuring the shortest path around the circle.
        float delta = targetX - currentX;
        if (delta > 0.5f)
            delta -= 1f;
        else if (delta < -0.5f)
            delta += 1f;

        // Smoothly update the UV using lerp factor
        float newX = currentX + delta * uvLerpFactor;

        // Wrap the newX value within [0, 1)
        if (newX < 0f)
            newX += 1f;
        else if (newX > 1f)
            newX -= 1f;

        CompassImage.uvRect = new Rect(newX, 0, 1, 1);

        int displayAngle = Mathf.RoundToInt(heading);
        CompassDirectionText.text = displayAngle + "°";
    }
}