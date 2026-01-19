using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.Utils;

public class POIMapObject : MonoBehaviour
{

    public Vector2d GPSLocation;
    public RoutePlanner routePlanner;
    
    public void ShowRouteToPOI(){
        
        routePlanner.ShowRoute(GPSLocation);
    }
}
