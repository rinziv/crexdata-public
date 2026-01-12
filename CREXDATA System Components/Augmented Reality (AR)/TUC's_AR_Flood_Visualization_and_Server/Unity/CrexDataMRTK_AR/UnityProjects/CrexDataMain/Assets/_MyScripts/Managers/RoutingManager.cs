using System.Collections.Generic;
using UnityEngine;
using System;

namespace Mapbox.Unity.MeshGeneration.Factories
{
    using Mapbox.Unity.Map;
    using Mapbox.Directions;
    using Mapbox.Utils;
    using System.Collections;
    using UnityEngine.Networking;
    using Mapbox.Utils.JsonConverters;
    using Newtonsoft.Json;
    using System.Globalization;
    using Custom.Solvers;

    public class RoutingManager : MonoBehaviour
    {
        // Nested enum inside RoutingManager class for easier external access
        public enum RoutingProfiles
        {
            driving,
            walking,
            cycling,
            drivingTraffic
        }

        private GameObject _routeGO;
        private GPSSpawner _spawner;
        private WebSocketConnection _webSocketConnection;
        public static RoutingManager Instance { get; private set; }

        [Header("Routing Profile")]
        [Tooltip("The routing profile to use when requesting directions.")]
        [SerializeField] private RoutingProfiles _routingProfile = RoutingProfiles.driving;

        // Static property that syncs with the inspector field
        public static RoutingProfiles RoutingProf
        {
            get => Instance != null ? Instance._routingProfile : RoutingProfiles.driving;
            set
            {
                if (Instance != null)
                {
                    Instance._routingProfile = value;
                    Debug.Log($"[RoutingManager] Profile changed to: {value}");
                }
            }
        }

        [Header("Map Settings")]
        [Tooltip("Reference to the Mapbox AbstractMap.")]
        [SerializeField] private AbstractMap _map;

        [Header("Route Settings")]
        [Tooltip("Transform representing the starting point.")]
        public Transform startPoint;

        [Tooltip("Transform representing the ending point.")]
        public Transform endPoint;

        [Tooltip("Parent transform for the route game object.")]
        [SerializeField] private Transform _parent;

        [Tooltip("Offset for the route line on the Y-axis.")]
        [Range(0.001f, 1f)]
        [SerializeField] private float _routeLineYOffset = 0.001f;

        public GameObject routePrefab;

        [SerializeField]
        private GameObject _directionIndicator;

        [Header("Exclusion Points")]
        [Tooltip("This list stores the exclusion points that will be used in the route request to exclude certain roads from the route path.")]
        public List<string> exclusionPoints = new();

        [Header("Current Route Points")]
        [Tooltip("This list stores the points that define the route geometry.")]
        [SerializeField] private List<Vector2d> _geometryPoints;
        public List<Vector2d> GeometryPoints => _geometryPoints;
        public List<GameObject> RoutePoints = new();

        [Header("Navigation Settings")]
        [Tooltip("Distance in meters to consider a waypoint reached")]
        [SerializeField] private float waypointReachedThreshold = 2.0f;

        [Tooltip("How frequently to update the navigation (in seconds)")]
        [SerializeField] private float navigationUpdateInterval = 1f;

        [Tooltip("Reference to the user's camera or position tracker")]
        [SerializeField] private Transform userPositionReference;

        [Header("Off-Route Detection")]
        [Tooltip("Maximum distance in meters from route before triggering reroute")]
        [SerializeField] private float offRouteThreshold = 50f;

        [Tooltip("Minimum time in seconds between reroute attempts (prevents spam)")]
        [SerializeField] private float rerouteMinInterval = 10f;

        [Tooltip("Enable/disable automatic rerouting when off-route")]
        [SerializeField] private bool autoRerouteEnabled = true;

        // Navigation state
        [SerializeField] private int currentWaypointIndex = 0;
        [SerializeField] private bool isNavigating = false;
        public List<Vector3> routeWorldPositions = new();
        private ArrowRotatorXZ _arrowRotator;

        // Progressive route rendering (Google Maps style)
        private List<Vector2d> _fullRouteGeoPositions; // Complete route in geo coordinates (lat/lng)
        private LineRenderer _routeLineRenderer; // Reference to the line renderer

        // Off-route detection state
        private float _lastRerouteTime = -1000f; // Last time we requested a reroute
        private bool _isOffRoute = false; // Current off-route status

        // Reusable lists to avoid allocations in UpdateRouteLineRenderer()
        private List<Vector3> _cachedFullRouteWorldPositions = new List<Vector3>();
        private List<Vector3> _cachedRemainingRoute = new List<Vector3>();
        private List<Vector3> _cachedLocalPositions = new List<Vector3>();

        //Temporary transform of the selected POI
        [HideInInspector]
        public Transform selectedPOI;
        private Coroutine navigationCoroutine;

        private Material _routeMaterial;

        [SerializeField] private bool _publishToKafkaTopic = true;

        public static bool HasActiveRoute()
        {
            return Instance != null && Instance.isNavigating;
        }

        public static bool CanPublishToKafkaTopic()
        {
            return Instance != null && Instance._publishToKafkaTopic;
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public RoutingProfile routingproo;

        void Start()
        {
            _spawner = FindObjectOfType<GPSSpawner>();
            _webSocketConnection = FindObjectOfType<WebSocketConnection>();

            if (_webSocketConnection == null)
            {
                Debug.LogWarning("WebSocketConnection not found in scene. Route publishing will be disabled.");
            }
            else
            {
                /* _webSocketConnection.SendJsonToServer(new
                {
                    type = "getRoutingProfile"
                }); */
                _webSocketConnection.OnHardcodedDataReceived += (dataArray) =>
                {
                    // Attempt to parse routing profile from hardcoded data
                    try
                    {
                        string profileString = dataArray?["drivingProfileForRouting"]?.ToString();
                        if (string.IsNullOrEmpty(profileString))
                        {
                            Debug.LogWarning("Routing profile is null or empty, defaulting to 'driving'");
                            RoutingProf = RoutingProfiles.driving;
                        }
                        else if (Enum.TryParse<RoutingProfiles>(profileString, true, out var parsedProfile))
                        {
                            RoutingProf = parsedProfile;
                            Debug.Log($"Routing profile set to: {parsedProfile}");
                        }
                        else
                        {
                            Debug.LogWarning($"Invalid routing profile '{profileString}', defaulting to 'driving'. Valid options: driving, walking, cycling, drivingTraffic");
                            RoutingProf = RoutingProfiles.driving;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error parsing routing profile: {ex.Message}, defaulting to 'driving'");
                        RoutingProf = RoutingProfiles.driving;
                    }
                };

                if (_directionIndicator != null)
                {
                    _arrowRotator = _directionIndicator.GetComponentInChildren<ArrowRotatorXZ>();
                }


            }
        }

        public void StartNavigation()
        {
            Debug.Log("[StartNavigation] === STARTING NAVIGATION ===");

            if (_geometryPoints == null || _geometryPoints.Count == 0)
            {
                Debug.LogWarning("Cannot start navigation without a route");
                return;
            }

            currentWaypointIndex = 0;
            isNavigating = true;

            // USER REFERENCE (for 3D arrow direction in AR - follows camera view)
            // Note: Route line on map uses startPoint position (GPS), NOT camera
            if (userPositionReference == null)
            {
                userPositionReference = Camera.main.transform;
            }

            if (_directionIndicator != null)
            {
                _directionIndicator.SetActive(true);

                // Set initial target
                UpdateDirectionIndicator();
            }

            navigationCoroutine = StartCoroutine(NavigationUpdateCoroutine());
        }
        public void StopNavigation()
        {
            // Only stop navigation if it's actually running
            if (!isNavigating && navigationCoroutine == null)
                return;

            isNavigating = false;

            // Stop the coroutine
            if (navigationCoroutine != null)
            {
                StopCoroutine(navigationCoroutine);
                navigationCoroutine = null;
            }

            if (_directionIndicator != null)
                _directionIndicator.SetActive(false);

            // Reset navigation state
            currentWaypointIndex = 0;
            _isOffRoute = false;

            // Don't call ClearRoute here - it will send network messages
            // ClearRoute is called from the UI or from OnDestroy
        }

        private IEnumerator NavigationUpdateCoroutine()
        {
            while (isNavigating)
            {
                UpdateNavigation();
                UpdateRouteLineRenderer();

                yield return new WaitForSeconds(navigationUpdateInterval);
            }
        }

        private void UpdateNavigation()
        {
            if (!isNavigating || routeWorldPositions.Count == 0)
                return;

            Vector3 userPosition = userPositionReference.position;
            //Vector3 userPositionOnMap = startPoint.position;

            // ==================== OFF-ROUTE DETECTION ====================
            if (autoRerouteEnabled && _fullRouteGeoPositions != null && _fullRouteGeoPositions.Count > 0)
            {
                float distanceToRoute = CalculateDistanceToRoute(userPosition);
                Debug.Log($"[OffRoute] Distance to route: {distanceToRoute:F1}m");

                if (distanceToRoute > offRouteThreshold)
                {
                    if (!_isOffRoute)
                    {
                        Debug.LogWarning($"[OffRoute] User is {distanceToRoute:F1}m from route (threshold: {offRouteThreshold}m)");
                        _isOffRoute = true;
                    }

                    // Check if enough time has passed since last reroute
                    float timeSinceLastReroute = Time.time - _lastRerouteTime;
                    if (timeSinceLastReroute >= rerouteMinInterval)
                    {
                        Debug.LogWarning($"[Reroute] Triggering automatic reroute (last reroute was {timeSinceLastReroute:F1}s ago)");
                        TriggerReroute();
                        _lastRerouteTime = Time.time;

                        //Here we need to exit the function to avoid further processing until the new route is received
                        return;
                    }
                    else
                    {
                        Debug.Log($"[Reroute] Cooldown active... {rerouteMinInterval - timeSinceLastReroute:F1}s remaining");
                    }
                }
                else
                {
                    // User is back on route
                    if (_isOffRoute)
                    {
                        Debug.Log($"[Reroute] User is back on route (distance: {distanceToRoute:F1}m)");
                        _isOffRoute = false;
                    }
                }
            }

            // ==================== WAYPOINT PROGRESS ====================
            // Check if we've reached the current waypoint
            Vector3 currentWaypointPosition = routeWorldPositions[currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(
                new Vector3(userPosition.x, currentWaypointPosition.y, userPosition.z),
                currentWaypointPosition);

            // Debug.Log($"Distance to waypoint {currentWaypointIndex}: {distanceToWaypoint}m");

            // If we've reached the current waypoint, move to the next one
            if (distanceToWaypoint <= waypointReachedThreshold)
            {
                // Move to next waypoint
                currentWaypointIndex++;
                // Did we reach the final destination?
                if (currentWaypointIndex >= routeWorldPositions.Count)
                {
                    Debug.Log("Destination reached!");
                    ClearRoute(true);
                    //StopNavigation();
                    // Maybe trigger UI response
                    return;
                }
                UpdateDirectionIndicator();

                Debug.Log($"Reached waypoint {currentWaypointIndex - 1}, moving to waypoint {currentWaypointIndex}");
            }
        }

        private void UpdateDirectionIndicator()
        {
            if (_directionIndicator == null)
                return;

            // Make sure the indicator is active
            _directionIndicator.SetActive(true);

            _arrowRotator.Target = RoutePoints[currentWaypointIndex].transform;
        }

        private void DisplayRoute(List<Vector3> positions)
        {
            _routeGO = new GameObject("Route");
            _routeGO.transform.SetParent(_parent, false);

            var lineRenderer = _routeGO.AddComponent<LineRenderer>();

            // Store reference to LineRenderer for progressive updates
            _routeLineRenderer = lineRenderer;

            _routeMaterial = new Material(Shader.Find("Sprites/Default"));

            // Set LineRenderer to use local space
            lineRenderer.useWorldSpace = false;

            // Convert world positions to local positions relative to _routeGO
            for (int i = 0; i < positions.Count; i++)
            {
                positions[i] = _routeGO.transform.InverseTransformPoint(positions[i]);
            }

            lineRenderer.positionCount = positions.Count;
            lineRenderer.SetPositions(positions.ToArray());

            // Configure the line appearance
            lineRenderer.startWidth = 0.001f;
            lineRenderer.endWidth = 0.001f;
            lineRenderer.material = _routeMaterial;
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }


        /// <summary>
        /// Updates the route line renderer to show only the remaining route from user's current position.
        /// This creates the "Google Maps" effect where the route line shrinks as you move.
        /// Called every navigation update (typically every 1 second).
        /// </summary>
        private void UpdateRouteLineRenderer()
        {
            // Safety checks
            if (!isNavigating || _fullRouteGeoPositions == null || _fullRouteGeoPositions.Count == 0)
                return;

            if (_routeLineRenderer == null || _routeGO == null)
            {
                Debug.LogError("[RouteLineUpdate] LineRenderer or RouteGO reference is NULL!");
                return;
            }

            // Get user's current GPS position on the map
            Vector3 userPos = startPoint.position;

            // REUSE cached list - clear instead of creating new
            _cachedFullRouteWorldPositions.Clear();

            // Convert geo coordinates to current world positions (updates when map moves!)
            foreach (var geoPos in _fullRouteGeoPositions)
            {
                var worldPos = _map.GeoToWorldPosition(geoPos, true);
                _cachedFullRouteWorldPositions.Add(new Vector3(worldPos.x, worldPos.y + _routeLineYOffset, worldPos.z));
            }

            // Find the closest waypoint index on the route ahead of us
            int closestIndex = currentWaypointIndex;//FindClosestWaypointAhead(userPos, _cachedFullRouteWorldPositions);

            // REUSE cached list - clear and build remaining route
            _cachedRemainingRoute.Clear();
            _cachedRemainingRoute.Add(userPos); // Add user's current position first

            // Copy remaining route points (from closest point to destination)
            for (int i = closestIndex; i < _cachedFullRouteWorldPositions.Count; i++)
            {
                _cachedRemainingRoute.Add(_cachedFullRouteWorldPositions[i]);
            }

            // REUSE cached list - convert to local space
            _cachedLocalPositions.Clear();
            foreach (var worldPos in _cachedRemainingRoute)
            {
                Vector3 localPos = _routeGO.transform.InverseTransformPoint(worldPos);
                _cachedLocalPositions.Add(localPos);
            }

            // Update the LineRenderer with the remaining route
            _routeLineRenderer.positionCount = _cachedLocalPositions.Count;
            _routeLineRenderer.SetPositions(_cachedLocalPositions.ToArray());
        }

        /// <summary>
        /// Finds the closest waypoint index on the route that is ahead of the user.
        /// This ensures we don't include waypoints we've already passed.
        /// </summary>
        private int FindClosestWaypointAhead(Vector3 userPos, List<Vector3> routeWorldPositions)
        {
            float minDistance = float.MaxValue;
            int closestIndex = currentWaypointIndex;

            // Only search from current waypoint onwards (not backwards)
            for (int i = currentWaypointIndex; i < routeWorldPositions.Count; i++)
            {
                // Calculate distance in 2D (XZ plane) to ignore height differences
                Vector3 waypointPos = routeWorldPositions[i];
                float distance = Vector2.Distance(
                    new Vector2(userPos.x, userPos.z),
                    new Vector2(waypointPos.x, waypointPos.z)
                );

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        /// <summary>
        /// Calculates the minimum distance from the user's position to any point on the route.
        /// This is used for off-route detection.
        /// </summary>
        private float CalculateDistanceToRoute(Vector3 userPos)
        {
            if (routeWorldPositions == null || routeWorldPositions.Count == 0)
                return float.MaxValue;

            float minDistance = float.MaxValue;

            // Check distance to all waypoints from current position onwards
            // (no need to check waypoints we've already passed)
            for (int i = currentWaypointIndex; i < routeWorldPositions.Count; i++)
            {
                Vector3 waypointPos = routeWorldPositions[i];

                // Calculate distance in 2D (XZ plane) to ignore height differences
                float distance = Vector2.Distance(
                    new Vector2(userPos.x, userPos.z),
                    new Vector2(waypointPos.x, waypointPos.z)
                );

                if (distance < minDistance)
                {
                    minDistance = distance;
                }

                // Also check distance to the line segment between this and next waypoint
                if (i < routeWorldPositions.Count - 1)
                {
                    Vector3 nextWaypointPos = routeWorldPositions[i + 1];
                    float distanceToSegment = DistanceToLineSegment2D(userPos, waypointPos, nextWaypointPos);

                    if (distanceToSegment < minDistance)
                    {
                        minDistance = distanceToSegment;
                    }
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the perpendicular distance from a point to a line segment in 2D (XZ plane).
        /// This gives a more accurate off-route detection than just checking waypoint distances.
        /// </summary>
        private float DistanceToLineSegment2D(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            // Convert to 2D vectors (XZ plane)
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a = new Vector2(lineStart.x, lineStart.z);
            Vector2 b = new Vector2(lineEnd.x, lineEnd.z);

            Vector2 ab = b - a;
            Vector2 ap = p - a;

            // Project point onto line segment
            float abLengthSquared = ab.sqrMagnitude;

            if (abLengthSquared == 0)
            {
                // Line segment is a point
                return Vector2.Distance(p, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / abLengthSquared);
            Vector2 projection = a + ab * t;

            return Vector2.Distance(p, projection);
        }

        /// <summary>
        /// Triggers an automatic reroute from the user's current position to the destination.
        /// This is called when the user goes off-route.
        /// </summary>
        private void TriggerReroute()
        {
            Debug.Log("[Reroute] ========== AUTOMATIC REROUTE TRIGGERED ==========");
            Debug.Log($"[Reroute] User position: {startPoint.position}");
            Debug.Log($"[Reroute] Destination: {endPoint.position}");

            // Clear current route (but keep the endpoint - we're going to the same destination)
            ClearRoute(clearEndPoint: false);

            // Request new route from current position to destination
            // The existing GetRoute() method will use startPoint and endPoint
            StartCoroutine(GetRouteCoroutine());

            Debug.Log("[Reroute] New route request sent to Mapbox API");
        }

        public void GetRoute()
        {
            StartCoroutine(GetRouteCoroutine());
        }

        public void GetStraightLineRoute(Transform target)
        {
            //Straight directions from user position to target position
            if (target == null)
            {
                Debug.LogError("Target transform is null. Cannot get straight line route.");
                return;
            }
            if (_directionIndicator != null)
            {
                _directionIndicator.SetActive(true);
                _arrowRotator.Target = target;
            }
        }

        IEnumerator GetRouteCoroutine()
        {
            var startGeoPos = _map.WorldToGeoPosition(startPoint.position);
            var endGeoPos = _map.WorldToGeoPosition(endPoint.position);

            Debug.Log($"Requesting route from {startGeoPos} to {endGeoPos}");

            string coordinates = $"{startGeoPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{startGeoPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture)};"
                               + $"{endGeoPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{endGeoPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

            // Build the request URL
            string accessToken = MapboxAccess.Instance.Configuration.AccessToken;
            string url = $"https://api.mapbox.com/directions/v5/mapbox/{(RoutingProf == RoutingProfiles.drivingTraffic ? "driving-traffic" : RoutingProf)}/{coordinates}"
                       + $"?language=el&steps=true{CreateExclusionString(ConvertStringCoordinatedToVector2d(exclusionPoints))}&geometries=polyline&access_token={accessToken}";

            UnityWebRequest webRequest = UnityWebRequest.Get(url);

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error getting directions: " + webRequest.error);
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;

                try
                {
                    // Deserialize using Mapbox's DirectionsResponse class
                    var response = JsonConvert.DeserializeObject<DirectionsResponse>(jsonResponse, JsonConverters.Converters);

                    HandleDirectionsResponse(response);

                    // Send route data to WebSocket server for Kafka publishing
                    PublishRouteToServer(response);
                }
                catch (JsonException ex)
                {
                    Debug.LogError("JSON Deserialization Error: " + ex.Message);
                }
            }
        }
        void HandleDirectionsResponse(DirectionsResponse response)
        {
            if (response == null || response.Routes.Count == 0)
            {
                Debug.LogError("Directions response is null or has no routes!");
                return;
            }

            _geometryPoints = response.Routes[0].Geometry;

            var routePositions = new List<Vector3>();
            foreach (var point in _geometryPoints)
            {
                var worldPos = _map.GeoToWorldPosition(point, true);
                routePositions.Add(worldPos);
            }

            var adjustedPositions = new List<Vector3>();
            foreach (var pos in routePositions)
            {
                adjustedPositions.Add(new Vector3(pos.x, pos.y + _routeLineYOffset, pos.z));
            }

            ClearRoute(false);
            // Store the FULL route in geo coordinates (never affected by map movement!)
            _fullRouteGeoPositions = new List<Vector2d>(_geometryPoints);
            DisplayRoute(adjustedPositions);
            _spawner.Spawn3DRoute();
            _directionIndicator.SetActive(true);

            // Reset navigation state
            currentWaypointIndex = 0;

            // Optional: Auto-start navigation
            StartNavigation();
        }

        private string CreateExclusionString(List<Vector2d> exclusionPoints)
        {
            if (exclusionPoints == null || exclusionPoints.Count == 0)
            {
                return "";
            }

            var sb = new System.Text.StringBuilder("&exclude=");

            for (int i = 0; i < exclusionPoints.Count; i++)
            {
                var point = exclusionPoints[i];
                sb.Append($"point({point.y.ToString(CultureInfo.InvariantCulture)} {point.x.ToString(CultureInfo.InvariantCulture)})");

                if (i < exclusionPoints.Count - 1)
                {
                    sb.Append(",");
                }
            }

            string exclusionString = sb.ToString();
            Debug.Log("Exclusion String: " + exclusionString);
            return exclusionString;
        }

        private List<Vector2d> ConvertStringCoordinatedToVector2d(List<string> exclusionPoints)
        {
            // Create a new list to hold the Vector2d points
            List<Vector2d> exclusionPointsVector2d = new List<Vector2d>();

            foreach (string coordString in exclusionPoints)
            {
                // Split the string by comma and remove any whitespace
                string[] parts = coordString.Split(',');

                if (parts.Length == 2)
                {
                    // Parse the latitude and longitude using InvariantCulture
                    if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double latitude) &&
                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double longitude))
                    {
                        // Create a Vector2d and add it to the list
                        Vector2d point = new Vector2d(latitude, longitude);
                        exclusionPointsVector2d.Add(point);
                    }
                    else
                    {
                        Debug.LogError($"Failed to parse coordinates: {coordString}");
                    }
                }
                else
                {
                    Debug.LogError($"Invalid coordinate format: {coordString}");
                }
            }
            return exclusionPointsVector2d;
        }

        /// <summary>
        /// Publishes the route data to the WebSocket server for Kafka publishing
        /// </summary>
        private void PublishRouteToServer(DirectionsResponse response)
        {
            if (!_publishToKafkaTopic)
            {
                return;
            }
            if (_webSocketConnection == null || response == null || response.Routes.Count == 0)
            {
                if (_webSocketConnection == null)
                    Debug.LogWarning("Cannot publish route: WebSocketConnection is null");
                return;
            }

            try
            {
                var route = response.Routes[0];

                // Convert geometry points to GeoJSON coordinate array [longitude, latitude]
                var coordinates = new List<double[]>();
                foreach (var point in route.Geometry)
                {
                    // GeoJSON standard: [longitude, latitude] order
                    coordinates.Add(new double[] { point.y, point.x });
                }

                // Get start and end coordinates
                var startGeoPos = _map.WorldToGeoPosition(startPoint.position);
                var endGeoPos = _map.WorldToGeoPosition(endPoint.position);

                var routeData = new
                {
                    type = "routePublish",
                    unityId = _webSocketConnection.uniqueID,
                    timestamp = System.DateTime.UtcNow.ToString("o"),
                    routeInfo = new
                    {
                        profile = RoutingProf.ToString(),
                        distance = route.Distance, // in meters
                        startPoint = new
                        {
                            latitude = startGeoPos.x,
                            longitude = startGeoPos.y
                        },
                        endPoint = new
                        {
                            latitude = endGeoPos.x,
                            longitude = endGeoPos.y
                        },
                        // GeoJSON LineString format for direct map rendering
                        geometry = new
                        {
                            type = "LineString",
                            coordinates
                        },
                        waypointCount = coordinates.Count
                    }
                };

                // Send to WebSocket server
                _webSocketConnection.SendJsonToServer(routeData);
                Debug.Log($"Route published to server: {coordinates.Count} waypoints, {route.Distance}m distance, DeviceID={_webSocketConnection.uniqueID}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error publishing route to server: {ex.Message}");
            }
        }

        public void ClearRoute(bool clearEndPoint)
        {
            // Only notify server if there's actually a route to clear
            bool hasActiveRoute = _geometryPoints != null && _geometryPoints.Count > 0;

            // ONLY send WebSocket messages if we're NOT quitting the application
            // This prevents background thread issues during shutdown
            if (!isApplicationQuitting && _webSocketConnection != null && clearEndPoint && hasActiveRoute && _publishToKafkaTopic)
            {
                try
                {
                    var routeClearData = new
                    {
                        type = "routeClear",
                        unityId = _webSocketConnection.uniqueID,  // Device identifier identifies the route
                        timestamp = System.DateTime.UtcNow.ToString("o"),
                        reason = isNavigating ? "cancelled" : "cleared"
                    };

                    _webSocketConnection.SendJsonToServer(routeClearData);
                    Debug.Log($"Route clear notification sent to server (DeviceID={_webSocketConnection.uniqueID}, reason: {(isNavigating ? "cancelled" : "cleared")})");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not send route clear message: {ex.Message}");
                }
            }

            // Stop any active navigation
            StopNavigation();

            //Clear Map route
            if (_routeGO != null)
                Destroy(_routeGO);

            //Clear 3D route
            foreach (var point in RoutePoints)
            {
                if (point != null)
                    Destroy(point);
            }

            if (_routeMaterial != null)
            {
                Destroy(_routeMaterial);
                _routeMaterial = null;
            }

            // Clear progressive rendering references
            _routeLineRenderer = null;
            _fullRouteGeoPositions = null;

            // Clear cached lists to free memory
            _cachedFullRouteWorldPositions.Clear();
            _cachedRemainingRoute.Clear();
            _cachedLocalPositions.Clear();

            RoutePoints.Clear();
            if (_directionIndicator != null)
                _directionIndicator.SetActive(false);
            routeWorldPositions.Clear();

            if (clearEndPoint)
                endPoint = null;
        }

        void OnApplicationQuit()
        {
            // CRITICAL: Prevent any WebSocket operations during shutdown
            isApplicationQuitting = true;

            // Note: Route clear on app close is now handled in WebSocketConnection.OnApplicationQuit()
            // This ensures the message is sent BEFORE the WebSocket connection is closed
            Debug.Log("[RoutingManager] OnApplicationQuit - route clear handled by WebSocketConnection");
        }

        private bool isApplicationQuitting = false;

        void OnDestroy()
        {
            // Clean up singleton instance
            if (Instance == this)
            {
                Instance = null;
            }

            // Stop any running coroutines first
            StopAllCoroutines();

            // Only send ClearRoute messages if NOT quitting the application
            // This prevents background thread issues during shutdown
            if (!isApplicationQuitting)
            {
                ClearRoute(true);
            }
            else
            {
                // Just cleanup locally without sending network messages
                isNavigating = false;
                navigationCoroutine = null;
            }

            // Clean up routes and materials
            if (_routeGO != null)
                Destroy(_routeGO);

            foreach (var point in RoutePoints)
            {
                if (point != null)
                    Destroy(point);
            }

            if (_routeMaterial != null)
            {
                Destroy(_routeMaterial);
                _routeMaterial = null;
            }

            // Unsubscribe from all events
            if (_webSocketConnection != null)
            {
                _webSocketConnection.OnHardcodedDataReceived -= null;
            }

            // Clear all lists to free memory
            RoutePoints.Clear();
            routeWorldPositions.Clear();
            _cachedFullRouteWorldPositions?.Clear();
            _cachedRemainingRoute?.Clear();
            _cachedLocalPositions?.Clear();
            _fullRouteGeoPositions?.Clear();
            _geometryPoints?.Clear();

            _routeLineRenderer = null;
            _webSocketConnection = null;
        }
    }
}