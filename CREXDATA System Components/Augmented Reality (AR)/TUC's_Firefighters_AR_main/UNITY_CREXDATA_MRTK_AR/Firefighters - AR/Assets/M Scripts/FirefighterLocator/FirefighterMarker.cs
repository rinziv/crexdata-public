using UnityEngine;
using Mapbox.Utils;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MixedReality.Toolkit.UX;

public class FirefighterMarker : MonoBehaviour
{
    public Firefighter firefighter; // Reference to the associated firefighter
    private FirefighterLocator locator;

    void Start()
    {
        // Find the FirefighterLocator in the scene
        locator = FindObjectOfType<FirefighterLocator>();

        if (locator == null)
        {
            Debug.LogError("FirefighterLocator not found in the scene.");
        }

    }

    public void OpenFirefighterPanel()
    {
        if (locator != null && firefighter != null)
        {
            locator.selectedFirefighter = firefighter.id;
            locator.OnFirefighterMarkerClicked(firefighter);
        }
    }

}
