using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using Mapbox.Geocoding;
using System;
using System.Linq;

[Serializable]
public class FeatureCollection
{
    public string type;
    public List<Feature> features;
}

[Serializable]
public class Feature
{
    public string type;
    public Properties properties;
    public Geometry geometry;
}

[Serializable]
public class Properties
{
    public float brightness;
    public float scan;
    public float track;
    public string acq_date;
    public string acq_time;
    public string satellite;
    public string instrument;
    public string confidence;
    public string version;
    public float bright_t31;
    public float frp;
    public string daynight;
}

[Serializable]
public class Geometry
{
    public string type;
    public double[] coordinates; // Use array instead of List<double>
}
public class FIRMSDataFetcher : MonoBehaviour
{
    public AbstractMap map; // Reference to your Mapbox map
    public GameObject fireMarkerPrefab; // Prefab for fire markers
    public float dataRefreshInterval = 600f; // Refresh interval in seconds

    private string[] dataUrls = new string[]
    {
        "https://firms.modaps.eosdis.nasa.gov/mapserver/viirs_SNPP/",
        "https://firms.modaps.eosdis.nasa.gov/mapserver/viirs_NOAA20/",
        "https://firms.modaps.eosdis.nasa.gov/mapserver/viirs_NOAA21/",
    };

    private List<GameObject> fireMarkers = new List<GameObject>();

    void Start()
    {
        StartCoroutine(FetchFireDataLoop());
    }

    IEnumerator FetchFireDataLoop()
    {
        while (true)
        {
            yield return FetchAllFireData();
            yield return new WaitForSeconds(dataRefreshInterval);
        }
    }

    IEnumerator FetchAllFireData()
    {
        // Clear existing markers
        ClearExistingMarkers();

        foreach (string baseUrl in dataUrls)
        {
            yield return StartCoroutine(FetchFireData(baseUrl));
        }
    }

    IEnumerator FetchFireData(string baseUrl)
    {
        // Define the parameters
        string request = "GetFeature";
        string service = "WFS";
        string version = "1.1.0";
        string typeName = GetTypeNameFromBaseUrl(baseUrl);
        string outputFormat = "application/json";
        string bbox = GetBoundingBox();

        // Construct the full URL
        string url = $"{baseUrl}?request={request}&service={service}&version={version}&typeName={typeName}&outputFormat={outputFormat}&bbox={bbox}";

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error fetching FIRMS data: " + www.error);
        }
        else
        {
            string jsonData = www.downloadHandler.text;
            ParseFireData(jsonData);
        }
    }

    string GetTypeNameFromBaseUrl(string baseUrl)
    {
        if (baseUrl.Contains("viirs_SNPP"))
            return "fires_viirs_snpp";
        else if (baseUrl.Contains("viirs_NOAA20"))
            return "fires_viirs_noaa20";
        else if (baseUrl.Contains("viirs_NOAA21"))
            return "fires_viirs_noaa21";
        else if (baseUrl.Contains("modis"))
            return "fires_modis";
        else if (baseUrl.Contains("landsat"))
            return "fires_landsat";
        else
            return "";
    }

    string GetBoundingBox()
    {
        // Define your bounding box based on the map's current view or a predefined area
        // Example: minX, minY, maxX, maxY
        double minX = -125.0;
        double minY = 32.0;
        double maxX = -113.0;
        double maxY = 42.0;

        return $"{minX},{minY},{maxX},{maxY}";
    }

    void ParseFireData(string jsonData)
    {
        // Use a wrapper class to handle the root object
        FeatureCollection featureCollection = JsonUtility.FromJson<FeatureCollection>(jsonData);

        if (featureCollection != null && featureCollection.features != null)
        {
            foreach (Feature feature in featureCollection.features)
            {
                Geometry geometry = feature.geometry;
                Properties properties = feature.properties;

                if (geometry != null && geometry.coordinates != null && geometry.coordinates.Count() >= 2)
                {
                    double longitude = geometry.coordinates[0];
                    double latitude = geometry.coordinates[1];

                    // Convert to Unity world position
                    Vector2d latLong = new Vector2d(latitude, longitude);
                    Vector3 worldPos = map.GeoToWorldPosition(latLong, true);

                    // Instantiate marker
                    GameObject marker = Instantiate(fireMarkerPrefab, worldPos, Quaternion.identity);
                    marker.transform.SetParent(this.transform);

                    // Set marker appearance based on properties
                    SetMarkerAppearance(marker, properties);

                    fireMarkers.Add(marker);
                }
            }
        }
        else
        {
            Debug.LogError("Failed to parse JSON data using JsonUtility.");
        }
    }

    void SetMarkerAppearance(GameObject marker, Properties properties)
    {
        // Example: Change color based on confidence
        string confidence = properties.confidence.ToLower();
        Renderer renderer = marker.GetComponent<Renderer>();

        if (confidence == "nominal")
        {
            renderer.material.color = Color.yellow;
        }
        else if (confidence == "high")
        {
            renderer.material.color = Color.red;
        }
        else
        {
            renderer.material.color = Color.gray;
        }
    }

    void ClearExistingMarkers()
    {
        foreach (var marker in fireMarkers)
        {
            Destroy(marker);
        }
        fireMarkers.Clear();
    }
}
