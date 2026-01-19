using UnityEngine;

public class LookAtUser : MonoBehaviour
{
    private Camera mainCamera;
    private Vector3 velocity = Vector3.zero;
    public bool locked = false;
    public float smoothTime = 0.3f;

    [Header("Spherical Settings")]
    private Vector3 sphericalOffset; // Stores direction and distance
    private Quaternion initialCameraRotation;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        FaceCamera();
    }

    void FaceCamera()
    {
        // Make the object face the camera
        Vector3 lookDirection = mainCamera.transform.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-lookDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTime * Time.deltaTime * 10);
        }
    }
}
