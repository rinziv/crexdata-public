using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManholeManager : MonoBehaviour
{
    public static ManholeManager Instance { get; private set; }

    public float disableDistance = 30f;
    public float updateInterval = 1f; // How often to check distances, in seconds.

    private readonly List<Manhole> manholes = new List<Manhole>();
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
    }

    private void Start()
    {
        mainCamera = Camera.main;
        StartCoroutine(UpdateManholesVisibility());
    }

    public void RegisterManhole(Manhole manhole)
    {
        if (!manholes.Contains(manhole))
        {
            manholes.Add(manhole);
        }
    }

    public void UnregisterManhole(Manhole manhole)
    {
        manholes.Remove(manhole);
    }

    private IEnumerator UpdateManholesVisibility()
    {
        WaitForSeconds wait = new WaitForSeconds(updateInterval);

        while (true)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogWarning("ManholeManager: Main Camera not found. Will try again.");
                    yield return wait;
                    continue;
                }
            }

            Vector3 cameraPosition = mainCamera.transform.position;
            float disableDistanceSqr = disableDistance * disableDistance;

            for (int i = manholes.Count - 1; i >= 0; i--)
            {
                Manhole manhole = manholes[i];
                if (manhole == null)
                {
                    manholes.RemoveAt(i);
                    continue;
                }

                float distanceSqr = (manhole.transform.position - cameraPosition).sqrMagnitude;
                bool shouldBeActive = distanceSqr <= disableDistanceSqr;

                if (manhole.gameObject.activeSelf != shouldBeActive)
                {
                    manhole.gameObject.SetActive(shouldBeActive);
                }
            }

            yield return wait;
        }
    }
}
