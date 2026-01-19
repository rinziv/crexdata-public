using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System;

public static class MapboxExtraFunctions
{
    public static GameObject DisplayOnMap(AbstractMap map, GameObject prefab, Vector2d GPSlocation, float scaleFactor, Transform parent, string name){

        // Instantiate the map marker prefab
        GameObject mapMarker = GameObject.Instantiate(prefab);

        // Set the new object's parent
        mapMarker.transform.SetParent(parent);

        // Translate the GPS coordinates to Unity world position relative to the map
        mapMarker.transform.position = map.GeoToWorldPosition(GPSlocation, true);

        // Apply the zoom-adjusted scale to the marker
        mapMarker.transform.localScale = mapMarker.transform.localScale * scaleFactor;
    
        // Match the map’s rotation
        mapMarker.transform.rotation = map.transform.rotation * Quaternion.Euler(90, 0, 0);

        mapMarker.name = name;

        return mapMarker;
    }

    public static void ClearMarkers(List<GameObject> markersList){

        foreach (var marker in markersList){
            marker.Destroy();
        }

        markersList.Clear();
    }

    public static double CalculateDistanceInScene(Vector2d userPosition, Vector2d objectPosition)
    {
        const double R = 6371000; // Earth's radius in meters

        // Convert latitude and longitude from degrees to radians
        double lat1Rad = DegreesToRadians(userPosition.x);
        double lat2Rad = DegreesToRadians(objectPosition.x);
        double deltaLat = DegreesToRadians(objectPosition.x - userPosition.x);
        double deltaLon = DegreesToRadians(objectPosition.y - userPosition.y);

        // Haversine formula
        double a = Mathf.Sin((float)deltaLat / 2) * Mathf.Sin((float)deltaLat / 2) +
                   Mathf.Cos((float)lat1Rad) * Mathf.Cos((float)lat2Rad) *
                   Mathf.Sin((float)deltaLon / 2) * Mathf.Sin((float)deltaLon / 2);

        double c = 2 * Mathf.Atan2(Mathf.Sqrt((float)a), Mathf.Sqrt((float)(1 - a)));

        double distance = R * c;

        return distance; // Distance in meters
    }

    public static Vector3 CalculatePositionInScene(Vector2d userPosition, Vector2d objectPosition)
    {
        const double EarthRadius = 6378137; // WGS84 equatorial radius in meters
        const double DegToRad = Math.PI / 180.0;
        const double MetersPerDegree = DegToRad * EarthRadius; // ~111319.5 meters per degree

        double latRad = userPosition.x * DegToRad;
        double latOffset = (objectPosition.x - userPosition.x) * MetersPerDegree;
        double lonOffset = (objectPosition.y - userPosition.y) * MetersPerDegree * Math.Cos(latRad);

        return new Vector3((float)lonOffset, 0, (float)latOffset);
    }

    public static Vector2d ConvertUnityToGeo(Vector2 objectPositionXZ, Vector3 userUnityPosition, Vector2d GPSlocation)
    {
        const double EarthRadius = 6371000; // Earth's radius in meters

        double userLatitude = GPSlocation.x;
        double userLongitude = GPSlocation.y;

        // Get the offset between the object's position and user's position in Unity
        float offsetX = objectPositionXZ.x - userUnityPosition.x;
        float offsetZ = objectPositionXZ.y - userUnityPosition.z;

        // Calculate the distance in meters based on Unity offset (assuming 1 Unity unit = 1 meter)
        double distanceMeters = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);

        // Calculate the bearing from user to the object
        double bearingRadians = Mathf.Atan2(offsetZ, offsetX);

        // Convert user's latitude and longitude from degrees to radians
        double userLatitudeRad = DegreesToRadians(userLatitude);
        double userLongitudeRad = DegreesToRadians(userLongitude);

        // Calculate new latitude using the Haversine formula
        double newLatitudeRad = Mathf.Asin((float)(Mathf.Sin((float)userLatitudeRad) * Mathf.Cos((float)(distanceMeters / EarthRadius)) +
            Mathf.Cos((float)userLatitudeRad) * Mathf.Sin((float)(distanceMeters / EarthRadius)) * Mathf.Cos((float)bearingRadians)));

        // Calculate new longitude using the Haversine formula
        double newLongitudeRad = userLongitudeRad + Mathf.Atan2(
            Mathf.Sin((float)bearingRadians) * Mathf.Sin((float)(distanceMeters / EarthRadius)) * Mathf.Cos((float)userLatitudeRad),
            Mathf.Cos((float)(distanceMeters / EarthRadius)) - Mathf.Sin((float)userLatitudeRad) * Mathf.Sin((float)newLatitudeRad));

        // Convert radians back to degrees
        double newLatitude = RadiansToDegrees(newLatitudeRad);
        double newLongitude = RadiansToDegrees(newLongitudeRad);

        return new Vector2d(newLongitude, newLatitude);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Mathf.PI / 180);
    }

    private static double RadiansToDegrees(double radians)
    {
        return radians * (180 / Mathf.PI);
    }

    public static float CalculateBearing(Vector2d userPosition, Vector2d waypoint)
    {
        // Convert latitude and longitude from degrees to radians
        float lat1Rad = (float)DegreesToRadians(userPosition.x);
        float lat2Rad = (float)DegreesToRadians(waypoint.x);
        float deltaLonRad = (float)DegreesToRadians(waypoint.y - userPosition.y);

        // Calculate bearing
        float y = Mathf.Sin(deltaLonRad) * Mathf.Cos(lat2Rad);
        float x = Mathf.Cos(lat1Rad) * Mathf.Sin(lat2Rad) -
                  Mathf.Sin(lat1Rad) * Mathf.Cos(lat2Rad) * Mathf.Cos(deltaLonRad);

        float bearing = Mathf.Atan2(y, x);
        bearing = bearing * Mathf.Rad2Deg; // Convert from radians to degrees

        // Normalize bearing to 0-360 degrees
        return (bearing + 360) % 360;
    }

}
