using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class POIAdjusterManager : MonoBehaviour
{
    public static POIAdjusterManager Instance { get; private set; }

    public float updateInterval = 0.5f; // How often to update the POIs, in seconds.

    private readonly List<POIAdjuster> poiAdjusters = new List<POIAdjuster>();
    private Camera mainCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        mainCamera = Camera.main;
    }

    private void Start()
    {
        StartCoroutine(UpdatePOIs());
    }

    public void RegisterPOI(POIAdjuster poi)
    {
        if (!poiAdjusters.Contains(poi))
        {
            poiAdjusters.Add(poi);
            poi.AdjustSize(mainCamera.transform.position);
        }
    }

    public void UnregisterPOI(POIAdjuster poi)
    {
        poiAdjusters.Remove(poi);
    }

    private IEnumerator UpdatePOIs()
    {
        while (true)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogWarning("POIAdjusterManager: Main Camera not found. Will try again.");
                    yield return new WaitForSeconds(updateInterval);
                    continue;
                }
            }

            Vector3 cameraPosition = mainCamera.transform.position;
            for (int i = 0; i < poiAdjusters.Count; i++)
            {
                poiAdjusters[i].AdjustSize(cameraPosition);
            }

            yield return new WaitForSeconds(updateInterval);
        }
    }
}
