using Mapbox.Directions;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using ExtraFunctionsMapbox = MapboxExtraFunctions;


public static class RoutePlannerFunctions
{
    
    public static GameObject LineRendererSetup(GameObject _routeGO, AbstractMap map, List<Vector3> routeGeometry, Material routeMat, float routeWidth){
        
        _routeGO = new GameObject("Route");
        _routeGO.transform.SetParent(map.gameObject.transform.parent);

        List<Vector3> positions = routeGeometry.ToList();

        // Convert world positions to local positions relative to _routeGO
        for (int i = 0; i < positions.Count; i++)
        {
            positions[i] = _routeGO.transform.InverseTransformPoint(positions[i]);
        }

        // Add and configure LineRenderer
        LineRenderer lineRenderer = _routeGO.AddComponent<LineRenderer>();
        lineRenderer.positionCount = routeGeometry.Count;
        lineRenderer.SetPositions(positions.ToArray());
        lineRenderer.material = routeMat;
        lineRenderer.widthMultiplier = routeWidth;
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment = LineAlignment.View;

        // Apply a small offset to the Z position of each point
        Vector3[] positionsTMP = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(positionsTMP);

        for (int i = 0; i < positionsTMP.Length; i++)
        {
            positionsTMP[i].z = positionsTMP[i].z - 0.0001f;
        }

        // Update the LineRenderer with the modified positions
        lineRenderer.SetPositions(positionsTMP);

        // Optional visual enhancements
        lineRenderer.sortingLayerName = "Default";
        lineRenderer.sortingOrder = 0;

        return _routeGO;
    }


    public static void PlaceWaypoints(Route route, RoutePlanner rp)
    {
        // Get user's current GPS position
        Vector2d userPosition = new Vector2d(rp.gpsLocationProvider.latitude, rp.gpsLocationProvider.longitude);
        
        // Place waypoints at corresponding positions
        var steps = route.Legs[0].Steps;

        for(int i = 0; i < steps.Count; i++){
            
            Step step = steps[i];
            
            Vector2d waypointLocation = step.Maneuver.Location;

            // Calculate relative position of the POI in scene
            Vector3 waypointLocationScene = ExtraFunctionsMapbox.CalculatePositionInScene(userPosition, waypointLocation);

            // Create the waypoint position in Unity world space
            float userHeight = Camera.main.transform.position.y;

            rp.waypointScenePositions.Add(waypointLocationScene);

            // Instantiate waypoint prefab at the position
            GameObject waypoint = GameObject.Instantiate(rp.waypointPrefab, waypointLocationScene, Quaternion.identity);
            waypoint.transform.SetParent(rp.gameObject.transform);
            rp.waypointsList.Add(waypoint);
        }
    }

    public static void UpdateArrow(RoutePlanner rp)
    {   
        // Check if there is a next waypoint to navigate to
        if(rp.waypointScenePositions.Count > rp._nextWaypointIndex){
            
            // Get the next waypoint(checkpoint) position and user position
            Vector3 nextWaypoint = rp.waypointScenePositions[rp._nextWaypointIndex];
            Vector3 userScenePosition = Camera.main.transform.position;
            Vector3 direction = nextWaypoint - userScenePosition;

            // Calculate the distance to the next waypoint
            Vector2 userScenePosition2D = new Vector2(userScenePosition.x, userScenePosition.z);
            Vector2 nextWaypointPos2D = new Vector2(nextWaypoint.x, nextWaypoint.z);
            float distanceToWaypoint = Vector2.Distance(userScenePosition2D, nextWaypointPos2D);

            float waypointReachThreshold = 2.0f;
            
            // Update next waypoint index if user has passed the current one
            if (distanceToWaypoint < waypointReachThreshold)
                rp.ShowRoute(rp._endLocation);

            // Calculate the arrow's new position, a fixed distance from the base object
            rp._arrowInstance.transform.position = userScenePosition + direction.normalized * 2.0f + new Vector3(0, -1.6f, 0);  // Adjust Y offset as needed
        
            // Rotate the arrow to point toward the next waypoint
            Vector3 arrowRotationOffset = new Vector3(90, 180, 0);
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            Vector3 arrowRotation = new Vector3(0, -angle, 0) + arrowRotationOffset;
            
            if (direction != Vector3.zero)
                rp._arrowInstance.transform.eulerAngles = arrowRotation;
        }
        
    }
}
