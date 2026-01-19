using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WebSocketSharp;
using TMPro;
using System.Globalization;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;

public class GPSWebSocketSender : MonoBehaviour
{
    public TextMeshProUGUI jsonText;

    [Header("Login Handler")]
    public LoginHandler loginHandler;
    public UIManager uiManager;

    [Header("UI TextMeshPro Components")]
    public TMP_Text ipText;
    public TMP_Text frequencyText;

    // Interval for sending GPS signals (in seconds)
    public float gpsSignalInterval = 5f;
    // WebSocket server URL
    public string serverUrl = "ws://yourserveraddress:port";

    private WebSocket ws;
    public bool isConnected = false;
    private bool locationReady = false;
    private bool sending = false;
    private Coroutine sendingCoroutine = null;

    public TMP_Text debugText;

    // Queue to marshal actions onto the main thread.
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    // Start is called before the first frame update.
    void Start()
    {
        // Start the location service when the app starts.
        StartCoroutine(StartLocationService());
    }

    void Update()
    {
        // Execute queued actions on the main thread.
        while (true)
        {
            Action action = null;
            lock (mainThreadActions)
            {
                if (mainThreadActions.Count > 0)
                    action = mainThreadActions.Dequeue();
            }
            if (action == null)
                break;
            action.Invoke();
        }
    }

    // Helper to queue actions for execution on the main thread.
    void EnqueueOnMainThread(Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    // Called by a UI event to update the frequency from the frequencyText component.
    public void OnFrequencyChanged()
    {
        string freqValue = frequencyText.text; // Fixed: use frequencyText.text here
        freqValue = freqValue.Replace("\u200B", "");
        Debug.Log("FREQ: " + freqValue);
        if (float.TryParse(freqValue, out float number))
        {
            gpsSignalInterval = number;
            Debug.Log("GPS update frequency updated to: " + gpsSignalInterval + " seconds");
            debugText.text = "GPS update frequency updated to: " + gpsSignalInterval + " seconds";
        }
        else
        {
            Debug.LogWarning("Invalid frequency input: " + frequencyText.text);
            debugText.text = "Invalid frequency input: " + frequencyText.text;
        }
    }

    IEnumerator StartLocationService()
    {
        // Check if the user has enabled location services.
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("Location services are not enabled by the user.");
            debugText.text = "Location services are not enabled by the user.";
            yield break;
        }

        // Start the location service.
        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }
        if (maxWait < 1)
        {
            Debug.Log("Timed out while initializing location services.");
            debugText.text = "Timed out while initializing location services.";
            yield break;
        }
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("Failed to obtain location.");
            debugText.text = "Failed to obtain location.";
            yield break;
        }

        locationReady = true;
        Debug.Log("Location service is ready.");
        debugText.text = "Location service is ready.";
    }

    void ConnectToWebSocket()
    {
        string ipValue = ipText.text;
        ipValue = ipValue.Replace("\u200B", "");

        if (ipValue.EndsWith(":8080"))
        {
            ws = new WebSocket("ws://" + ipValue);
        }
        else
        {
            ws = new WebSocket("ws://" + ipValue + ":8080");
        }

        // Attach event handlers.
        ws.OnOpen += OnWebSocketOpen;
        ws.OnMessage += OnMessageReceived;
        ws.OnError += OnWebSocketError;
        ws.OnClose += OnWebSocketClose;

        ws.ConnectAsync();
    }

    private void OnMessageReceived(object sender, MessageEventArgs e)
    {
        EnqueueOnMainThread(() =>
        {
            HandleMessage(e.Data);
        });
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
                    case "connectionAck":
                        HandleAknowledgmentMessage(jsonObject);
                        break;

                    default:
                        Debug.Log($"Received unknown JSON message type: {messageType}");
                        break;
                }

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

            uiManager.MakeButtonInteractable((uiManager.openGPSButton).GetComponent<Button>());
            uiManager.ClosePanel(uiManager.introPanel);
            uiManager.OpenPanel(uiManager.mainPanel);

        }
        catch (Exception ex)
        {
            Debug.LogError("Error handling acknowledgment message: " + ex.Message);
        }
    }

    // Called when the WebSocket connection is established.
    private void OnWebSocketOpen(object sender, EventArgs e)
    {
        // Create a registration message
        var registrationMessage = new
        {
            type = "registration",
            deviceType = "phone",
            clientId = loginHandler.hololensID,
        };
        // Convert to JSON and send
        string jsonMessage = JsonConvert.SerializeObject(registrationMessage);
        SendMessageToServer(jsonMessage);

        EnqueueOnMainThread(() =>
            {
                Debug.Log("WebSocket connection established.");
                debugText.text = "WebSocket connection established.";
                isConnected = true;
            });
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
    // Called when there is an error.
    private void OnWebSocketError(object sender, EventArgs e)
    {
        Debug.Log("WebSocket error occurred.");
        debugText.text = "WebSocket error occurred.";
        isConnected = false;
        uiManager.OnServerDisconect();
    }

    // Called when the WebSocket connection is closed.
    private void OnWebSocketClose(object sender, EventArgs e)
    {
        EnqueueOnMainThread(() =>
        {
            Debug.Log("WebSocket connection closed.");
            debugText.text = "WebSocket connection closed.";
            isConnected = false;
            uiManager.OnServerDisconect();
            if (sending)
                StopGPSSending();

        });
    }

    // Optionally reconnect the WebSocket when the IP/URL changes.
    private void ReconnectToWebSocket()
    {
        if (ws != null && isConnected)
        {
            ws.Close();
        }
        ConnectToWebSocket();
    }

    // start sending GPS data.
    public void OpenConnection()
    {
        ConnectToWebSocket();
    }

    // Call this method (e.g., via a UI button) to stop sending GPS data.
    public void StopConnection()
    {
        print(isConnected);
        if (isConnected)
        {
            ws.Close();
            uiManager.OnServerDisconect();
            sendingCoroutine = null;
            Debug.Log("Not connected.");
            debugText.text = "Not connected.";
        }
        else
        {
            uiManager.OnServerDisconect();

            Debug.Log("The device is not connected to the server.");
            debugText.text = "The device is not connected to the server.";
        }
    }

    // start sending GPS data.
    public void StartGPSSending()
    {
        sending = true;
#if UNITY_EDITOR
        sendingCoroutine = StartCoroutine(SendFakeGPSData());
#else
        sendingCoroutine = StartCoroutine(SendGPSData());
#endif
        Debug.Log("Sending GPS data...");
        debugText.text = "Sending GPS data...";
    }

    // stop sending GPS data.
    public void StopGPSSending()
    {
        StopCoroutine(sendingCoroutine);
        sending = false;
        Debug.Log("Stopped sending GPS data.");
        debugText.text = "Stopped sending GPS data.";
    }

    IEnumerator SendGPSData()
    {
        while (true)
        {
            if (isConnected && locationReady)
            {
                // Retrieve the phone's actual GPS data
                LocationInfo loc = Input.location.lastData;

                // Create JSON object
                var gpsMessage = new
                {
                    type = "gps",
                    senderId = loginHandler.hololensID,
                    lat = loc.latitude,
                    lon = loc.longitude,
                    data = "Live GPS data from mobile device"
                };

                // Convert to JSON and send
                string jsonMessage = JsonConvert.SerializeObject(gpsMessage);
                SendMessageToServer(jsonMessage);

                Debug.Log($"Sent GPS: Lat {loc.latitude}, Long {loc.longitude}");
                debugText.text = $"Sent GPS: Lat {loc.latitude}, Long {loc.longitude}";
            }
            else
            {
                Debug.Log("WebSocket not connected or location services not running.");
                debugText.text = "WebSocket not connected or location services not running.";
            }

            yield return new WaitForSeconds(gpsSignalInterval);
        }
    }

    //send fake static gps data.
    IEnumerator SendFakeGPSData()
    {
        // Hardcoded coordinates
        float fakeLat = 38.246639f;
        float fakeLong = 21.735222f;

        while (true)
        {
            if (isConnected)
            {
                // Create proper JSON object matching server expectations
                var gpsMessage = new
                {
                    type = "gps",
                    senderId = loginHandler.hololensID,
                    lat = fakeLat,
                    lon = fakeLong,
                    data = "Fake GPS data from mobile device"
                };

                // Convert to JSON and send
                string jsonMessage = JsonConvert.SerializeObject(gpsMessage);
                SendMessageToServer(jsonMessage);

                Debug.Log($"Sent fake GPS: Lat {fakeLat}, Long {fakeLong}");
                debugText.text = $"Sent fake GPS: Lat {fakeLat}, Long {fakeLong}";
            }
            else
            {
                Debug.Log("WebSocket not connected. Cannot send fake GPS data.");
                debugText.text = "WebSocket not connected. Cannot send fake GPS data.";
            }

            yield return new WaitForSeconds(gpsSignalInterval);
        }
    }

    void OnApplicationQuit()
    {
        if (ws != null)
        {
            ws.Close();
        }
    }
}
