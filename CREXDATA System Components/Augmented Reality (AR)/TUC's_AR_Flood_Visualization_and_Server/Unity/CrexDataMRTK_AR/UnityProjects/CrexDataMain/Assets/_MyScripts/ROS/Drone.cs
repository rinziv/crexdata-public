using System.Collections;
using TMPro;
using UnityEngine;

public class Drone : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _droneDistanceInfoText;
    [SerializeField] private float refresh_rate = 1f; // Refresh rate in seconds
    private Transform _userTransform;
    private Coroutine _updateDistanceCoroutine;
    public Vector2 droneGPSCoordinates;

    //Event to spawn POI to fire with coordinates
    public delegate void POIDropEventHandler(Transform dronePosition, POIType poiType, Vector2 gpsCoordinates);
    public static event POIDropEventHandler OnPOIDrop;


    void Awake()
    {
        _userTransform = Camera.main.transform;
        if (_droneDistanceInfoText != null)
        {
            _droneDistanceInfoText.text = "0.00 m";
        }
        //_updateDistanceCoroutine = StartCoroutine(UpdateDistance());
    }
    IEnumerator UpdateDistance()
    {
        while (true)
        {
            Vector3 objectPositionXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 userPositionXZ = new Vector3(_userTransform.position.x, 0, _userTransform.position.z);
            float distance = Vector3.Distance(objectPositionXZ, userPositionXZ);
            if (_droneDistanceInfoText != null)
                _droneDistanceInfoText.text = $"{distance:F2} m";
            yield return new WaitForSeconds(refresh_rate); // Wait for 1 second
        }
    }

    //If this item is disabled stop the distance update coroutine
    private void OnDisable()
    {
        if (_updateDistanceCoroutine != null)
        {
            StopCoroutine(_updateDistanceCoroutine);
            _updateDistanceCoroutine = null;
        }
        if (_droneDistanceInfoText != null)
        {
            _droneDistanceInfoText.text = "0.00 m"; // Reset distance text
        }
    }

    // If this item is enabled start the distance update coroutine
    private void OnEnable()
    {
        if (_updateDistanceCoroutine == null)
        {
            _updateDistanceCoroutine = StartCoroutine(UpdateDistance());
        }
        if (_droneDistanceInfoText != null)
        {
            _droneDistanceInfoText.text = "0.00 m"; // Reset distance text
        }
    }

    //Make drone ebla to drop and spawn POIs when this method is called
    public void DropPOI(POIType poiType)
    {
        Debug.Log("Dropping POI at drone's current position: " + transform.position);
        if (OnPOIDrop != null)
        {
            OnPOIDrop(transform, poiType, droneGPSCoordinates);
        }
        else
        {
            Debug.LogWarning("No listeners for OnPOIDrop event.");
        }
    }
}
