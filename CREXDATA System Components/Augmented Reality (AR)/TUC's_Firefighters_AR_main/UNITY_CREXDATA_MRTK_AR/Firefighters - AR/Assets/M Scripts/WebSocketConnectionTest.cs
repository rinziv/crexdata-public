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

public class WebSocketConnectionTest : MonoBehaviour
{
    public SystemSetupHandler systemSetupHandler;

    //Json textmesh pro
    WebSocket ws;
    public string ip;
    public bool isApplicationQuitting = false;
    public int attempt = 0;
    public string uniqueID;
    NetworkReachability previousReachability;

    void Start()
    {
        //spawner = GetComponent<GPSMulti>();

        previousReachability = Application.internetReachability;
        uniqueID = "34";
        ConnectToServer();
    }

    void SaveLastMessageInfo()
    {
        //SaveLoadManager.SaveLastMessages();
        Debug.Log("Saved last messages");
    }


    void Update()
    {
        if (Application.internetReachability != previousReachability)
        {
            previousReachability = Application.internetReachability;

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.Log("Lost WiFi connection.");
            }
            else if (Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
            {
                Debug.Log("WiFi connection restored.");
                if (ws.ReadyState != WebSocketState.Open)
                {
                    Debug.Log("Reconnecting to server...");
                }
            }
        }
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
        if (message == "Readys")
        {
            Debug.Log("Ready!");
        }

        if (message.Contains("gps"))
        {
            double latitude = double.Parse(message.Split(',')[0], CultureInfo.InvariantCulture);
            double longitude = double.Parse(message.Split(',')[1], CultureInfo.InvariantCulture);

            print("Latitude: " + latitude + " Longitude: " + longitude);

            try
            {
                GPSLocationProvider.Instance.UpdateGPS(latitude, longitude);
                systemSetupHandler.SetGPSConnected();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error updating reference coordinates: " + e.Message);
            }

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
        }
    }
    public void SendInputMessage()
    {
        //string message = inputField.text;
        // if (message == "ID")
        //{
        // SendMessageToServer(SystemInfo.deviceUniqueIdentifier);
        //}
        // else
        // {
        SendMessageToServer("flood1,23:59");
        // }
        // inputField.text = ""; // Clear the input field
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
    void DisplayJsonData(JObject json)
    {
        // Example: Display the JSON data in the Unity UI (e.g., using a Text component)
        // Assume you have a Text component in your scene called "JsonText"
        //jsonText.text = json.ToString(Formatting.Indented);

    }
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
