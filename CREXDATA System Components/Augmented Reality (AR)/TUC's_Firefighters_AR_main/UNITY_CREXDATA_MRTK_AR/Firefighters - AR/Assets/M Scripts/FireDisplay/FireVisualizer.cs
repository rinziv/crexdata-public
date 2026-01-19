using UnityEngine;
using System.Collections.Generic;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using Newtonsoft.Json;
using ExtraFunctionsMapbox = MapboxExtraFunctions;

public class FireVisualizer : MonoBehaviour
{
    // Reference to the Mapbox AbstractMap
    public AbstractMap map;

    // Reference to your fire marker prefab
    public GameObject fireMarkerPrefab;
    public GameObject mapParent;

    private List<FireData> fireDataList;
    private List<GameObject> fireMarkersList = new List<GameObject>();
    private bool firesDisplayed = false;

    public float ScaleAdjustment = 0.25f;
    public float currentMapZoomLevel = 1;


    // Fire data JSON string
    public TextAsset fireDataJson;

    // Fire POI
    public GameObject firePOI_Prefab;
    public Transform ScenePOIParent;
    public RoutePlanner routePlanner;
    private List<GameObject> spawnedPrefabs = new List<GameObject>();
    public GPSLocationProvider locationProvider;

    void Start()
    {
        fireDataList = LoadFireData(fireDataJson.text);
    }

    // Load the fire data from a JSON string
    public List<FireData> LoadFireData(string json)
    {
        return JsonConvert.DeserializeObject<List<FireData>>(json);
    }

    public void AddFireData(float latitude, float longitude, string acq_date, string acq_time, float brightness, float frp)
    {
        FireData fireData = new FireData
        {
            latitude = latitude,
            longitude = longitude,
            acq_date = acq_date,
            acq_time = acq_time,
            brightness = brightness,
            frp = frp
        };

        // Add the new fire data to the list
        fireDataList.Add(fireData);
    }

    // Display the fire locations on the map
    public void DisplayFireLocations(List<FireData> fireDataList)
    {
        float currentZoom = map.Zoom;
        float baseScaleFactor = 0.1f * ScaleAdjustment * currentMapZoomLevel; // Adjust the scale factor based on the current zoom level

        // Display each fire location on the map
        foreach (var fire in fireDataList)
        {
            Vector2d fireLocation = new Vector2d(fire.latitude, fire.longitude);
            GameObject marker = ExtraFunctionsMapbox.DisplayOnMap(map, fireMarkerPrefab, fireLocation, baseScaleFactor, mapParent.transform, "fire_marker");

            fireMarkersList.Add(marker);
            SpawnPOIInScene(fireLocation);
        }
    }

    // Spawns a single POI marker in the AR scene relative to the user's position.
    private void SpawnPOIInScene(Vector2d fireLocation)
    {

        Vector3 userPosition = Camera.main.transform.position;

        // Convert user's current location to Vector2d
        Vector2d userPositionGPS = new Vector2d(locationProvider.latitude, locationProvider.longitude);

        double distanceMeters = ExtraFunctionsMapbox.CalculateDistanceInScene(userPositionGPS, fireLocation);
        float bearing = ExtraFunctionsMapbox.CalculateBearing(userPositionGPS, fireLocation);

        // Convert bearing and distance to local (x, z)
        float rotatedX = (float)(distanceMeters * Mathf.Cos(bearing * Mathf.Deg2Rad));
        float rotatedZ = (float)(distanceMeters * Mathf.Sin(bearing * Mathf.Deg2Rad));

        // Create the POI position relative to user (0,0,0)
        Vector3 poiPosition = new Vector3(rotatedX, 5.2f, rotatedZ);

        // Instantiate the marker in the AR scene
        GameObject sceneMarker = Instantiate(firePOI_Prefab);
        sceneMarker.transform.SetParent(ScenePOIParent.transform);
        sceneMarker.transform.localPosition = poiPosition;
        sceneMarker.name = $"Scene_FirePOI";
        
        POIMapObject poiMap = sceneMarker.GetComponent<POIMapObject>();
        poiMap.GPSLocation = fireLocation;
        poiMap.routePlanner = routePlanner;

        // Track the marker for future clearing
        spawnedPrefabs.Add(sceneMarker);
    }

    // Clears all spawned POI markers from the map and the AR scene.
    private void ClearPOIs()
    {
        // Destroy all scene markers
        foreach (var marker in spawnedPrefabs)
        {
            if (marker != null)
                Destroy(marker);
        }
        spawnedPrefabs.Clear();
    }

    public void ClearFireLocations()
    {
        ExtraFunctionsMapbox.ClearMarkers(fireMarkersList);
    }

    public void TogleFireMapDisplay(){

        if(!firesDisplayed)
        {
            DisplayFireLocations(fireDataList);
            firesDisplayed = true;
        }
        else
        {
            ClearFireLocations();
            ClearPOIs();
            firesDisplayed = false;
        }
    }

    public void AdjustScale(float multiplier){
        
        ScaleAdjustment *= multiplier; 
    }

    public bool getState(){
        return firesDisplayed;
    }
}
