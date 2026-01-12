using UnityEngine;

/// <summary>
/// This script dynamically scales a GameObject to ensure it remains visible when it is far from the camera.
/// It maintains a minimum apparent size for the object, preventing it from becoming too small at a distance.
/// The scaling is applied smoothly to preserve realism and depth perception.
/// </summary>
public class PerspectiveScaler : MonoBehaviour
{
    [Header("Camera Reference")]
    [Tooltip("The camera used to calculate the distance. If not set, it defaults to the main camera.")]
    public Camera viewCamera;

    [Header("Scaling Settings")]
    [Tooltip("The distance from the camera at which the object will have its original scale.")]
    [SerializeField] private float distanceForBaseScale = 5f;

    [Tooltip("The minimum apparent size of the object, defined as the size it would have at the 'distanceForBaseScale'.")]
    [SerializeField] private float minPerceivedSize = 1.0f;

    [Tooltip("The distance at which the minimum scaling effect begins. Before this distance, the object scales normally.")]
    [SerializeField] private float startScalingDistance = 10f;

    [Header("Smoothing")]
    [Tooltip("The speed at which the scale changes are smoothed. Higher values result in faster transitions.")]
    [SerializeField] private float smoothing = 5f;
    [SerializeField] private float refreshInterval = 5f;
    private Vector3 _initialScale;
    private Vector3 _targetScale;
    private Coroutine _scaleCoroutine;

    private void Awake()
    {
        // If no camera is assigned, find the main camera in the scene.
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                Debug.LogError("PerspectiveScaler: No camera found. Please assign a camera.");
                enabled = false;
                return;
            }
        }

        // Store the original scale of the GameObject.
        _initialScale = transform.localScale;
        _targetScale = _initialScale;
    }

    private void OnEnable()
    {
        // Start the coroutine to periodically update the scale when the object is enabled.
        _scaleCoroutine = StartCoroutine(UpdateScaleCoroutine());
    }

    private void OnDisable()
    {
        // Stop the coroutine when the object is disabled.
        if (_scaleCoroutine != null)
        {
            StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = null;
        }
    }

    private void Update()
    {
        // Smoothly interpolate from the current scale to the target scale for a natural effect.
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * smoothing);
    }

    private System.Collections.IEnumerator UpdateScaleCoroutine()
    {
        while (true)
        {
            if (viewCamera != null)
            {
                // Calculate the distance from the camera to this GameObject.
                float distance = Vector3.Distance(viewCamera.transform.position, transform.position);

                // Determine the target scale based on the distance.
                if (distance <= startScalingDistance)
                {
                    // If the object is closer than the start distance, use its original scale.
                    _targetScale = _initialScale;
                }
                else
                {
                    // Calculate the scale factor needed to maintain the minimum perceived size.
                    // This formula ensures that the object's apparent size does not fall below the desired minimum.
                    float scaleFactor = distance / distanceForBaseScale * minPerceivedSize;
                    _targetScale = _initialScale * scaleFactor;
                }
            }

            // Wait for 5 seconds before the next update.
            yield return new WaitForSeconds(refreshInterval);
        }
    }
}
