using UnityEngine;
using System;
using Microsoft.MixedReality.QR;  // Make sure you have the Microsoft.MixedReality.QR DLLs in your project
using System.Threading.Tasks;

public class SceneCalibrationManagerQR : MonoBehaviour
{
    private QRCodeWatcher qrWatcher;

    private async void Start()
    {
        // 1. Request access to the QR code API
        QRCodeWatcherAccessStatus accessStatus = await QRCodeWatcher.RequestAccessAsync();

        if (accessStatus == QRCodeWatcherAccessStatus.Allowed)
        {
            Debug.Log("QR Code tracking allowed.");
            
            // 2. Create the watcher
            qrWatcher = new QRCodeWatcher();
            
            // 3. Subscribe to events
            qrWatcher.Added += OnQRCodeAdded;
            qrWatcher.Updated += OnQRCodeUpdated;
            qrWatcher.Removed += OnQRCodeRemoved;

            // 4. Start the watcher
            qrWatcher.Start();
        }
        else
        {
            Debug.LogWarning("QR Code tracking not allowed: " + accessStatus);
        }
    }

    private void OnQRCodeAdded(object sender, QRCodeAddedEventArgs args)
    {
        // Extract the QR code data (the "string" you want)
        QRCode qrCode = args.Code;
        Debug.Log($"QR Code Added! Data: {qrCode.Data}");
    }

    private void OnQRCodeUpdated(object sender, QRCodeUpdatedEventArgs args)
    {
        QRCode qrCode = args.Code;
        Debug.Log($"QR Code Updated! Data: {qrCode.Data}");
    }

    private void OnQRCodeRemoved(object sender, QRCodeRemovedEventArgs args)
    {
        QRCode qrCode = args.Code;
        Debug.Log($"QR Code Removed! Data: {qrCode.Data}");
    }
}