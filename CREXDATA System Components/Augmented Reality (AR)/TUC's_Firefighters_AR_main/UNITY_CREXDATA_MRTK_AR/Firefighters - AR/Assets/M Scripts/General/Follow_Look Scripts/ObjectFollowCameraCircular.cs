using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectFollowCameraCircular : MonoBehaviour
{
    // Reference to the camera's transform (assign in inspector or defaults to main camera)
    private Transform cameraTransform;

    // Stored local position and relative rotation
    private Vector3 localPos;
    private Quaternion relativeRotation;

    // Flag to indicate if the object is locked
    public bool isOpen = true;
    public bool isLocked = true;

    // Lerp speed for smooth movement and rotation
    public float lerpSpeed = 5f;

    public Vector3 initialOffset;

    // Initialize camera transform if not set
    void Awake()
    {
        if(cameraTransform == null)
            cameraTransform = Camera.main.transform;
            
        if (isOpen)
        {
            // Calculate local position relative to camera
            localPos = cameraTransform.InverseTransformPoint(initialOffset);

            // Calculate relative rotation
            relativeRotation = transform.rotation * Quaternion.Inverse(cameraTransform.rotation);
        }
    }

    // Public method to lock the object to the camera
    public void SpawnOject()
    {
        // Calculate the offset in world space using the camera's full orientation
        Vector3 offsetInWorld = cameraTransform.TransformVector(initialOffset);

        // Calculate the new position in world space
        Vector3 newPosition = cameraTransform.position + offsetInWorld;

        // Set the object's world position
        transform.position = newPosition;

        // Calculate local position relative to the camera
        localPos = cameraTransform.InverseTransformPoint(newPosition);

        // Mark as locked
        isOpen = true;
    }

    public void HideObject()
    {
        isOpen = false;
        transform.localPosition = transform.position - new Vector3(0, -500, 0);
    }

    public void ToggleHide(){
        if(isOpen){
            HideObject();
        }else{
            SpawnOject();
        }
    }

    // Update position and rotation after camera movement using Lerp
    void LateUpdate()
    {
        if (isOpen && cameraTransform != null && isLocked)
        {
            // Calculate target position and rotation
            Vector3 targetPosition = cameraTransform.TransformPoint(localPos);

            // Smoothly interpolate position and rotation
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
        }
    }

    public void ToggleLock()
    {
        isLocked = !isLocked;

        // If loced, recalculate the offset
        if (isLocked)
        {
            LockObject();
        }
    }

    void LockObject(){
        
        // Calculate local position relative to camera
        localPos = cameraTransform.InverseTransformPoint(transform.position);
    }

    
    float CorrectAngle(float angle){

        float correctAngle = angle;
        if(angle < 0)
            correctAngle = 360 - angle;

        return correctAngle;
    }

}
