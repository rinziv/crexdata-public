using TMPro;
using UnityEngine;
using System.Collections;
using Mapbox.Unity.MeshGeneration.Factories;
using MixedReality.Toolkit.UX;
using MixedReality.Toolkit;
using Newtonsoft.Json.Linq;

public class POIInteraction : MonoBehaviour
{
    public string poiName;
    public string poiDescription;
    public float riskLevel;
    public string poiId; // Added for tracking POI by ID
    private Transform _userTransform;
    private InfoPanelController _infoPanelController;
    public TextMeshProUGUI poiDistanceText;
    private readonly float refresh_rate = 1;
    [SerializeField] private bool _isForMapObject = false;

    [Header("Dialog Manager to display Info")]
    [Tooltip("The DialogPool to use for displaying information")]
    private DialogPool DialogPool;
    private StatefulInteractable _statefulInteractable;

    private Coroutine _updateDistanceCoroutine;


    void Awake()
    {
        if (DialogPool == null)
        {
            DialogPool = FindAnyObjectByType<DialogPool>();
        }
        _statefulInteractable = GetComponent<StatefulInteractable>();

        if (!_isForMapObject)
        {
            // Find the InfoPanelController once and cache it.
            _infoPanelController = FindObjectOfType<InfoPanelController>();
            if (_infoPanelController == null)
            {
                Debug.LogWarning("InfoPanelController not found in the scene.");
            }
        }
    }

    private void OnEnable()
    {
        if (!_isForMapObject)
        {
            // Start the coroutine only if it's not already running.
            if (_updateDistanceCoroutine == null)
            {
                _userTransform = Camera.main.transform; // Cache the camera transform
                _updateDistanceCoroutine = StartCoroutine(UpdateDistance());
            }
        }
        else
        {
            _statefulInteractable.IsRaySelected.OnExited.AddListener((args) => ShowPOIDialog());
            _statefulInteractable.IsPokeSelected.OnEntered.AddListener((args) => ShowPOIDialog());
        }
    }

    private void OnDisable()
    {
        // Stop the coroutine when the object is disabled to prevent it from running in the background.
        if (_updateDistanceCoroutine != null)
        {
            StopCoroutine(_updateDistanceCoroutine);
            _updateDistanceCoroutine = null;
        }

        if (_isForMapObject)
        {
            _statefulInteractable.IsRaySelected.OnExited.RemoveListener((args) => ShowPOIDialog());
            _statefulInteractable.IsPokeSelected.OnEntered.RemoveListener((args) => ShowPOIDialog());
        }
    }

    public void UpdateDataFromJson(JObject poiData)
    {
        poiName = poiData["properties"]?["name"]?.ToString()
            ?? poiData["properties"]?["title"]?.ToString()
            ?? "Unknown POI";

        poiId = poiData["poiId"]?.ToString() ?? "noID";

        poiDescription = poiData["properties"]?["description"]?.ToString() ?? "No description available";

        string severityStr = poiData["properties"]?["severity"]?.ToString()?.ToLower();
        switch (severityStr)
        {
            case "high":
                riskLevel = 100f;
                ChangeColor(Color.red);
                break;
            case "medium":
                riskLevel = 66f;
                ChangeColor(Color.yellow);
                break;
            case "low":
                riskLevel = 33f;
                ChangeColor(Color.white);
                break;
            default:
                // Try to parse numeric value if provided
                if (poiData["properties"]?["riskLevel"] != null &&
                    float.TryParse(poiData["properties"]["riskLevel"].ToString(), out float parsedRisk))
                {
                    riskLevel = parsedRisk;
                    // Set color based on parsed risk
                    if (riskLevel > 75) ChangeColor(Color.red);
                    else if (riskLevel > 50) ChangeColor(Color.yellow);
                    else if (riskLevel > 25) ChangeColor(Color.blue);
                    else ChangeColor(Color.green);
                }
                else
                {
                    riskLevel = 50f;
                    ChangeColor(Color.white);
                }
                break;
        }

        // Extract timestamp if available
        string timestamp = poiData["properties"]?["timestamp"]?.ToString();
        if (!string.IsNullOrEmpty(timestamp))
        {
            poiDescription += $"\nTime: {timestamp}";
        }

        // Handle any additional properties by appending to description
        JObject properties = poiData["properties"] as JObject;
        if (properties != null)
        {
            foreach (var property in properties)
            {
                string key = property.Key;
                // Skip properties we've already handled
                if (key == "name" || key == "title" || key == "description" ||
                    key == "severity" || key == "riskLevel" || key == "timestamp")
                    continue;

                // Add this property to the description
                poiDescription += $"\n{char.ToUpper(key[0])}{key.Substring(1)}: {property.Value}";
            }
        }

        /* // Notify any UI elements
        if (poiDistanceText != null && poiDistanceText.gameObject.activeInHierarchy)
        {
            StartCoroutine(UpdateDistance());
        } */

        Debug.Log($"Updated POI '{poiName}' with risk level {riskLevel} from JSON data");
    }

    IEnumerator UpdateDistance()
    {
        while (true)
        {
            Vector3 objectPositionXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 userPositionXZ = new Vector3(_userTransform.position.x, 0, _userTransform.position.z);
            float distance = Vector3.Distance(objectPositionXZ, userPositionXZ);
            if (poiDistanceText != null)
                poiDistanceText.text = $"{distance:F2} m";
            yield return new WaitForSeconds(refresh_rate); // Wait for 1 second
        }
    }

    public void ShowInfo()
    {
        Vector3 objectPositionXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 userPositionXZ = new Vector3(_userTransform.position.x, 0, _userTransform.position.z);
        float distance = Vector3.Distance(objectPositionXZ, userPositionXZ);
        string infoText = $"Name: {poiName}\nDescription: {poiDescription}\nDistance: {distance:F2} meters\nRisk Level: {riskLevel:F0}%";
        _infoPanelController.ShowInfoPanel(infoText);
    }

    public void ChangeColor(Color color)
    {
        Renderer renderer = GetComponent<Renderer>();
        Material material = renderer.material;

        // Change the base color
        material.color = color;

        // Change the emission color
        material.SetColor("_EmissionColor", color * Mathf.LinearToGammaSpace(1f));
    }

    public void CreateRoute()
    {
        RoutingManager.Instance.endPoint = transform;
        RoutingManager.Instance.GetRoute();
    }

    public void ShowPOIDialog()
    {
        RoutingManager.Instance.selectedPOI = transform;
        IDialog dialog;
        if (RoutingManager.Instance.endPoint == transform)
        {
            dialog = DialogPool.Get()
                .SetHeader("Routing Information")
                .SetBody("The selected POI is already your destination. Do you want to update the route?")
                .SetPositive("Yes", (args) => CreateRoute())
                .SetNegative("No (Return)", (args) => Debug.Log("Code-driven dialog says " + args.ButtonType))
                .SetNeutral("Clear Route", (args) => RoutingManager.Instance.ClearRoute(true));
        }
        else
        {
            dialog = DialogPool.Get()
               .SetHeader("Routing Confirmation")
               .SetBody("Do you want to select this POI as your destination?")
               .SetPositive("Yes", (args) => CreateRoute())
               .SetNegative("No (Return)", (args) => Debug.Log("Code-driven dialog says " + args.ButtonType));


        }
        // Get the GameObject of the dialog about to show
        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.UpdatePosition();

        dialog.Show();
    }
}
