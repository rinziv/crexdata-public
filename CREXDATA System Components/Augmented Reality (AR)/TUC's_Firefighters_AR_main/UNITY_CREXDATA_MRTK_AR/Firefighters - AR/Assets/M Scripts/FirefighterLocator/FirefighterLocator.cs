using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections.Generic;
using ExtraFunctionsMapbox = MapboxExtraFunctions;
using TMPro; 
using System;

public class FirefighterLocator : MonoBehaviour
{
    public GPSLocationProvider gpsLocationProvider;
    public RoutePlanner routePlanner;
    private bool ffDisplayed = false;

    [Header("Map Settings")]
    public AbstractMap map; // Reference to the Mapbox map
    public GameObject mapParent;
    public GameObject firefighterMapMarkerPrefab; // Prefab for map markers
    public GameObject MapPrefabsParent;
    public GameObject firefighterScenePrefab; // Prefab for AR scene markers
    public GameObject ScenePrefabsParent;

    [Header("Firefighters' Data")]
    public List<Firefighter> firefighters = new List<Firefighter>(); // List of firefighters
    private List<Vector3> firefighterScenePositions = new List<Vector3>(); // Unity scene positions

    private List<GameObject> ffMarkersList = new List<GameObject>();
    private List<GameObject> ffPrefabsList = new List<GameObject>();

    // Firefighter Info panel
    public GameObject FFinfoPanel;
    public TextMeshPro FFname_text;
    public int selectedFirefighter = -1;

    void Start()
    {
        InitializeFirefighters();
    }

    // Initializes hardcoded firefighters locations
    void InitializeFirefighters()
    {
        // Example hardcoded firefighters
        AddFirefighter(0, "John", new Vector2d(35.51238673776553, 24.014829003286554));
        AddFirefighter(1, "George", new Vector2d(35.511635682091764, 24.01630958267915));  
        AddFirefighter(2, "Michael", new Vector2d(35.51392376002959, 24.015333258583272));
    }

    public void UpdateFirefighterPosition(int id, string name, Vector2d gpsLocation){
        // Find the firefighter by ID
        Firefighter firefighter = firefighters.Find(f => f.id == id);
        if (firefighter != null)
        {
            // Update the firefighter's location
            firefighter.GPSlocation = gpsLocation;
        }
        else
        {
            AddFirefighter(id, name, gpsLocation);
            Debug.LogWarning($"Firefighter with ID {id} not found. Adding new firefighter.");
        }
    }

    void AddFirefighter(int id, string name, Vector2d gpsLocation)
    {
        Firefighter firefighter = new Firefighter { id = id, Name = name, GPSlocation = gpsLocation };
        firefighters.Add(firefighter);
    }

    // Displays firefighters on the Mapbox map
    public void DisplayFirefightersOnMap()
    {
        // Get the current zoom level
        float currentZoom = map.Zoom;

        // Adjust the base scale factor based on the zoom level
        float scaleFactor = 0.01f; 

        foreach (var firefighter in firefighters)
        {
            GameObject marker = ExtraFunctionsMapbox.DisplayOnMap(map, firefighterMapMarkerPrefab, firefighter.GPSlocation, scaleFactor, MapPrefabsParent.transform, firefighter.Name);
            ffMarkersList.Add(marker);

            // Assign the Firefighter data to the marker
            FirefighterMarker markerScript = marker.GetComponent<FirefighterMarker>();
            if (markerScript != null)
                markerScript.firefighter = firefighter;
            else
                Debug.LogWarning("FirefighterMarker script not found on marker prefab.");
        }
    }

    public void TogleFirefighters(){

        if(!ffDisplayed)
        {
            DisplayFirefighters();
            ffDisplayed = true;
        }
        else
        {
            ClearFF();
            ffDisplayed = false;
        }
    }

    public void DisplayFirefighters(){
        DisplayFirefightersInScene();
        DisplayFirefightersOnMap();
    }

    public void ClearFF(){
        ExtraFunctionsMapbox.ClearMarkers(ffMarkersList);
        ExtraFunctionsMapbox.ClearMarkers(ffPrefabsList);
    }

    // Displays firefighters in the Unity AR scene relative to the user's position
    public void DisplayFirefightersInScene()
    {
        foreach (var firefighter in firefighters)
        {
            // Get user's current GPS position
            Vector2d userPositionGPS = new Vector2d(gpsLocationProvider.latitude, gpsLocationProvider.longitude);

            // Calculate distance and bearing from user to firefighter
            Vector3 firefighterPositionScene = ExtraFunctionsMapbox.CalculatePositionInScene(userPositionGPS, firefighter.GPSlocation);
            firefighterScenePositions.Add(firefighterPositionScene);

            // Instantiate firefighter prefab at the relative position
            GameObject sceneMarker = GameObject.Instantiate(firefighterScenePrefab);
            sceneMarker.transform.SetParent(ScenePrefabsParent.transform);
            sceneMarker.transform.localPosition = firefighterPositionScene;

            // Tag the firefighter scene markers for easy identification
            sceneMarker.name = "FirefighterScenePrefab";

            ffPrefabsList.Add(sceneMarker);

            // Optional: Add a label or indicator
            //AddLabelToMarker(sceneMarker, firefighter.Name);
        }
    }

    // Adds a text label to a marker
    void AddLabelToMarker(GameObject marker, string label)
    {
        TextMesh textMesh = marker.AddComponent<TextMesh>();
        textMesh.text = label;
        textMesh.characterSize = 0.5f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
    }
    
    // Method to be called when a firefighter marker is clicked
    public void OnFirefighterMarkerClicked(Firefighter firefighter)
    {
        Debug.Log($"Firefighter marker clicked! GPS Location: {firefighter.GPSlocation}");
        FFinfoPanel.SetActive(true);
        FFname_text.text = "Name: " + firefighter.Name;
    }

    // Handler for the "Show Route" button
    public void ShowRouteByFirefighter(){
        
        if (selectedFirefighter != -1)
        {   
            Firefighter firefigter = firefighters.Find(f => f.id == selectedFirefighter);
            Debug.Log(firefighters.Find(f => f.id == selectedFirefighter)); 
            routePlanner.ShowRoute(firefigter.GPSlocation);
            // Hide the panel after initiating navigation
            FFinfoPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("No firefighter selected for navigation.");
        }
    }
    
    public bool getState(){
        return ffDisplayed;
    }
}
