using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Runtime.InteropServices;

#if ENABLE_WINMD_SUPPORT && UNITY_WSA && !UNITY_EDITOR
using HL2UnityPlugin;
#endif

/// <summary>
/// Research Mode IMU Stream for HoloLens 2
/// Provides real-time access to accelerometer, gyroscope, and magnetometer data
/// Based on petergu684's HoloLens2-ResearchMode-Unity implementation
/// Enhanced with TextMeshPro UI and error handling - REAL DATA ONLY
/// </summary>
public class ResearchModeImuStream : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT && UNITY_WSA && !UNITY_EDITOR
    HL2ResearchMode researchMode;
#endif

    [Header("IMU Data")]
    private float[] accelSampleData = null;
    private Vector3 accelVector;
    private float[] gyroSampleData = null;
    private Vector3 gyroEulerAngle;
    private float[] magSampleData = null;

    [Header("UI Display - TextMeshPro")]
    [Tooltip("TextMeshPro component to display accelerometer data")]
    public TextMeshProUGUI AccelText = null;

    [Tooltip("TextMeshPro component to display gyroscope data")]
    public TextMeshProUGUI GyroText = null;

    [Tooltip("TextMeshPro component to display magnetometer data")]
    public TextMeshProUGUI MagText = null;

    [Tooltip("TextMeshPro component to display system status")]
    public TextMeshProUGUI StatusText = null;

    [Tooltip("TextMeshPro component to display compass heading in degrees")]
    public TextMeshProUGUI CompassText = null;

    [Header("Settings")]
    [Tooltip("Enable/disable real-time IMU data streaming")]
    public bool enableRealtimePreview = true;

    [Tooltip("Display precision for sensor values")]
    [Range(1, 6)]
    public int displayPrecision = 3;

    [Tooltip("Show debug information in console")]
    public bool showDebugInfo = false;

    [Header("Compass Settings")]
    [Tooltip("Enable compass heading calculation")]
    public bool enableCompass = true;

    [Tooltip("Apply tilt compensation for more accurate compass readings")]
    public bool tiltCompensation = true;

    [Tooltip("Magnetic declination angle in degrees (adjust for your location)")]
    [Range(-30f, 30f)]
    public float magneticDeclination = 4.13f; // 4° 8' E for your location

    // Private fields
    private bool isInitialized = false;
    private string lastErrorMessage = "";

    // Compass data
    private float compassHeading = 0f;
    private Vector3 magneticNorth = Vector3.zero;
    private Vector3 trueNorth = Vector3.zero;

    void Start()
    {
        InitializeResearchMode();
    }

    /// <summary>
    /// Initialize Research Mode IMU sensors with error handling
    /// </summary>
    private void InitializeResearchMode()
    {
#if ENABLE_WINMD_SUPPORT && UNITY_WSA && !UNITY_EDITOR
        try
        {
            UpdateStatus("Initializing Research Mode IMU...");
            
            researchMode = new HL2ResearchMode();
            
            // Initialize individual sensors
            researchMode.InitializeAccelSensor();
            researchMode.InitializeGyroSensor();
            researchMode.InitializeMagSensor();

            // Start sensor loops
            researchMode.StartAccelSensorLoop();
            researchMode.StartGyroSensorLoop();
            researchMode.StartMagSensorLoop();
            
            isInitialized = true;
            UpdateStatus("Research Mode IMU Active");
            
            if (showDebugInfo)
                Debug.Log("HoloLens 2 Research Mode IMU initialized successfully");
        }
        catch (System.Exception e)
        {
            lastErrorMessage = e.Message;
            isInitialized = false;
            UpdateStatus($"IMU Init Failed: {e.Message}");
            Debug.LogError($"Failed to initialize Research Mode IMU: {e.Message}");
        }
#else
        isInitialized = false;
        UpdateStatus("Research Mode ONLY - Not available in Editor/Non-UWP builds");
        Debug.LogError("Research Mode IMU requires HoloLens 2 device with native plugin!");
#endif
    }

    void LateUpdate()
    {
        if (!enableRealtimePreview || !isInitialized) return;

#if ENABLE_WINMD_SUPPORT && UNITY_WSA && !UNITY_EDITOR
        try
        {
            // Update Accelerometer Sample
            if (researchMode.AccelSampleUpdated())
            {
                accelSampleData = researchMode.GetAccelSample();
                if (accelSampleData != null && accelSampleData.Length == 3)
                {
                    UpdateAccelText();
                }
            }

            // Update Gyroscope Sample
            if (researchMode.GyroSampleUpdated())
            {
                gyroSampleData = researchMode.GetGyroSample();
                if (gyroSampleData != null && gyroSampleData.Length == 3)
                {
                    UpdateGyroText();
                }
            }

            // Update Magnetometer Sample
            if (researchMode.MagSampleUpdated())
            {
                magSampleData = researchMode.GetMagSample();
                if (magSampleData != null && magSampleData.Length == 3)
                {
                    UpdateMagText();
                    
                    // Update compass if enabled
                    if (enableCompass)
                    {
                        UpdateCompass();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (showDebugInfo)
                Debug.LogError($"Error reading IMU data: {e.Message}");
        }
#endif

        // Convert to Vector3 for visualization
        accelVector = CreateAccelVector(accelSampleData);
        gyroEulerAngle = CreateGyroEulerAngle(gyroSampleData);
    }



    /// <summary>
    /// Update accelerometer text display
    /// </summary>
    private void UpdateAccelText()
    {
        if (AccelText != null && accelSampleData != null && accelSampleData.Length == 3)
        {
            string format = $"F{displayPrecision}";
            AccelText.text = $"Accel: {accelSampleData[0].ToString(format)}, {accelSampleData[1].ToString(format)}, {accelSampleData[2].ToString(format)} m/s²";
        }
    }

    /// <summary>
    /// Update gyroscope text display
    /// </summary>
    private void UpdateGyroText()
    {
        if (GyroText != null && gyroSampleData != null && gyroSampleData.Length == 3)
        {
            string format = $"F{displayPrecision}";
            GyroText.text = $"Gyro: {gyroSampleData[0].ToString(format)}, {gyroSampleData[1].ToString(format)}, {gyroSampleData[2].ToString(format)} rad/s";
        }
    }

    /// <summary>
    /// Update magnetometer text display
    /// </summary>
    private void UpdateMagText()
    {
        if (MagText != null && magSampleData != null && magSampleData.Length == 3)
        {
            string format = $"F{displayPrecision}";
            MagText.text = $"Mag: {magSampleData[0].ToString(format)}, {magSampleData[1].ToString(format)}, {magSampleData[2].ToString(format)} µT";
        }
    }

    /// <summary>
    /// Update compass heading calculation using magnetometer and accelerometer data
    /// </summary>
    private void UpdateCompass()
    {
        if (magSampleData == null || magSampleData.Length < 3) return;

        // Convert magnetometer data to Unity coordinates (µT)
        // Based on HoloLens Research Mode coordinate system:
        // magSampleData[0] : Down direction
        // magSampleData[1] : Back direction  
        // magSampleData[2] : Right direction
        Vector3 magnetometer = new Vector3(
            -magSampleData[2],      // Right -> Unity X (East)
            magSampleData[0],     // Down -> Unity Y (Up) 
            magSampleData[1]      // Back -> Unity Z (Forward/North)
        );

        if (showDebugInfo)
        {
            Debug.Log($"Raw Mag: [{magSampleData[0]:F2}, {magSampleData[1]:F2}, {magSampleData[2]:F2}] µT");
            Debug.Log($"Converted Mag: [{magnetometer.x:F2}, {magnetometer.y:F2}, {magnetometer.z:F2}] µT");
            Debug.Log($"Accel Vector: [{accelVector.x:F2}, {accelVector.y:F2}, {accelVector.z:F2}] m/s²");
        }
        if (tiltCompensation && accelSampleData != null && accelSampleData.Length >= 3)
        {
            // Tilt-compensated compass using accelerometer for gravity reference
            compassHeading = CalculateTiltCompensatedHeading(magnetometer, accelVector);
        }
        else
        {
            // Simple 2D compass (assumes device is flat)
            compassHeading = CalculateSimpleHeading(magnetometer);
        }

        // Apply magnetic declination correction
        compassHeading += magneticDeclination;

        // Normalize to 0-360 degrees
        //if (compassHeading < 0) compassHeading += 360f;
        //if (compassHeading >= 360) compassHeading -= 360f;

        // Update compass display
        UpdateCompassText();
    }

    /// <summary>
    /// Calculate tilt-compensated compass heading
    /// </summary>
    private float CalculateTiltCompensatedHeading(Vector3 mag, Vector3 accel)
    {
        // Normalize accelerometer vector (gravity reference)
        Vector3 down = accel.normalized;

        // Calculate east vector (down × mag) - perpendicular to both gravity and magnetic field
        Vector3 east = Vector3.Cross(down, mag).normalized;

        // Calculate north vector (east × down) - horizontal north
        Vector3 north = Vector3.Cross(east, down).normalized;

        // Calculate heading from horizontal north and east vectors
        // Use atan2(east_component, north_component) for proper quadrant handling
        float heading = Mathf.Atan2(east.x, north.x) * Mathf.Rad2Deg;

        if (showDebugInfo)
        {
            Debug.Log($"Down: [{down.x:F3}, {down.y:F3}, {down.z:F3}]");
            Debug.Log($"East: [{east.x:F3}, {east.y:F3}, {east.z:F3}]");
            Debug.Log($"North: [{north.x:F3}, {north.y:F3}, {north.z:F3}]");
            Debug.Log($"Calculated heading: {heading:F1}°");
        }

        return heading;
    }    /// <summary>
         /// Calculate simple 2D compass heading (assumes device is flat)
         /// </summary>
    private float CalculateSimpleHeading(Vector3 mag)
    {
        // Calculate heading in horizontal plane (assumes device is level)
        // X = East, Z = North in Unity coordinates
        float heading = Mathf.Atan2(mag.x, mag.z) * Mathf.Rad2Deg;

        if (showDebugInfo)
        {
            Debug.Log($"Simple heading calc: atan2({mag.x:F2}, {mag.z:F2}) = {heading:F1}°");
        }

        return heading;
    }    /// <summary>
         /// Update compass text display
         /// </summary>
    private void UpdateCompassText()
    {
        if (CompassText != null)
        {
            // Convert to cardinal directions
            string direction = GetCardinalDirection(compassHeading);
            CompassText.text = $"Compass: {compassHeading:F1}° {direction}";
        }
    }

    /// <summary>
    /// Convert degrees to cardinal direction string
    /// </summary>
    private string GetCardinalDirection(float degrees)
    {
        string[] directions = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                               "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        int index = Mathf.RoundToInt(degrees / 22.5f) % 16;
        return directions[index];
    }

    /// <summary>
    /// Update status display
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (StatusText != null)
        {
            StatusText.text = $"Status: {message}";
        }

        if (showDebugInfo)
        {
            Debug.Log($"ResearchModeIMU: {message}");
        }
    }

    private Vector3 CreateAccelVector(float[] accelSample)
    {
        Vector3 vector = Vector3.zero;
        if ((accelSample?.Length ?? 0) == 3)
        {
            // Positive directions
            //  accelSample[2] : Down direction
            //  accelSample[1] : Back direction
            //  accelSample[0] : Right direction
            vector = new Vector3(
                accelSample[2],
                -1.0f * accelSample[0],
                -1.0f * accelSample[1]
                );
        }
        return vector;
    }

    private Vector3 CreateGyroEulerAngle(float[] gyroSample)
    {
        Vector3 vector = Vector3.zero;
        if ((gyroSample?.Length ?? 0) == 3)
        {
            // Axis of rotation
            //  gyroSample[0] : Unity Y axis(Plus)
            //  gyroSample[1] : Unity Z axis(Plus)
            //  gyroSample[2] : Unity X axis(Plus)
            vector = new Vector3(
                gyroSample[2],
                gyroSample[0],
                gyroSample[1]
                );
        }
        return vector;
    }

    /// <summary>
    /// Stop all IMU sensor loops and cleanup resources
    /// </summary>
    public void StopSensorsEvent()
    {
#if ENABLE_WINMD_SUPPORT && UNITY_WSA && !UNITY_EDITOR
        try
        {
            if (researchMode != null)
            {
                researchMode.StopAllSensorDevice();
                UpdateStatus("Research Mode IMU Stopped");
                
                if (showDebugInfo)
                    Debug.Log("Research Mode IMU sensors stopped");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error stopping Research Mode sensors: {e.Message}");
        }
#endif
        enableRealtimePreview = false;
        isInitialized = false;
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            StopSensorsEvent();
        }
    }

    void OnDestroy()
    {
        StopSensorsEvent();
    }

    // Public API for accessing processed IMU data

    /// <summary>
    /// Get current accelerometer data as Unity Vector3 (m/s²)
    /// </summary>
    public Vector3 GetAccelerometer() => accelVector;

    /// <summary>
    /// Get current gyroscope data as Unity Vector3 (rad/s)
    /// </summary>
    public Vector3 GetGyroscope() => gyroEulerAngle;

    /// <summary>
    /// Get raw accelerometer sample data
    /// </summary>
    public float[] GetRawAccelData() => accelSampleData;

    /// <summary>
    /// Get raw gyroscope sample data
    /// </summary>
    public float[] GetRawGyroData() => gyroSampleData;

    /// <summary>
    /// Get raw magnetometer sample data
    /// </summary>
    public float[] GetRawMagData() => magSampleData;

    /// <summary>
    /// Check if Research Mode IMU is active and working
    /// </summary>
    public bool IsIMUActive() => isInitialized && enableRealtimePreview;

    /// <summary>
    /// Get the magnitude of current acceleration (including gravity)
    /// </summary>
    public float GetAccelerationMagnitude() => accelVector.magnitude;

    /// <summary>
    /// Get the magnitude of current angular velocity
    /// </summary>
    public float GetAngularVelocityMagnitude() => gyroEulerAngle.magnitude;

    // Compass API Methods

    /// <summary>
    /// Get current compass heading in degrees (0-360°, 0° = North)
    /// </summary>
    public float GetCompassHeading() => compassHeading;

    /// <summary>
    /// Get compass heading as cardinal direction string (N, NE, E, etc.)
    /// </summary>
    public string GetCompassDirection() => GetCardinalDirection(compassHeading);

    /// <summary>
    /// Get magnetic field vector in Unity coordinates (µT)
    /// </summary>
    public Vector3 GetMagneticField()
    {
        if (magSampleData?.Length == 3)
            return new Vector3(magSampleData[2], -magSampleData[0], -magSampleData[1]);
        return Vector3.zero;
    }

    /// <summary>
    /// Get magnetic field strength (magnitude in µT)
    /// </summary>
    public float GetMagneticFieldStrength()
    {
        return GetMagneticField().magnitude;
    }
}