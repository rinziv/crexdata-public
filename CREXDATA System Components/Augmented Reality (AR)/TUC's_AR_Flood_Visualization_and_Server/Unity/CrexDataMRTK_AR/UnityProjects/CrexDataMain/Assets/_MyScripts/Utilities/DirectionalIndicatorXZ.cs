using Mapbox.Unity.MeshGeneration.Factories;
using UnityEngine;

namespace Custom.Solvers
{
    /// <summary>
    /// Rotates the arrow to point toward a target object, restricting rotation to the XZ plane.
    /// Adds a fixed 90-degree rotation on the X-axis to correct arrow orientation.
    /// </summary>
    [AddComponentMenu("Custom/Solvers/Arrow Rotator XZ")]
    public class ArrowRotatorXZ : MonoBehaviour
    {
        [Tooltip("The target Transform to point the arrow toward.")]
        public Transform Target;
        private Quaternion _xRotationOffset = Quaternion.Euler(90, 0, 0);// Add a 90-degree rotation on the X-axis to fix the Arrow prefab orientation
        private void Update()
        {
            /*             if (Target == null)
                        {
                            try
                            {
                                Target = RoutingManager.Instance.RoutePoints[0].transform;
                            }
                            catch
                            {
                                Debug.LogWarning("Target not assigned. Please assign a target Transform.");
                            }
                            return;
                        } */
            if (Target != null)
            {
                // --- Calculate Direction to Target on XZ Plane ---
                Vector3 directionToTarget = Target.position - transform.position; // Direction vector
                directionToTarget.y = 0; // Ignore vertical component for XZ rotation
                directionToTarget.Normalize();

                // --- Set Rotation with X-Axis Offset ---
                // Rotate the arrow to face the target direction
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
                transform.rotation = targetRotation * _xRotationOffset;
            }
        }
    }
}
