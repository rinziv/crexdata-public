using MixedReality.Toolkit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class CameraController : MonoBehaviour
{
    private GameObject _camera;
    public float speed = 5f;

    void Start()
    {
        try
        {
            _camera = Camera.main.gameObject;
        }
        catch (System.Exception e)
        {
            Debug.LogError("CameraController: No main camera found. Please ensure a camera with the 'MainCamera' tag exists in the scene.");
            Debug.LogException(e);
        }
    }
    // Update is called once per frame
    void Update()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = new Vector3(inputX, 0, inputY).normalized;

        _camera.transform.position += moveDirection * speed * Time.deltaTime;
    }
}
