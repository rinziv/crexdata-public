using UnityEngine;
using Mapbox.Utils;
using Mapbox.Unity.MeshGeneration.Factories;

/// <summary>
/// Tracks camera movement and converts it to GPS coordinate updates for map simulation.
/// Works with existing MRTK3 camera controls - no additional input handling needed.
/// </summary>
public class EditorGPSSimulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MapAndPlayerManager mapManager;
    [SerializeField] private GPSSpawner gpsSpawner;
    [SerializeField] private RoutingManager routingManager; // For startPoint reference
    [SerializeField] private Transform cameraTransform; // Main camera that MRTK3 moves

    [Header("Simulation Settings")]
    [Tooltip("Enable GPS simulation (only works in editor)")]
    [SerializeField] private bool enableSimulation = true;

    [Tooltip("Automatically disable when real GPS data is received from server")]
    [SerializeField] private bool autoDisableOnRealGPS = true;

    [Tooltip("GPS update frequency (updates per second)")]
    [SerializeField] private float updateFrequency = 1f;

    [Tooltip("Minimum movement distance (in meters) to trigger GPS update")]
    [SerializeField] private float minimumMovementThreshold = 0.1f;

    [Header("Debug Info")]
    [SerializeField] private Vector2d currentGPSPosition;
    [SerializeField] private bool isInitialized = false;
    [SerializeField] private float totalDistanceMoved = 0f;

    // Internal state
    private Vector3 lastCameraPosition;
    private float timeSinceLastUpdate = 0f;
    private const float METERS_PER_DEGREE_LAT = 111320f; // Constant for latitude
    private float metersPerDegreeLon; // Varies by latitude

    void Start()
    {
#if !UNITY_EDITOR
        // Disable simulation in builds
        enableSimulation = false;
        Debug.Log("[GPS Simulator] Disabled - not in editor");
        return;
#endif

        // Auto-find references
        if (mapManager == null)
            mapManager = FindObjectOfType<MapAndPlayerManager>();

        if (gpsSpawner == null)
            gpsSpawner = FindObjectOfType<GPSSpawner>();

        if (routingManager == null)
            routingManager = FindObjectOfType<RoutingManager>();

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;

        // Wait a frame before initializing to let other components set up
        StartCoroutine(DelayedInitialize());
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        // Wait for other systems to initialize
        yield return new WaitForSeconds(0.5f);

        // Check if real GPS has already been received
        if (autoDisableOnRealGPS && gpsSpawner != null && gpsSpawner.referenceCoordinatesSet)
        {
            Debug.Log("[GPS Simulator] Real GPS already set - staying disabled until manually enabled");
            enableSimulation = false;
            yield break;
        }

        // Initialize GPS position from reference GPS coordinates
        InitializeGPSPosition();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!enableSimulation || !isInitialized)
            return;

        // Track camera position changes
        Vector3 currentCameraPosition = cameraTransform.position;
        Vector3 cameraMovement = currentCameraPosition - lastCameraPosition;

        // Check if there's significant movement
        if (cameraMovement.magnitude > minimumMovementThreshold)
        {
            // Convert Unity world movement to GPS offset
            ConvertMovementToGPS(cameraMovement);

            // Update total distance
            totalDistanceMoved += cameraMovement.magnitude;

            // Update last position
            lastCameraPosition = currentCameraPosition;
        }

        // Update timer
        timeSinceLastUpdate += Time.deltaTime;

        // Send GPS update at specified frequency
        if (timeSinceLastUpdate >= (1f / updateFrequency))
        {
            SendGPSUpdate();
            timeSinceLastUpdate = 0f;
        }
#endif
    }

    private void InitializeGPSPosition()
    {
        // Try to get initial GPS from RoutingManager's startPoint first
        if (routingManager != null && routingManager.startPoint != null && mapManager != null && mapManager.map != null)
        {
            Vector3 startPos = routingManager.startPoint.position;

            // Validate position is finite
            if (!IsValidPosition(startPos))
            {
                Debug.LogError($"[GPS Simulator] ❌ Invalid startPoint position: {startPos}");
                return;
            }

            currentGPSPosition = mapManager.map.WorldToGeoPosition(startPos);

            // Validate GPS coordinates are valid
            if (!IsValidGPS(currentGPSPosition))
            {
                Debug.LogError($"[GPS Simulator] ❌ Invalid GPS from WorldToGeoPosition: {currentGPSPosition.x}, {currentGPSPosition.y}");
                return;
            }

            lastCameraPosition = cameraTransform.position;
            CalculateMetersPerDegreeLon();
            isInitialized = true;

            Debug.Log($"[GPS Simulator] ✅ Initialized from RoutingManager startPoint at GPS: {currentGPSPosition.x:F6}, {currentGPSPosition.y:F6}");
        }
        // Fallback: use GPSSpawner's reference coordinates
        else if (gpsSpawner != null && gpsSpawner.referenceCoordinatesSet)
        {
            double lat = gpsSpawner.referenceLatitude;
            double lon = gpsSpawner.referenceLongitude;

            // Validate GPS coordinates
            if (!IsValidLatitude(lat) || !IsValidLongitude(lon))
            {
                Debug.LogError($"[GPS Simulator] ❌ Invalid reference GPS: {lat}, {lon}");
                return;
            }

            currentGPSPosition = new Vector2d(lat, lon);
            lastCameraPosition = cameraTransform.position;
            CalculateMetersPerDegreeLon();
            isInitialized = true;

            Debug.Log($"[GPS Simulator] ✅ Initialized from GPSSpawner reference GPS: {currentGPSPosition.x:F6}, {currentGPSPosition.y:F6}");
        }
        else
        {
            Debug.LogWarning("[GPS Simulator] ⚠️ Cannot initialize - need RoutingManager.startPoint or GPSSpawner reference coordinates set");
        }
    }

    private void ConvertMovementToGPS(Vector3 movementInMeters)
    {
        // Unity coordinates: X = East/West, Z = North/South
        float deltaEast = movementInMeters.x;   // X axis = longitude change
        float deltaNorth = movementInMeters.z;  // Z axis = latitude change

        // Convert meters to degrees
        float deltaLat = deltaNorth / METERS_PER_DEGREE_LAT;
        float deltaLon = deltaEast / metersPerDegreeLon;

        // Update GPS position
        currentGPSPosition.x += deltaLat; // Latitude
        currentGPSPosition.y += deltaLon; // Longitude

        // Recalculate longitude conversion factor (varies with latitude)
        CalculateMetersPerDegreeLon();
    }

    private void SendGPSUpdate()
    {
        if (!isInitialized)
            return;

        // Validate GPS before sending
        if (!IsValidGPS(currentGPSPosition))
        {
            Debug.LogError($"[GPS Simulator] ❌ Invalid GPS position, skipping update: {currentGPSPosition.x}, {currentGPSPosition.y}");
            return;
        }

        // Update map manager
        if (mapManager != null)
        {
            mapManager.SetCoordinates(currentGPSPosition);
            mapManager.UpdateMapAndPlayer();
        }
    }

    /// <summary>
    /// Calculates meters per degree of longitude at current latitude.
    /// Longitude degrees get smaller as you approach the poles.
    /// </summary>
    private void CalculateMetersPerDegreeLon()
    {
        float latInRadians = (float)(currentGPSPosition.x * Mathf.Deg2Rad);
        metersPerDegreeLon = METERS_PER_DEGREE_LAT * Mathf.Cos(latInRadians);

        // Validate result
        if (!float.IsFinite(metersPerDegreeLon) || metersPerDegreeLon <= 0)
        {
            Debug.LogError($"[GPS Simulator] ❌ Invalid metersPerDegreeLon: {metersPerDegreeLon}");
            metersPerDegreeLon = METERS_PER_DEGREE_LAT; // Fallback to equator value
        }
    }

    // ==================== VALIDATION METHODS ====================

    /// <summary>
    /// Checks if a Vector3 position contains valid finite values
    /// </summary>
    private bool IsValidPosition(Vector3 pos)
    {
        return float.IsFinite(pos.x) && float.IsFinite(pos.y) && float.IsFinite(pos.z);
    }

    /// <summary>
    /// Checks if GPS coordinates are valid
    /// </summary>
    private bool IsValidGPS(Vector2d gps)
    {
        return IsValidLatitude(gps.x) && IsValidLongitude(gps.y);
    }

    /// <summary>
    /// Checks if latitude is valid (-90 to 90)
    /// </summary>
    private bool IsValidLatitude(double lat)
    {
        return double.IsFinite(lat) && lat >= -90.0 && lat <= 90.0;
    }

    /// <summary>
    /// Checks if longitude is valid (-180 to 180)
    /// </summary>
    private bool IsValidLongitude(double lon)
    {
        return double.IsFinite(lon) && lon >= -180.0 && lon <= 180.0;
    }

    /// <summary>
    /// Call this to reinitialize if map is toggled on/off
    /// </summary>
    public void Reinitialize()
    {
        totalDistanceMoved = 0f;
        InitializeGPSPosition();
    }

    // Simple on-screen display
    void OnGUI()
    {
#if UNITY_EDITOR
        if (!enableSimulation)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 180));
        GUILayout.Box("🗺️ GPS SIMULATOR - Tracking Camera Movement");

        if (isInitialized)
        {
            GUILayout.Label($"📍 Current GPS:");
            GUILayout.Label($"   Lat: {currentGPSPosition.x:F7}");
            GUILayout.Label($"   Lon: {currentGPSPosition.y:F7}");
            GUILayout.Label($"📏 Total Distance: {totalDistanceMoved:F2}m");
            GUILayout.Label($"🔄 Update Rate: {updateFrequency:F1} Hz");
            GUILayout.Label($"✅ Tracking MRTK3 camera movement");

            if (GUILayout.Button("🔄 Reset Distance Counter"))
            {
                totalDistanceMoved = 0f;
            }
        }
        else
        {
            GUILayout.Label("⚠️ NOT INITIALIZED");
            GUILayout.Label("Make sure map is active and visible");

            if (GUILayout.Button("🔄 Try Initialize"))
            {
                InitializeGPSPosition();
            }
        }

        GUILayout.EndArea();
#endif
    }
}
