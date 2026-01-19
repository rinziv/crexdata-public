using System.Collections;
using WebSocketSharp;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Reflection;
using System.Linq;
using Mapbox.Unity.Location;
using Mapbox.Unity.Map;
using System.Globalization;
using Mapbox.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class WebSocketConnection : MonoBehaviour
{
    WebSocket ws;
    public string ip;
    public bool isApplicationQuitting = false;
    public int attempt = 0;

    public bool customID = false;
    public string uniqueID;
    public TextMeshPro uniqueIDText;

    public SystemSetupHandler systemSetupHandler;
    public PairingManager pairingManager;

    public FirefighterLocator firefighterLocator;
    public POIManager poiManager;
    public FireVisualizer fireVisualizer;
    public FireSimulationLoader fireSimulationManager;

    public event Action<string, string> OnPairingStatusReceived; // (status, phoneId)
    public event Action<string> OnPhoneLostReceived; // (phoneId)
    public event Action<string> OnPairedReceived; // (phoneId)

    NetworkReachability previousReachability;

    void Start()
    {
        previousReachability = Application.internetReachability;

        if (!customID)
            uniqueID = null;

        ConnectToServer();


    }

    // This method is responsible for connecting to the WebSocket server.
    void ConnectToServer()
    {
        // Initialize the WebSocket connection to the specified URL.
        string ipValue = ip;
        ipValue = ipValue.Replace("\u200B", "");
        
        if (ipValue.EndsWith(":8080"))
        {
            ws = new WebSocket("ws://" + ipValue);
        }
        else
        {
            print("IP: " + "ws://" + ipValue + ":8080");
            ws = new WebSocket("ws://" + ipValue + ":8080");
        }

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

    #region WS Events
    // This method is called when the connection is opened.
    void OnConnectionOpened(object sender, EventArgs e)
    {
        // Create a registration message
        var registrationMessage = new
        {
            type = "registration",
            deviceType = "unity",
            clientId = uniqueID
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

        attempt = 0;
    }
    // This method is called when a message is received from the server.
    void OnMessageReceived(object sender, MessageEventArgs e)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            HandleMessage(e.Data);
        });
    }
    // This method is called when the connection is closed.
    async void OnConnectionClosed(object sender, CloseEventArgs e)
    {
        Debug.Log($"Connection closed. Attempting to reconnect. Total attempts: {++attempt}");

        // Wait for a delay before attempting to reconnect. The delay increases with each failed attempt.
        int delay = 3000;
        await Task.Delay(delay);

        // Check if the application is quitting before attempting to reconnect.
        if (!isApplicationQuitting)
            ConnectToServer();
    }
    #endregion

    #region Message Handling
    void HandleMessage(string message)
    {
        try
        {
            // Try to parse as JSON first
            JObject jsonObject = JObject.Parse(message);
            string messageType = jsonObject["type"]?.ToString();
            JObject payload = (JObject)jsonObject["payload"];

            if (messageType != null)
            {
                // Handle JSON messages by type
                switch (messageType)
                {
                    case "connectionAck":
                        HandleAknowledgmentMessage(jsonObject);
                        break;

                    case "registrationResult":
                        string registrationStatus = jsonObject["status"].ToString();
                        uniqueID = jsonObject["clientId"].ToString();
                        uniqueIDText.text = "Device ID: " + uniqueID;
                        string registrationMessage = jsonObject["message"].ToString();
                        Debug.Log($"Registration result: {registrationStatus}, " +
                                  $"Message: {registrationMessage}");

                        RequestPairing(uniqueID);

                        SendJsonToServer(new
                        {
                            type = "getStaticPoisFire"
                        });

                        break;

                    case "gps":
                        HandleGpsMessage(jsonObject);
                        break;

                    case "addPOI":

                        String poiCase = payload["case"].ToString();
                        if (poiCase == null)
                            break;

                        switch (poiCase)
                            {
                                case "POI":
                                    poiManager.AddPOI(
                                        payload["properties"]["name"].ToString(),
                                        payload["position"]["latitude"].Value<double>(),
                                        payload["position"]["longitude"].Value<double>(),
                                        payload["poiType"].ToString()
                                    );
                                    break;

                                case "fireLocation":
                            
                                    fireVisualizer.AddFireData(
                                        payload["latitude"].Value<float>(),
                                        payload["longitude"].Value<float>(),
                                        payload["acq_date"].ToString(),
                                        payload["acq_time"].ToString(),
                                        payload["brightness"].Value<float>(),
                                        payload["frp"].Value<float>()
                                    );

                                    Debug.Log(new Vector2d(
                                        payload["latitude"].Value<double>(),
                                        payload["longitude"].Value<double>()
                                    ));

                                    fireSimulationManager.fireLocation = new Vector2d(
                                        payload["latitude"].Value<double>(),
                                        payload["longitude"].Value<double>()
                                    );
                                    break;

                                case "teamMember":
                                    firefighterLocator.UpdateFirefighterPosition(
                                        payload["id"].Value<int>(),
                                        payload["name"].ToString(),
                                        new Vector2d(payload["latitude"].Value<double>(), payload["longitude"].Value<double>())
                                    );
                                    break;

                                default:
                                    Debug.Log($"Uknown case: {poiCase}");
                                    break;
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

                    default:
                        Debug.Log($"Received unknown JSON message type: {messageType}");
                        break;
                }

                /*
                // Display the JSON for debugging
                if (jsonText != null)
                {
                    DisplayJsonData(jsonObject);
                }
                */
                return; // Successfully handled as JSON
            }
        }
        catch (JsonReaderException)
        {
            Debug.Log($"Not a JSON message, using legacy handling: {message.Substring(0, Math.Min(50, message.Length))}...");
            // Not a valid JSON, continue with legacy handling
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

        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling acknowledgment message: " + ex.Message);
        }
    }

    void HandleGpsMessage(JObject jsonObject)
    {
        try
        {
            double latitude = jsonObject["lat"].Value<double>();
            double longitude = jsonObject["lon"].Value<double>();

            Debug.Log($"Received GPS: Lat {latitude}, Long {longitude}");

            GPSLocationProvider.Instance.UpdateGPS(latitude, longitude);
            systemSetupHandler.SetGPSConnected();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling GPS message: " + ex.Message);
        }
    }

    void HandleSpawnMessage(JObject jsonObject)
    {
        try
        {
            double latitude = jsonObject["position"]["latitude"].Value<double>();
            double longitude = jsonObject["position"]["longitude"].Value<double>();
            string poiType = jsonObject["poiType"].ToString();

            string spawnString = $"{latitude},{longitude},{poiType}";
            Debug.Log("Spawn string: " + spawnString);

            /*
            if (spawner != null)
            {
                spawner.SpawnPOIFromString(spawnString);
            }
            else
            {
                Debug.Log("Spawner is null");
            }*/ 
        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling spawn message: " + ex.Message);
        }
    }

    public void SendMessageToServer(string message)
    {
        try
        {
            if (ws.ReadyState == WebSocketState.Open)
            {
                ws.Send(message);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred when trying to send a message: " + ex.Message);
        }
    }

    public void SendJsonToServer(object data)
    {
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

    // This method is called when the application is quitting.
    void OnApplicationQuit()
    {
        // Set the flag to indicate that the application is quitting.
        isApplicationQuitting = true;
        if (ws != null && ws.IsAlive)
        {
            // Unsubscribe from the OnMessage event.
            ws.OnMessage -= OnMessageReceived;
            // Unsubscribe from the OnClose event.
            ws.OnClose -= OnConnectionClosed;
            ws.OnOpen -= OnConnectionOpened;
            ws.CloseAsync();
            ws = null;
            Debug.Log("WebSocket connection closed on application quit.");
        }

        Debug.Log("CYA");
    }
}