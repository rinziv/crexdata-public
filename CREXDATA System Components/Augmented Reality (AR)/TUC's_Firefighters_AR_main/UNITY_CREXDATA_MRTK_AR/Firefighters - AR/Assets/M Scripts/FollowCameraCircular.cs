using UnityEngine;

public class FollowCameraCircular : MonoBehaviour
{
    // Reference to the camera's transform (assign in inspector or defaults to main camera)
    private Transform cameraTransform;

    // Stored local position and relative rotation
    private Vector3 localPos;
    private Quaternion relativeRotation;

    // Flag to indicate if the object is locked
    public bool isLocked = true;

    // Hide Map
    public bool hidden = true;

    // Store the initial offset from the camera
    private Vector3 initialOffset;
    public Vector3 lastPos;

    void Start()
    {
        // Get the main camera at the start
        cameraTransform = Camera.main.transform;
        initialOffset = lastPos;
    }

    void Update()
    {   
        if (isLocked && !hidden)
        {
            // Set world position based on camera's transform and stored local position
            transform.position = cameraTransform.TransformPoint(localPos);
        }
    }

    private void hiddeMap(){

        hidden = true;
        lastPos = transform.localPosition - cameraTransform.position;
        initialOffset = lastPos;
        transform.position = new Vector3(0, -2000, 0);
    }

    private void openMap()
    {
        // Keep the same distance and Y position relative to the camera
        float distance = initialOffset.z;

        float cameraYaw = CorrectAngle(-cameraTransform.eulerAngles.y);

        // Convert to radians
        float cameraYawRad = cameraYaw * Mathf.Deg2Rad;

        float distanceX = distance * Mathf.Sin(cameraYawRad);
        float distanceZ = distance * Mathf.Cos(cameraYawRad);

        Vector3 newPosition = new Vector3(cameraTransform.position.x + distanceX, 1.7f, cameraTransform.position.z + distanceZ);

        // Update position
        initialOffset = newPosition - cameraTransform.position;
        localPos = cameraTransform.InverseTransformPoint(newPosition);

        transform.localPosition = newPosition;

        hidden = false;
    }

    // Public method to lock the object to the camera
    public void LockToCamera()
    {
        // Calculate local position relative to camera
        localPos = cameraTransform.InverseTransformPoint(transform.position);

        // Calculate relative rotation
        relativeRotation = transform.rotation * Quaternion.Inverse(cameraTransform.rotation);

        // Mark as locked
        isLocked = true;
    }

    // Public method to unlock the object from the camera
    public void UnlockFromCamera()
    {
        isLocked = false;
    }

    public void ToggleMapHide(){

        if(!hidden)
            hiddeMap();
        else
            openMap();
    }

    // Method to toggle the lock state and recalculate the offset if needed
    public void ToggleLock(bool lockState)
    {
        // If locked, recalculate the offset
        if (lockState)
            LockToCamera();
        else
            UnlockFromCamera();
    }

    public float CorrectAngle(float angle){

        float correctAngle = angle;
        if(angle < 0)
            correctAngle = 360 - angle;

        return correctAngle;
    }
    
}
