using System.Collections.Generic;
using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using MixedReality.Toolkit.UX;
using Mapbox.Unity.Utilities;
using Mapbox.Unity.MeshGeneration.Factories;
using MixedReality.Toolkit.SpatialManipulation;
using TMPro;
using System.Collections;
using pulse.cdm.bind;

public class MapAndPlayerManager : MonoBehaviour
{
    #region Variables
    public GameObject MapGameobject;
    public AbstractMap map;
    public GameObject Player;

    [Header("Player Rotation Settings")]
    [Tooltip("The camera/HoloLens transform to track for player rotation")]
    [SerializeField] private Transform userCamera;

    [Tooltip("The Y rotation value that represents North (0 degrees). For example, if -170 is North, set this to -170")]
    [SerializeField] private float northOffsetAngle = 0f;

    [Tooltip("Should the player arrow rotate to show the direction the user is facing?")]
    [SerializeField] private bool rotatePlayerWithCamera = true;

    [SerializeField]
    private Vector2d coordinates;
    public Dictionary<GameObject, Vector2d> spawnedPOIs = new();
    private bool mapInitialized = false;

    [SerializeField] private PressableButton _pinMapButton;
    public Transform poisParent;

    #endregion

    public enum MapInteractionState
    {
        Free,       // Following the user (RadialView active)
        Pinned,     // Fixed in place (RadialView disabled)
        Grabbed,    // Being manipulated by user
        Releasing   // Just released from being grabbed
    }

    [SerializeField] private MapInteractionState _mapState = MapInteractionState.Free;

    void Start()
    {
        //_pinMapButton.gameObject.SetActive(false);
        SetUpPinButton();
        GPSSpawner spawner = FindObjectOfType<GPSSpawner>();
        if (spawner != null && spawner.mapGameObject == null)
        {
            spawner.mapGameObject = MapGameobject;
        }

        // Auto-assign main camera if not set
        if (userCamera == null)
        {
            userCamera = Camera.main.transform;
        }

        if (rotatePlayerWithCamera)
        {
            StartCoroutine(UpdatePlayerRotationCoroutine());
        }
    }

    /// <summary>
    /// Coroutine that updates the player arrow rotation every second
    /// </summary>
    private IEnumerator UpdatePlayerRotationCoroutine()
    {
        while (true)
        {
            // Update player rotation if everything is ready
            if (rotatePlayerWithCamera && userCamera != null && Player != null && mapInitialized && MapGameobject.activeSelf)
            {
                UpdatePlayerRotation();
            }

            // Wait for 1 second before next update
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// Updates the player arrow rotation on the map to show the direction the user is facing.
    /// Applies the north offset calibration.
    /// </summary>
    private void UpdatePlayerRotation()
    {
        // Get the camera's current Y rotation
        float cameraYRotation = userCamera.localEulerAngles.y;

        // Calculate the rotation relative to north
        float relativeRotation = cameraYRotation - northOffsetAngle;

        // Normalize to 0-360 range
        relativeRotation = NormalizeAngle(relativeRotation);

        // Apply only Y-axis rotation to the player (keep X and Z as they are)
        Vector3 currentRotation = Player.transform.localEulerAngles;
        Player.transform.localEulerAngles = new Vector3(
            currentRotation.x,
            relativeRotation,
            currentRotation.z
        );
    }

    /// <summary>
    /// Normalizes an angle to the 0-360 range
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle < 0f)
            angle += 360f;
        while (angle >= 360f)
            angle -= 360f;
        return angle;
    }

    public void UpdateNorthOffsetAngle()
    {
        northOffsetAngle = userCamera.localEulerAngles.y;
    }

    private void InitializeMapAtLocation()
    {
        map.SetCenterLatitudeLongitude(coordinates);
        int zoomLevel = (int)map.Zoom;
        map.Initialize(coordinates, zoomLevel);
        mapInitialized = true;
        //UpdateMapAndPlayer();
    }

    public void UpdatePlayerLocation(Vector2d coordinates)
    {
        if (!mapInitialized) return;

        try
        {
            if (MapGameobject.activeSelf)
            {
                map.SetCenterLatitudeLongitude(coordinates);
                map.UpdateMap();
                UpdateSpawnedPOIs();
                Player.transform.position = map.GeoToWorldPosition(coordinates);
                //Debug.Log("Player location:" + Player.transform.position);
            }
        }
        catch (System.NullReferenceException)
        {
            Debug.LogWarning("Map is not initialized yet");
        }
    }

    public void UpdateSpawnedPOIs()
    {
        if (!mapInitialized || !MapGameobject.activeSelf) return;

        foreach (KeyValuePair<GameObject, Vector2d> poi in spawnedPOIs)
        {
            poi.Key.transform.position = map.GeoToWorldPosition(poi.Value, true);
            //poi.Key.transform.localPosition = new Vector3(poi.Key.transform.position.x, poi.Key.transform.position.y + 0.03f, poi.Key.transform.position.z);
        }
    }

    public void DeleteMapPOI(string poiId)
    {
        GameObject poiToDelete = null;
        foreach (var poi in spawnedPOIs.Keys)
        {
            POIInteraction poiInteraction = poi.GetComponentInChildren<POIInteraction>();
            if (poiInteraction != null && poiInteraction.poiId == poiId)
            {
                poiToDelete = poi;
                break;
            }
        }

        if (poiToDelete != null)
        {
            spawnedPOIs.Remove(poiToDelete);
            Destroy(poiToDelete);
            if (mapInitialized)
            {
                UpdateSpawnedPOIs();
            }
        }
    }

    public void UpdateSpecificPOI(GameObject poi, Vector2d coordinates)
    {
        if (!mapInitialized || !MapGameobject.activeSelf) return;

        if (spawnedPOIs.ContainsKey(poi))
        {
            spawnedPOIs[poi] = coordinates;
            poi.transform.position = map.GeoToWorldPosition(coordinates, true);
            //poi.transform.localPosition = new Vector3(poi.transform.position.x, poi.transform.position.y + 0.03f, poi.transform.position.z);
        }
    }

    public void SetCoordinates(Vector2d coordinates)
    {
        this.coordinates = coordinates;
    }
    public void UpdateMapAndPlayer()
    {
        //this.coordinates = coordinates;
        UpdatePlayerLocation(coordinates);
    }
    public void ToggleMapVisibility()
    {
        MapGameobject.SetActive(!MapGameobject.activeSelf);
        //_pinMapButton.gameObject.SetActive(MapGameobject.activeSelf);
        if (MapGameobject.activeSelf)
        {
            if (!mapInitialized)
            {
                InitializeMapAtLocation();
                poisParent.parent = map.transform;

            }
            UpdateMapAndPlayer();//Manual update
            UpdateSpawnedPOIs();
        }
    }
    private void SetUpPinButton()
    {
        _pinMapButton.IsToggled.OnEntered.AddListener((args) =>
        {
            _mapState = MapInteractionState.Pinned;
            MapGameobject.GetComponent<RadialView>().enabled = false;

            //_pinMapButton.GetComponentInChildren<FontIconSelector>().CurrentIconName = "Icon 121";
            //_pinIcon.faceColor = new Color(191f / 255f, 44f / 255f, 0f, 1f);
        });
        _pinMapButton.IsToggled.OnExited.AddListener((args) =>
        {
            _mapState = MapInteractionState.Free;
            MapGameobject.GetComponent<RadialView>().enabled = true;

            //_pinMapButton.GetComponentInChildren<FontIconSelector>().CurrentIconName = "Icon 120";
            //_pinIcon.faceColor = Color.white;
        });
    }
    public void AdjustRadialViewBasedOnInteraction()
    {
        if (_mapState == MapInteractionState.Free || _mapState == MapInteractionState.Pinned)
        {
            // Map is in Free or Pinned mode so that means it is being grabbed now
            MapGameobject.GetComponent<RadialView>().enabled = false;
            _mapState = MapInteractionState.Grabbed;
        }
        else if (_mapState == MapInteractionState.Grabbed)
        {
            _mapState = MapInteractionState.Releasing;

            // Determine what to do based on pin state and previous state
            bool isPinned = _pinMapButton.IsToggled;

            if (isPinned)
            {
                StartCoroutine(WaitForMapToReposition());
            }
            else //Map not pinned
            {
                MapGameobject.GetComponent<RadialView>().enabled = true;
                _mapState = MapInteractionState.Free;
            }
        }
        else if (_mapState == MapInteractionState.Releasing)
        {
            StopAllCoroutines();
            _mapState = MapInteractionState.Grabbed;
            MapGameobject.GetComponent<RadialView>().enabled = false;
            return;
        }
    }

    IEnumerator WaitForMapToReposition()
    {
        MapGameobject.GetComponent<RadialView>().enabled = true;
        yield return new WaitForSeconds(1f); // Wait for 0.5 seconds to allow the map to reposition
        MapGameobject.GetComponent<RadialView>().enabled = false;
        _mapState = MapInteractionState.Pinned;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}