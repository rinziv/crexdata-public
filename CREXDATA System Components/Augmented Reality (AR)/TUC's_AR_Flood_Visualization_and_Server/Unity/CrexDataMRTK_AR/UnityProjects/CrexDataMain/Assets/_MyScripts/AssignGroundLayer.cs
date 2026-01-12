using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class AssignGroundLayer : MonoBehaviour
{
    public ARMeshManager arMeshManager;
    public string groundLayerName = "Ground";

    void OnEnable()
    {
        arMeshManager.meshesChanged += OnMeshesChanged;
    }

    void OnDisable()
    {
        arMeshManager.meshesChanged -= OnMeshesChanged;
    }

    void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        foreach (var mesh in args.added)
        {
            mesh.gameObject.layer = LayerMask.NameToLayer(groundLayerName);
        }

        foreach (var mesh in args.updated)
        {
            mesh.gameObject.layer = LayerMask.NameToLayer(groundLayerName);
        }
    }
}
