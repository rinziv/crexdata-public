using MixedReality.Toolkit.UX;
using System;
using System.Collections;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Mapbox.Unity.MeshGeneration.Factories;
using MixedReality.Toolkit.SpatialManipulation;
using TMPro; // 1. Import the namespace

public class RoboticPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField]
    private PressableButton _connectToRosDataStreamButton, _connectToRosDataStreamButton2;
    [SerializeField]
    private PressableButton _disconnectFromRosDataStreamButton, _disconnectFromRosDataStreamButton2;

    [Header("Visuals")]
    [SerializeField]
    private Renderer _statusIndicatorRenderer, _statusIndicatorRenderer2;
    [SerializeField]
    private string _connectedColorHex = "#4FF859"; // Default: Green
    [SerializeField]
    private string _disconnectedColorHex = "#B01D1D"; // Default: Red
    [SerializeField]
    private TextMeshProUGUI _detectionsText, _dangersText, _generalWarningsText;
    private short _detectionsCount, _dangersCount, _generalWarningsCount;

    [Header("ROS Connection")]
    private ROSConnection ros;
    private bool wasConnected = false;
    //public bool isConnecting = false;

    [SerializeField] private float connectionTimeoutSeconds = 10f;

    [Header("Dependencies")]
    public GPSSpawner gpsSpawner;

    [Header("Panel Controls")]
    [SerializeField] private PressableButton _pinPanelButton;
    [SerializeField] private GameObject[] _buttonsForConnectionAndActions;

    public GameObject roboticPanelForFirstResponder, roboticPanelForDroneOperator;
    [SerializeField] private PressableButton _roboticDataPanelsToggleButton;

    private DialogPool DialogPool;

    private enum RouteType { None, Road, StraightLine }
    private RouteType _lastRouteType = RouteType.None;
    private WebSocketConnection _webSocketConnection;

    #region Events
    public event Action RosConnected;
    public event Action RosDisconnected;
    #endregion

    void Awake()
    {
        if (DialogPool == null)
        {
            DialogPool = FindAnyObjectByType<DialogPool>();
        }
        if (_webSocketConnection == null)
        {
            _webSocketConnection = FindAnyObjectByType<WebSocketConnection>();
            _webSocketConnection.OnAlertReceivedEvent += HandleAlertReceived;
        }
    }

    private void Start()
    {
        SetUpPinButton();
        _connectToRosDataStreamButton.OnClicked.AddListener(OnConnectToRosDataStreamButtonPressed);
        _disconnectFromRosDataStreamButton.OnClicked.AddListener(OnDisconnectFromRosDataStreamButtonPressed);

        _connectToRosDataStreamButton2.OnClicked.AddListener(OnConnectToRosDataStreamButtonPressed);
        _disconnectFromRosDataStreamButton2.OnClicked.AddListener(OnDisconnectFromRosDataStreamButtonPressed);

        //Drone.OnPOIDrop += SpawnPOIAtDroneTransform;

        ros = ROSConnection.GetOrCreateInstance();

        UpdateConnectionStatusVisuals(IsRosConnected());
        wasConnected = IsRosConnected();


        EnableActionButtons(IsRosConnected());
    }

    private void SpawnPOIAtDroneTransform(Transform position, POIType poiType, Vector2 gpsCoordinates, string poiId)
    {
        // Make vector2 coordinates to string
        gpsSpawner.SpawnPOIAtSpecificTransform(position, poiType, gpsCoordinates, poiId);
    }

    private void Update()
    {
        bool isConnected = IsRosConnected();
        if (isConnected != wasConnected)
        {
            // Connection state changed
            if (isConnected)
            {
                // Connection established
                RosConnected?.Invoke();
                EnableActionButtons(true);
                UpdateConnectionStatusVisuals(true);
                Debug.Log("ROS Connection established successfully!");
            }
            else
            {
                // Connection lost
                ForceUntoggleButtons();
                EnableActionButtons(false);
                UpdateConnectionStatusVisuals(false);
                RosDisconnected?.Invoke();
                Debug.Log("ROS Connection lost!");
            }
            wasConnected = isConnected;
        }
    }
    public bool IsRosConnected()
    {
        // A connection is active if the connection thread is running and there are no reported errors.
        return ros.HasConnectionThread && !ros.HasConnectionError;
    }
    public bool IsRosConnecting()
    {
        // A connection is active if the connection thread is running and there are no reported errors.
        return ros.HasConnectionThread && ros.HasConnectionError;
    }
    private void OnDisconnectFromRosDataStreamButtonPressed()
    {
        if (IsRosConnected())
        {
            Debug.Log("Disconnecting from ROS.");
            ros.Disconnect();
        }
        else if (IsRosConnecting())
        {
            Debug.Log("Cancelling connection attempt...");
            ros.Disconnect();
            //isConnecting = false;
        }
        else
        {
            Debug.Log("Already disconnected from ROS.");
        }
    }
    private void OnConnectToRosDataStreamButtonPressed()
    {
        // Prevent multiple connection attempts
        if (IsRosConnected())
        {
            Debug.Log("Already connected to ROS.");
            return;
        }

        if (IsRosConnecting())
        {
            Debug.Log("Connection attempt already in progress. Please wait...");
            return;
        }

        Debug.Log("Connecting to ROS...");
        //isConnecting = true;

        try
        {
            // The ROSConnection component will attempt to connect using its settings.
            ros.Connect();
            // Start a coroutine to timeout the connection attempt
            //StartCoroutine(ConnectionTimeoutCoroutine());
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect to ROS data stream: {ex.Message}");
            //isConnecting = false;
        }
    }

    private void UpdateConnectionStatusVisuals(bool connected)
    {
        if (_statusIndicatorRenderer != null && _statusIndicatorRenderer2 != null)
        {
            Color color;
            if (connected)
            {
                ColorUtility.TryParseHtmlString(_connectedColorHex, out color);
            }
            else
            {
                ColorUtility.TryParseHtmlString(_disconnectedColorHex, out color);
            }
            _statusIndicatorRenderer.material.color = color;
            _statusIndicatorRenderer2.material.color = color;
        }
        else
        {
            Debug.LogWarning("Status indicator renderer is not assigned.");
        }
    }

    private void SetUpPinButton()
    {
        _pinPanelButton.IsToggled.OnEntered.AddListener((args) =>
        {
            foreach (var radialView in gameObject.GetComponentsInChildren<RadialView>())
            {
                radialView.enabled = false;
            }

        });
        _pinPanelButton.IsToggled.OnExited.AddListener((args) =>
        {
            foreach (var radialView in gameObject.GetComponentsInChildren<RadialView>())
            {
                radialView.enabled = true;
            }

        });
    }

    void EnableActionButtons(bool enable)
    {
        if (_buttonsForConnectionAndActions == null || _buttonsForConnectionAndActions.Length == 0)
        {
            Debug.LogWarning("No buttons assigned for connection and actions.");
            return;
        }
        foreach (var button in _buttonsForConnectionAndActions)
        {
            button.SetActive(enable);
        }
    }

    void CreateRouteToDrone()
    {
        Debug.Log("Creating route to drone...");
        RoutingManager.Instance.endPoint = gameObject.GetComponent<DronePositionVisualizer>().droneObjectOnMap.transform;
        RoutingManager.Instance.GetRoute();
        _lastRouteType = RouteType.Road;
    }

    void CreateStraightLineRouteToDrone()
    {
        Debug.Log("Creating straight line route to drone");
        RoutingManager.Instance.endPoint = gameObject.GetComponent<DronePositionVisualizer>().droneObjectOnMap.transform;
        RoutingManager.Instance.GetStraightLineRoute(gameObject.GetComponent<DronePositionVisualizer>().droneObject.transform);
        _lastRouteType = RouteType.StraightLine;
    }

    public void ClearRoute()
    {
        Debug.Log("Clearing route...");
        RoutingManager.Instance.ClearRoute(true);
        _lastRouteType = RouteType.None;
    }
    public void ShowRoutingDialog()
    {
        Transform droneTransform = gameObject.GetComponent<DronePositionVisualizer>().droneObjectOnMap.transform;
        RoutingManager.Instance.selectedPOI = droneTransform;
        IDialog dialog;
        if (RoutingManager.Instance.endPoint == droneTransform)
        {
            Action<DialogButtonEventArgs> updateAction;
            switch (_lastRouteType)
            {
                case RouteType.Road:
                    updateAction = (args) => CreateRouteToDrone();
                    break;
                case RouteType.StraightLine:
                    updateAction = (args) => CreateStraightLineRouteToDrone();
                    break;
                default: // Fallback if type is None or unset
                    updateAction = (args) => ShowRoutingDialog(); // Re-show selection
                    break;
            }

            dialog = DialogPool.Get()
                .SetHeader("Routing Information")
                .SetBody("The selected drone is already your destination. Do you want to update the route?")
                .SetPositive("Yes", updateAction)
                .SetNegative("No (Return)", (args) => Debug.Log("Update cancelled."))
                .SetNeutral("Clear Route", (args) => ClearRoute());
        }
        else
        {
            dialog = DialogPool.Get()
               .SetHeader("Choose Route Type")
               .SetBody("How would you like to create the route to the drone?")
               .SetPositive("By Road", (args) => CreateRouteToDrone())
               .SetNegative("Straight Line", (args) => CreateStraightLineRouteToDrone())
               .SetNeutral("Cancel", (args) => Debug.Log("Routing cancelled."));


        }
        // Get the GameObject of the dialog about to show
        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }

    public void ShowRoboticDataPanelDialog()
    {
        IDialog dialog;
        dialog = DialogPool.Get()
            .SetHeader("Routing Information")
            .SetBody("Choose the role for the robotic data panel.")
            .SetPositive("First responder", (args) =>
            {
                roboticPanelForFirstResponder.SetActive(true);
                roboticPanelForDroneOperator.SetActive(false);
            })
            .SetNegative("Drone Operator", (args) =>
            {
                roboticPanelForFirstResponder.SetActive(false);
                roboticPanelForDroneOperator.SetActive(true);
            })
            .SetNeutral("Cancel", (args) =>
            {
                Debug.Log("Robotic data panel selection cancelled.");
                roboticPanelForFirstResponder.SetActive(false);
                roboticPanelForDroneOperator.SetActive(false);

                _roboticDataPanelsToggleButton.ForceSetToggled(false); // Reset the toggle button state


                //Reset the 
            });
        // Get the GameObject of the dialog about to show
        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }

    void CreateStraightLineRouteToDetectedPerson()
    {
        Debug.Log("Creating straight line route to detected person...");
        // Get the latest spawned DetectedPerson POI's transform
        if (gpsSpawner.spawnedObjects.TryGetValue(POIType.DetectedPerson, out var detectedPersonList) && detectedPersonList.Count > 0)
        {
            Transform latestDetectedPersonTransform = detectedPersonList[^1].transform;
            RoutingManager.Instance.endPoint = latestDetectedPersonTransform;
            RoutingManager.Instance.GetStraightLineRoute(latestDetectedPersonTransform);
        }
        else
        {
            Debug.LogWarning("No DetectedPerson POIs found.");
        }
        _lastRouteType = RouteType.StraightLine;
    }

    private void ShowAlertWindowForDetectedPerson()
    {
        IDialog dialog = DialogPool.Get()
            .SetHeader("CRITICAL ALERT")
            .SetBody("A person has been detected in the area in critical condition. Do you want to take any action?")
            .SetPositive("Guide to the location", (args) =>
            {
                CreateStraightLineRouteToDetectedPerson();
                Debug.Log("Alert for detected person sent.");
            })
            .SetNegative("Ignore", (args) =>
            {
                Debug.Log("Alert for detected person ignored.");
            });

        // Get the GameObject of the dialog about to show
        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }
    private IEnumerator ConnectionTimeoutCoroutine()
    {
        float elapsedTime = 0f;

        while (IsRosConnecting() && elapsedTime < connectionTimeoutSeconds)
        {
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;
        }

        if (IsRosConnecting())
        {
            // Connection timed out
            Debug.LogWarning($"ROS connection attempt timed out after {connectionTimeoutSeconds} seconds.");
            //isConnecting = false;
            ros.Disconnect(); // Cancel any ongoing connection attempt
        }
    }

    void ForceUntoggleButtons()
    {
        foreach (var button in _buttonsForConnectionAndActions)
        {
            if (button.TryGetComponent(out PressableButton pressableButton))
            {
                if (pressableButton.ToggleMode == MixedReality.Toolkit.StatefulInteractable.ToggleType.Toggle)
                {
                    pressableButton.ForceSetToggled(false);
                }
            }
        }
    }
    void HandleAlertReceived(string alertMessage, string alertID)
    {
        //Parse string to alertType
        if (Enum.TryParse(alertMessage, out AlertType alertType))
        {
            Debug.Log($"Alert received: {alertType}");
            Drone drone = FindObjectOfType<Drone>();
            switch (alertType)
            {
                case AlertType.Person:
                    _detectionsCount++;
                    _detectionsText.text = $"{_detectionsCount}";
                    SpawnPOIAtDroneTransform(drone.transform, POIType.DetectedPerson, drone.droneGPSCoordinates, alertID);
                    ShowAlertWindowForDetectedPerson();
                    break;
                case AlertType.Manhole:
                    _dangersCount++;
                    _dangersText.text = $"{_dangersCount}";
                    SpawnPOIAtDroneTransform(drone.transform, POIType.Manhole, drone.droneGPSCoordinates, alertID);
                    break;
                case AlertType.General:
                    _generalWarningsCount++;
                    _generalWarningsText.text = $"{_generalWarningsCount}";
                    SpawnPOIAtDroneTransform(drone.transform, POIType.General, drone.droneGPSCoordinates, alertID);
                    break;
                default:
                    Debug.LogWarning($"Alert type: {alertType}");
                    break;
            }
        }
        else
        {
            Debug.LogWarning($"Received an unrecognized alert message: {alertMessage}");
        }
    }
    private void OnDestroy()
    {
        // Clean up when the object is destroyed
        if (ros != null)
        {
            ros.Disconnect();
        }
        if (_webSocketConnection != null)
        {
            _webSocketConnection.OnAlertReceivedEvent -= HandleAlertReceived;
        }
    }
}
