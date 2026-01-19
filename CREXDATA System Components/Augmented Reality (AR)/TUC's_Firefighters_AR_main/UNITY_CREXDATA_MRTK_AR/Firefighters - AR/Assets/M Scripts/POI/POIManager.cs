using System.Collections.Generic;
using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;

using ExtraFunctionsMapbox = MapboxExtraFunctions;

[System.Serializable]
public class POI
{
    public string Name;
    public double Latitude;
    public double Longitude;
    public POI_Type type;
}

public enum POI_Type
{
    WaterSource,
    SettlementArea,
    PowerLine,
    RiskZone
}

public class POIManager : MonoBehaviour
{
    [Header("Mapbox and AR References")]
    public AbstractMap map;            // Reference to the Mapbox map
    public GPSLocationProvider locationProvider;    // User's latitude (could come from a GPS provider)
    public RoutePlanner routePlanner;

    [Header("POIs List")]
    public List<POI> pointsOfInterest = new List<POI>(); // Define your POIs here

    // Dictionary to POI type to Image
    private Dictionary<POI_Type, GameObject[]> PoiTypeDict;
    
    [Header("POIs Prefabs List [0]: Scene, [1]: Map")]
    public GameObject[] waterSourcePOI = new GameObject[2];
    public GameObject[] settlementAreaPOI = new GameObject[2];
    public GameObject[] powerLinePOI = new GameObject[2];
    public GameObject[] riskZonePOI = new GameObject[2];


    // Keep track of spawned markers for later removal
    private List<GameObject> spawnedMapMarkers = new List<GameObject>();
    private List<GameObject> spawnedSceneMarkers = new List<GameObject>();

    public GameObject POI_SceneParent;
    public GameObject POI_MapParent;

    private bool activated = false;

    void Awake()
    {
        // Initialize the dictionary
        PoiTypeDict = new Dictionary<POI_Type, GameObject[]>
        {
            { POI_Type.WaterSource, waterSourcePOI },
            { POI_Type.SettlementArea, settlementAreaPOI },
            { POI_Type.PowerLine, powerLinePOI },
            { POI_Type.RiskZone, riskZonePOI }
        };
    }

    public void AddPOI(string name, double latitude, double longitude, string type)
    {
        POI poi = new POI { Name = name, Latitude = latitude, Longitude = longitude, type = ChoosePOIType(type) };
        pointsOfInterest.Add(poi);
    }

    private POI_Type ChoosePOIType(string type)
    {
        switch (type)
        {
            case "WaterSource":
                return POI_Type.WaterSource;
            case "SettlementArea":
                return POI_Type.SettlementArea;
            case "PowerLine":
                return POI_Type.PowerLine;
            case "RiskZone":
                return POI_Type.RiskZone;
            default:
                Debug.LogError("Invalid POI type: " + type);
                return POI_Type.RiskZone; // Default case
        }
    }

    // Spawns all POIs in both the map and the AR scene.
    private void SpawnAllPOIs()
    {
        // Before spawning, optionally clear existing markers
        ClearPOIs();

        foreach (var poi in pointsOfInterest)
        {
            // Spawn on the map
            SpawnPOIOnMap(poi);

            // Spawn in the AR scene
            SpawnPOIInScene(poi);
        }
    }

    // Spawns a single POI marker on the Mapbox map using geo-coordinates.
    private void SpawnPOIOnMap(POI poi)
    {
        if (map == null)
        {
            Debug.LogWarning("Map or mapMarkerPrefab not assigned, cannot spawn POI on map.");
            return;
        }

        // Convert the POI's latitude/longitude to a Unity world position on the map
        Vector2d geoPosition = new Vector2d(poi.Latitude, poi.Longitude);

        // Adjust the base scale factor based on the zoom level
        float scaleFactor = 0.025f; 

        // Instantiate the marker on the mapParent(POI_Parent.transform); // Keep markers organized under the map in the hierarchy
        GameObject mapMarker = ExtraFunctionsMapbox.DisplayOnMap(map, GetPoiTypePrefab(poi.type, 1), geoPosition, scaleFactor, POI_MapParent.transform, "Map_" + poi.Name);
        POIMapObject poiMap = mapMarker.GetComponent<POIMapObject>();
        poiMap.GPSLocation = geoPosition;
        poiMap.routePlanner = routePlanner;

        // Track this marker for later removal
        spawnedMapMarkers.Add(mapMarker);
    }

    // Spawns a single POI marker in the AR scene relative to the user's position.
    private void SpawnPOIInScene(POI poi)
    {
        Vector3 userPosition = Camera.main.transform.position;

        // Convert user's current location to Vector2d
        Vector2d userPositionGPS = new Vector2d(locationProvider.latitude, locationProvider.longitude);

        // Calculate distance & bearing from user to this POI using your existing snippet approach
        Vector2d poiGPSLocation = new Vector2d(poi.Latitude, poi.Longitude);

        // Calculate relative position of the POI in scene
        Vector3 poiLocationScene = ExtraFunctionsMapbox.CalculatePositionInScene(userPositionGPS, poiGPSLocation);

        // Instantiate the marker in the AR scene
        GameObject sceneMarker = Instantiate(GetPoiTypePrefab(poi.type, 0));
        sceneMarker.transform.SetParent(POI_SceneParent.transform);
        sceneMarker.transform.localPosition = poiLocationScene;
        sceneMarker.name = $"Scene_{poi.Name}";
        
        POIMapObject poiMap = sceneMarker.GetComponent<POIMapObject>();
        poiMap.GPSLocation = poiGPSLocation;
        poiMap.routePlanner = routePlanner;

        // Track the marker for future clearing
        spawnedSceneMarkers.Add(sceneMarker);
    }
  
    // Clears all spawned POI markers from the map and the AR scene.
    private void ClearPOIs()
    {
        // Destroy all map markers
        foreach (var marker in spawnedMapMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        spawnedMapMarkers.Clear();

        // Destroy all scene markers
        foreach (var marker in spawnedSceneMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        spawnedSceneMarkers.Clear();
    }

    // poiCase [0]: Scene, [1]: Map
    public GameObject GetPoiTypePrefab(POI_Type type, int poiCase)
    {
        if (PoiTypeDict.TryGetValue(type, out GameObject[] data))
        {
            return data[poiCase];
        }
        else
        {
            Debug.LogError("POI type not found!");
            return null;
        }
    }

    public void TogglePOIs(){

        activated = !activated;

        if(activated){
            SpawnAllPOIs();
        }
        else{
            ClearPOIs();
        }
    }
    
    public bool getState(){
        return activated;
    }
}
