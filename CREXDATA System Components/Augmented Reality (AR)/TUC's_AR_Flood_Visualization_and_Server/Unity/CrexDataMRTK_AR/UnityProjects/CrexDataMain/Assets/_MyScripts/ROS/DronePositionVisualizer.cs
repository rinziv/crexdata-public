using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Nav;
using RosMessageTypes.Sensor;
using NaughtyAttributes;
using System;
using System.Collections.Generic;
using Mapbox.Utils;
using RosMessageTypes.Std;
using Newtonsoft.Json.Linq;

public class DronePositionVisualizer : MonoBehaviour
{
    public enum AltitudeMode { Gnss, Relative }

    [Header("ROS Topic Settings")]
    [SerializeField] private string odomTopic = "/odom";
    [SerializeField] private string gnssTopic = "/mavros/global_position/global";

    [Header("Altitude Settings")]
    public AltitudeMode altitudeMode = AltitudeMode.Gnss;
    [Tooltip("The ROS topic for relative altitude data (e.g., from a barometer).")]
    [SerializeField] private string relativeAltitudeTopic = "/mavros/global_position/rel_alt";

    [Header("GameObject References")]
    public GameObject droneObject;
    public GameObject droneObjectOnMap;

    [Header("Data Timeouts")]
    public float dataTimeout = 10f;

    [Header("GNSS Staleness Settings")]
    public float gnssStaleTime = 0.5f;  // Time after which GNSS data starts to be considered stale
    public float gnssMaxStaleTime = 1.5f;  // Time after which GNSS data is ignored completely

    [Header("Coordinate Transformation")]
    public Transform originTransform;
    [Tooltip("Weight for GNSS data (0-1). Higher values trust GNSS more than odometry")]
    [Range(0f, 1f)]
    public float gnssWeight = 0.3f;

    [Header("GNSS Spoofing (Optional)")]
    [Tooltip("Enable to override the initial GNSS location with a custom one.")]
    public bool useSpoofedGnss = false;
    [Tooltip("The custom latitude to start the drone from.")]
    public double spoofedLatitude;
    [Tooltip("The custom longitude to start the drone from.")]
    public double spoofedLongitude;

    private WebSocketConnection webSocketConnection;

    [Header("User GPS Reference (For Initial Calibration)")]
    public double userReferenceLatitude;
    public double userReferenceLongitude;
    public double userReferenceAltitude;
    [Tooltip("Unity world position corresponding to the user reference GPS")]
    public Vector3 userReferenceUnityPosition;
    private MapAndPlayerManager mapManager;

    // Sensor data
    private OdomData lastOdomData = new OdomData();
    private GnssData lastGnssData = new GnssData();
    private double lastRelativeAltitude = 0.0;

    // Timeout tracking
    private float lastOdomTime = 0f;
    private float lastGnssTime = 0f;
    private float lastRelativeAltitudeTime = 0f;
    private bool receivedOdom = false;
    private bool receivedGnss = false;
    private bool receivedRelativeAltitude = false;

    // GNSS spoofing state
    private GnssData initialRealGnssData;
    private bool receivedInitialRealGnss = false;

    // Initial values for offset calculation
    private bool initializedOdom = false;
    private Vector3 initialOdomPosition;
    private Quaternion initialOdomRotation;

    // Earth constants for GPS calculations
    private const double EARTH_RADIUS = 6378137.0; // WGS84 Earth radius in meters
    private const double DEG_TO_RAD = Math.PI / 180.0;

    #region Trajectory Visualization
    [Header("Trajectory Visualization")]
    public bool showTrajectory = true;
    public int maxTrajectoryPoints = 500;
    public float trajectoryPointInterval = 0.5f; // Time between points in seconds
    public Material trajectoryMaterial;
    public float trajectoryWidth = 0.15f;
    public Color trajectoryColor = Color.red;

    private LineRenderer trajectoryLine;
    private List<Vector3> trajectoryPoints = new List<Vector3>();
    private float lastTrajectoryPointTime = 0f;
    #endregion

    private Coroutine updateMapPositionCoroutine;

    #region Events
    public static event Action OnDataTimeout; // Event for data timeout handling;

    #endregion

    // Classes to store incoming data
    private class OdomData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public DateTime timestamp;
    }

    private class GnssData
    {
        public double latitude;
        public double longitude;
        public double altitude;
        public DateTime timestamp;
        public int status;
        public double[] positionCovariance = new double[9]; // Store full matrix
        public double horizontalAccuracy; // For backwards compatibility
        public double verticalAccuracy;
    }

    void Start()
    {
        mapManager = FindObjectOfType<MapAndPlayerManager>();
        webSocketConnection = FindObjectOfType<WebSocketConnection>();

        if (webSocketConnection != null)
        {
            webSocketConnection.OnHardcodedDataReceived += HandleHardcodedDataReceived;
        }

        /*         // Subscribe to both topics
                var ros = ROSConnection.GetOrCreateInstance();
                ros.Subscribe<OdometryMsg>(odomTopic, OdomCallback);
                ros.Subscribe<NavSatFixMsg>(gnssTopic, GnssCallback); */

        lastOdomTime = Time.time;
        lastGnssTime = Time.time;

        // Ensure we have a valid drone object
        if (droneObject == null)
        {
            Debug.LogError("DronePositionVisualizer: No drone GameObject assigned!");
            droneObject = this.gameObject; // Fallback to self
        }
        if (showTrajectory)
        {
            SetupTrajectoryRenderer();
        }
    }

    private void RequestSpoofedGps()
    {
        var request = new
        {
            type = "getSpoofedGps",
            usecase = "Drone",
        };
        webSocketConnection.SendJsonToServer(request);
    }

    private void HandleHardcodedDataReceived(JObject jObject)
    {

        var phoneGPSObject = jObject["spoofedGPSCoordinatesForPhone"];
        if (phoneGPSObject != null && phoneGPSObject is JObject)
        {
            string latitudeStr = phoneGPSObject["latitude"]?.ToString();
            string longitudeStr = phoneGPSObject["longitude"]?.ToString();

            if (double.TryParse(latitudeStr, out double latitude) && double.TryParse(longitudeStr, out double longitude))
            {
                HandleSpoofedGpsReceived(latitude, longitude, "Phone");
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



        var spoofedGPSObject = jObject["spoofedGPSCoordinatesForDrone"];
        if (spoofedGPSObject != null && spoofedGPSObject is JObject)
        {
            var gpsData = (JObject)spoofedGPSObject;
            double latitude = gpsData["latitude"]?.Value<double>() ?? 0.0;
            double longitude = gpsData["longitude"]?.Value<double>() ?? 0.0;
            useSpoofedGnss = gpsData["useSpoofedGnss"]?.Value<bool>() ?? false;
            HandleSpoofedGpsReceived(latitude, longitude, "Drone");
        }
        else
        {
            Debug.LogWarning("spoofedGPSCoordinatesForDrone is missing or invalid in hardcoded data");
        }
    }

    private void HandleSpoofedGpsReceived(double latitude, double longitude, string usecase)
    {
        // Only handle spoofed GPS if the usecase matches "Drone"
        if (usecase == "Drone" && useSpoofedGnss)
        {
            // Update the spoofed coordinates
            spoofedLatitude = latitude;
            spoofedLongitude = longitude;
            Debug.Log($"Received spoofed GPS for Drone: {latitude}, {longitude}");
        }
        else if (usecase == "Phone")
        {
            userReferenceLatitude = latitude;
            userReferenceLongitude = longitude;
        }
        else
        {
            Debug.LogWarning($"Received spoofed GPS for unknown usecase: {usecase}");
        }


    }

    public void SubscribeToTopics()
    {
        var ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<OdometryMsg>(odomTopic, OdomCallback);
        ros.Subscribe<NavSatFixMsg>(gnssTopic, GnssCallback);
        if (altitudeMode == AltitudeMode.Relative)
        {
            ros.Subscribe<Float64Msg>(relativeAltitudeTopic, RelativeAltitudeCallback);
            Debug.Log("Subscribed to relative altitude topic: " + relativeAltitudeTopic);
        }
        Debug.Log("Subscribed to ROS topics: " + odomTopic + ", " + gnssTopic);
    }

    public void UnsubscribeFromTopics()
    {
        var ros = ROSConnection.GetOrCreateInstance();
        ros.Unsubscribe(odomTopic);
        ros.Unsubscribe(gnssTopic);
        if (altitudeMode == AltitudeMode.Relative)
        {
            ros.Unsubscribe(relativeAltitudeTopic);
            Debug.Log("Unsubscribed from relative altitude topic: " + relativeAltitudeTopic);
        }
        Debug.Log("Unsubscribed from ROS topics: " + odomTopic + ", " + gnssTopic);
    }

    void Update()
    {
        // Check for timeouts
        bool odomTimedOut = receivedOdom && (Time.time - lastOdomTime) > dataTimeout;
        bool gnssTimedOut = receivedGnss && (Time.time - lastGnssTime) > dataTimeout;
        bool relativeAltitudeTimedOut = receivedRelativeAltitude && (Time.time - lastRelativeAltitudeTime) > dataTimeout;

        // Handle timeout events
        if (odomTimedOut && gnssTimedOut)
        {
            ResetDroneObject();
            receivedOdom = false;
            receivedGnss = false;
            OnDataTimeout?.Invoke();
            Debug.LogWarning("Both Odom and GNSS data timed out. Resetting drone position.");

        }

        if (gnssTimedOut && updateMapPositionCoroutine != null)
        {
            StopCoroutine(updateMapPositionCoroutine);
            updateMapPositionCoroutine = null;
            Debug.LogWarning("Stopped map update coroutine due to GNSS timeout.");
        }

        if (altitudeMode == AltitudeMode.Relative && relativeAltitudeTimedOut)
        {
            Debug.LogWarning("Relative altitude data timed out. Altitude updates may be incorrect.");
            receivedRelativeAltitude = false; // Allow using stale data but warn
        }
    }

    void SetupTrajectoryRenderer()
    {
        trajectoryLine = droneObject.GetComponent<LineRenderer>();
        if (trajectoryLine == null)
        {
            trajectoryLine = droneObject.AddComponent<LineRenderer>();
        }

        // Use widthMultiplier for the maximum width of the trail (at the drone)
        trajectoryLine.widthMultiplier = trajectoryWidth;

        // Create a width curve that tapers from thin at the tail to wide at the head.
        // Time 0 on the curve corresponds to the oldest point (tail).
        // Time 1 corresponds to the newest point (at the drone).
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0.0f, 0.1f); // Tail is 10% of the max width
        widthCurve.AddKey(0.7f, 1.0f); // Ramps up to full width near the drone
        widthCurve.AddKey(1.0f, 1.0f); // Head is at full width
        trajectoryLine.widthCurve = widthCurve;

        trajectoryLine.positionCount = 0;
        trajectoryLine.useWorldSpace = true;

        if (trajectoryMaterial != null)
        {
            trajectoryLine.material = trajectoryMaterial;
        }
        else

        {
            // Using the standard URP/Unlit shader as requested.
            // This is a safe and performant choice for HoloLens 2.
            trajectoryLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        // Create a color gradient to fade the trail from transparent to opaque.
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            // The color of the trail remains constant.
            new GradientColorKey[] { new GradientColorKey(trajectoryColor, 0.0f), new GradientColorKey(trajectoryColor, 1.0f) },
            // The alpha fades from transparent at the tail to fully opaque at the drone.
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        trajectoryLine.colorGradient = gradient;
    }

    void SpawnDroneOnMap()
    {
        if (droneObject == null)
        {
            Debug.LogError("DronePositionVisualizer: No drone GameObject assigned!");
            return;
        }
        else if (droneObjectOnMap == null)
        {
            Drone drone = droneObject.GetComponent<Drone>();
            FindAnyObjectByType<GPSSpawner>().SpawnPOIForMap(
                drone.droneGPSCoordinates,
                POIType.Drone,
                out droneObjectOnMap
            );
        }
    }

    void OdomCallback(OdometryMsg msg)
    {
        //Debug.Log($"Received Odom: Position={msg.pose.pose.position}, Orientation={msg.pose.pose.orientation}");
        lastOdomTime = Time.time;
        receivedOdom = true;

        // Extract and store odometry data
        lastOdomData.position = new Vector3(
            (float)msg.pose.pose.position.x,
            (float)msg.pose.pose.position.y,
            (float)msg.pose.pose.position.z
        );

        lastOdomData.rotation = new Quaternion(
            (float)msg.pose.pose.orientation.x,
            (float)msg.pose.pose.orientation.y,
            (float)msg.pose.pose.orientation.z,
            (float)msg.pose.pose.orientation.w
        );

        lastOdomData.linearVelocity = new Vector3(
            (float)msg.twist.twist.linear.x,
            (float)msg.twist.twist.linear.y,
            (float)msg.twist.twist.linear.z
        );

        lastOdomData.angularVelocity = new Vector3(
            (float)msg.twist.twist.angular.x,
            (float)msg.twist.twist.angular.y,
            (float)msg.twist.twist.angular.z
        );

        lastOdomData.timestamp = DateTime.Now;

        // Initialize odometry references if needed
        if (!initializedOdom)
        {
            initializedOdom = true;
            initialOdomPosition = lastOdomData.position;
            initialOdomRotation = lastOdomData.rotation;
            Debug.Log($"Initial Odometry Reference: Position={initialOdomPosition}, Rotation={initialOdomRotation}");
        }

        UpdateDronePosition();
    }

    void RelativeAltitudeCallback(Float64Msg msg)
    {
        lastRelativeAltitude = msg.data;
        receivedRelativeAltitude = true;
        lastRelativeAltitudeTime = Time.time;
    }

    void GnssCallback(NavSatFixMsg msg)
    {
        lastGnssTime = Time.time;
        receivedGnss = true;

        if (useSpoofedGnss)
        {
            if (!receivedInitialRealGnss)
            {
                // First real GNSS message. Store it as the anchor for real-world offsets.
                initialRealGnssData = new GnssData
                {
                    latitude = msg.latitude,
                    longitude = msg.longitude,
                    altitude = msg.altitude
                };
                receivedInitialRealGnss = true;

                // Use the spoofed location as the base for the drone's position.
                lastGnssData.latitude = spoofedLatitude;
                lastGnssData.longitude = spoofedLongitude;
                lastGnssData.altitude = msg.altitude; // Use real altitude unless specified otherwise
            }
            else
            {
                // Calculate the offset from the initial real location.
                double latOffset = msg.latitude - initialRealGnssData.latitude;
                double lonOffset = msg.longitude - initialRealGnssData.longitude;
                double altOffset = msg.altitude - initialRealGnssData.altitude;

                // Apply this offset to our spoofed location.
                lastGnssData.latitude = spoofedLatitude + latOffset;
                lastGnssData.longitude = spoofedLongitude + lonOffset;
                lastGnssData.altitude = initialRealGnssData.altitude + altOffset; // Apply offset to initial real altitude
            }
        }
        else
        {
            // Original behavior: use the incoming data directly.
            lastGnssData.latitude = msg.latitude;
            lastGnssData.longitude = msg.longitude;
            lastGnssData.altitude = msg.altitude;
        }

        // Store the rest of the GNSS data
        droneObject.GetComponent<Drone>().droneGPSCoordinates = new Vector2((float)lastGnssData.latitude, (float)lastGnssData.longitude);
        lastGnssData.status = msg.status.status;
        lastGnssData.timestamp = DateTime.Now;

        // Extract position accuracy from covariance (if available)
        if (msg.position_covariance.Length <= 9)
        {
            Array.Copy(msg.position_covariance, lastGnssData.positionCovariance, 9);
            lastGnssData.horizontalAccuracy = Math.Max(msg.position_covariance[0], msg.position_covariance[4]);
            lastGnssData.verticalAccuracy = msg.position_covariance[8]; // Z/altitude variance
        }

        if (receivedGnss && updateMapPositionCoroutine == null)
        {
            updateMapPositionCoroutine = StartCoroutine(UpdateDronePositionOnMapCoroutine());
        }

        UpdateDronePosition();
    }

    System.Collections.IEnumerator UpdateDronePositionOnMapCoroutine()
    {
        while (true)
        {
            UpdateDronePositionOnMap();
            yield return new WaitForSeconds(3f);
        }
    }

    void UpdateDronePosition()
    {
        if (!initializedOdom || droneObject == null)
            return;

        // Calculate position from odometry (relative to initial position)
        Vector3 odomOffsetPosition = CalculateOdomOffsetPosition();
        Quaternion odomOffsetRotation = CalculateOdomOffsetRotation();

        // If we have GNSS data, use it for absolute positioning
        if (receivedGnss)
        {
            // Calculate how stale the GNSS data is
            float gnssAge = Time.time - lastGnssTime;

            if (gnssAge < gnssMaxStaleTime)
            {
                // Determine which altitude to use based on the selected mode
                double altitudeForConversion = lastGnssData.altitude;
                if (altitudeMode == AltitudeMode.Relative && receivedRelativeAltitude)
                {
                    altitudeForConversion = lastRelativeAltitude;
                }

                Vector3 gnssWorldPosition = ConvertGnssToUnityPosition(
                    lastGnssData.latitude,
                    lastGnssData.longitude,
                    altitudeForConversion
                );

                // Horizontal weight
                float horizontalGnssWeight = gnssWeight;
                if (lastGnssData.horizontalAccuracy > 0)
                {
                    horizontalGnssWeight = Mathf.Clamp(gnssWeight * (1.0f / (float)(1.0 + Mathf.Abs(1f - (float)lastGnssData.horizontalAccuracy))), 0.05f, gnssWeight);
                }

                // Vertical weight
                float verticalGnssWeight = gnssWeight;
                if (lastGnssData.verticalAccuracy > 0)
                {
                    verticalGnssWeight = Mathf.Clamp(gnssWeight * (1.0f / (float)(1.0 + Mathf.Abs(1f - (float)lastGnssData.verticalAccuracy))), 0.05f, gnssWeight);
                }

                // Reduce weights based on staleness
                if (gnssAge > gnssStaleTime)
                {
                    float stalenessFactor = 1.0f - Mathf.Clamp01((gnssAge - gnssStaleTime) / (gnssMaxStaleTime - gnssStaleTime));
                    horizontalGnssWeight *= stalenessFactor;
                    verticalGnssWeight *= stalenessFactor;
                    Debug.LogWarning($"Using stale GNSS data ({gnssAge:F1}s old). Weight reduced to H:{horizontalGnssWeight:F3}, V:{verticalGnssWeight:F3}");
                }

                // Apply different weights to horizontal and vertical components
                Vector3 finalPosition = new Vector3(
                    Mathf.Lerp(odomOffsetPosition.x, gnssWorldPosition.x, horizontalGnssWeight),
                    Mathf.Lerp(odomOffsetPosition.y, gnssWorldPosition.y, verticalGnssWeight),
                    Mathf.Lerp(odomOffsetPosition.z, gnssWorldPosition.z, horizontalGnssWeight)
                );

                droneObject.transform.SetPositionAndRotation(finalPosition, odomOffsetRotation);
            }
            else
            {
                // GNSS data too stale, use odometry only
                droneObject.transform.position = odomOffsetPosition;
                droneObject.transform.rotation = odomOffsetRotation;
                Debug.LogWarning($"GNSS data too stale ({(Time.time - lastGnssTime):F1}s old). Using odometry only.");
            }
        }
        else
        {
            // No GNSS data received yet, use just odometry
            droneObject.transform.SetPositionAndRotation(odomOffsetPosition, odomOffsetRotation);

        }
        //Update drone position on map if too
        // UpdateDronePositionOnMap();
        if (showTrajectory)
        {
            UpdateTrajectory();
        }
    }

    Vector3 CalculateOdomOffsetPosition()
    {
        Vector3 relativePosition = lastOdomData.position - initialOdomPosition;

        // Convert from ROS coordinate system (X-forward, Z-up) to Unity (Z-forward, Y-up)
        return new Vector3(
            relativePosition.y, // ROS Y → Unity X
            relativePosition.z, // ROS Z → Unity Y
            relativePosition.x  // ROS X → Unity Z
        );
    }

    Quaternion CalculateOdomOffsetRotation()
    {
        // Calculate rotation offset from initial orientation
        Quaternion currentRotation = lastOdomData.rotation;
        Quaternion offsetRotation = currentRotation * Quaternion.Inverse(initialOdomRotation);

        // Convert from ROS to Unity coordinate system
        return ConvertRosToUnityRotation(offsetRotation);
    }

    Quaternion ConvertRosToUnityRotation(Quaternion rosRotation)
    {
        // This rotation conversion depends on the specific setup
        // A common conversion is a 90-degree rotation around the X axis
        return Quaternion.Euler(90, 0, 0) * rosRotation * Quaternion.Euler(-90, 0, 0);
    }

    Vector3 ConvertGnssToUnityPosition(double latitude, double longitude, double altitude)
    {
        // Convert GPS coordinates to local XYZ coordinates
        // Using a simple flat Earth approximation centered at the user's reference position

        double dLat = (latitude - userReferenceLatitude) * DEG_TO_RAD;
        double dLon = (longitude - userReferenceLongitude) * DEG_TO_RAD;
        double y;

        if (altitudeMode == AltitudeMode.Relative)
        {
            // Use relative altitude directly, ignoring user reference altitude.
            y = altitude;
        }
        else
        {
            // Use GNSS altitude, which requires subtracting the user's reference altitude.
            double dAlt = altitude - userReferenceAltitude;
            y = dAlt;
        }

        // Calculate approximation (Equirectangular projection)
        double x = dLon * EARTH_RADIUS * Math.Cos(userReferenceLatitude * DEG_TO_RAD);
        double z = dLat * EARTH_RADIUS;

        // Convert to Unity coordinate system and add the reference offset
        Vector3 localPosition = new Vector3((float)x, (float)y, (float)z);

        // Apply the user reference position to convert to Unity world space
        if (originTransform != null)
        {
            return originTransform.TransformPoint(localPosition + userReferenceUnityPosition);
        }
        else
        {
            return localPosition + userReferenceUnityPosition;
        }
    }

    [Button("Reset Drone Object")]
    public void ResetDroneObject()
    {
        initializedOdom = false;
        receivedInitialRealGnss = false; // Reset spoofing anchor

        if (droneObject != null)
        {
            droneObject.transform.position = Vector3.zero;
            droneObject.transform.rotation = Quaternion.identity;
            Debug.Log("Drone object reset to origin position and rotation.");
        }

        GetComponent<RoboticPanelManager>().ClearRoute();
        // Clear trajectory when resetting
        ClearTrajectory();
    }

    [Button("Calibrate User Position")]
    public void CalibrateUserPosition()
    {
        if (receivedGnss)
        {
            // Store the current GNSS reading as the user reference
            userReferenceLatitude = lastGnssData.latitude;
            userReferenceLongitude = lastGnssData.longitude;
            userReferenceAltitude = lastGnssData.altitude;

            // Use the current position as reference point in Unity
            if (originTransform != null)
            {
                userReferenceUnityPosition = originTransform.position;
            }
            else
            {
                userReferenceUnityPosition = Vector3.zero;
            }

            Debug.Log($"User position calibrated. GPS: {userReferenceLatitude},{userReferenceLongitude},{userReferenceAltitude}, Unity: {userReferenceUnityPosition}");

            // Reset everything to use this new reference
            ResetDroneObject();
        }
        else
        {
            Debug.LogError("Cannot calibrate: No GNSS data received yet!");
        }
    }

    void UpdateTrajectory()
    {
        // Add points at defined intervals
        if (Time.time - lastTrajectoryPointTime >= trajectoryPointInterval)
        {
            AddTrajectoryPoint(droneObject.transform.position);
            lastTrajectoryPointTime = Time.time;
        }
    }

    void AddTrajectoryPoint(Vector3 point)
    {
        // Add the new point to our list
        trajectoryPoints.Add(point);

        // Limit number of points
        if (trajectoryPoints.Count > maxTrajectoryPoints)
        {
            trajectoryPoints.RemoveAt(0);
        }

        // Update the line renderer
        trajectoryLine.positionCount = trajectoryPoints.Count;
        for (int i = 0; i < trajectoryPoints.Count; i++)
        {
            trajectoryLine.SetPosition(i, trajectoryPoints[i]);
        }
    }

    [Button("Clear Trajectory")]
    public void ClearTrajectory()
    {
        trajectoryPoints.Clear();
        if (trajectoryLine != null)
        {
            trajectoryLine.positionCount = 0;
        }
    }

    [Button("Spawn Drone on Map")]
    public void SpawnDroneOnMapButton()
    {
        SpawnDroneOnMap();
    }

    [Button("Update Drone Position on Map")]
    public void UpdateDronePositionOnMap()
    {
        if (droneObject == null)
        {
            Debug.LogError("DronePositionVisualizer: No drone GameObject assigned!");
            return;
        }

        if (droneObject.TryGetComponent<Drone>(out var drone))
        {
            mapManager.UpdateSpecificPOI(droneObjectOnMap, new Vector2d(drone.droneGPSCoordinates.x, drone.droneGPSCoordinates.y));
            //mapManager.UpdatePlayerLocation(new Vector2d(drone.droneGPSCoordinates.x, drone.droneGPSCoordinates.y));
        }
        else
        {
            Debug.LogError("DronePositionVisualizer: Drone component not found on the assigned GameObject!");
        }
    }

    void OnDestroy()
    {
        if (webSocketConnection != null)
        {
            webSocketConnection.OnSpoofedGpsReceived -= HandleSpoofedGpsReceived;
        }

        UnsubscribeFromTopics();
        if (updateMapPositionCoroutine != null)
        {
            StopCoroutine(updateMapPositionCoroutine);
            updateMapPositionCoroutine = null;
        }
    }
}