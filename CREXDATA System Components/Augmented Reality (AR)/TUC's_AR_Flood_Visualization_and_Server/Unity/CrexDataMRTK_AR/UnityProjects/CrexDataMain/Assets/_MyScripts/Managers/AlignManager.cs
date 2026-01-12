using Mapbox.Map;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlignManager : MonoBehaviour
{

    [SerializeField] private GameObject _objectForAlignment; //The object that will be aligned (The whole scene is MYORIGIN object)
    [SerializeField] private Camera _mainCamera;
    private int _deegrees;
    public MapAndPlayerManager _mapAndPlayerManager;

    public void Align()
    {

        //Calculate the angle between the given deegrees and the main camera
        float angle = _objectForAlignment.transform.rotation.eulerAngles.y - _mainCamera.transform.rotation.eulerAngles.y;
        //Debug.Log("Angle: " + angle);
        //Change the align objects Y rotation to the calculated angle
        _objectForAlignment.transform.rotation = Quaternion.Euler(0, angle, 0);
        _mapAndPlayerManager.UpdateNorthOffsetAngle();
        this.gameObject.SetActive(false);
    }
}
