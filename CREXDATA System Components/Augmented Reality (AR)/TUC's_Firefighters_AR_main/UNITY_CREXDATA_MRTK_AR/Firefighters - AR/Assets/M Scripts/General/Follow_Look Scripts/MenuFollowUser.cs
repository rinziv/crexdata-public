using UnityEngine;

public class MenuFollowUser : MonoBehaviour
{
    // The main camera reference
    private Transform mainCamera;

    // Store the initial offset from the camera
    private Vector3 initialOffset;

    // Smoothing speed
    public float smoothSpeed = 0.125f;

    // Hide Map
    public bool hidden = true;
    public Vector3 lastPos;
        
    void Start()
    {
        // Get the main camera at the start
        mainCamera = Camera.main.transform;
        initialOffset = lastPos;
    }

    void Update()
    {   
        if (!hidden)
        {
            transform.position = Vector3.Lerp(transform.position, mainCamera.position + initialOffset, smoothSpeed * Time.deltaTime);
        }
    }

    private void hiddeMenu(){

        hidden = true;
        lastPos = transform.localPosition - mainCamera.position;
        initialOffset = lastPos;
        transform.position = new Vector3(0, -2000, 0);
    }

    private void openMenu()
    {
        // Keep the same distance and Y position relative to the camera
        float distance = initialOffset.z;

        float cameraYaw = CorrectAngle(-mainCamera.eulerAngles.y);
        Debug.Log(cameraYaw);

        // Convert to radians
        float cameraYawRad = cameraYaw * Mathf.Deg2Rad;

        float distanceX = distance * Mathf.Sin(cameraYawRad);
        float distanceZ = distance * Mathf.Cos(cameraYawRad);

        Vector3 newPosition = new Vector3(mainCamera.position.x + distanceX + initialOffset.x, 1.7f, mainCamera.position.z + distanceZ);

        // Update position
        transform.localPosition = newPosition;

        initialOffset = newPosition - mainCamera.position;
        
        hidden = false;
    }

    public float CorrectAngle(float angle){

        float correctAngle = angle;
        if(angle < 0)
            correctAngle = 360 - angle;

        return correctAngle;
    }

    public void ToggleMenuHide(){

        if(!hidden)
            hiddeMenu();
        else
            openMenu();
    }

}
