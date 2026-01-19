using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridCellObject : MonoBehaviour
{    
    public int cellX;
    public int cellY;
    public float arrivalTime;
    public float flameHeight;
    public float fireIntensity;

    public void UpdateCell(Material cellMaterial)
    {
        Renderer cellRenderer = this.GetComponent<Renderer>();
        cellRenderer.material = cellMaterial;
    }
}
