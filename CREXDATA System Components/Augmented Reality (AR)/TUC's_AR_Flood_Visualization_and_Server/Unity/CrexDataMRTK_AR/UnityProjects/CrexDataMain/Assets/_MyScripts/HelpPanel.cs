using UnityEngine;
using TMPro;

public class HelpPanel : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI paragraphText;
    public GameObject helpPanel;
    void Start()
    {
        titleText.text = "Help Guide";
        subtitleText.text = "Using HoloLens 2 and the Application";

        paragraphText.text = "Welcome to the AR Flood Visualization App! This guide will help you get started with HoloLens 2 and our application.\n\n" +
                             "<b>Interacting with POIs:</b> Use eye gaze to focus on a Point of Interest (POI). To interact, use a gaze pinch gesture. POIs provide essential information when selected.\n\n" +
                             "<b>Using the Map:</b> The map follows your head movement and can be grabbed to reposition it. To grab, use a pinch gesture with your hand.\n\n" +
                             "<b>Hand Menu:</b> To access the hand menu, hold your palm up facing the HoloLens. The menu includes important functions such as calibration and settings.\n\n" +
                             "<b>Calibration:</b> At the start of the application, perform the calibration process. Open the hand menu and select 'Calibration' to enable accurate placement and interaction with virtual objects.\n\n" +
                             "Enjoy your experience and stay informed!";
    }

    // Method to toggle the help panel visibility
    public void ToggleHelpPanel()
    {
        if (helpPanel.activeSelf)
        {
            helpPanel.SetActive(false);
        }
        else
        {
            helpPanel.SetActive(true);
        }
    }
}