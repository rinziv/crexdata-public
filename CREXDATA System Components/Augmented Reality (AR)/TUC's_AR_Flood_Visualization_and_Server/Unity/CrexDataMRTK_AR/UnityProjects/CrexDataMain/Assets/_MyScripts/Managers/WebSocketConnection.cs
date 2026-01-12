using WebSocketSharp;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using System;
using System.Globalization;
using Mapbox.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Mapbox.Unity.MeshGeneration.Factories;

public class WebSocketConnection : MonoBehaviour
{
    [SerializeField] private bool _displayJsonInConsole = false;
    //Json textmesh pro
    public TextMeshProUGUI jsonText;
    WebSocket ws;
    public string ip;
    public bool isApplicationQuitting = false;
    public int attempt = 0;
    public string uniqueID;
    private string _deviceId;
    [SerializeField] private MapAndPlayerManager mapManager;
    //WaterController
    public WaterLevelController waterController;

    public GPSSpawner spawner;
    public PairingManager pairingManager;

    public event Action<string, string> OnPairingStatusReceived; // (status, phoneId)
    public event Action<string> OnPhoneLostReceived; // (phoneId)
    public event Action<string> OnPairedReceived; // (phoneId)
    public event Action<double, double, string> OnSpoofedGpsReceived; // <latitude, longitude>

    public event Action<JObject> OnHardcodedDataReceived; // (data)

    private Queue<string> _messageQueue = new Queue<string>();

    [Header("Latency Tracking")]
    private float _lastPingSent;
    private float _currentLatency = 0;
    private const float PING_INTERVAL = 3.0f;

    [Header("Debug - Thread Monitoring")]
    [Tooltip("Enable detailed WebSocket thread state logging")]
    [SerializeField] private bool enableThreadMonitoring = false;
    private System.Threading.Thread _websocketThread;

    // Hardcoded data storage
    private JObject _hardcodedDataObject;

    public JObject HardcodedDataRaw => _hardcodedDataObject;

    //MENTAL FATIGUE VARIABLES
    [SerializeField] private MessageHandler handler;

    void Start()
    {
        _deviceId = GetDeviceIdentifier();
        Debug.Log("Device ID: " + _deviceId);
    }

    void Update()
    {
        // Latency Ping Check
        if (ws != null && ws.ReadyState == WebSocketState.Open)
        {
            // Send ping every few seconds
            if (Time.time - _lastPingSent > PING_INTERVAL)
            {
                SendPing();
                //_lastPingSent = Time.time;
            }
        }
    }
    // This method is responsible for connecting to the WebSocket server.
    public void ConnectToServer()
    {
        // Initialize the WebSocket connection to the specified URL.
        ws = new WebSocket("ws://" + ip + ":8080");
        Debug.Log("Connecting to server...");
        // Subscribe to the OnOpen event. This event is triggered when the connection is opened.
        ws.OnOpen += OnConnectionOpened;

        // Subscribe to the OnMessage event. This event is triggered when a message is received from the server.
        ws.OnMessage += OnMessageReceived;

        // Subscribe to the OnClose event. This event is triggered when the connection is closed.
        ws.OnClose += OnConnectionClosed;

        // Connect to the server asynchronously.
        ws.ConnectAsync();
    }

    private void ProcessMessageQueue()
    {
        while (_messageQueue.Count > 0 && ws != null && ws.ReadyState == WebSocketState.Open)
        {
            string message = _messageQueue.Dequeue();
            ws.Send(message);
            Debug.Log("Sent queued message: " + message);
        }
    }
    #region WS Events

    public event EventHandler OnConnectionOpenedEvent;
    public event EventHandler<CloseEventArgs> OnConnectionClosedEvent;
    public event EventHandler OnMessageReceivedEvent;
    public event Action<string> OnUniqueIDReceivedEvent;
    //Create Event for when alert is recieved
    public event Action<string, string> OnAlertReceivedEvent;

    void OnConnectionOpened(object sender, EventArgs e)
    {
        // Track WebSocket thread
        if (enableThreadMonitoring)
        {
            _websocketThread = System.Threading.Thread.CurrentThread;
            Debug.Log($"[WebSocket-Thread] ✅ Connection opened on thread ID: {_websocketThread.ManagedThreadId}, Name: {_websocketThread.Name ?? "Unnamed"}");
        }

        // Create a registration message
        var registrationMessage = new
        {
            type = "registration",
            deviceType = "unity",
            useCase = "floodManagement",
            clientId = uniqueID,
            deviceId = _deviceId
        };

        // Convert to JSON and send
        string jsonMessage = JsonConvert.SerializeObject(registrationMessage);
        SendMessageToServer(jsonMessage);

        if (attempt > 0)
        {
            Debug.Log("Reconnected to the server.");
            SendMessageToServer("Recome");
        }
        else
        {
            Debug.Log("Connected to the server");
            SendMessageToServer("Come");
        }

        ProcessMessageQueue();

        attempt = 0;

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            OnConnectionOpenedEvent?.Invoke(this, e);
        });
    }
    void OnMessageReceived(object sender, MessageEventArgs e)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            OnMessageReceivedEvent?.Invoke(this, EventArgs.Empty);
            HandleMessage(e.Data);
        });
    }
    async void OnConnectionClosed(object sender, CloseEventArgs e)
    {
        // CRITICAL: Stop immediately if application is quitting
        // This prevents background thread from trying to reconnect during shutdown
        if (isApplicationQuitting)
        {
            if (enableThreadMonitoring)
            {
                Debug.Log($"[WebSocket-Thread] ⏹️ Connection closed during app quit on thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            }
            Debug.Log("Application is quitting - skipping reconnection attempt");
            return;
        }

        if (enableThreadMonitoring)
        {
            Debug.Log($"[WebSocket-Thread] 🔌 Connection closed on thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        }

        Debug.Log($"Connection closed. Attempting to reconnect. Total attempts: {++attempt}");

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            OnConnectionClosedEvent?.Invoke(this, e);
        });

        // Wait for a delay before attempting to reconnect.
        int delay = 3000;

        if (enableThreadMonitoring)
        {
            Debug.Log($"[WebSocket-Thread] ⏳ Waiting {delay}ms before reconnect attempt on thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        }

        await Task.Delay(delay);

        // Check AGAIN if the application is quitting before attempting to reconnect
        // This check happens after the 3 second delay
        if (!isApplicationQuitting && ws != null)
        {
            if (enableThreadMonitoring)
            {
                Debug.Log($"[WebSocket-Thread] 🔄 Reconnecting on thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            }
            ConnectToServer();
        }
        else
        {
            if (enableThreadMonitoring)
            {
                Debug.Log($"[WebSocket-Thread] ❌ Reconnect aborted on thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId} (quitting={isApplicationQuitting}, ws={(ws == null ? "null" : "exists")})");
            }
            Debug.Log("Application quit during reconnection delay - aborting reconnect");
        }
    }

    #endregion

    #region Message Handling
    void HandleLegacyMessage(string message)
    {
        if (message == "Readys")
        {
            Debug.Log("Ready!");
        }

        if (message.Contains("gps"))
        {
            double latitude = double.Parse(message.Split(',')[0], CultureInfo.InvariantCulture);
            double longitude = double.Parse(message.Split(',')[1], CultureInfo.InvariantCulture);


            try
            {
                //if (!spawner.referenceCoordinatesSet)
                spawner.UpdateReferenceCoordinates(latitude, longitude);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error updating reference coordinates: " + e.Message);
            }

            mapManager.SetCoordinates(new Vector2d(latitude, longitude));
            mapManager.UpdatePlayerLocation(new Vector2d(latitude, longitude));
        }
        if (message.Contains("info"))
        {
            //format the message
            string rawMessage;
            try
            {
                rawMessage = RawMessage(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("An error occurred when formatting the message: " + ex.Message);
                rawMessage = message;
            }
            //Debug.Log(message);
            if (rawMessage.Contains("spawn"))
            {
                Debug.Log("Spawn section handling: " + rawMessage);
                // This assumes that the latitude and longitude are part of the 'raw_message'
                string[] spawnParts = rawMessage.Split(',');
                //check if spawnParts is the correct length
                if (spawnParts.Length < 3)
                {
                    Debug.LogWarning("Invalid spawn message: " + rawMessage);
                    return;
                }
                string spawnString = spawnParts[0] + "," + spawnParts[1] + "," + spawnParts[2];
                Debug.Log("Spawn string: " + spawnString);

                if (spawner != null)
                {
                    try
                    {
                        spawner.SpawnPOIFromString(spawnString);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("An error occurred when spawning the object: " + ex.Message);
                    }
                }
                else
                    Debug.Log("Spawner is null");
            }
            Debug.Log("Info section handling: " + message);
        }
        if (message.Contains("water"))
        {
            Debug.Log("Water level: " + message);
            string[] parts = message.Split(',');
            float[] waterHeights = new float[3];
            // Parse the first 3 parts of the message as water heights
            // Parse the first three values to floats
            for (int i = 0; i < 3; i++)
            {
                if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
                {
                    waterHeights[i] = result / 100;
                }
                else
                {
                    Debug.LogError($"Failed to parse '{parts[i]}' as float.");
                    waterHeights[i] = 0f; // Assign a default value or handle error as needed
                }
            }
            Debug.Log("Water heights: " + string.Join(", ", waterHeights));
            waterController.UpdateWaterHeights(waterHeights);
        }
    }
    void HandleMessage(string message)
    {
        try
        {
            // Try to parse as JSON first
            JObject jsonObject = JObject.Parse(message);
            string messageType = jsonObject["type"]?.ToString();

            if (messageType != null)
            {
                // Handle JSON messages by type
                switch (messageType)
                {
                    case "pong":
                        try
                        {
                            //float sentTime = jsonObject["timestamp"].Value<float>();
                            _currentLatency = (Time.time - _lastPingSent) * 1000f; // Convert to milliseconds
                            //Debug.Log($"Current latency: {_currentLatency:F1} ms");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error parsing pong message: {ex.Message}");
                        }
                        break;

                    case "connectionAck":
                        HandleAknowledgmentMessage(jsonObject);
                        break;

                    case "registrationResult":
                        string registrationStatus = jsonObject["status"].ToString();
                        uniqueID = jsonObject["clientId"].ToString();
                        OnUniqueIDReceivedEvent?.Invoke(uniqueID);
                        string registrationMessage = jsonObject["message"].ToString();

                        Debug.Log($"Registration result: {registrationStatus}, " +
                                  $"Message: {registrationMessage}");

                        RequestPairing();
                        break;

                    case "gps":
                        HandleGpsMessage(jsonObject);
                        break;

                    case "water":
                        HandleWaterMessage(jsonObject);
                        break;

                    case "hardcodedData":
                        HandleHardcodedDataMessage(jsonObject);
                        break;

                    case "spawnPOI":
                        HandleSpawnMessage(jsonObject);
                        break;

                    case "spoofedGps":
                        HandleSpoofedGpsMessage(jsonObject);
                        break;

                    case "routingProfile":
                        try
                        {
                            string profileString = jsonObject["profile"]?.ToString();
                            if (string.IsNullOrEmpty(profileString))
                            {
                                Debug.LogWarning("Routing profile is null or empty, defaulting to 'driving'");
                                RoutingManager.RoutingProf = RoutingManager.RoutingProfiles.driving;
                            }
                            else if (Enum.TryParse<RoutingManager.RoutingProfiles>(profileString, true, out var parsedProfile))
                            {
                                RoutingManager.RoutingProf = parsedProfile;
                                Debug.Log($"Routing profile set to: {parsedProfile}");
                            }
                            else
                            {
                                Debug.LogWarning($"Invalid routing profile '{profileString}', defaulting to 'driving'. Valid options: driving, walking, cycling, drivingTraffic");
                                RoutingManager.RoutingProf = RoutingManager.RoutingProfiles.driving;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error parsing routing profile: {ex.Message}, defaulting to 'driving'");
                            RoutingManager.RoutingProf = RoutingManager.RoutingProfiles.driving;
                        }
                        break;

                    case "pairingStatus":
                        string status = jsonObject["status"].ToString();
                        string statusPhoneId = jsonObject["phoneId"].ToString();
                        OnPairingStatusReceived?.Invoke(status, statusPhoneId);
                        break;

                    case "paired":
                        string pairedWith = jsonObject["pairedWith"].ToString();
                        OnPairedReceived?.Invoke(pairedWith);
                        break;

                    case "phoneLost":
                        string lostPhoneId = jsonObject["phoneId"].ToString();
                        OnPhoneLostReceived?.Invoke(lostPhoneId);
                        break;

                    case "staticPoisResult":
                        Debug.Log("Static POI result: " + message);
                        break;

                    case "kafkaStatus":
                        string kafkaStatus = jsonObject["status"].ToString();
                        string kafkaConfig = jsonObject["configKey"].ToString();
                        string reason = kafkaStatus == "error" ? jsonObject["reason"].ToString() : "No reason";
                        string topic = kafkaStatus == "ready" ? jsonObject["topic"].ToString() : "No topic";

                        Debug.Log($"Kafka status: {kafkaStatus}, " +
                                  $"Config Key: {kafkaConfig}, " +
                                  $"Reason: {reason}, " +
                                  $"Topic: {topic}");
                        break;

                    case "deletePOI":
                        string poiIdToDelete = (string)jsonObject["poiId"];
                        if (!string.IsNullOrEmpty(poiIdToDelete))
                        {
                            spawner.DeletePOI(poiIdToDelete);
                        }
                        break;

                    case "alert":
                        string alertType = jsonObject["alertType"]?.ToString();
                        string metadata = jsonObject["meta"]?.ToString();
                        string alertId = jsonObject["alertID"]?.ToString();

                        // Trigger the alert received event
                        OnAlertReceivedEvent?.Invoke(alertType, alertId);
                        //Debug.Log($"Alert received: {alertType}, Metadata: {metadata}");

                        break;

                    case "mental":
                        HandleMentalMessage(jsonObject);
                        break;

                    default:
                        Debug.Log($"Received unknown JSON message type: {messageType}");
                        break;
                }

                // Display the JSON for debugging
                if (_displayJsonInConsole)
                {
                    DisplayJsonData(jsonObject);
                }

                return; // Successfully handled as JSON
            }
        }
        catch (JsonReaderException)
        {
            Debug.Log($"Not a JSON message, using legacy handling: {message.Substring(0, Math.Min(50, message.Length))}...");
            // Not a valid JSON, continue with legacy handling
        }

        HandleLegacyMessage(message);
    }

    void HandleMentalMessage(JObject jsonObject)
    {
        UserMessage msg = null;
        try
        {
            var payload = jsonObject["payload"];
            if (payload != null)
            {
                string timestamp = payload["last_real_datetime"]?.ToString();
                string mentalState = payload["label"]?.ToString();
                string raspberryId = payload["raspberryID"]?.ToString();

                Debug.Log($"Mental state received: {mentalState} at {timestamp} from {raspberryId}");
                msg = new UserMessage(raspberryId, mentalState, timestamp);
                handler.recieveMessage(msg);
            }
            else
            {
                Debug.LogWarning("Mental message has no payload");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling mental message: " + ex.Message);
        }
    }

    void HandleHardcodedDataMessage(JObject jsonObject)
    {
        try
        {
            // The "data" field is a JObject, not a JArray
            JObject dataObject = (JObject)jsonObject["data"];

            if (dataObject == null)
            {
                Debug.LogError("Hardcoded data object is null");
                return;
            }

            // Raw JSON
            _hardcodedDataObject = dataObject;

            // Invoke event with the data object
            OnHardcodedDataReceived?.Invoke(dataObject);

            var phoneGPSObject = _hardcodedDataObject["spoofedGPSCoordinatesForPhone"];
            if (phoneGPSObject != null && phoneGPSObject is JObject)
            {
                HandleSpoofedGpsMessage((JObject)phoneGPSObject);
            }
            else
            {
                Debug.LogWarning("spoofedGPSCoordinatesForPhone is missing or invalid in hardcoded data");
            }

            HandleWaterMessage(dataObject);

        }
        catch (Exception ex)
        {
            Debug.LogError($"Error handling hardcoded data message: {ex.Message}\nStack trace: {ex.StackTrace}");
        }
    }
    void HandleAknowledgmentMessage(JObject jsonObject)
    {
        try
        {
            // Extract all fields with null checking
            string message = jsonObject["message"]?.ToString() ?? "No message";
            string connectionId = jsonObject["connectionId"]?.ToString() ?? "Unknown";
            string userAgent = jsonObject["userAgent"]?.ToString() ?? "Unknown";
            string ipAddress = jsonObject["ipAddress"]?.ToString() ?? "Unknown";
            string connectionTime = jsonObject["connectionTime"]?.ToString() ?? "Unknown";

            Debug.Log($"Connection acknowledgment received:\n" +
                      $"Message: {message}\n" +
                      $"Connection ID: {connectionId}\n" +
                      $"User Agent: {userAgent}\n" +
                      $"IP Address: {ipAddress}\n" +
                      $"Connection Time: {connectionTime}");

            // Formated string for UI if needed later***
            if (jsonText != null)
            {
                string formattedInfo = $"Connection Details:\n" +
                                       $"• Message: {message}\n" +
                                       $"• Connection ID: {connectionId}\n" +
                                       $"• User Agent: {userAgent}\n" +
                                       $"• IP Address: {ipAddress}\n" +
                                       $"• Connection Time: {connectionTime}";

                jsonText.text = formattedInfo;
            }

            var requestHardcodedData = new
            {
                type = "requestHardcodedData",
                unityId = uniqueID
            };
            SendJsonToServer(requestHardcodedData);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling acknowledgment message: " + ex.Message);
        }
    }
    void RequestPairing()
    {
        if (pairingManager != null)
        {
            pairingManager.StartPairing();
        }
        else
        {
            Debug.LogWarning("PairingManager not found in the scene.");
        }
    }
    void HandleGpsMessage(JObject jsonObject)
    {
        try
        {
            double latitude = jsonObject["location"]["latitude"].Value<double>();
            double longitude = jsonObject["location"]["longitude"].Value<double>();

            Debug.Log($"Received GPS: Lat {latitude}, Long {longitude}");

            try
            {
                if (spawner != null && !spawner.referenceCoordinatesSet)
                {
                    spawner.UpdateReferenceCoordinates(latitude, longitude);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error updating reference coordinates: " + e.Message);
            }

            //mapManager.SetCoordinates(new Vector2d(latitude, longitude));
            //mapManager.UpdatePlayerLocation(new Vector2d(latitude, longitude));
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling GPS message: " + ex.Message);
        }
    }
    void HandleWaterMessage(JObject jsonObject)
    {
        try
        {
            JArray heightsArray = (JArray)jsonObject["waterHeightsForVisualization"];
            float[] waterHeights = new float[3];

            for (int i = 0; i < Math.Min(3, heightsArray.Count); i++)
            {
                waterHeights[i] = heightsArray[i].Value<float>();
            }

            Debug.Log("Water heights: " + string.Join(", ", waterHeights));
            waterController.UpdateWaterHeights(waterHeights);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling water message: " + ex.Message);
        }
    }
    void HandleSpoofedGpsMessage(JObject jsonObject)
    {
        try
        {
            var payload = jsonObject["payload"];
            var metadata = jsonObject["meta"];
            string usecase = null;
            if (metadata != null)
            {
                usecase = metadata["usecase"]?.ToString();
            }
            if (payload != null)
            {
                double latitude = payload["latitude"].Value<double>();
                double longitude = payload["longitude"].Value<double>();

                OnSpoofedGpsReceived?.Invoke(latitude, longitude, usecase);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling spoofed GPS message: " + ex.Message);
        }
    }

    void HandleSpawnMessage(JObject jsonObject)
    {
        try
        {
            // Extract POI info payload
            JObject payload = (JObject)jsonObject["payload"];

            if (payload == null)
            {
                Debug.LogError("Spawn POI message has no payload");
                return;
            }

            if (spawner != null)
            {
                var spawnMethod = spawner.GetType().GetMethod("SpawnPOIFromJson");
                if (spawnMethod != null)
                {
                    // Use enhanced method that preserves all metadata
                    string payloadJson = payload.ToString(Formatting.None);
                    Debug.Log($"Spawning POI using JSON data");
                    spawner.SpawnPOIFromJson(payloadJson);
                    return;
                }

                // Fall back to basic string-based method
                double latitude = payload["position"]["latitude"].Value<double>();
                double longitude = payload["position"]["longitude"].Value<double>();
                string poiType = spawner.ParsePOIType(payload["poiType"]?.ToString()).ToString();

                string spawnString = $"{latitude},{longitude},{poiType}";
                spawner.SpawnPOIFromString(spawnString);
                Debug.Log($"Spawned POI using basic string data: {spawnString}");
            }
            else
            {
                Debug.LogWarning("Spawner is null, cannot spawn POI");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error handling Kafka POI message: {ex.Message}");
        }
    }

    public void SendInputMessage()
    {
        // Create a flood data request as JSON
        var floodRequest = new
        {
            type = "floodData",
            time = "23:59"
        };

        // Send as JSON
        SendMessageToServer(JsonConvert.SerializeObject(floodRequest));
    }
    public void SendMessageToServer(string message)
    {
        // CRITICAL: Don't send messages if application is quitting
        if (isApplicationQuitting)
        {
            Debug.LogWarning("Blocked message send - application is quitting");
            return;
        }

        if (ws != null && ws.ReadyState == WebSocketState.Open)
        {
            ws.Send(message);
        }
        else
        {
            Debug.LogWarning("WebSocket not connected. Queuing message: " + message);
            _messageQueue.Enqueue(message);
        }
    }

    public void SendJsonToServer(object data)
    {
        // CRITICAL: Don't send messages if application is quitting
        if (isApplicationQuitting)
        {
            Debug.LogWarning("Blocked JSON send - application is quitting");
            return;
        }

        string jsonMessage = JsonConvert.SerializeObject(data);
        SendMessageToServer(jsonMessage);
    }
    public void RequestPairing(string phoneId)
    {
        var pairRequest = new
        {
            type = "pair",
            unityId = uniqueID,
            phoneId = phoneId
        };

        SendJsonToServer(pairRequest);
    }

    public static string RawMessage(string input)
    {
        // Split the message by commas
        string[] parts = input.Split(',');

        // Known positions
        string prefix = parts[0] + "," + parts[1]; // "safran4,4"
        string suffix = parts[parts.Length - 2] + "," + parts[parts.Length - 1]; // "5,info"

        // Extract raw_message starting from index 2 to parts.Length - 3
        string rawMessage = string.Join(",", parts, 2, parts.Length - 4);

        // Reformat into the desired structure
        return rawMessage;
    }
    #endregion

    #region Latency Tracking
    private void SendPing()
    {
        if (ws != null && ws.ReadyState == WebSocketState.Open)
        {
            var pingMessage = new
            {
                type = "ping",
                //timestamp = Time.time
            };

            string jsonMessage = JsonConvert.SerializeObject(pingMessage);
            _lastPingSent = Time.time;
            SendMessageToServer(jsonMessage);
        }
    }
    public float GetLatency()
    {
        return _currentLatency;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Gets a safe device identifier that works in both Editor and Build
    /// </summary>
    private string GetDeviceIdentifier()
    {
#if UNITY_EDITOR
        // In Editor, use a combination of machine name and a persistent identifier
        string editorId = $"UnityEditor_{System.Environment.MachineName}_{SystemInfo.deviceModel}";
        return editorId.Replace(" ", "_");
#else
        // In Build, use the actual device unique identifier
        return SystemInfo.deviceUniqueIdentifier;
#endif
    }
    #endregion

    void DisplayJsonData(JObject json)
    {
        Debug.LogWarning(json.ToString(Formatting.Indented));
    }
    // This method is called when the application is quitting.
    void OnApplicationQuit()
    {
        Debug.Log("[WebSocket] OnApplicationQuit called - initiating shutdown");

        // Set flag FIRST to stop all async operations
        isApplicationQuitting = true;

        // Send final route clear BEFORE disconnecting (if WebSocket is still open)
        if (ws != null && ws.IsAlive && RoutingManager.HasActiveRoute() && RoutingManager.CanPublishToKafkaTopic())
        {
            try
            {
                var routeClearData = new
                {
                    type = "routeClear",
                    unityId = uniqueID,
                    timestamp = System.DateTime.UtcNow.ToString("o"),
                    reason = "appClosed"
                };

                string jsonMessage = JsonConvert.SerializeObject(routeClearData);
                ws.Send(jsonMessage); // Direct send, bypass SendMessageToServer (which checks isApplicationQuitting)
                Debug.Log($"[WebSocket] Final route clear sent on app quit (DeviceID={uniqueID})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebSocket] Could not send final route clear: {ex.Message}");
            }
        }

        // Small delay to ensure message is sent before disconnecting
        System.Threading.Thread.Sleep(100);

        DisconnectFromServer();
    }

    void DisconnectFromServer()
    {
        Debug.Log("[WebSocket] DisconnectFromServer called");

        if (enableThreadMonitoring)
        {
            Debug.Log($"[WebSocket-Thread] 🔌 Disconnect called on thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            if (_websocketThread != null)
            {
                Debug.Log($"[WebSocket-Thread] 📊 WebSocket thread state: IsAlive={_websocketThread.IsAlive}, IsBackground={_websocketThread.IsBackground}");
            }
        }

        if (ws != null)
        {
            try
            {
                // Unsubscribe from events FIRST to prevent callbacks during shutdown
                ws.OnMessage -= OnMessageReceived;
                ws.OnClose -= OnConnectionClosed;
                ws.OnOpen -= OnConnectionOpened;

                // Only attempt to close if websocket is alive
                if (ws.IsAlive)
                {
                    Debug.Log("[WebSocket] Closing WebSocket connection...");
                    ws.CloseAsync();
                }

                ws = null;
                Debug.Log("[WebSocket] WebSocket connection closed successfully");

                if (enableThreadMonitoring && _websocketThread != null)
                {
                    Debug.Log($"[WebSocket-Thread] ✅ WebSocket cleaned up, thread should terminate shortly");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebSocket] Exception during disconnect: {ex.Message}");
            }
        }

        // Clear message queue
        _messageQueue.Clear();
    }

    void OnDestroy()
    {
        Debug.Log("[WebSocket] OnDestroy called");

        // Ensure cleanup happens
        if (!isApplicationQuitting)
        {
            isApplicationQuitting = true;
            DisconnectFromServer();
        }
    }
}
