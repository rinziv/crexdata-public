using MixedReality.Toolkit;
using System.Collections.Generic;
using UnityEngine;

public class FilterMenu : MonoBehaviour
{

    private GPSSpawner gpsSpawner;

    [SerializeField]
    private StatefulInteractable stationToggle, hospitalToggle, schoolToggle, churchToggle, generalToggle, maholeToggle, detectedAlertsToggle;
    void Awake()
    {
        gpsSpawner = FindObjectOfType<GPSSpawner>();
        if (gpsSpawner == null)
        {
            Debug.LogError("FilterMenu: GPSSpawner is not assigned and could not be found in the scene!");
            enabled = false; // Disable this script if GPSSpawner is not found
            return;
        }
        schoolToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.School, schoolToggle.IsToggled));
        hospitalToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.Hospital, hospitalToggle.IsToggled));
        stationToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.Station, stationToggle.IsToggled));
        churchToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.Church, churchToggle.IsToggled));
        generalToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.General, generalToggle.IsToggled));
        maholeToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.Manhole, maholeToggle.IsToggled));
        detectedAlertsToggle.OnClicked.AddListener(() => OnInteractableClicked(POIType.DetectedPerson, detectedAlertsToggle.IsToggled));
    }
    // Start is called before the first frame update
    private void OnInteractableClicked(POIType poiType, bool isToggled)
    {
        //Debug.Log("Interactable clicked. POIType: " + poiType + ", isToggled: " + isToggled);

        // Get the list of objects for this POI type
        if (gpsSpawner.spawnedObjects.TryGetValue(poiType, out List<GameObject> objects))
        {
            //Debug.Log("Found " + objects.Count + " objects for POIType: " + poiType);

            // Set the active state of each object
            foreach (GameObject obj in objects)
            {
                //Debug.Log("Setting active state of object to " + isToggled);
                obj.SetActive(isToggled);
            }
        }
        else
        {
            Debug.Log("No objects found for POIType: " + poiType);
        }
    }
}
