using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using UnityEngine.UI;
using ExtraFunctionsMapbox = MapboxExtraFunctions;
using UnityEngine.InputSystem.EnhancedTouch;

public class MapInitializer : MonoBehaviour
{
    // Reference to the AbstractMap (Mapbox Map)
    public AbstractMap map;
    public GPSLocationProvider gpsLocationProvider;

    // Initial latitude, longitude, and zoom level
    public float initialZoomLevel;

    void Start()
    {
        // Initialize the map with the specified latitude, longitude, and zoom level
        //InitializeMap(gpsLocationProvider.latitude, gpsLocationProvider.longitude, initialZoomLevel);

    }

    public void StartMap()
    {
        // Initialize the map with the specified latitude, longitude, and zoom level
        InitializeMap(gpsLocationProvider.latitude, gpsLocationProvider.longitude, initialZoomLevel);
    }

    // Method to initialize the map
    public void InitializeMap(double latitude, double longitude, float zoomLevel)
    {
        // Convert latitude and longitude to a Mapbox Vector2d
        Vector2d initialLocation = new Vector2d(latitude, longitude);

        // Set the map's center and zoom level
        map.Initialize(initialLocation, (int)zoomLevel);

        // Optionally, update the map after initializing (depending on your setup)
        map.UpdateMap();

        
        map.transform.localPosition = new Vector3(0, 0, 0);

    }

}
