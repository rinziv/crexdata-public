using UnityEngine;
using Mapbox.Unity;
using Mapbox.Utils;
using System.Collections;

public class GPSLocationProvider : MonoBehaviour
{
    public static GPSLocationProvider Instance { set; get; }

    // Chania Centre (35.513746, 24.017531), Dikastiria (35.510013, 24.030095), Hmmy (35.531299, 24.068385), Lesxh (35.527726, 24.069051)
    [Header("Geographic Coordinates")]
    public double latitude = 35.531299;  // Hardcoded latitude
    public double longitude = 24.068385; // Hardcoded longitude


    [Header("GPS Update Settings")]
    public float GPSUpdateTime = 2.0f; // Update interval in seconds

    [Header("Editor Simulation Settings")] // Editor-only settings, Move the user in editor mode
    public Vector3 originPosition = Vector3.zero; // Unity world position corresponding to the hardcoded lat/lon
    public float metersPerDegreeLatitude = 111000f; // Approximate meters per degree latitude
    public float metersPerDegreeLongitude = 111000f; // Approximate meters per degree longitude at the origin's latitude


    private void Awake()
    {
        Instance = this;

#if UNITY_EDITOR
        //StartCoroutine(SimulateGPSService());
#endif
    }

    private IEnumerator SimulateGPSService()
    {
        // In the Unity Editor, initialize the origin position
        Debug.Log("Unity Editor detected. Using camera-based GPS simulation.");
        originPosition = Camera.main.transform.position;
        
        // In the Unity Editor, derive GPS data from camera position
        Vector3 cameraPosition = Camera.main.transform.position;

        // Calculate the delta from the origin
        Vector3 deltaPosition = cameraPosition - originPosition;

        // Convert delta meters to degrees
        double deltaLatitude = deltaPosition.z / metersPerDegreeLatitude;
        double deltaLongitude = deltaPosition.x / metersPerDegreeLongitude;
        
        // Update latitude and longitude
        UpdateGPS(latitude + deltaLatitude, longitude + deltaLongitude);
        
        // Wait for GPSUpdateTime seconds before the next update
        yield return new WaitForSeconds(GPSUpdateTime);
       
    }

    public Vector2d GetCurrentLocation()
    {
        // Return the current GPS coordinates as a Vector2d
        return new Vector2d(latitude, longitude);
    }

    public void UpdateGPS(double lat, double lon)
    {
        latitude = lat;
        longitude = lon;
    }

}
