using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Nav;
using NaughtyAttributes;
using System.Collections;  // For OdometryMsg

public class OdomSubscriber : MonoBehaviour
{
    [Header("ROS Topic Settings")]
    [SerializeField] private string odomTopic = "/odom";
    [Header("GameObject to Update")]
    public GameObject odomObject;

    // Timeout settings
    public float dataTimeout = 10f;
    private float lastDataTime = 0f;
    private bool _imInTouch = false;

    #region Initial Values
    private bool gotInitialPose = false;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    #endregion

    void Start()
    {
        // Subscribe to the /odom topic once the scene starts
        ROSConnection.GetOrCreateInstance().Subscribe<OdometryMsg>(odomTopic, OdomCallback);

        lastDataTime = Time.time;
    }

    void Update()
    {
        // Check if the connection is up and no new data has been received for more than dataTimeout seconds.
        if (_imInTouch && (Time.time - lastDataTime) > dataTimeout)
        {
            ResetOdomObject();
            // Optionally, update lastDataTime here to prevent repeated resets.
            _imInTouch = false;
        }
    }

    void OdomCallback(OdometryMsg msg)
    {
        if (!_imInTouch)
            _imInTouch = true;

        lastDataTime = Time.time;
        //Extract position
        float px = (float)msg.pose.pose.position.x;
        float py = (float)msg.pose.pose.position.y;
        float pz = (float)msg.pose.pose.position.z;

        //Extract orientation (quaternion)
        float ox = (float)msg.pose.pose.orientation.x;
        float oy = (float)msg.pose.pose.orientation.y;
        float oz = (float)msg.pose.pose.orientation.z;
        float ow = (float)msg.pose.pose.orientation.w;

        //Extract velocity/ang. velocity
        float vx = (float)msg.twist.twist.linear.x;
        float vy = (float)msg.twist.twist.linear.y;
        float vz = (float)msg.twist.twist.linear.z;
        float wx = (float)msg.twist.twist.angular.x;
        float wy = (float)msg.twist.twist.angular.y;
        float wz = (float)msg.twist.twist.angular.z;

        // If we haven't stored the initial pose yet, do so now.
        if (!gotInitialPose)
        {
            gotInitialPose = true;
            initialPosition = new Vector3(px, py, pz);
            initialRotation = new Quaternion(ox, oy, oz, ow);
            Debug.Log("Initial Odom Pose: Position = " + initialPosition
                      + ", Rotation = " + initialRotation);
        }

        // Subtract the initial odometry position from the current position
        // so that the object *starts* at (0,0,0) in Unity

        //SOS: (ROS: X-forward, Z-up; Unity: Z-forward, Y-up)
        Vector3 offsetPosition = new Vector3(
            py - initialPosition.y,
            pz - initialPosition.z,
            px - initialPosition.x
        );

        // For rotation offset, you can either:
        // 1) Directly apply the orientation ignoring the initial, OR
        // 2) 'undo' the initial rotation from the new orientation 
        //    so the object "starts" at zero rotation in Unity.
        //
        // Option A (simpler): Just subtract the initial rotation's 'eulerAngles'
        // But full quaternion math is more precise. For example:
        Quaternion currentRotation = new Quaternion(ox, oy, oz, ow);
        // We want: offset = current * inverse(initial)
        Quaternion offsetRotation = currentRotation * Quaternion.Inverse(initialRotation);

        // Now apply these offsets to your object
        if (odomObject != null)
        {
            odomObject.transform.localPosition = offsetPosition;
            odomObject.transform.localRotation = offsetRotation;
        }
    }

#if UNITY_EDITOR
    [Button("Reset Odom Object")]
#endif
    public void ResetOdomObject()
    {
        gotInitialPose = false;
        odomObject.transform.localPosition = Vector3.zero;
        odomObject.transform.localRotation = Quaternion.identity;
        print("Odom object reset to (0,0,0) position and rotation.");
    }
}
