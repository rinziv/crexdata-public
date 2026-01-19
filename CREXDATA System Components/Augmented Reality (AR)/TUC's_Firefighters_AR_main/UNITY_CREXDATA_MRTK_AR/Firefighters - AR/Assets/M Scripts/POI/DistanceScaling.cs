using UnityEngine;
using TMPro;

public class DistanceScaling : MonoBehaviour
{
    [Header("Scaling Settings")]
    [Tooltip("The size of the object when the user is 1 meter away.")]
    public float sizeAtOneMeter = 0.5f;

    [Tooltip("Minimum distance to scale at (prevents zero-scale when user is too close).")]
    public float minScaleDistance = 2.0f;

    [Tooltip("Maximum distance beyond which the object stops scaling. (Optional)")]
    public float maxScaleDistance = 999f;

    private Renderer _renderer;

    // Time between scale updates
    private float sizeUpdateRate = 1/30;
    private float textUpdateRate = 3;
    private float scaleTimer = 1/30;
    private float distanceTimer = 3;

    // Distance Text
    public TextMeshPro distanceText;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    void Update()
    {
        // Calculate distance from the user to this object
        float distance = Vector3.Distance(Camera.main.transform.position, transform.position);
        
        distanceTimer += Time.deltaTime;
        if(distanceTimer > textUpdateRate){

            updateText(distance);
            distanceTimer = 0;
        }
        
        scaleTimer += Time.deltaTime;
        if(scaleTimer > sizeUpdateRate){

            // Clamp the distance to avoid zero or extremely large scale
            distance = Mathf.Clamp(distance, minScaleDistance, maxScaleDistance);

            float scale = sizeAtOneMeter * distance/2;
            float distanceSizeReducer = distance/7;

            // Apply the scale to the transform uniformly
            transform.localScale = (Vector3.one * (scale - (scale/100)*distanceSizeReducer));
            transform.localPosition = new Vector3(transform.localPosition.x, (distance/8.5f), transform.localPosition.z);

            scaleTimer = 0;
        }
    }

    private void updateText(float distance){

        distanceText.text = distance.ToString("#.00") + "m";
    }

}
