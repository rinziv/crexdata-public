using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Simulates and provides fake sensor data for a drone to display on a UI panel.
/// This script is designed for demonstration purposes when live ROS data is unavailable.
/// It updates data periodically via a coroutine for better performance and only runs
/// when the associated GameObject is active and the system is connected to ROS.
/// </summary>
public class FakeDroneSensorDataProvider : MonoBehaviour
{
    [System.Serializable]
    public class SensorDisplaySet
    {
        [Tooltip("Text field to display the simulated air temperature.")]
        public TextMeshProUGUI temperatureText;
        [Tooltip("Text field to display the drone\'s GPS coordinates.")]
        public TextMeshProUGUI coordinatesText;
        [Tooltip("Text field to display the drone\'s altitude.")]
        public TextMeshProUGUI altitudeText;
        [Tooltip("Text field to display simulated air quality (e.g., CO levels in PPM).")]
        public TextMeshProUGUI airQualityText;
        [Tooltip("Text field to display simulated signal strength to the drone.")]
        public TextMeshProUGUI signalStrengthText;
        [Tooltip("Text field to display the drone\'s simulated battery level.")]
        public TextMeshProUGUI batteryLevelText;
        [Tooltip("Text field to display simulated wind speed.")]
        public TextMeshProUGUI windSpeedText;
    }

    [Header("UI Display Panels")]
    [Tooltip("A list of UI panel sets to update with sensor data.")]
    public List<SensorDisplaySet> sensorDisplays = new List<SensorDisplaySet>();

    [Header("Data Settings")]
    [Tooltip("How often to update the sensor data, in seconds.")]
    public float updateInterval = 3f;
    [Tooltip("Starting battery level. Will decrease over time.")]
    [Range(0f, 100f)]
    public float initialBatteryLevel = 100f;
    [Tooltip("Rate at which the battery depletes per minute.")]
    public float batteryDepletionRate = 0.5f;
    public Material droneMaterial;
    private Color _initialDroneMaterialColor;
    public bool useColorForBattery = false; // Whether to change drone color based on battery level

    [Header("Dependencies")]
    [Tooltip("Reference to the DronePositionVisualizer to get real-time position data.")]
    public DronePositionVisualizer dronePositionVisualizer;
    [Tooltip("Reference to the RoboticPanelManager to check for ROS connection status.")]
    public RoboticPanelManager roboticPanelManager;


    private Coroutine _updateCoroutine;
    private float _currentBatteryLevel;
    private const string _disconnectedText = "---";

    private void Awake()
    {
        // Ensure dependencies are assigned
        if (dronePositionVisualizer == null)
        {
            Debug.LogError("FakeDroneSensorDataProvider: DronePositionVisualizer is not assigned!");
            this.enabled = false;
        }
        if (roboticPanelManager == null)
        {
            Debug.LogError("FakeDroneSensorDataProvider: RoboticPanelManager is not assigned!");
            this.enabled = false;
        }

        roboticPanelManager.RosConnected += OnRosConnected;
        roboticPanelManager.RosDisconnected += OnRosDisconnected;

        _initialDroneMaterialColor = droneMaterial != null ? droneMaterial.color : Color.white;
    }

    void Start()
    {
        //Initialize text on all fields to a default disconnected state
        ClearAllTextFields();
    }

    private void OnRosConnected()
    {
        // When the panel becomes active, start the data simulation
        _currentBatteryLevel = initialBatteryLevel;
        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateSensorDataCoroutine());
        }
    }

    private void OnRosDisconnected()
    {
        // When the panel is deactivated, stop the simulation to save resources
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
        ClearAllTextFields();
    }

    public void ToggleColorForBattery()
    {
        useColorForBattery = !useColorForBattery;
        UpdateDroneColorBasedOnBattery(); // Update color immediately if enabled
    }

    private IEnumerator UpdateSensorDataCoroutine()
    {
        while (true)
        {
            if ((roboticPanelManager.roboticPanelForDroneOperator != null && roboticPanelManager.roboticPanelForDroneOperator.activeSelf) ||
                (roboticPanelManager.roboticPanelForFirstResponder != null && roboticPanelManager.roboticPanelForFirstResponder.activeSelf))
                UpdateAllSensorData();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void UpdateAllSensorData()
    {
        // --- Update Data ---
        // Temperature: Simulates a plausible outdoor temperature in Celsius.
        float temperature = Random.Range(18.0f, 25.5f);

        // Air Quality: Simulates Carbon Monoxide levels in Parts Per Million (PPM).
        // Normal air is < 9 PPM. Spikes could indicate fire/danger.
        int airQuality = Random.Range(5, 25);

        // Signal Strength: Simulates wireless signal quality.
        int signalStrength = Random.Range(85, 100);

        // Wind Speed: Simulates wind in km/h.
        float windSpeed = Random.Range(2.0f, 15.0f);

        // Battery Level: Depletes over time.
        _currentBatteryLevel -= batteryDepletionRate / 60 * updateInterval;
        _currentBatteryLevel = Mathf.Max(_currentBatteryLevel, 0); // Clamp at 0
        if (_currentBatteryLevel == 0)
        {
            _currentBatteryLevel = 99f; // Ensure it doesn't go negative
        }
        UpdateDroneColorBasedOnBattery();

        // --- Get Real Data from DronePositionVisualizer ---
        Vector2 coordinates = dronePositionVisualizer.droneObject.GetComponent<Drone>().droneGPSCoordinates;
        float altitude = dronePositionVisualizer.droneObject.transform.position.y;


        // --- Update UI Text Fields for all display sets ---
        foreach (var displaySet in sensorDisplays)
        {
            SetText(displaySet.temperatureText, $"<size=8>Temperature</size>\n<size=6><alpha=#88>{temperature:F1} °C</size>");
            SetText(displaySet.coordinatesText, $"<size=8>Coordinates</size>\n<size=6><alpha=#88>{coordinates.x:F6}, {coordinates.y:F6}</size>");
            SetText(displaySet.altitudeText, $"<size=8>Altitude</size>\n<size=6><alpha=#88>{altitude:F1} m</size>");
            SetText(displaySet.airQualityText, $"<size=8>Air Quality</size>\n<size=6><alpha=#88>{airQuality} PPM (CO)</size>");
            SetText(displaySet.signalStrengthText, $"<size=8>Signal Strength</size>\n<size=6><alpha=#88>{signalStrength}%</size>");
            SetText(displaySet.batteryLevelText, $"<size=8>Battery Level</size>\n<size=6><alpha=#88>{_currentBatteryLevel:F1}%</size>");
            SetText(displaySet.windSpeedText, $"<size=8>Wind Speed</size>\n<size=6><alpha=#88>{windSpeed:F1} km/h</size>");
        }
    }

    private void UpdateDroneColorBasedOnBattery()
    {
        if (droneMaterial != null)
        {
            // Calculate color based on battery level
            float batteryPercentage = _currentBatteryLevel / initialBatteryLevel;
            Color droneColor = !useColorForBattery ? _initialDroneMaterialColor : Color.Lerp(Color.red, Color.green, batteryPercentage);
            droneMaterial.color = droneColor;
        }
        else
        {
            Debug.LogWarning("FakeDroneSensorDataProvider: Drone material is not assigned!");
        }
    }

    private void ClearAllTextFields()
    {
        foreach (var displaySet in sensorDisplays)
        {
            SetText(displaySet.temperatureText, $"<size=8>Temperature</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
            SetText(displaySet.coordinatesText, $"<size=8>Coordinates</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
            SetText(displaySet.altitudeText, $"<size=8>Altitude</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
            SetText(displaySet.airQualityText, $"<size=8>Air Quality</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
            SetText(displaySet.signalStrengthText, $"<size=8>Signal Strength</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
            SetText(displaySet.batteryLevelText, $"<size=8>Battery Level</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
            SetText(displaySet.windSpeedText, $"<size=8>Wind Speed</size>\n<size=6><alpha=#88>{_disconnectedText}</size>");
        }
    }

    private void SetText(TextMeshProUGUI textField, string value)
    {
        if (textField != null)
        {
            textField.text = value;
        }
    }

    private void OnDisable()
    {
        // When the panel is deactivated, stop the simulation to save resources
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }

        // Unsubscribe from events to prevent memory leaks
        roboticPanelManager.RosConnected -= OnRosConnected;
        roboticPanelManager.RosDisconnected -= OnRosDisconnected;

        droneMaterial.color = _initialDroneMaterialColor; // Reset drone color
    }

    private void OnDestroy()
    {
        // Ensure we unsubscribe from events to prevent memory leaks
        if (roboticPanelManager != null)
        {
            roboticPanelManager.RosConnected -= OnRosConnected;
            roboticPanelManager.RosDisconnected -= OnRosDisconnected;
        }
        droneMaterial.color = _initialDroneMaterialColor; // Reset drone color
    }
}
