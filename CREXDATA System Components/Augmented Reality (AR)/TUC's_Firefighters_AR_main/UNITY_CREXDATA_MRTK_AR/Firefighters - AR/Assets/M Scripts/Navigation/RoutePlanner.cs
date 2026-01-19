using UnityEngine;
using Mapbox.Unity;
using Mapbox.Directions;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using System.Collections.Generic;
using ExtraFunctionsMapbox = MapboxExtraFunctions;
using FunctionsRoutePlanner = RoutePlannerFunctions;
using Mapbox.VectorTile.Geometry;

public class RoutePlanner : MonoBehaviour
{
    //-- UI Manager --//
    public UIManager UIM;

    //--- Map ---//
    public GPSLocationProvider gpsLocationProvider;
    public AbstractMap map;

    //--- Arrow | Waypoints ---//
    public GameObject arrowPrefab;
    public GameObject waypointPrefab;
    public GameObject _arrowInstance;
    public int _nextWaypointIndex = 1;
    public List<Vector3> waypointScenePositions; // Stores waypoint positions in the Unity scene
    public List<GameObject> waypointsList;

    //--- Route ---//
    private bool routeDisplayed = false;
    private Vector2d _startLocation;
    public Vector2d _endLocation;
    public Material routeMaterial;
    public float routeWidth = 0.05f;
    private Directions _directions;
    private GameObject _routeGO;
    private List<Vector3> routeGeometry; // Declared as a class-level variable
    public GameObject routeOptionsPanel;


    private float time = 0;

    void Start()
    {
        // Initialize Directions
        _directions = MapboxAccess.Instance.Directions;

        // Iniatialize waypointScenePositions list, waypoints list, and routeGeometry
        waypointScenePositions = new List<Vector3>();
        waypointsList = new List<GameObject>();
        routeGeometry = new List<Vector3>();
    }

    void Update()
    {
        // Update the arrow position every X (0.1) seconds
        time += Time.deltaTime;
        if(time > 0.1f){
            if (_arrowInstance != null && routeGeometry != null && routeGeometry.Count > _nextWaypointIndex)
                RoutePlannerFunctions.UpdateArrow(this);

            time = 0;
        }
    }

    public void ShowRoute(Vector2d destination)
    {
        _startLocation = new Vector2d(gpsLocationProvider.latitude, gpsLocationProvider.longitude);
        _endLocation = destination;

        CalculateRoute(_startLocation, _endLocation);
        UIM.OpenPanel(routeOptionsPanel);
    }

    public void ShowRouteScenePosition(Vector3 worldPosition)
    {   
        _startLocation = new Vector2d(gpsLocationProvider.latitude, gpsLocationProvider.longitude);
        _endLocation = map.WorldToGeoPosition(worldPosition);
        
        CalculateRoute(_startLocation, _endLocation);
    }

    private void CalculateRoute(Vector2d startLocation, Vector2d endLocation)
    {
        var directionResource = new DirectionResource(new Vector2d[] { startLocation, endLocation }, RoutingProfile.Walking);
        directionResource.Steps = true;

        _directions.Query(directionResource, HandleDirectionsResponse);
    }

    void HandleDirectionsResponse(DirectionsResponse response)
    {
        if (response == null)
        {
            Debug.LogError("Directions response is null. The API call may have failed.");
            return;
        }
        else if (response.Routes == null || response.Routes.Count == 0)
        {
            Debug.LogError("No routes found in the directions response.");
            return;
        }

        // Clear existing route
        ClearRoute();
        routeDisplayed = true;

        for(int i = 0; i < response.Routes[0].Geometry.Count; i++){
            
            Vector2d point = response.Routes[0].Geometry[i];

            print("Point: " + point);
            
            Vector3 worldPos = map.GeoToWorldPosition(point, true);
            routeGeometry.Add(worldPos);
        }

        // Create line renderer
        _routeGO = FunctionsRoutePlanner.LineRendererSetup(_routeGO, map, routeGeometry, routeMaterial, routeWidth);
        _routeGO.transform.localPosition += new Vector3(0, 2.2f, 0);

        // Instantiate the arrow
        if (_arrowInstance == null)
            _arrowInstance = Instantiate(arrowPrefab);

        // Place waypoints at corresponding Unity scene positions
        ClearWaypoints();
        RoutePlannerFunctions.PlaceWaypoints(response.Routes[0], this);

        // Update Arrow
        RoutePlannerFunctions.UpdateArrow(this);
    }

    public void RespawnRoute(){
        CalculateRoute(_startLocation, _endLocation);
    }

    public void TogleRouteDisplay(){

        if(!routeDisplayed)
        {
            ShowRoute(_endLocation);
            routeDisplayed = true;
        }
        else
        {
            ClearRoute();
            routeDisplayed = false;
        }
    }

    public void ClearRoute(){
        
        // Clear existing route
        if(_routeGO != null)
            Destroy(_routeGO);
        
        if (_arrowInstance != null)
            Destroy(_arrowInstance);
        
        routeGeometry.Clear();

        ClearWaypoints();
        
        routeDisplayed = false;
    }

    private void ClearWaypoints(){

        // Clear existing waypoints
        ExtraFunctionsMapbox.ClearMarkers(waypointsList);
        waypointsList.Clear();
        waypointScenePositions.Clear(); 
    }

    public bool getState(){

        return routeDisplayed;
    }

}
