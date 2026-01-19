using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.Utils;

public class TargetLocationManager : MonoBehaviour
{
    public GameObject targetLocationMarker;
    public RoutePlanner routePlanner;

    private float time = 0.0f;
    private Vector3 positionLast;
    public float minDistance;
    public bool targetLocationActive = false;

    void Start(){

        positionLast = targetLocationMarker.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;

        if(time > 0.4f && targetLocationActive){

            // Calculate the distance between the previous and current potision of the marker.
            float distance = Vector2.Distance(positionLast, targetLocationMarker.transform.position);
            //Debug.Log(distance);
            if(distance > minDistance){
                positionLast = targetLocationMarker.transform.position;
                RecalculateRoute();
            }

            time = 0.0f;
        }
    }

    public void ToggleTargetLocation(){

        if(!targetLocationActive)
        {
            targetLocationMarker.SetActive(true);
            targetLocationActive = true;
        }
        else
        {
            targetLocationMarker.SetActive(false);
            routePlanner.ClearRoute();
            targetLocationActive = false;
        }
    }

    public void RecalculateRoute(){

        routePlanner.ShowRouteScenePosition(targetLocationMarker.transform.position);
        positionLast = targetLocationMarker.transform.position;
    }

    public bool getState(){

        return targetLocationActive;
    }
}
