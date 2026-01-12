using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayBounce : MonoBehaviour
{
    private Camera _camera;
    // Start is called before the first frame update
    void Start()
    {
        try
        {
            _camera = Camera.main;
        }
        catch (System.Exception e)
        {
            Debug.LogError("CameraController: No main camera found. Please ensure a camera with the 'MainCamera' tag exists in the scene.");
            Debug.LogException(e);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {

    }


    void BounceRay(Vector2 origin, short bounces)
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.green);
            Vector3 normal = hit.normal;
            Vector3 reflectDirection = Vector3.Reflect(ray.direction, normal);
            Debug.DrawRay(hit.point + normal * 0.001f, reflectDirection * 10f, Color.blue);
            Debug.DrawLine(hit.point, hit.point + normal * 2f, Color.yellow);

        }
        else
        {
            Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red);
        }
    }
}
