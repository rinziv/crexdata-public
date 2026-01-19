using UnityEngine;
using System.Collections.Generic;
using System.IO.Compression;
using FSUF = FireSpreadUsefullFunctions;
using FireSimulation.Enums;
using Mapbox.Unity;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using UnityEngine.UI;

public class FireSpreadGrid : MonoBehaviour
{
    // Grid dimensions
    [Header("Grid Settings")]
    public int width = 20;
    public int height = 20;

    // Maximum time periods
    public int maxTime = 20;

    // Initial fire sources
    [Header("Initial Fire Sources")]
    public List<Vector2Int> initialFireSources;

    [System.Serializable]
    public struct Cell
    {
        public int burnTime; // Time when the cell will START burning (-1 if never)
        public FireState[] state; // Array of states for each time period
    }

    [Header("Fire Spread Settings")]
    [Range(0f, 1f)]
    public float burnChance = 0.45f;

    [Header("Wind Settings")]
    public WindDirection windDirection = WindDirection.None;
    [Range(0f, 1f)]
    public float windStrength = 0.2f;

    // Current time period being visualized
    public int stateTime = 0;

    private Cell[,] cellGrid;

    // Prefab for cell visualization (e.g., a colored cube)
    public GameObject cellPrefab;
    [Header("Tile Materials")]
    public Material nothingMAT;
    public Material burnedMAT;
    public Material burningMAT;
    public Material willburnMAT;

    public float cellSize = 0.0093f;

    // Parent object to hold all cell instances
    public GameObject cellParent;

    // 2D array to hold references to cell GameObjects
    private GameObject[,] cellObjects;

    // InitialFireLocation
    public GameObject gridHolder;
    public Vector2d fireLocation;
    public AbstractMap map;

    public Vector3 spawnOffset;

    private bool enabled = false;


    private static readonly Vector2Int[] NeighborOffsets = new Vector2Int[]
    {
        new Vector2Int(0, 1),  // Up
        new Vector2Int(0, -1), // Down
        new Vector2Int(-1, 0), // Left
        new Vector2Int(1, 0)   // Right
    };

    void Start()
    {
        InitializeGrids();
        SpreadFire();
        InitializeVisualization();
        UpdateVisualization();

        Invoke("placeGridOnMap", 3f);
    }

    void InitializeGrids()
    {
        cellGrid = new Cell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cellGrid[x, y].burnTime = -1; // Default: never burns
                cellGrid[x, y].state = new FireState[maxTime + 1]; // state array for t=0 to t=maxTime
                
            }
        }

        // Set initial fire sources
        foreach (var source in initialFireSources)
        {
            if (IsWithinGrid(source.x, source.y))
            {
                cellGrid[source.x, source.y].burnTime = 0; // Starts burning at time 0
            }
        }
    }

    void SpreadFire()
    {
        // Calculate all 'burnTime' values based on spread logic
        for (int t = 0; t < maxTime; t++) 
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (cellGrid[x, y].burnTime == t) // If this cell starts burning at current time 't'
                    {
                        foreach (Vector2Int offset in NeighborOffsets)
                        {
                            int nx = x + offset.x;
                            int ny = y + offset.y;

                            // If neighbor is valid and not yet scheduled to burn
                            if (IsWithinGrid(nx, ny) && cellGrid[nx, ny].burnTime == -1)
                            {
                                Vector2Int directionToNeighbor = offset; 
                                WindDirection neighborDirection = FSUF.GetWindDirectionFromVector(directionToNeighbor);
                                float adjustedBurnChance = FSUF.AdjustBurnChance(burnChance, neighborDirection, windDirection, windStrength);

                                if (Random.Range(0f,1f) <= adjustedBurnChance)
                                {
                                    if (t + 1 <= maxTime)
                                    {
                                        cellGrid[nx, ny].burnTime = t + 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Set the 'state' arrays for all cells based on their final 'burnTime'
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int cellBurnTime = cellGrid[x, y].burnTime;
                for (int t_state = 0; t_state <= maxTime; t_state++) // Iterate through each time step for the state array
                {
                    if (cellBurnTime == -1) // Cell never burns
                    {
                        cellGrid[x, y].state[t_state] = FireState.Nothing;
                    }
                    else // Cell is scheduled to burn at 'cellBurnTime'
                    {
                        if (t_state < cellBurnTime) 
                        {
                            cellGrid[x, y].state[t_state] = (t_state == cellBurnTime - 1) ? FireState.WillBurn : FireState.Nothing;
                        }
                        else if (t_state == cellBurnTime)
                        {
                            cellGrid[x, y].state[t_state] = FireState.Burning;
                        }
                        else // (t_state > cellBurnTime) 
                        {
                            cellGrid[x, y].state[t_state] = FireState.Burned;
                        }
                    }
                }
            }
        }
    }

    // Initializes the visualization by creating cell GameObjects.
    void InitializeVisualization()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Cell Prefab is not assigned.");
            return;
        }

        cellObjects = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(cellParent.transform.position.x - (width * cellSize) / 2 + x * cellSize, cellParent.transform.position.y - (height * cellSize) / 2 + y * cellSize, cellParent.transform.position.z);
                GameObject cell = Instantiate(cellPrefab, position, Quaternion.identity, cellParent.transform);
                float objectScaleAdjuster = 0.5f;
                cell.transform.localScale = new Vector3(cell.transform.localScale.x * objectScaleAdjuster, cell.transform.localScale.y * objectScaleAdjuster, cell.transform.localScale.z * objectScaleAdjuster);
                cellObjects[x, y] = cell;
            }
        }
    }

    // Updates the visualization based on the current stateTime.
    public void UpdateVisualization()
    {
        if (cellGrid == null)
        {
            Debug.LogError("TimeGrids are not initialized.");
            return;
        }

        if (stateTime < 0 || stateTime > maxTime)
        {
            Debug.LogError("stateTime is out of range.");
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject cell = cellObjects[x, y];
                if (cell != null)
                {
                    Renderer cellRenderer = cell.GetComponent<Renderer>();
                    if (cellRenderer != null)
                    {
                        // Update cell material based on state
                        switch (cellGrid[x, y].state[stateTime])
                        {
                            case FireState.Burning:
                                cellRenderer.sharedMaterial = burningMAT;
                                break;
                            case FireState.WillBurn:
                                cellRenderer.sharedMaterial = willburnMAT;
                                break;
                            case FireState.Burned:
                                cellRenderer.sharedMaterial = burnedMAT;
                                break;
                            default: // FireState.Nothing
                                cellRenderer.sharedMaterial = nothingMAT;
                                break;
                        }
                    }
                }
            }
        }
    }

    List<Vector2Int> GetAdjacentCells(int x, int y)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>(); // This still allocates if called

        if (IsWithinGrid(x, y + 1))
            neighbors.Add(new Vector2Int(x, y + 1));
        if (IsWithinGrid(x, y - 1))
            neighbors.Add(new Vector2Int(x, y - 1));
        if (IsWithinGrid(x - 1, y))
            neighbors.Add(new Vector2Int(x - 1, y));
        if (IsWithinGrid(x + 1, y))
            neighbors.Add(new Vector2Int(x + 1, y));

        return neighbors;
    }

    bool IsWithinGrid(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Enum for cell states for better readability
    public enum FireState
    {
        Nothing,
        Burning,
        Burned,
        WillBurn
    }


    public void ScaleGrid(float zoom)
    {
        cellParent.transform.localScale = new Vector3(cellParent.transform.localScale.x * zoom, cellParent.transform.localScale.y * zoom, cellParent.transform.localScale.z);
    }

    public void placeGridOnMap()
    {
        Vector3 targetPos = map.GeoToWorldPosition(fireLocation, true) - map.transform.parent.parent.position;
        Debug.Log("Target: " + targetPos);
        gridHolder.transform.localPosition = new Vector3(targetPos.x, targetPos.y, gridHolder.transform.localPosition.z);
    }

    public void ToggleFireSpread()
    {
        enabled = !enabled;
    }

    public bool getState()
    {
        return enabled;
    }
}