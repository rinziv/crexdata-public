using Mapbox.Unity.Map;
using UnityEngine;
using Mapbox.Utils;

public class MapController : MonoBehaviour
{
    [SerializeField]
    AbstractMap _mapManager;

    [SerializeField]
    Transform mapTransform;

    private bool _isInitialized = false;
    private Vector3 _initialScale;
    private float _initialZoom;

    // Tracks the current style: false for Normal Streets, true for Satellite Streets
    private bool isSatelliteStreet = false;

    // Map zoom Handling
    public int totalStates = 3;
    public float objectZoomFactor = 1.6818f;

    // Managers That display markers on map
    public FireVisualizer fireVisualizer;
    public FirefighterLocator fireFighterLocator;
    public POIManager poiManager;
    public UserMarkerUpdate UMU;
    public RoutePlanner routeManager;
    public TargetLocationManager targetLocationManager;
    public FireSpreadGrid fireGridManager;
    public FireSimulationLoader fireSpreadSimulation;
    public WeatherDataManager weatherDataManager;

    void Awake()
    {
        _mapManager.OnInitialized += () =>
        {
            _isInitialized = true;
            _initialScale = mapTransform.localScale;
            _initialZoom = _mapManager.Zoom;
        };
    }

    public void ZoomMap(int direction)
    {
        /*
        // Hold Marker Last Position
        Vector2d weatherMarkerLocation = Vector2d.zero;
        if(weatherDataManager.targetLocationSet){
            weatherMarkerLocation = _mapManager.WorldToGeoPosition(weatherDataManager.targetLocationMarker.transform.position);
        }*/

        if (!_isInitialized) return;

        float zoomFactor = (float)direction * (1/(float)totalStates);
        var zoom = Mathf.Clamp(_mapManager.Zoom + zoomFactor, 0.0f, 21.0f);

        if (Mathf.Abs(zoom - _mapManager.Zoom) > 0.0f)
        {
            _mapManager.UpdateMap(_mapManager.CenterLatitudeLongitude, zoom);
        }

        float objectZoom = objectZoomFactor;
        if(direction == 1){
            objectZoom = 1.0f/objectZoomFactor;
        }

        UMU.UpdateMapCenter();
        fireVisualizer.currentMapZoomLevel *= objectZoom;
        RespawnMapMarkers(objectZoom);

        /*
        // Reposition Marker
        if(weatherDataManager.targetLocationSet){
            weatherDataManager.targetLocationMarker.transform.position = _mapManager.GeoToWorldPosition(weatherMarkerLocation);
        }
        */
    }

    private void RespawnMapMarkers(float zoom){

        if(fireVisualizer.getState()){
            fireVisualizer.TogleFireMapDisplay();
            fireVisualizer.TogleFireMapDisplay();
            //fireVisualizer.AdjustScale(zoom);
        }
        
        if(fireFighterLocator.getState()){
            fireFighterLocator.TogleFirefighters();
            fireFighterLocator.TogleFirefighters();
        }

        if(poiManager.getState()){
            poiManager.TogglePOIs();
            poiManager.TogglePOIs();
        }

        if(targetLocationManager.getState()){
            targetLocationManager.RecalculateRoute();
        }

        if(routeManager.getState()){
            routeManager.RespawnRoute();
        }
        
        if(fireGridManager.getState()){
            fireGridManager.ScaleGrid(zoom);
            fireGridManager.placeGridOnMap();
        }

        if(fireSpreadSimulation.getState()){
            fireSpreadSimulation.ScaleGrid(zoom);
            fireSpreadSimulation.placeGridOnMap();
        }
    }

    public void ToggleStyle()
    {
        // Ensure the map reference is set
        if (_mapManager == null)
        {
            Debug.LogError("AbstractMap reference is not set!");
            return;
        }

        // Toggle the style flag
        isSatelliteStreet = !isSatelliteStreet;

        // Set the new style based on the flag
        ImagerySourceType newSource = isSatelliteStreet ? ImagerySourceType.MapboxSatelliteStreet : ImagerySourceType.MapboxStreets;
        _mapManager.ImageLayer.SetLayerSource(newSource);

        // Refresh the map to apply the new style
        _mapManager.UpdateMap();
    }

}
