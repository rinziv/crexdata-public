using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FollowUser : MonoBehaviour
{
    // The main camera reference
    private Transform mainCamera;

    // Boolean to lock/unlock the following behavior
    public bool locked = true;

    // Store the initial offset from the camera
    private Vector3 initialOffset;

    // Smoothing speed
    public float smoothSpeed = 0.125f;

    void Start()
    {
        // Get the main camera at the start
        mainCamera = Camera.main.transform;
    }

    void Update()
    {   
        if (locked)
        {
            transform.position = Vector3.Lerp(transform.position, mainCamera.position + initialOffset, smoothSpeed * Time.deltaTime);
        }
    }

    // Method to toggle the lock state and recalculate the offset if needed
    public void ToggleLock(bool lockState)
    {
        locked = lockState;

        // If locked, recalculate the offset
        if (locked)
        {
            initialOffset = transform.position - mainCamera.position;
        }
    }
}
