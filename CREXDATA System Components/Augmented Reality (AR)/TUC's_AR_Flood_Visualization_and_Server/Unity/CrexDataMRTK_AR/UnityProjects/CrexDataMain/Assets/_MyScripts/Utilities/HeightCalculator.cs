using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class HeightCalculator : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("Camera from which the raycast is fired.")]
    public Camera mainCamera;
    [Tooltip("Layer mask to use for detecting the ground.")]
    public LayerMask groundLayer;
    [Tooltip("Maximum distance for the downward raycast.")]
    public float rayDistance = 10f;
    private float _groundHeight;

    [Header("AR Mesh Manager Settings")]
    [Tooltip("ARMeshManager for generating and managing AR meshes.")]
    public ARMeshManager arMeshManager;
    [Tooltip("Time in seconds for scanning the environment to create the ground mesh.")]
    public float scanningDuration = 5f;
    [Tooltip("Name of the layer to assign to AR meshes.")]
    public string groundLayerName = "Ground";

#if UNITY_EDITOR
    [Header("Measurement Settings (Editor Only)")]
    [Tooltip("Toggle on to run the height measurement routine (Editor Only).")]
    public bool enableHeightMeasurement = false;
    [Tooltip("Time interval (in seconds) between height measurements.")]
    public float measurementInterval = 0.5f;

    // Reference to the running coroutine.
    private Coroutine measurementCoroutine;
#endif

    #region Unity Lifecycle

    void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Try to find the ARMeshManager if not assigned.
        if (arMeshManager == null)
        {
            arMeshManager = FindObjectOfType<ARMeshManager>();
            if (arMeshManager == null)
            {
                Debug.LogWarning("ARMeshManager not found in the scene.");
            }
        }
    }

    void OnEnable()
    {
        if (arMeshManager != null)
        {
            arMeshManager.meshesChanged += OnMeshesChanged;
        }
    }

    void OnDisable()
    {
        if (arMeshManager != null)
        {
            arMeshManager.meshesChanged -= OnMeshesChanged;
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        if (enableHeightMeasurement && measurementCoroutine == null)
        {
            measurementCoroutine = StartCoroutine(HeightMeasurementRoutine());
        }
        else if (!enableHeightMeasurement && measurementCoroutine != null)
        {
            StopCoroutine(measurementCoroutine);
            measurementCoroutine = null;
            Debug.Log("Height measurement feature stopped.");
        }
    }
#endif

    #endregion

    #region AR Mesh Layer Assignment

    /// <summary>
    /// Callback invoked when AR meshes are added or updated.
    /// This assigns the meshes to the specified ground layer.
    /// </summary>
    /// <param name="args">The event arguments containing the meshes.</param>
    void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        // Convert the layer name to a layer index
        int groundLayerInt = LayerMask.NameToLayer(groundLayerName);

        foreach (var mesh in args.added)
        {
            mesh.gameObject.layer = groundLayerInt;
        }

        // Also update the layer for every updated mesh
        foreach (var mesh in args.updated)
        {
            mesh.gameObject.layer = groundLayerInt;
        }
    }

    #endregion

    #region Height Measurement (Editor Only)

#if UNITY_EDITOR
    /// <summary>
    /// Coroutine that runs the height measurement routine.
    /// It enables AR mesh scanning for a set duration, clears the meshes,
    /// then raycasts to compute and log the grounds position in the AR environment.
    /// </summary>
    IEnumerator HeightMeasurementRoutine()
    {
        // 1. Enable AR mesh scanning.
        if (arMeshManager != null)
        {
            arMeshManager.gameObject.SetActive(true);
            Debug.Log("AR Mesh Manager scanning enabled.");
        }
        else
        {
            Debug.LogWarning("ARMeshManager is not assigned. Skipping mesh scanning.");
        }

        // Wait for the scanning duration so that meshes are generated.
        yield return new WaitForSeconds(scanningDuration);

        // 2. Clear the meshes and disable further scanning.
        if (arMeshManager != null)
        {
            arMeshManager.DestroyAllMeshes();
            arMeshManager.gameObject.SetActive(false);
            Debug.Log("AR Mesh Manager scanning disabled and meshes cleared.");
        }

        // 3. Calculate an initial ground height.
        _groundHeight = CalculateGroundHeight();
        Debug.Log("Ground height determined: " + _groundHeight);

        while (enableHeightMeasurement)
        {
            float userHeight = Mathf.Abs(mainCamera.transform.position.y - _groundHeight);
            Debug.Log("User Height (distance from camera to ground): " + userHeight);
            yield return new WaitForSeconds(measurementInterval);
        }

        measurementCoroutine = null;
    }
#endif

    #endregion

    #region Ground Height Calculation

    /// <summary>
    /// Performs a downward raycast from the camera to detect the ground.
    /// Returns the Y position of the collision point, or -1112 if no ground is detected.
    /// </summary>
    public float CalculateGroundHeight()
    {
        Ray ray = new Ray(mainCamera.transform.position, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, groundLayer))
        {
            _groundHeight = hit.point.y;
            Debug.Log("Collision Point: " + hit.point);
            return _groundHeight;
        }
        else
        {
            Debug.Log("No ground detected within the ray distance.");
            return -1112f;
        }
    }

    /// <summary>
    /// Returns the last calculated ground height.
    /// </summary>
    public float GetGroundHeight()
    {
        return _groundHeight;
    }

    #endregion
}
