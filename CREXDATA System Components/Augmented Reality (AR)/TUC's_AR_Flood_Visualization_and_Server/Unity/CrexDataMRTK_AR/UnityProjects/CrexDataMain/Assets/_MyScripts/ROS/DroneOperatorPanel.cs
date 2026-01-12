using MixedReality.Toolkit.UX;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum AlertType
{
    None,
    Person,
    Manhole,
    General
}

[Serializable]
public class AlertButton
{
    public PressableButton button;
    public AlertType alertType;
}

public class DroneOperatorPanel : MonoBehaviour
{
    private DialogPool DialogPool;
    private WebSocketConnection _webSocketConnection;
    [SerializeField] private AlertButton[] _alertButtons;
    [SerializeField] private PressableButton _deleteLastPOIButton, _deleteAllPOIButton;

    [SerializeField]
    private List<string> _spawnedPois = new List<string>();

    void Awake()
    {
        if (DialogPool == null)
        {
            DialogPool = FindAnyObjectByType<DialogPool>();
        }
        if (_webSocketConnection == null)
        {
            _webSocketConnection = FindAnyObjectByType<WebSocketConnection>();
        }

        GPSSpawner.OnAlertSpawned += (poiId) => _spawnedPois.Add(poiId);
    }

    void Start()
    {
        foreach (var alertButton in _alertButtons)
        {
            //Based on the alert type, set the button's action
            switch (alertButton.alertType)
            {
                case AlertType.None:
                    alertButton.button.OnClicked.AddListener(() => Debug.Log("No action defined for this button"));
                    break;
                default:
                    alertButton.button.OnClicked.AddListener(() => ShowConfirmationDialog(alertButton.alertType));
                    break;
            }
        }

        _deleteLastPOIButton.OnClicked.AddListener(ShowDeleteConfirmationDialog);
        _deleteAllPOIButton.OnClicked.AddListener(ShowDeleteAllPOIsDialog);
    }

    void ShowDeleteConfirmationDialog()
    {
        if (_spawnedPois.Count == 0)
        {
            ShowInfoDialog("No operator sent POIs to delete.");
            return;
        }

        IDialog dialog;
        dialog = DialogPool.Get()
            .SetHeader("Confirm Deletion")
            .SetBody("Are you sure you want to delete the last spawned POI?")
            .SetPositive("Yes", (args) =>
            {
                DeletePOI(_spawnedPois[_spawnedPois.Count - 1]);
            })
            .SetNegative("No", (args) =>
            {
                Debug.Log("Deletion cancelled.");
            });

        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }

    void ShowInfoDialog(string message)
    {
        IDialog dialog = DialogPool.Get()
            .SetHeader("Information")
            .SetBody(message)
            .SetPositive("Ok", (args) => { });

        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }

    void ShowDeleteAllPOIsDialog()
    {
        IDialog dialog = DialogPool.Get()
            .SetHeader("Confirm Deletion")
            .SetBody("Are you seure you want to delete all sent POIs?")
            .SetPositive("Proceed", (args) =>
            {
                DeleteAllPOIs();
            })
            .SetNegative("Cancel", (args) =>
            {
                Debug.Log("Deletion of all POIs cancelled.");
            });

        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }

    void ShowConfirmationDialog(AlertType alertType)
    {
        IDialog dialog;
        dialog = DialogPool.Get()
            .SetHeader("Confirm Action")
            .SetBody($"Are you sure you want to send an alert for detected {alertType}?")
            .SetPositive("Yes", (args) =>
            {
                SendAlert(alertType);
                Debug.Log($"Alert for {alertType} sent.");
            })
            .SetNegative("No", (args) =>
            {
                Debug.Log($"Alert for detected {alertType} cancelled.");
            });

        // Get the GameObject of the dialog about to show
        GameObject dialogGameObject = (dialog as MonoBehaviour)?.gameObject;
        dialogGameObject.TryGetComponent(out MyDialogBoxSetup myDialogBoxSetup);
        myDialogBoxSetup.SetupForDrone();

        dialog.Show();
    }

    public void SendDeletePOIRequest(string poiId)
    {
        var deleteRequest = new
        {
            type = "deletePOI",
            poiId
        };
        _webSocketConnection.SendJsonToServer(deleteRequest);
    }

    public void SendAlert(AlertType alertType)
    {
        // Create an alert request as JSON
        var alertRequest = new
        {
            type = "alert",
            alertType = alertType.ToString(),
            alertID = Guid.NewGuid().ToString() // Generate a unique ID for the alert
        };

        // Send as JSON
        _webSocketConnection.SendJsonToServer(alertRequest);
    }

    void DeletePOI(string poiId)
    {
        // Remove the POI from the spawned list
        if (_spawnedPois.Contains(poiId))
        {
            _spawnedPois.Remove(poiId);
            SendDeletePOIRequest(poiId);
            Debug.Log($"POI with ID {poiId} deleted.");
        }
        else
        {
            Debug.LogWarning($"POI with ID {poiId} not found in the spawned list.");
        }
    }

    void DeleteAllPOIs()
    {
        if (_spawnedPois.Count == 0)
        {
            ShowInfoDialog("No operator sent POIs to delete.");
            return;
        }

        foreach (var poiId in _spawnedPois)
        {
            SendDeletePOIRequest(poiId);
        }
        _spawnedPois.Clear();
        Debug.Log("All POIs deleted.");
    }
}
