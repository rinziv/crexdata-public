using UnityEngine;
using System;
using System.Threading.Tasks;

public class SceneCalibrationManager : MonoBehaviour
{
    [SerializeField]
    private Transform cameraRoot; // The root of the camera hierarchy


    public void CalibrateToNorth()
    {
        // Get the main camera’s current forward vector
        Vector3 cameraForward = Camera.main.transform.forward;

        // Ignore any vertical tilt (flatten forward to horizontal plane)
        cameraForward.y = 0.0f;

        // If the vector is not zero, apply that as the new forward
        if (cameraForward.sqrMagnitude > 0.0001f)
        {
            cameraForward.Normalize();

            // Create a rotation that looks in the forward direction we just computed
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward, Vector3.up);

            // Apply this rotation to the root of the scene
            cameraRoot.rotation = targetRotation;

            Debug.Log($"Camera north rotated to match the Real-World north");
        }
        else
        {
            Debug.LogWarning("Does not need to calibrate north because the difference is too small.");
        }
    }
}
