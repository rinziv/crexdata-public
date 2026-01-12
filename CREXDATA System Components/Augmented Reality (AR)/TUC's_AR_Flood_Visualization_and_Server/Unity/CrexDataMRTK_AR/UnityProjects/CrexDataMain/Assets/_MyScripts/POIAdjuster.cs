using UnityEngine;

[System.Serializable]
public struct SizeDistance
{
    public float distance;
    public float size;
}

public class POIAdjuster : MonoBehaviour
{
    public SizeDistance[] sizeDistances; // Array to define sizes at different distances
    public float minScale = 1f; // Minimum scale for the POI

    /*     private void Start()
        {
            if (POIAdjusterManager.Instance != null)
            {
                POIAdjusterManager.Instance.RegisterPOI(this);
            }
        } */

    private void OnEnable()
    {
        if (POIAdjusterManager.Instance != null)
        {
            POIAdjusterManager.Instance.RegisterPOI(this);
        }
    }

    private void OnDisable()
    {
        if (POIAdjusterManager.Instance != null)
        {
            POIAdjusterManager.Instance.UnregisterPOI(this);
        }
    }

    public void AdjustSize(Vector3 cameraPosition)
    {
        if (sizeDistances == null || sizeDistances.Length == 0)
            return;

        float distance = Vector3.Distance(transform.position, cameraPosition);

        // Find the appropriate size for the given distance
        float targetSize = GetSizeForDistance(distance);

        // Calculate the scale to make the POI appear the same size at different distances
        float scale = targetSize * distance;

        // Ensure the scale does not go below the minimum scale
        scale = Mathf.Max(scale, minScale);

        transform.localScale = Vector3.one * scale;
    }

    private float GetSizeForDistance(float distance)
    {
        for (int i = 0; i < sizeDistances.Length - 1; i++)
        {
            if (distance < sizeDistances[i + 1].distance)
            {
                float t = (distance - sizeDistances[i].distance) / (sizeDistances[i + 1].distance - sizeDistances[i].distance);
                return Mathf.Lerp(sizeDistances[i].size, sizeDistances[i + 1].size, t);
            }
        }
        return sizeDistances[sizeDistances.Length - 1].size;
    }
}
