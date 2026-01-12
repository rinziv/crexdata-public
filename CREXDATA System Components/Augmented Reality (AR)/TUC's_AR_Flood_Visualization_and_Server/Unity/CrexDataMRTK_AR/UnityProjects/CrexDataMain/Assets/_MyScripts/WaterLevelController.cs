using UnityEngine;
using MixedReality.Toolkit.UX;
using System.Collections;
using UnityEngine.XR.ARFoundation;
using TMPro;
using MixedReality.Toolkit.SpatialManipulation;
using Unity.XR.CoreUtils;

public class WaterLevelController : MonoBehaviour
{
    public WaterPlane waterPlaneScript; // Reference to the WaterPlane script
    public TextMeshProUGUI waterInfoText; // Reference to the water info text
    public TextMeshProUGUI PredictedTime; // Reference to the water level text
    public TextMeshProUGUI velocityText, UncertaintyText, worstCaseHeightText; // Reference to the velocity, uncertainty and time text
    public Slider slider;
    public GameObject sliderGameObject; // Reference to the slider gameobject
    public PressableButton toggleButton; // Reference to the MRTK3 button
    public GameObject arMeshManager; // Reference to the ARMeshManager
    private ARMeshManager _arMeshManagerScript; // Reference to the ARMeshManager script
    public float scanDuration = 5f; // Duration to wait for scanning
    public GameObject instructionPannel;
    private bool _isWaterVisible = false; // Start with water hidden
    private Coroutine _scanningCoroutine; // Reference to the scanning coroutine
    public static float CurrentSliderValue { get; private set; } = 1; // Default to the first value

    [SerializeField] private PressableButton _pinSliderButton;

    [SerializeField] private RadialView _sliderRadialViewComponent;



    void Start()
    {
        //_pinSliderButton.gameObject.SetActive(false);
        SetUpPinButton();
        if (slider != null)
        {
            slider.OnValueUpdated.AddListener(OnSliderValueChanged);
            CurrentSliderValue = slider.Value;
        }

        if (toggleButton != null)
        {
            toggleButton.OnClicked.AddListener(ToggleWaterVisualization);
        }
        _arMeshManagerScript = arMeshManager.GetComponent<ARMeshManager>();
        // Set initial visibility
        if (waterPlaneScript != null && waterPlaneScript.IsWaterPlaneNull() == false)
        {
            waterPlaneScript.SetWaterVisibility(_isWaterVisible);
        }
    }

    private void OnSliderValueChanged(SliderEventData eventData)
    {
        CurrentSliderValue = eventData.NewValue;
        if (waterPlaneScript != null)
        {
            waterPlaneScript.UpdateWaterLevel(CurrentSliderValue);
            ChangePOIColorRandom();
            UpdateWaterLevelInfo();
            PredictedTime.text = PredictionTime(CurrentSliderValue);
        }

        worstCaseHeightText.text = "+ " + waterPlaneScript.worstCaseWaterPlaneHeight * 100 + "cm";
        //FOR TESTING PURPOSES
        //Make uncertainty text random between 90-100% on slider position 1, between 60-90% on slider position 2 and between 20-60% on slider position 3
        float randomValue = Random.Range(0.0f, 1.0f);
        if (CurrentSliderValue == 1)
        {
            UncertaintyText.text = $"Prediction Certainty\n\n{randomValue * 10 + 90:F0}%";
            velocityText.text = "Water Velocity\n\n0.2 m/s";
        }
        else if (CurrentSliderValue == 2)
        {
            UncertaintyText.text = $"Prediction Certainty\n\n{randomValue * 30 + 60:F0}%";
            velocityText.text = "Water Velocity\n\n2 m/s";
        }
        else
        {
            UncertaintyText.text = $"Prediction Certainty\n\n{randomValue * 40 + 20:F0}%";
            velocityText.text = "Water Velocity\n\n3 m/s";
        }
        ///////////////////////////////////////

    }

    public string PredictionTime(float SliderValue)
    {
        if (SliderValue == 1)
        {
            //Return current Time without seconds
            return System.DateTime.Now.ToString("HH:mm");
        }
        else if (SliderValue == 2)
        {
            //Return current Time + 10 minutes without seconds
            return System.DateTime.Now.AddMinutes(10).ToString("HH:mm");

        }
        else if (SliderValue == 3)
        {
            return System.DateTime.Now.AddMinutes(20).ToString("HH:mm");
        }
        else
        {
            return "Prediction Error";
        }
    }
    public void ToggleWaterVisualization()
    {
        _isWaterVisible = !_isWaterVisible;
        if (waterPlaneScript != null && waterPlaneScript.IsWaterPlaneNull() == false)
        {
            if (_isWaterVisible)
            {
                UpdateWaterLevelInfo();
                PredictedTime.text = PredictionTime(CurrentSliderValue);
                _scanningCoroutine = StartCoroutine(StartScanningAndSpawnWater());
            }
            else
            {
                if (_scanningCoroutine != null)
                {
                    StopCoroutine(_scanningCoroutine);
                    _scanningCoroutine = null;
                }
                try
                {
                    GameObject.Find("UncertaintyBarsBtn").GetComponent<PressableButton>().ForceSetToggled(false);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[WaterLevelController] Button is not enabled: {e.Message}");
                }
                sliderGameObject.SetActive(false);
                //find gameobject by name

                //_pinSliderButton.gameObject.SetActive(false);
                instructionPannel.SetActive(false); // Hide the instruction panel
                waterPlaneScript.SetWaterVisibility(_isWaterVisible);
                arMeshManager.SetActive(false); // Disable scanning if water is not visible
                _arMeshManagerScript.DestroyAllMeshes(); // Clear the scanned meshes
            }
        }
    }

    private void SetUpPinButton()
    {
        _pinSliderButton.IsToggled.OnEntered.AddListener((args) =>
        {
            _sliderRadialViewComponent.enabled = false;

            //_pinSliderButton.GetComponentInChildren<FontIconSelector>().CurrentIconName = "Icon 121";
            //_pinIcon.faceColor = new Color(191f / 255f, 44f / 255f, 0f, 1f);
        });
        _pinSliderButton.IsToggled.OnExited.AddListener((args) =>
        {
            _sliderRadialViewComponent.enabled = true;

            //_pinSliderButton.GetComponentInChildren<FontIconSelector>().CurrentIconName = "Icon 120";
            //_pinIcon.faceColor = Color.white;
        });
    }

    private IEnumerator StartScanningAndSpawnWater()
    {
        arMeshManager.SetActive(true); // Enable scanning
        instructionPannel.SetActive(true); // Show the instruction panel
        yield return new WaitForSeconds(scanDuration); // Wait for the scanning duration
        instructionPannel.SetActive(false); // Hide the instruction panel
        waterPlaneScript.SetWaterVisibility(_isWaterVisible);
        waterPlaneScript.BringWater(); // Spawn the water after scanning
        // Enable slider gameobject
        sliderGameObject.SetActive(true);
        //_pinSliderButton.gameObject.SetActive(true);
    }

    public void UpdateWaterHeights(float[] newHeights)
    {
        waterPlaneScript.SetWaterHeights(newHeights);
    }

    //Function to change the color of 2 random POIs in the scene to yellow if slider is on position 2 and red if slider is on position 3. All POIs are under a Gameobject called POIs. On Slider Value 1 reset the color of all POIs to white.
    public void ChangePOIColorRandom()
    {

        int sliderValue = (int)CurrentSliderValue;

        if (sliderValue == 2 || sliderValue == 3)
        {

            Transform POIs = GameObject.Find("#POIs").transform;
            int childCount = POIs.childCount;
            if (childCount < 2)
            {
                return;
            }
            int[] randomIndices = new int[2];
            for (int i = 0; i < 2; i++)
            {
                randomIndices[i] = Random.Range(0, childCount);
            }

            for (int i = 0; i < 2; i++)
            {
                Transform child = POIs.GetChild(randomIndices[i]);

                POIInteraction[] poiInteractions = child.GetComponentsInChildren<POIInteraction>();

                foreach (var poiInteraction in poiInteractions)
                {
                    if (sliderValue == 2)
                    {
                        poiInteraction.ChangeColor(Color.yellow);
                    }
                    else if (sliderValue == 3)
                    {
                        poiInteraction.ChangeColor(Color.red);
                    }
                }
            }
        }
        else if (sliderValue == 1)
        {

            Transform POIs = GameObject.Find("#POIs").transform;
            int childCount = POIs.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = POIs.GetChild(i);

                POIInteraction[] poiInteractions = child.GetComponentsInChildren<POIInteraction>();

                foreach (var poiInteraction in poiInteractions)
                {
                    poiInteraction.ChangeColor(Color.white);
                }
            }
        }

    }

    public void UpdateWaterLevelInfo()
    {
        if (waterPlaneScript != null)
        {
            waterInfoText.text = waterPlaneScript.UpdateWaterInfoPanel(CurrentSliderValue);
        }
    }
}
