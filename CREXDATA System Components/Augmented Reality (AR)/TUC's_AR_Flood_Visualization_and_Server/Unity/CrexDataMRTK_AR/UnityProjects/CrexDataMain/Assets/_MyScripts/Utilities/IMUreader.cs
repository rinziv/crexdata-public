using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
#endif

/// <summary>
/// HoloLens 2 Research Mode IMU Reader
/// Based on petergu684's implementation: https://github.com/petergu684/HoloLens2-ResearchMode-Unity
/// Provides access to raw accelerometer, gyroscope, and magnetometer data
/// </summary>
public class IMUreader : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    HL2ResearchMode researchMode;
#endif

    [Header("IMU Data")]
    public Vector3 accelerometerData = Vector3.zero;
    public Vector3 gyroscopeData = Vector3.zero;
    public Vector3 magnetometerData = Vector3.zero;

    [Header("Settings")]
    public bool enableIMUReading = true;
    public bool showDebugLogs = false;

    [Header("UI Display (Optional)")]
    public Text AccelText = null;
    public Text GyroText = null;
    public Text MagText = null;

    // Internal data arrays from Research Mode
    private float[] accelSampleData = null;
    private float[] gyroSampleData = null;
    private float[] magSampleData = null;

    // Status flags
    private bool isInitialized = false;

    void Start()
    {
        InitializeResearchMode();
    }

    private void InitializeResearchMode()
    {
#if ENABLE_WINMD_SUPPORT
        try
        {
            // Create Research Mode instance
            researchMode = new HL2ResearchMode();
            
            // Initialize IMU sensors
            researchMode.InitializeAccelSensor();
            researchMode.InitializeGyroSensor();
            researchMode.InitializeMagSensor();

            // Start sensor loops
            researchMode.StartAccelSensorLoop();
            researchMode.StartGyroSensorLoop();
            researchMode.StartMagSensorLoop();
            
            isInitialized = true;
            
            if (showDebugLogs)
                Debug.Log("HoloLens 2 Research Mode IMU initialized successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Research Mode IMU: {e.Message}");
            isInitialized = false;
        }
#else
        Debug.LogWarning("Research Mode not available in Editor. IMU data will be simulated.");
        isInitialized = false;
#endif
    }

    void LateUpdate()
    {
        if (!enableIMUReading) return;

        if (isInitialized)
        {
            ReadResearchModeIMU();
        }
        else
        {
            // Fallback to simulated data for testing
            SimulateIMUData();
        }

        // Update UI if available
        UpdateUI();
    }

    private void ReadResearchModeIMU()
    {
#if ENABLE_WINMD_SUPPORT
        try
        {
            // Read Accelerometer data
            if (researchMode.AccelSampleUpdated())
            {
                accelSampleData = researchMode.GetAccelSample();
                if (accelSampleData != null && accelSampleData.Length == 3)
                {
                    accelerometerData = CreateAccelVector(accelSampleData);
                }
            }

            // Read Gyroscope data
            if (researchMode.GyroSampleUpdated())
            {
                gyroSampleData = researchMode.GetGyroSample();
                if (gyroSampleData != null && gyroSampleData.Length == 3)
                {
                    gyroscopeData = CreateGyroVector(gyroSampleData);
                }
            }

            // Read Magnetometer data
            if (researchMode.MagSampleUpdated())
            {
                magSampleData = researchMode.GetMagSample();
                if (magSampleData != null && magSampleData.Length == 3)
                {
                    magnetometerData = CreateMagVector(magSampleData);
                }
            }
        }
        catch (System.Exception e)
        {
            if (showDebugLogs)
                Debug.LogError($"Error reading IMU data: {e.Message}");
        }
#endif
    }

    /// <summary>
    /// Convert Research Mode accelerometer data to Unity Vector3
    /// Research Mode coordinate system to Unity coordinate system conversion
    /// </summary>
    private Vector3 CreateAccelVector(float[] accelSample)
    {
        if (accelSample == null || accelSample.Length != 3)
            return Vector3.zero;

        // Convert Research Mode coordinate system to Unity
        // Research Mode: X=right, Y=up, Z=back
        // Unity: X=right, Y=up, Z=forward
        return new Vector3(
            accelSample[0],   // X axis (right)
            accelSample[1],   // Y axis (up) 
            -accelSample[2]   // Z axis (forward, negated)
        );
    }

    /// <summary>
    /// Convert Research Mode gyroscope data to Unity Vector3
    /// </summary>
    private Vector3 CreateGyroVector(float[] gyroSample)
    {
        if (gyroSample == null || gyroSample.Length != 3)
            return Vector3.zero;

        // Convert from Research Mode to Unity coordinate system
        // Gyro data is angular velocity in rad/s
        return new Vector3(
            gyroSample[2],   // Unity X axis
            gyroSample[0],   // Unity Y axis  
            gyroSample[1]    // Unity Z axis
        );
    }

    /// <summary>
    /// Convert Research Mode magnetometer data to Unity Vector3
    /// </summary>
    private Vector3 CreateMagVector(float[] magSample)
    {
        if (magSample == null || magSample.Length != 3)
            return Vector3.zero;

        // Convert from Research Mode to Unity coordinate system
        // Magnetometer data is in microtesla (µT)
        return new Vector3(
            magSample[0],   // X axis
            magSample[1],   // Y axis
            -magSample[2]   // Z axis (negated for Unity)
        );
    }

    /// <summary>
    /// Simulate IMU data for testing when Research Mode is not available
    /// </summary>
    private void SimulateIMUData()
    {
        // Simulate realistic accelerometer data (gravity + small movements)
        accelerometerData = new Vector3(
            Mathf.Sin(Time.time * 0.5f) * 0.2f,           // Small X movement
            9.81f + Mathf.Cos(Time.time * 0.3f) * 0.1f,   // Gravity + small Y variation
            Mathf.Sin(Time.time * 0.7f) * 0.15f           // Small Z movement
        );

        // Simulate gyroscope data (small rotations)
        gyroscopeData = new Vector3(
            Mathf.Sin(Time.time * 0.8f) * 0.05f,   // Small X rotation
            Mathf.Cos(Time.time * 0.6f) * 0.03f,   // Small Y rotation
            Mathf.Sin(Time.time * 0.4f) * 0.04f    // Small Z rotation
        );

        // Simulate magnetometer data (Earth's magnetic field)
        magnetometerData = new Vector3(
            22.5f + Mathf.Sin(Time.time * 0.1f) * 2f,   // ~North component
            -5.2f + Mathf.Cos(Time.time * 0.15f) * 1f,  // East component  
            -42.8f + Mathf.Sin(Time.time * 0.12f) * 1.5f // Down component
        );
    }

    /// <summary>
    /// Update UI text displays if assigned
    /// </summary>
    private void UpdateUI()
    {
        if (AccelText != null)
        {
            AccelText.text = $"Accel: {accelerometerData.x:F3}, {accelerometerData.y:F3}, {accelerometerData.z:F3}";
        }

        if (GyroText != null)
        {
            GyroText.text = $"Gyro: {gyroscopeData.x:F3}, {gyroscopeData.y:F3}, {gyroscopeData.z:F3}";
        }

        if (MagText != null)
        {
            MagText.text = $"Mag: {magnetometerData.x:F1}, {magnetometerData.y:F1}, {magnetometerData.z:F1}";
        }
    }

    /// <summary>
    /// Stop all sensor loops when application loses focus or is destroyed
    /// </summary>
    public void StopSensors()
    {
#if ENABLE_WINMD_SUPPORT
        try
        {
            if (researchMode != null)
            {
                researchMode.StopAllSensorDevice();
                if (showDebugLogs)
                    Debug.Log("Research Mode sensors stopped");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error stopping sensors: {e.Message}");
        }
#endif
    }

    void OnDestroy()
    {
        StopSensors();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            StopSensors();
        }
    }

    // Public API for other scripts
    public Vector3 GetAccelerometerData() => accelerometerData;
    public Vector3 GetGyroscopeData() => gyroscopeData;
    public Vector3 GetMagnetometerData() => magnetometerData;
    public bool IsIMUActive() => isInitialized && enableIMUReading;

    // Utility methods
    public float GetAccelerometerMagnitude() => accelerometerData.magnitude;
    public Vector3 GetGravityDirection() => accelerometerData.normalized;
    public float GetAngularVelocityMagnitude() => gyroscopeData.magnitude;
    public Vector3 GetMagneticFieldDirection() => magnetometerData.normalized;
}
