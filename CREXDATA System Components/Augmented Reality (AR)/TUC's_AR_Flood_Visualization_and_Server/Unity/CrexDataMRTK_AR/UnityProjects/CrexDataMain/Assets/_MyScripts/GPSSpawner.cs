using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using Mapbox.Utils;
using Mapbox.Unity.MeshGeneration.Factories;
using Newtonsoft.Json.Linq;

public enum POIType
{
    School,
    Hospital,
    Church,
    Station,
    Manhole,
    DetectedPerson,
    Drone, // Added for drone map POI
    General
}

[Serializable]
public class GPSCoordinate
{
    [Tooltip("Input format: 'Latitude,Longitude' e.g., '34.0522,-118.2437'")]
    public string coordinates;
    public POIType POIType;
    public string poiName;
    public string poiId; // Added for tracking POI by ID
    public string poiDescription;
    public float riskLevel;
    public double Latitude
    {
        get
        {
            return double.Parse(coordinates.Split(',')[0].Trim());
        }
    }
    public double Longitude
    {
        get
        {
            return double.Parse(coordinates.Split(',')[1].Trim());
        }
    }
}
public class GPSSpawner : MonoBehaviour
{
    #region Variables
    [Header("Google Distance of two points for debugging")]
    [SerializeField] float _googleDistance;

    [Header("POI Scriptable Objects")]
    public List<POI> poiScriptableObjects; // List of all POI data (TEST)

    [Header("Object Coordinates")]
    [Tooltip("Add all object coordinates here")]
    public List<GPSCoordinate> staticCoordinates = new();

    [Header("Area Coordinates")]
    public List<string> areaCoordinates;

    [Header("POI Height")]
    [Tooltip("Height of the POI objects above the ground")]
    public float height = 30.0f;

    [Header("Maximum Distance")]
    [Tooltip("Maximum distance from the camera to spawn POIs")]
    [SerializeField] private float maxPOIDistance = 20000f;

    private Dictionary<POIType, GameObject> prefabs;
    private Dictionary<POIType, GameObject> mapPrefabs;
    public Dictionary<POIType, List<GameObject>> spawnedObjects = new();

    [Header("Reference Coordinate Values")]
    [SerializeField] private bool _useFakeCoordinates = false;
    public string referenceCoordinates;
    public Vector3 referenceUnityPosition = Vector3.zero;
    public double referenceLatitude = 0.0;
    public double referenceLongitude = 0.0;
    public bool referenceCoordinatesSet = false;

    public GameObject mapGameObject;
    public Transform poisForMapParent;

    [Header("POI Prefabs")]
    public GameObject schoolPrefab;
    public GameObject hospitalPrefab;
    public GameObject churchPrefab;
    public GameObject stationPrefab;
    public GameObject manholePrefab;
    public GameObject detectedPersonPrefab;
    public GameObject generalPrefab;


    [Header("Map POI Prefabs")]
    public GameObject map_schoolPrefab;
    public GameObject map_hospitalPrefab;
    public GameObject map_churchPrefab;
    public GameObject map_stationPrefab;
    public GameObject drone_forMapPrefab;
    public GameObject map_detectedPersonPrefab;
    public GameObject map_generalPrefab;


    private WebSocketConnection webSocketConnection;

    #endregion

    #region Initialize
    private void Awake()
    {
        prefabs = new Dictionary<POIType, GameObject>
        {
            { POIType.School, schoolPrefab },
            { POIType.Hospital, hospitalPrefab },
            { POIType.Church, churchPrefab },
            { POIType.Station, stationPrefab },
            { POIType.Manhole, manholePrefab },
            { POIType.DetectedPerson, detectedPersonPrefab },
            { POIType.General, generalPrefab }
        };

        mapPrefabs = new Dictionary<POIType, GameObject>
        {
            { POIType.School, map_schoolPrefab },
            { POIType.Hospital, map_hospitalPrefab },
            { POIType.Church, map_churchPrefab },
            { POIType.Station, map_stationPrefab },
            { POIType.Drone, drone_forMapPrefab },
            { POIType.DetectedPerson, map_detectedPersonPrefab },
            { POIType.General, map_generalPrefab }
        };
    }
    private void Start()
    {
        webSocketConnection = FindObjectOfType<WebSocketConnection>();
        if (webSocketConnection != null)
        {
            webSocketConnection.OnHardcodedDataReceived += HandleHardcodedDataReceived;
            //RequestSpoofedGps();
        }
        StartCoroutine(WaitForReferenceCoordinatesAndSpawnPOIs());
    }

    private void HandleHardcodedDataReceived(JObject dataObject)
    {
        var phoneGPSObject = dataObject["spoofedGPSCoordinatesForPhone"];
        if (phoneGPSObject != null && phoneGPSObject is JObject)
        {
            string latitudeStr = phoneGPSObject["latitude"]?.ToString();
            string longitudeStr = phoneGPSObject["longitude"]?.ToString();

            if (double.TryParse(latitudeStr, out double latitude) && double.TryParse(longitudeStr, out double longitude))
            {
                HandleSpoofedGpsReceived(latitude, longitude);
            }
            else
            {
                Debug.LogWarning("Invalid latitude or longitude in hardcoded data");
            }
        }
        else
        {
            Debug.LogWarning("spoofedGPSCoordinatesForPhone is missing or invalid in hardcoded data");
        }
    }

    private void HandleSpoofedGpsReceived(double latitude, double longitude)
    {

        referenceCoordinates = $"{latitude},{longitude}";
        if (referenceCoordinatesSet == true)
            UpdateReferenceCoordinates(latitude, longitude);
        Debug.Log($"Received spoofed GPS: {latitude}, {longitude}");
    }

    private void RequestSpoofedGps()
    {
        var request = new
        {
            type = "getSpoofedGps",
            usecase = "UserFakeGPS"
        };
        webSocketConnection.SendJsonToServer(request);
    }
    public void UpdateReferenceCoordinates(double latitude, double longitude)
    {
        referenceLatitude = latitude;
        referenceLongitude = longitude;
        referenceCoordinatesSet = true;

        referenceUnityPosition = Camera.main.transform.position;

        mapGameObject.GetComponentInParent<MapAndPlayerManager>().SetCoordinates(new Vector2d(latitude, longitude));
        mapGameObject.GetComponentInParent<MapAndPlayerManager>().UpdatePlayerLocation(new Vector2d(latitude, longitude));
    }
    private IEnumerator WaitForReferenceCoordinatesAndSpawnPOIs()
    {
        while (!referenceCoordinatesSet)
        {
            yield return null;
        }
#if UNITY_EDITOR
        if (_useFakeCoordinates)
            SkipPhoneAndUseHardcodedCoordinates(); //FOR TESTING PURPOSES ONLY
#endif
        //Spawn POIs from server request
        webSocketConnection.SendJsonToServer(new
        {
            type = "getStaticPoisFlood"
        });
    }
    #endregion

    #region Spawn POIs

    public void DeletePOI(string poiId)
    {
        // Finf the POIs gameobject in spawnedObjects and check the POIinteraction component in the gameobject to compare the poiId and delete that gameobject
        foreach (var poiList in spawnedObjects.Values)
        {
            for (int i = poiList.Count - 1; i >= 0; i--)
            {
                POIInteraction poiInteraction = poiList[i].GetComponentInChildren<POIInteraction>();
                if (poiInteraction != null && poiInteraction.poiId == poiId)
                {
                    Destroy(poiList[i]);
                    poiList.RemoveAt(i);
                    mapGameObject.GetComponentInParent<MapAndPlayerManager>().DeleteMapPOI(poiId);
                    Debug.Log($"Deleted POI with ID: {poiId}");
                    return;
                }
            }
        }
    }
    private void SpawnStaticPOIs()
    {
        foreach (var coord in staticCoordinates)
        {
            SpawnPOIAtLocation(coord);
        }

        foreach (string area in areaCoordinates)
        {
            List<GPSCoordinate> myAreaCoordinates = ParseCoordinates(area);
            string name = "Area " + (areaCoordinates.IndexOf(area) + 1);
            CreateArea(myAreaCoordinates, name);
        }
    }
    public void SpawnPOIForMap(GPSCoordinate coord)
    {
        if (referenceCoordinatesSet)
        {
            try
            {
                GameObject prefab = mapPrefabs[coord.POIType];
                //spawn the object as child of mapGameObject
                GameObject createdObj = Instantiate(prefab, mapGameObject.GetComponentInParent<MapAndPlayerManager>().poisParent);

                POIInteraction poiInteraction = createdObj.GetComponentInChildren<POIInteraction>();
                if (poiInteraction != null)
                {
                    poiInteraction.poiName = coord.poiName;
                    poiInteraction.poiDescription = coord.poiDescription;
                    poiInteraction.riskLevel = coord.riskLevel;
                    poiInteraction.poiId = coord.poiId;
                }
                //Set created object's position to 0,0,0
                createdObj.transform.localPosition = new Vector3(0, 0, 0);
                //Add to mapObjects list of the mapManager
                mapGameObject.GetComponentInParent<MapAndPlayerManager>().spawnedPOIs.Add(createdObj, new Vector2d(coord.Latitude, coord.Longitude));
                //Update the spawned POIs on the map
                mapGameObject.GetComponentInParent<MapAndPlayerManager>().UpdateSpawnedPOIs();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to spawn map point object: " + ex.ToString());
            }
        }
        else
        {
            Debug.LogError("Reference coordinates not set");
        }
    }
    public void SpawnPOIForMap(Vector2 coordinates, POIType poiType, out GameObject createdObj)
    {
        createdObj = null;
        if (referenceCoordinatesSet)
        {
            try
            {
                GameObject prefab = mapPrefabs[poiType];
                //spawn the object as child of mapGameObject
                createdObj = Instantiate(prefab, mapGameObject.GetComponentInParent<MapAndPlayerManager>().poisParent);
                //Set created object's position to 0,0,0
                createdObj.transform.localPosition = new Vector3(0, 0, 0);
                //Add to mapObjects list of the mapManager
                mapGameObject.GetComponentInParent<MapAndPlayerManager>().spawnedPOIs.Add(createdObj, new Vector2d(coordinates.x, coordinates.y));
                //Update the spawned POIs on the map
                mapGameObject.GetComponentInParent<MapAndPlayerManager>().UpdateSpawnedPOIs();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to spawn map point object: " + ex.ToString());
            }
        }
        else
        {
            Debug.LogError("Reference coordinates not set");
        }
    }

    public void SpawnPOIAtLocation(GPSCoordinate coord)
    {
        if (referenceCoordinatesSet)
        {
            try
            {

                Vector3 position = CalculateObjectLocalPosition(coord.Latitude, coord.Longitude);

                float distance = GetFlatDistanceOfTwoPoints(position, Camera.main.transform.position);

                // If the object is too far away aboard the spawning
                if (distance > maxPOIDistance)
                {
                    Debug.LogWarning("Object is too far away to spawn. Distance: " + GetFlatDistanceOfTwoPoints(position, Camera.main.transform.position));
                    return;
                }
                //Debug.Log("Distance: " + distance);
                GameObject prefab = prefabs[coord.POIType];

                GameObject createdObj = Instantiate(prefab, new Vector3(position.x, coord.POIType != POIType.Manhole ? position.y + height : Camera.main.transform.position.y - 1.75f, position.z), Quaternion.identity);

                //Absolute distance from camera without taking height into account
                //float distanceDiff = Math.Abs(_googleDistance - distance);
                //Debug.Log("<b>Distance: <color=#68615b>" + distance + "</color>" + " || From google: <color=#7fd6fc>" + distanceDiff + "</color></b>");

                // Find the "POIs" GameObject and set it as the parent of the createdObj
                GameObject pois = GameObject.Find("#POIs");
                if (pois != null)
                {
                    createdObj.transform.parent = pois.transform;
                }

                POIInteraction poiInteraction = createdObj.GetComponentInChildren<POIInteraction>();
                if (poiInteraction != null)
                {
                    poiInteraction.poiName = coord.poiName;
                    poiInteraction.poiDescription = coord.poiDescription;
                    poiInteraction.riskLevel = coord.riskLevel;
                    poiInteraction.poiId = coord.poiId;
                }

                if (!spawnedObjects.ContainsKey(coord.POIType))
                {
                    spawnedObjects[coord.POIType] = new List<GameObject>();
                }
                spawnedObjects[coord.POIType].Add(createdObj);

                //If the POI is not a manhole, spawn it on the map
                if (coord.POIType != POIType.Manhole)
                    SpawnPOIForMap(coord);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to spawn object: " + ex.ToString());
            }
        }
        else
        {
            Debug.LogError("Reference coordinates not set");
        }
    }
    public void SpawnPOIFromString(string data)
    {
        try
        {
            string[] components = data.Split(',');
            double latitude = double.Parse(components[0]);
            double longitude = double.Parse(components[1]);

            string coords = latitude + "," + longitude;
            string poiTypeString = components[2].Substring(0, 1).ToUpper() + components[2].Substring(1).ToLower(); ;

            if (!Enum.IsDefined(typeof(POIType), poiTypeString))
            {
                poiTypeString = "General";
            }

            POIType poiType = (POIType)Enum.Parse(typeof(POIType), poiTypeString);

            GPSCoordinate coord = new() { coordinates = coords, POIType = poiType };

            SpawnPOIAtLocation(coord);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to spawn POI from string: " + ex.ToString());
        }
    }
    public void SpawnPOIFromJson(string jsonData)
    {
        try
        {
            JObject poiData = JObject.Parse(jsonData);

            double latitude = poiData["position"]["latitude"].Value<double>();
            double longitude = poiData["position"]["longitude"].Value<double>();
            string poiTypeStr = poiData["poiType"]?.ToString() ?? "General";
            string poiId = poiData["poiId"]?.ToString() ?? Guid.NewGuid().ToString();

            // Create coordinate with additional data
            GPSCoordinate coord = new()
            {
                coordinates = $"{latitude},{longitude}",
                POIType = ParsePOIType(poiTypeStr),
                poiName = poiData["properties"]?["name"]?.ToString() ?? poiData["properties"]?["title"]?.ToString() ?? "Unknown",
                poiId = poiData["poiId"]?.ToString() ?? "noID",
                poiDescription = poiData["properties"]?["description"]?.ToString() ?? "",
                riskLevel = poiData["properties"]?["severity"]?.ToString()?.ToLower() switch
                {
                    "high" => 1.0f,
                    "medium" => 0.66f,
                    "low" => 0.33f,
                    _ => 0.5f
                }
            };

            // Track the last spawned POI type
            POIType lastSpawnedType = coord.POIType;
            int spawnedCount = spawnedObjects.ContainsKey(lastSpawnedType) ? spawnedObjects[lastSpawnedType].Count : 0;

            SpawnPOIAtLocation(coord);

            // Get the newly spawned POI (it should be the last one added to the list)
            if (spawnedObjects.ContainsKey(lastSpawnedType) && spawnedObjects[lastSpawnedType].Count > spawnedCount)
            {
                GameObject spawnedPOI = spawnedObjects[lastSpawnedType][^1]; // Last Element

                POIInteraction poiInteraction = spawnedPOI.GetComponentInChildren<POIInteraction>();
                if (poiInteraction != null)
                {
                    poiInteraction.UpdateDataFromJson(poiData);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to spawn POI from JSON: {ex}");
        }
    }


    public static event Action<string> OnAlertSpawned; // <poiId>
    public void SpawnPOIAtSpecificTransform(Transform transform, POIType poiType, Vector2 gpsCoordinates, string poiId)
    {
        if (referenceCoordinatesSet)
        {
            try
            {
                Vector3 position = transform.position;

                GameObject prefab = prefabs[poiType];
                GameObject createdObj = Instantiate(prefab, new Vector3(position.x, poiType == POIType.Manhole || poiType == POIType.DetectedPerson ? Camera.main.transform.position.y - 1.7f : position.y + 10f, position.z), Quaternion.identity);

                // Find the "POIs" GameObject and set it as the parent of the createdObj
                GameObject pois = GameObject.Find("#POIs");
                if (pois != null)
                {
                    createdObj.transform.parent = pois.transform;
                }

                POIInteraction poiInteraction = createdObj.GetComponentInChildren<POIInteraction>();
                if (poiInteraction != null)
                {
                    poiInteraction.poiName = "Drone Dropped POI";
                    poiInteraction.poiDescription = "This POI was dropped by the drone.";
                    poiInteraction.riskLevel = 0.5f; // Default risk level
                    poiInteraction.poiId = poiId;
                }

                if (!spawnedObjects.ContainsKey(poiType))
                {
                    spawnedObjects[poiType] = new List<GameObject>();
                }
                spawnedObjects[poiType].Add(createdObj);


                // Spawn the POI on the map
                if (poiType != POIType.Manhole)
                {
                    GPSCoordinate coord = new() { coordinates = $"{gpsCoordinates.x},{gpsCoordinates.y}", POIType = poiType, poiId = poiId };
                    OnAlertSpawned?.Invoke(poiId);
                    SpawnPOIForMap(coord);
                }

            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to spawn object at specific transform: " + ex.ToString());
            }
        }
        else
        {
            Debug.LogError("Reference coordinates not set");
        }
    }
    public POIType ParsePOIType(string typeString)
    {
        // Try to parse the type string to our enum
        if (Enum.TryParse<POIType>(typeString, true, out POIType result))
        {
            return result;
        }

        // Handle special cases or aliases
        switch (typeString.ToLower())
        {
            case "emergency":
            case "hospital":
            case "clinic":
                return POIType.Hospital;

            case "education":
            case "university":
            case "college":
                return POIType.School;

            case "transport":
            case "subway":
            case "bus":
            case "transportation":
                return POIType.Station;

            case "religious":
            case "worship":
                return POIType.Church;

            default:
                return POIType.General;

        }
    }
    public void Spawn3DRoute()
    {
        List<Vector2d> routePoints = RoutingManager.Instance.GeometryPoints;
        if (routePoints.Count > 0)
        {
            foreach (var point in routePoints)
            {
                GPSCoordinate coord = new() { coordinates = $"{point.x},{point.y}", POIType = POIType.General };
                Vector3 position = CalculateObjectLocalPosition(coord.Latitude, coord.Longitude);

                GameObject createdObj = Instantiate(RoutingManager.Instance.routePrefab, new Vector3(position.x, position.y, position.z), Quaternion.identity);
                GameObject pointsParent = GameObject.Find("3DRoutePoints");
                if (pointsParent != null)
                {
                    createdObj.transform.parent = pointsParent.transform;
                }

                RoutingManager.Instance.RoutePoints.Add(createdObj);
                RoutingManager.Instance.routeWorldPositions.Add(createdObj.transform.position);
            }
        }
        else
        {
            Debug.LogError("No route points found");
        }
    }
    #endregion

    #region GPS translation and Area creation

    /// <summary>
    /// Calculate the local position of an object based on its GPS coordinates.
    /// </summary>
    /// <param name="latitude">The latitude of the object.</param>
    /// <param name="longitude">The longitude of the object.</param>
    /// <returns>The local position of the object.</returns>
    Vector3 CalculateObjectLocalPosition(double latitude, double longitude)
    {
        const double EarthRadius = 6378137; // WGS84 equatorial radius in meters
        const double DegToRad = Math.PI / 180.0;
        const double MetersPerDegree = DegToRad * EarthRadius; // ~111319.5 meters per degree

        double latRad = referenceLatitude * DegToRad;
        double latOffset = (latitude - referenceLatitude) * MetersPerDegree;
        double lonOffset = (longitude - referenceLongitude) * MetersPerDegree * Math.Cos(latRad);

        return referenceUnityPosition + new Vector3((float)lonOffset, 0, (float)latOffset);
    }
    public void CreateArea(List<GPSCoordinate> areaCoordinates, string name)
    {
        GameObject areaGameObject = new(name);
        MeshFilter meshFilter = areaGameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = areaGameObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = areaGameObject.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.isTrigger = false;

        meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
        {
            color = Color.white
        };

        areaGameObject.AddComponent<AreaScript>();

        // Convert GPS coordinates to local positions and create vertices
        Vector3[] vertices = new Vector3[areaCoordinates.Count];
        for (int i = 0; i < areaCoordinates.Count; i++)
        {
            vertices[i] = CalculateObjectLocalPosition(areaCoordinates[i].Latitude, areaCoordinates[i].Longitude);
        }

        // Calculate the center of the vertices
        Vector3 center = Vector3.zero;
        foreach (var vertex in vertices)
        {
            center += vertex;
        }
        center /= vertices.Length;

        // Adjust vertices to center the mesh pivot
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= center;
        }

        // Assuming the area is flat on the ground, we only need to define vertices in the XZ plane
        int[] triangles = Triangulate(vertices.Length);

        Mesh mesh = new()
        {
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        // Move the areaGameObject to the center of the mesh
        areaGameObject.transform.position = center;
    }
    // Simple triangulation function for convex shapes. For complex shapes, use a proper triangulation library.
    private int[] Triangulate(int vertexCount)
    {
        List<int> triangles = new List<int>();
        for (int i = 1; i < vertexCount - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }
        return triangles.ToArray();
    }
    public List<GPSCoordinate> ParseCoordinates(string coordinatesString)
    {
        List<GPSCoordinate> coordinatesList = new();
        string[] coordinatePairs = coordinatesString.Split(new char[] { ' ', '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < coordinatePairs.Length; i += 2)
        {
            double latitude = double.Parse(coordinatePairs[i]);
            double longitude = double.Parse(coordinatePairs[i + 1]);

            // Add the parsed coordinates to the list
            coordinatesList.Add(new GPSCoordinate
            {
                coordinates = $"{latitude},{longitude}",
                POIType = POIType.General
            });
        }

        return coordinatesList;
    }
    float GetFlatDistanceOfTwoPoints(Vector3 point1, Vector3 point2)
    {
        Vector3 difference = point2 - point1;
        difference.y = 0; // Ignore the Y-axis
        return difference.magnitude;
    }
    #endregion
    void OnDestroy()
    {
        if (webSocketConnection != null)
        {
            webSocketConnection.OnHardcodedDataReceived -= HandleHardcodedDataReceived;
        }
    }
    public void SkipPhoneAndUseHardcodedCoordinates()
    {
        referenceLatitude = double.Parse(referenceCoordinates.Split(',')[0].Trim());
        referenceLongitude = double.Parse(referenceCoordinates.Split(',')[1].Trim());
        referenceCoordinatesSet = true;

        referenceUnityPosition = Camera.main.transform.position;

        mapGameObject.GetComponentInParent<MapAndPlayerManager>().SetCoordinates(new Vector2d(referenceLatitude, referenceLongitude));

    }
}
