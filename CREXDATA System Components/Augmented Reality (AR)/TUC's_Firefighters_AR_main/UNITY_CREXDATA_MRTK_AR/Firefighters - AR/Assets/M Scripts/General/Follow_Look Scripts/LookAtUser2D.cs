using UnityEngine;

public class LookAtUser2D : MonoBehaviour
{
    private float time = 0.0f;

    void Update()
    {
        time += Time.deltaTime;

        if (time > 0.4f)
        {
            Vector3 targetPosition = Camera.main.transform.position;
            // Set the target position's y component to be the same as this object's y
            targetPosition.y = transform.position.y;

            // Make the object face the user on the xz plane
            transform.LookAt(targetPosition);
            transform.Rotate(new Vector3(0, 180, 0));

            time = 0;
        }
    }
}
