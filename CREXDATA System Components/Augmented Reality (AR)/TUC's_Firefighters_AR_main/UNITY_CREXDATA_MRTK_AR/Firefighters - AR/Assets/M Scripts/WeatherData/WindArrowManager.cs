using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using ExtraFunctionsMapbox = MapboxExtraFunctions;

public class WindArrowManager : MonoBehaviour
{
    public GameObject arrowPrefab;  // Assign your arrow prefab in the inspector
    public int gridSizeX;  // Number of arrows along X-axis
    public int gridSizeZ;  // Number of arrows along Z-axis
    public float spacing = 5.0f;  // Spacing between arrows
    public Transform windArrowParent;  // Parent object for the arrows

    private bool windDisplayed = false;
    public List<GameObject> windArrowsList = new List<GameObject>();

    // Spawn the grid of arrows centered around the user's position
    public void SpawnArrows()
    {
        // Get the user's (main camera) position
        Vector3 userPosition = Camera.main.transform.position;

        // Calculate the offset to center the grid around the user
        float gridOffsetX = (gridSizeX - 1) * spacing / 2f;
        float gridOffsetZ = (gridSizeZ - 1) * spacing / 2f;

        // Spawn arrows with the center aligned to the user's position
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                // Adjust the position so the grid is centered around the user
                Vector3 position = new Vector3(x * spacing - gridOffsetX, 0, z * spacing - gridOffsetZ) + new Vector3(userPosition.x, 0, userPosition.z);
                GameObject arrow = Instantiate(arrowPrefab, position, Quaternion.identity, windArrowParent);
                arrow.transform.eulerAngles = new Vector3(90, 180, 0);
                windArrowsList.Add(arrow);
            }
        }
    }
    
    public void ClearWindArrows(){

        ExtraFunctionsMapbox.ClearMarkers(windArrowsList);
    }

    // Update the rotation of all arrows based on the wind direction
    public void UpdateArrowDirections(float windDirection)
    {
        if(windDisplayed){
            Vector3 arrowRotationOffset = new Vector3(90, 180, 0);
            Vector3 arrowRotation = new Vector3(0, -windDirection, 0) + arrowRotationOffset;
            foreach (GameObject arrow in windArrowsList)
            {
                arrow.transform.eulerAngles = arrowRotation;
                arrow.GetComponent<ArrowMover>().maxDistance = 3f / new Vector2(Mathf.Cos(windDirection * Mathf.Deg2Rad), Mathf.Sin(windDirection * Mathf.Deg2Rad)).magnitude;
            }
        }        
    }
    
    public void TogleWindDisplay(){

        if(!windDisplayed)
        {
            SpawnArrows();
            windDisplayed = true;
        }
        else
        {
            ClearWindArrows();
            windDisplayed = false;
        }
    }
}
