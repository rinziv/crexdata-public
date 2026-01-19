using JetBrains.Annotations;
using MixedReality.Toolkit.UX;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

public class SystemSetupHandler : MonoBehaviour
{
    public bool gpsConnected = false;
    public bool systemCalibrated = false;
    public bool systemReady = false;

    // Update Timer
    public float callInterval = 2f; // How many seconds between calls
    private float timer;

    // UI Elements
    public Button gpsConnectedButton;
    public Button calibrateButton;
    public PressableButton gpsConnectedPressablebutton;
    public PressableButton calibratePressablebutton;
    public GameObject gpsConnectionPanel;
    public Material gpsButtonMaterial;
    public Material calibrateButtonMaterial;
    public TextMeshPro gpsPanelTitle;
    public Material disabledButtonMaterial;
    public MapInitializer mapInitializer;

    // Hand Menu
    public GameObject handMenu;


    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= callInterval)
        {
            if (gpsConnected && systemCalibrated && !systemReady)
            {
                systemReady = true;
                InitializeSystem();
                Debug.Log("System Ready");
            }
            timer = 0f;
        }
    }

    public void SetServerConnected(){
        
        gpsPanelTitle.text = "Connect To GPS!";
    }

    public void SetGPSConnected()
    {
        if(!gpsConnected)
            mapInitializer.StartMap();

        gpsConnected = true;
        gpsConnectedButton.interactable = true;
        gpsConnectedPressablebutton.enabled = true;
        gpsConnectedButton.GetComponent<MeshRenderer>().material = gpsButtonMaterial;
        gpsPanelTitle.text = "GPS Connected!";
        gpsPanelTitle.color = new Color32(21, 41, 72, 255);

    }

    public void GPSDisconnected(){
        gpsConnected = false;
        gpsConnectedButton.interactable = false;
        gpsConnectedPressablebutton.enabled = false;
        gpsConnectedButton.GetComponent<MeshRenderer>().material = disabledButtonMaterial;
        gpsPanelTitle.text = "GPS Disconnected!";
        gpsPanelTitle.color = new Color32(26, 26, 26, 255);
    }

    public void SetSystemCalibrated()
    {
        systemCalibrated = true;
        Debug.Log("System Calibrated");
    }

    public void InitializeSystem()
    {
        handMenu.SetActive(true);
        Debug.Log("System Initialized");
    }

    public void CalibrateSystem()
    {
        StartCoroutine(CallFunctionAfterDelay(2f));
        Debug.Log("System Calibrating");
    }

    IEnumerator CallFunctionAfterDelay(float delay) {
        
        yield return new WaitForSeconds(delay);
        EnableCalibratinButton();
    }

    void EnableCalibratinButton() {
        calibrateButton.interactable = true;
        calibratePressablebutton.enabled = true;
        calibrateButton.GetComponent<MeshRenderer>().material = calibrateButtonMaterial;
    }
}
