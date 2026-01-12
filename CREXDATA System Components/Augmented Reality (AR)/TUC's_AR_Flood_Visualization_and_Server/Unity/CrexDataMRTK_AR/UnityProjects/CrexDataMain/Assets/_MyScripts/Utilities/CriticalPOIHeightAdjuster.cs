using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script dynamically adjusts the height of a GameObject to match the camera's height
/// when the camera is within a specified distance. The height adjustment is applied smoothly.
/// </summary>
public class DetectedPersonPOI : MonoBehaviour
{
    [Header("Camera Reference")]
    [Tooltip("The camera used to calculate the distance and height. If not set, it defaults to the main camera.")]
    private Camera _viewCamera;

    [Header("Height Adjustment Settings")]
    [Tooltip("The height adjustment will only activate if the camera is within this distance.")]
    [SerializeField] private float activationDistance = 300f;

    [Header("Smoothing and Performance")]
    [Tooltip("The speed at which the height changes are smoothed. Higher values result in faster transitions.")]
    [SerializeField] private float smoothing = 5f;
    [Tooltip("The interval in seconds at which the height check is performed.")]
    [SerializeField] private float refreshInterval = 3f;

    private float _initialY;
    private float _targetY;
    private Coroutine _adjustHeightCoroutine;

    private void Awake()
    {
        if (_viewCamera == null)
        {
            _viewCamera = Camera.main;
            if (_viewCamera == null)
            {
                Debug.LogError("DetectedPersonPOI: No camera found. Please assign a camera.");
                enabled = false;
                return;
            }
        }

        _initialY = transform.position.y;
        _targetY = _initialY;
    }

    void OnEnable()
    {
        _adjustHeightCoroutine = StartCoroutine(AdjustHeightCoroutine());
    }

    void OnDisable()
    {
        if (_adjustHeightCoroutine != null)
        {
            StopCoroutine(_adjustHeightCoroutine);
            _adjustHeightCoroutine = null;
        }
    }

    private void Update()
    {
        float newY = Mathf.Lerp(transform.position.y, _targetY, Time.deltaTime * smoothing);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    IEnumerator AdjustHeightCoroutine()
    {
        while (true)
        {
            if (_viewCamera != null)
            {
                float distance = Vector3.Distance(_viewCamera.transform.position, transform.position);

                if (distance <= activationDistance)
                {

                    _targetY = _viewCamera.transform.position.y;
                }
                else
                {
                    // If the object is too far, revert to its original height.
                    _targetY = _initialY;
                }
            }

            yield return new WaitForSeconds(refreshInterval);
        }
    }
}
