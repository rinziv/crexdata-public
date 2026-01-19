using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;using Mapbox.Unity;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using UnityEngine.UI;

public class FireSimulationLoader : MonoBehaviour
{
    // Reference to the CSV file
    public TextAsset fireDataCSV;

    // Sumulation variables
    public int gridSize = 1280;
    public int gridUseSize = 1280; // Percentage of the grid size to use
    public int cellSize = 30; // Size of each cell in meters
    public int minX = 874710; // Minimum X coordinate in the given data
    public int minY = -4247490; // Minimum Y coordinate in the given data

    public int maxTime = 3600; // Maximum time in seconds
    public float spawnPosModifier = 0.01f; // Prefab for the grid cell

    // List of grid cells
    private List<GridCellObject> grid;

    // Prefab and Materials for the grid cell
    public GameObject cellPrefab;
    
    [Header("Tile Materials")]
    public Material nothingMAT;
    public Material burnedMAT;
    public Material burningMAT;
    public Material willburnMAT;
    
    public Transform gridParent;
    public Transform gridHolder;

    // Initialization variables
    public Vector2d fireLocation;
    public AbstractMap map;

    // Control map scale
    private bool enabled = false;
    public MapController mapController;
    
    void Start()
    {
        ParseFireSpreadData();
    }

    void ParseFireSpreadData()
    {
        if (fireDataCSV == null)
        {
            Debug.LogError("CSV file not found");
            return;
        }

        string[] lines = fireDataCSV.text.Split('\n');

        grid = new List<GridCellObject>();
        int rowIndex = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if(rowIndex > 5000) // Limit to 5000 lines for performance
                return;

            // Split by semicolon
            string[] tokens = line.Split(',');

            if (tokens.Length < 5) continue; // Ensure there are enough tokens

            // Convert from comma decimal to dot and parse
            float x = float.Parse(tokens[0], System.Globalization.CultureInfo.InvariantCulture);
            float y = float.Parse(tokens[1].Split(';')[1], System.Globalization.CultureInfo.InvariantCulture);

            int xIndex = Mathf.FloorToInt(Mathf.Abs((x - minX) / cellSize));
            int yIndex = Mathf.FloorToInt(Mathf.Abs((y - minY) / cellSize));

            if (xIndex < gridUseSize && yIndex < gridUseSize)
            {
                try
                {
                    float arrivalTime = float.Parse(tokens[2].Split(';')[1], System.Globalization.CultureInfo.InvariantCulture);
                    float flameHeight = float.Parse(tokens[3].Split(';')[1], System.Globalization.CultureInfo.InvariantCulture);
                    float fireIntensity = float.Parse(tokens[4].Split(';')[1], System.Globalization.CultureInfo.InvariantCulture);

                    // Create a new GridCellObject
                    GameObject cellObject = SpawnGridCell(xIndex, yIndex);
                    
                    // Set the cell values
                    SetCellValues(cellObject, xIndex, yIndex, arrivalTime, flameHeight, fireIntensity);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Error parsing line: " + line + " - " + ex.Message);
                }
            }
            else
            {
                Debug.LogWarning($"Index out of bounds for coordinates ({x}, {y}) - indices ({xIndex}, {yIndex})");
            }
            rowIndex++;
        }

        Debug.Log("CSV loaded. Grid initialized with " + rowIndex + " entries.");
    }

    public void UpdateVisualization(int timeStep){

        foreach (var cell in grid)
        {
            if(cell.arrivalTime > timeStep - 300 && cell.arrivalTime <= timeStep)
                cell.UpdateCell(burningMAT);
            else if (cell.arrivalTime > timeStep && cell.arrivalTime <= timeStep + 300)
                cell.UpdateCell(willburnMAT);
            else if (cell.arrivalTime <= timeStep - 300)
                cell.UpdateCell(burnedMAT);
            else
                cell.UpdateCell(nothingMAT);
        }
    }

    private GameObject SpawnGridCell(int xIndex, int yIndex)
    {
        // Calculate the position based on the index and spawn position modifier
        Vector3 position = new Vector3(xIndex * spawnPosModifier, yIndex * spawnPosModifier, 0);

        // Calculate the scale based on the prefab's scale and spawn position modifier
        float XYaxisScale = cellPrefab.transform.localScale.x * spawnPosModifier * 54f;
        Vector3 scale = new Vector3(XYaxisScale, XYaxisScale, cellPrefab.transform.localScale.z);

        // Instantiate the cell prefab at the calculated position and scale
        GameObject cellObject = Instantiate(cellPrefab, position, Quaternion.identity, gridHolder);
        cellObject.transform.localPosition = position;
        cellObject.transform.localScale = scale;
        
        cellObject.name = $"Cell_{xIndex}_{yIndex}";

        return cellObject;
    }

    private void SetCellValues(GameObject cellObject, int xIndex, int yIndex, float arrivalTime, float flameHeight, float fireIntensity)
    {
        // Check if the cellObject has a GridCellObject component
        if (cellObject == null)
        {
            Debug.LogError("Cell object is null.");
            return;
        }

        GridCellObject cell = cellObject.AddComponent<GridCellObject>();

        cell.cellX = xIndex;
        cell.cellY = yIndex;
        cell.arrivalTime = arrivalTime;
        cell.flameHeight = flameHeight;
        cell.fireIntensity = fireIntensity;

        if(arrivalTime < 120)
            cell.UpdateCell(burningMAT);
        else if (arrivalTime < 420)
            cell.UpdateCell(willburnMAT);
        else
            cell.UpdateCell(nothingMAT);

        grid.Add(cell);
    }
    
    public void placeGridOnMap()
    {
        Vector3 targetPos = map.GeoToWorldPosition(fireLocation, true) - map.transform.parent.parent.position;
        Debug.Log("Target: " + targetPos);
        gridParent.transform.localPosition = new Vector3(targetPos.x, targetPos.y, gridParent.transform.localPosition.z);
    }

    public void ScaleGrid(float zoom)
    {
        gridHolder.transform.localScale = new Vector3(gridHolder.transform.localScale.x * zoom, gridHolder.transform.localScale.y * zoom, gridHolder.transform.localScale.z);
    }

    public void ToggleFireSpread()
    {
        enabled = !enabled;

        if (enabled)
        {
            mapController.ZoomMap(-5);
        }
        else
        {
            mapController.ZoomMap(5);
        }
    }

    public bool getState()
    {
        return enabled;
    }
}
