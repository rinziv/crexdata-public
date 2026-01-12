using UnityEngine;

public class FollowParentXZ : MonoBehaviour
{
    public Transform parentTransform; // Reference to the parent (camera) transform

    void Update()
    {
        // Get the new position from the parent, keeping the y position of the feet
        Vector3 newFeetPosition = new(parentTransform.position.x, parentTransform.position.y, parentTransform.position.z);

        // Update the feet GameObject's position
        transform.position = newFeetPosition;
    }
}
