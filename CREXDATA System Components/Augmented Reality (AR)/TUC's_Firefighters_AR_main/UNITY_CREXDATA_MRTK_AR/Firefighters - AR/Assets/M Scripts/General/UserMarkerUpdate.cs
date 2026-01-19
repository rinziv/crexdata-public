using System.Collections;
using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using ExtraFunctionsMapbox = MapboxExtraFunctions;

public class UserMarkerUpdate : MonoBehaviour
{
    public GPSLocationProvider GPSProvider;
    public AbstractMap map;
    public float updateRatio = 0.1f;

    // Map Centering
    public Transform mapParent;


    void Start()
    {
        StartCoroutine(RepeatedFunction());
    }

    IEnumerator RepeatedFunction()
    {
        while (true)
        {
            UpdateMarkerPosition();

            yield return new WaitForSeconds(updateRatio);
            //Debug.Log("position updated");
        }
    }

    private void UpdateMarkerPosition(){

        Vector2d gpsPosition = new Vector2d(GPSProvider.latitude, GPSProvider.longitude);
        transform.position = map.GeoToWorldPosition(gpsPosition, true);
    }

    public void UpdateMapCenter()
    {
        UpdateMarkerPosition();
        //Debug.Log(mapParent.position);
        mapParent.position -= new Vector3(this.transform.localPosition.x, this.transform.localPosition.y, 0);
        //Debug.Log(mapParent.position);
        mapParent.localPosition = new Vector3(mapParent.localPosition.x, mapParent.localPosition.y, 0);
    }
    
}
