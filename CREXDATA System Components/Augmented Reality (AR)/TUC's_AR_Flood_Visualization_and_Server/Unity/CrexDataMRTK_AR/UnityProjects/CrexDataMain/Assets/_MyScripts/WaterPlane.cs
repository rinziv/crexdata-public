using UnityEngine;

public class WaterPlane : MonoBehaviour
{
    public WaterLevelController waterLevelController;
    public GameObject waterPrefab; // Assign the prefab in the inspector
    public GameObject directionIndicatorPrefab; // Assign the direction indicator prefab in the inspector
    private GameObject waterPlane;
    private GameObject directionIndicator;
    public float worstCaseWaterPlaneHeight = 0.1f;
    public float[] waterHeights = { 0.1f, 0.5f, 1.0f }; // Example values in meters
    public HeightCalculator heightCalculator; // Reference to the HeightCalculator
    public float transitionDuration = 1.0f; // Duration for the smooth transition
    private float addedHeight = 0.0f;

    void Start()
    {
        waterLevelController = GameObject.Find("WaterLevelController").GetComponent<WaterLevelController>();
        if (waterPrefab != null)
        {
            // Instantiate the water prefab
            waterPlane = Instantiate(waterPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            //Spawn the worst case water plane as child of the water plane

            // Instantiate the direction indicator prefab
            directionIndicator = Instantiate(directionIndicatorPrefab, new Vector3(0, -0.1f, 0), Quaternion.identity, waterPlane.transform);

            // Set the water plane to inactive initially
            waterPlane.SetActive(false);
            directionIndicator.SetActive(false);
        }
    }

    public void BringWater()
    {
        float groundHeight = heightCalculator.CalculateGroundHeight();
        if (groundHeight != -1112f)
        {
            // Position the water plane based on the calculated ground height and slider value
            float waterHeight = waterHeights[Mathf.Clamp((int)WaterLevelController.CurrentSliderValue - 1, 0, waterHeights.Length - 1)];
            float adjustedHeight = groundHeight + waterHeight;
            StartCoroutine(SmoothMove(new Vector3(heightCalculator.mainCamera.transform.position.x, adjustedHeight, heightCalculator.mainCamera.transform.position.z)));
        }
    }

    public void UpdateWaterLevel(float forecastHour)
    {
        if (waterPlane != null)
        {
            float groundHeight = heightCalculator.GetGroundHeight();
            if (groundHeight != -1112f)
            {
                // Ensure the forecastHour is within the valid range
                int index = Mathf.Clamp((int)forecastHour - 1, 0, waterHeights.Length - 1);
                if (index >= 0 && index < waterHeights.Length)
                {
                    float waterHeight = waterHeights[index];

                    // Set the water height relative to the ground
                    float adjustedHeight = groundHeight + waterHeight;
                    StartCoroutine(SmoothMove(new Vector3(waterPlane.transform.position.x, adjustedHeight + addedHeight, waterPlane.transform.position.z)));
                }
                else
                {
                    Debug.LogError("Forecast hour index is out of range: " + index);
                }
            }
        }
    }

    public void SetWaterVisibility(bool isVisible)
    {
        if (waterPlane != null)
        {
            waterPlane.SetActive(isVisible);
        }
    }

    public void ToggleWorstCaseWaterVisibility()
    {
        if (waterPlane != null)
        {
            addedHeight = addedHeight == 0.0f ? worstCaseWaterPlaneHeight : 0.0f;
            UpdateWaterLevel(WaterLevelController.CurrentSliderValue);
            waterLevelController.UpdateWaterLevelInfo();
        }
    }

    public void ToggleDirectionIndicatorVisibility()
    {
        if (directionIndicator != null)
        {
            directionIndicator.SetActive(!directionIndicator.activeSelf);
        }
    }

    public bool IsWaterPlaneNull()
    {
        return waterPlane == null;
    }

    private System.Collections.IEnumerator SmoothMove(Vector3 targetPosition)
    {
        float elapsedTime = 0f;
        Vector3 startingPosition = waterPlane.transform.position;

        while (elapsedTime < transitionDuration)
        {
            waterPlane.transform.position = Vector3.Lerp(startingPosition, targetPosition, (elapsedTime / transitionDuration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        waterPlane.transform.position = targetPosition;
    }

    public void SetWaterHeights(float[] heights)
    {
        waterHeights = heights;
        Debug.Log("Water heights updated: " + string.Join(", ", heights));
    }

    public string UpdateWaterInfoPanel(float CurrentSliderValue)
    {
        string waterInfo = "";
        if (waterPlane != null)
        {
            // Ensure the forecastHour is within the valid range
            int index = Mathf.Clamp((int)CurrentSliderValue - 1, 0, waterHeights.Length - 1);
            float waterHeightCM = (waterHeights[index] + addedHeight) * 100;
            //Debug.Log("Current Water Height: " + waterHeightCM + "cm");
            waterInfo = "Current Water Height\n\n" + waterHeightCM + "cm";
        }
        return waterInfo;
    }
}
