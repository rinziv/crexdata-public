using UnityEngine;

public class ArrowMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;            // Speed at which the arrow moves forward (units per second)
    public float maxDistance = 3f;     // Maximum distance before resetting position
    
    private Vector3 startPosition;      // Starting position of the arrow

    void Start()
    {
        // Store the initial position when the game starts
        startPosition = transform.position;
    }

    void Update()
    {
        // Move the arrow forward in its local forward direction
        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.Self);

        // Calculate the distance traveled from the starting position
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);

        // Check if the arrow has traveled beyond the maximum distance
        if (distanceTraveled >= maxDistance)
        {
            // Reset the arrow's position to the starting point
            transform.position = startPosition;
        }
    }
}
