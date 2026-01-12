using UnityEngine;
using TMPro;
using MixedReality.Toolkit.UX;
using NaughtyAttributes;
using System.Collections;
using System;
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine.UI;

public class StartingConfigurationPanels : MonoBehaviour
{
    #region Fields and Properties

    [Header("Panel References")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject calibrationMenuPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private PressableButton actionButton;
    [SerializeField] private TextMeshProUGUI uniqueIDText;

    private TextMeshPro _buttonText;

    [Header("Status Indicators")]
    [SerializeField] private GameObject serverStatusIndicator;
    [SerializeField] private GameObject phoneStatusIndicator;
    [SerializeField] private Material greenMaterial;
    [SerializeField] private Material redMaterial;

    [Header("Managers")]
    [SerializeField] private WebSocketConnection webSocketConnection;
    [SerializeField] private PairingManager pairingManager;

    [Header("Palm Tracking")]
    private HandConstraintPalmUp palmUpConstraint;
    [SerializeField] private bool enablePalmTrackingAfterConnection = true;
    [SerializeField] private GameObject mainHandMenuPanel;

    [Header("Connection Monitoring")]
    [SerializeField] private TextMeshProUGUI latencyText;
    [SerializeField] private TextMeshProUGUI messageCountText;

    private bool _connnectedOnce, _isConnecting = false;
    private float _lastLatencyCheck;
    private int _messageCount;
    private Coroutine _latencyCheckCoroutine;

    private enum PanelState
    {
        Welcome,
        ServerConnection,
        ConnectedDashboard
    }

    private PanelState currentState = PanelState.Welcome;
    private bool isServerConnected = false;
    private bool isPhonePaired = false;

    #endregion

    #region Unity Lifecycle Methods

    void Start()
    {
        // Find button text component
        if (actionButton != null)
        {
            _buttonText = actionButton.GetComponentInChildren<TextMeshPro>();
            if (_buttonText == null)
            {
                Debug.LogError("Button text component not found on action button!");
            }
        }

        //ShowWelcomePanel();
        UpdateConnectionIndicators();

        // Subscribe to connection events
        if (webSocketConnection != null)
        {
            webSocketConnection.OnConnectionOpenedEvent += HandleServerConnected;
            webSocketConnection.OnConnectionClosedEvent += HandleServerDisconnected;
            webSocketConnection.OnMessageReceivedEvent += HandleMessageReceived;
            webSocketConnection.OnUniqueIDReceivedEvent += HandleOnUniqueIDReceived;
        }

        if (pairingManager != null)
        {
            webSocketConnection.OnPairedReceived += HandlePhonePaired;
            webSocketConnection.OnPhoneLostReceived += HandlePhoneDisconnected;
        }

        if (actionButton != null)
        {
            actionButton.OnClicked.AddListener(OnActionButtonClicked);
        }

        //Make Sure hand constraint is disabled at start!
        if (palmUpConstraint == null)
        {
            palmUpConstraint = GetComponent<HandConstraintPalmUp>();
            palmUpConstraint.enabled = false;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (webSocketConnection != null)
        {
            webSocketConnection.OnConnectionOpenedEvent -= HandleServerConnected;
            webSocketConnection.OnConnectionClosedEvent -= HandleServerDisconnected;
            webSocketConnection.OnMessageReceivedEvent -= HandleMessageReceived;
            webSocketConnection.OnUniqueIDReceivedEvent -= HandleOnUniqueIDReceived;
        }

        if (pairingManager != null)
        {
            webSocketConnection.OnPairedReceived -= HandlePhonePaired;
            webSocketConnection.OnPhoneLostReceived -= HandlePhoneDisconnected;
        }

        // Remove button listeners
        if (actionButton != null)
        {
            actionButton.OnClicked.RemoveListener(OnActionButtonClicked);
        }

        if (_latencyCheckCoroutine != null)
        {
            StopCoroutine(_latencyCheckCoroutine);
        }
    }

    #endregion

    #region Panel State Management

    public void ShowWelcomePanel()
    {
        currentState = PanelState.Welcome;
        titleText.text = "Welcome to CrexData";
        contentText.text = "Welcome to the CrexData Emergency use case system. This system will help you visualize data in mixed reality.\n\nClick 'Continue' to set up your connection.";

        if (_buttonText != null)
            _buttonText.text = "Continue";

        mainPanel.SetActive(true);
    }

    private void ShowServerConnectionPanel()
    {
        currentState = PanelState.ServerConnection;
        titleText.text = "Server Connection";

        if (serverStatusIndicator != null)
            serverStatusIndicator.SetActive(true);
        if (phoneStatusIndicator != null)
            phoneStatusIndicator.SetActive(true);

        UpdateConnectionStatusText();

        if (_buttonText != null)
            _buttonText.text = "Connect";
    }

    private void OnActionButtonClicked()
    {
        switch (currentState)
        {
            case PanelState.Welcome:
                ShowServerConnectionPanel();
                break;

            case PanelState.ServerConnection:
                if (!isServerConnected)
                {
                    if (!_connnectedOnce)
                        webSocketConnection.ConnectToServer();
                    if (_buttonText != null)
                        _buttonText.text = "Connecting...";
                    _isConnecting = true;
                }
                else
                {
                    if (isPhonePaired)
                    {
                        if (calibrationMenuPanel != null)
                            calibrationMenuPanel.SetActive(true);

                        currentState = PanelState.ConnectedDashboard;
                        EnablePalmTrackingMode();
                        mainHandMenuPanel.SetActive(true);

                    }
                    else if (_buttonText != null)
                        _buttonText.text = "Waiting for pair...";
                }
                break;
        }
    }

    #endregion

    #region Connection UI Management

    private void UpdateConnectionStatusText()
    {
        string serverStatus = isServerConnected ? "Connected" : "Disconnected";
        string phoneStatus = isPhonePaired ? "Paired" : "Not Paired";

        contentText.text = $"Server Status: {serverStatus}\nPhone Status: {phoneStatus}\n\n";

        if (!isServerConnected)
        {
            contentText.text += "Click 'Connect' to establish a server connection.";
        }
        else if (!isPhonePaired)
        {
            contentText.text += "Open your android CrexData application and enter the ip <color=green><b>147.27.41.154</b></color> and your <color=green><b>UniqueID</b></color> to pair your phone.\n\n" +
                                "Once paired, you can continue to the calibration menu.";
        }
        else
        {
            contentText.text += "Your device is fully connected and ready to use.";
            if (_buttonText != null)
                _buttonText.text = "Continue to Calibration";
        }
    }

    private void UpdateConnectionIndicators()
    {
        if (serverStatusIndicator != null)
        {
            Renderer renderer = serverStatusIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = isServerConnected ? greenMaterial : redMaterial;
            }
        }

        // Update phone indicator
        if (phoneStatusIndicator != null)
        {
            Renderer renderer = phoneStatusIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = isPhonePaired ? greenMaterial : redMaterial;
            }
        }

        if (currentState == PanelState.ServerConnection)
        {
            UpdateConnectionStatusText();
        }
    }

    #endregion

    #region Palm Tracking and Monitoring

    private void EnablePalmTrackingMode()
    {
        if (!enablePalmTrackingAfterConnection) return;

        Debug.Log("Enabling palm tracking mode");

        if (palmUpConstraint != null)
        {
            palmUpConstraint.enabled = true;
        }

        mainPanel.GetComponent<RadialView>().enabled = false;
        mainPanel.GetComponent<SolverHandler>().enabled = false;
        mainPanel.transform.position = Vector3.zero;
        mainPanel.transform.rotation = Quaternion.identity;
        mainPanel.SetActive(false);

        ///Adjust spere Indicators Y position
        Vector3 newPosition = serverStatusIndicator.transform.localPosition;
        newPosition.y = -10f;
        serverStatusIndicator.transform.localPosition = newPosition;

        newPosition = phoneStatusIndicator.transform.localPosition;
        newPosition.y = -28f;
        phoneStatusIndicator.transform.localPosition = newPosition;

        if (titleText != null)
        {
            titleText.text = "Connection Status";
        }

        // Hide the original button
        if (actionButton != null)
        {
            actionButton.gameObject.SetActive(false);
        }

        if (latencyText != null)
        {
            latencyText.gameObject.SetActive(true);
        }

        if (messageCountText != null)
        {
            messageCountText.gameObject.SetActive(true);
        }

        // Start monitoring connection metrics
        if (_latencyCheckCoroutine != null)
        {
            StopCoroutine(_latencyCheckCoroutine);
        }
        _latencyCheckCoroutine = StartCoroutine(MonitorConnectionMetrics());
        UpdateConnectionMonitoringInfo();
    }

    private IEnumerator MonitorConnectionMetrics()
    {
        while (isServerConnected)
        {
            UpdateConnectionMonitoringInfo();
            yield return new WaitForSeconds(1.0f); // Once per second
        }

        HandleConnectionLost();
    }

    private void UpdateConnectionMonitoringInfo()
    {
        string serverStatus = isServerConnected ? "Connected" : "Disconnected";
        string phoneStatus = isPhonePaired ? "Paired" : "Not Paired";

        contentText.text = $"Server: {serverStatus}\n\nPhone: {phoneStatus}";

        if (latencyText != null && webSocketConnection != null)
        {
            float latency = webSocketConnection.GetLatency();
            latencyText.text = $"Latency: {latency:F1} ms";

            // Color based on latency quality
            if (latency < 50)
                latencyText.color = Color.green;
            else if (latency < 100)
                latencyText.color = Color.yellow;
            else
                latencyText.color = Color.red;
        }
    }

    private void HandleConnectionLost()
    {
        Debug.Log("Connection lost! Showing reconnect UI");

        // Stop monitoring
        if (_latencyCheckCoroutine != null)
        {
            StopCoroutine(_latencyCheckCoroutine);
            _latencyCheckCoroutine = null;
        }

        UpdateConnectionMonitoringInfo();
    }

    #endregion

    #region Event Handlers

    private void HandleServerConnected(object sender, System.EventArgs e)
    {
        isServerConnected = true;
        _connnectedOnce = true;
        _isConnecting = false;
        UpdateConnectionIndicators();

        if (currentState == PanelState.ConnectedDashboard)
        {
            if (_latencyCheckCoroutine == null)
            {
                _latencyCheckCoroutine = StartCoroutine(MonitorConnectionMetrics());
            }
        }
        else
        {
            if (_buttonText != null)
                _buttonText.text = "Server Connected";
        }
    }

    private void HandleServerDisconnected(object sender, WebSocketSharp.CloseEventArgs e)
    {
        isServerConnected = false;

        if (!_isConnecting && _buttonText != null)
            _buttonText.text = "Connect";

        _isConnecting = true;
        isPhonePaired = false;
        UpdateConnectionIndicators();
    }

    private void HandlePhonePaired(string phoneId)
    {
        isPhonePaired = true;
        UpdateConnectionIndicators();
    }

    private void HandlePhoneDisconnected(string phoneId)
    {
        isPhonePaired = false;

        if (_buttonText != null)
            _buttonText.text = "Phone Disconnected";

        UpdateConnectionIndicators();
    }

    private void HandleMessageReceived(object sender, EventArgs e)
    {
        _messageCount++;

        if (currentState == PanelState.ConnectedDashboard && messageCountText != null)
        {
            messageCountText.text = $"Messages: {_messageCount}";
        }
    }

    private void HandleOnUniqueIDReceived(string uniqueID)
    {
        if (uniqueIDText != null)
        {
            uniqueIDText.text = $"{uniqueID}";
        }
    }
    #endregion

    #region Editor Utilities

#if UNITY_EDITOR
    [Button("Cheat Connection")]
    private void CheatConnection()
    {
        isServerConnected = true;
        isPhonePaired = true;
        UpdateConnectionIndicators();
    }
#endif

    #endregion

    public void SkipPhonePairing()
    {
        isPhonePaired = true;
        webSocketConnection.spawner.SkipPhoneAndUseHardcodedCoordinates();
        UpdateConnectionIndicators();
    }
}