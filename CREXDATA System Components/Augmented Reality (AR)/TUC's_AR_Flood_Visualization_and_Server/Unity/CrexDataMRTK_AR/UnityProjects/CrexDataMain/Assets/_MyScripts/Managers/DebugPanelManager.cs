using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugPanelManager : MonoBehaviour
{
    public TextMeshProUGUI debugText; // Reference to the TextMeshProUGUI component
    public RectTransform panelRect; // Reference to the RectTransform of the panel
    public float lineHeight = 20f; // Height of each line of text

    private static DebugPanelManager instance;

    public static event System.Action<string> OnDebugMessageAdded;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        //OnDebugMessageAdded += DisplaySingleDebugLine;
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        OnDebugMessageAdded -= DisplaySingleDebugLine;
    }

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            return;
        }

        debugText.text += message + " \n";
        AdjustPanelSize();
        Canvas.ForceUpdateCanvases();
        //scrollRect.verticalNormalizedPosition = 0;
    }
    public static void Log(string message)
    {
        OnDebugMessageAdded?.Invoke(message);
    }

    private void DisplaySingleDebugLine(string message)
    {
        // Clear existing messages and display only the new message
        debugText.text = message;
        AdjustPanelSize();
    }

    private void AdjustPanelSize()
    {
        Vector2 newSize = new Vector2(panelRect.sizeDelta.x, lineHeight);
        panelRect.sizeDelta = newSize;
    }
}
