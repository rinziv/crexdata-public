using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;

public class TFDebugLogger : MonoBehaviour
{
    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>("/tf", OnTFReceived);
    }

    void OnTFReceived(TFMessageMsg msg)
    {
        foreach (var transform in msg.transforms)
        {
            Debug.Log($"TF from {transform.child_frame_id} relative to {transform.header.frame_id}");
        }
    }
}
