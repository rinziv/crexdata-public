using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public GPSWebSocketSender gpsWebSocketSender;
    public Button connectButton;
    public bool ipSet = false;
    public bool usernameSet = false;
    public bool hololensIDSet = false;

    // UI Objects
    public GameObject introPanel;
    public GameObject mainPanel;
    public GameObject openGPSButton;
    public GameObject closeGPSButton;

    // Timer
    private float timer = 0.0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer > 1.0f)
        {
            if (!gpsWebSocketSender.isConnected && ipSet && usernameSet && hololensIDSet)
            {
                ActivateButton(connectButton);
            }
            else
            {
                DeactivateButton(connectButton);
            }
            timer = 0.0f;
        }


    }

    public void SetIP()
    {
        ipSet = true;
    }

    public void SetUsername()
    {
        usernameSet = true;
    }

    public void SetHololensID()
    {
        hololensIDSet = true;
    }

    public void ClosePanel(GameObject panel)
    {
        panel.SetActive(false);
    }

    public void OpenPanel(GameObject panel)
    {
        panel.SetActive(true);
    }

    public void ToglePanel(GameObject panel)
    {
        panel.SetActive(!panel.activeSelf);
    }

    public void ActivateButton(Button button)
    {
        button.interactable = true;
    }

    public void DeactivateButton(Button button)
    {
        button.interactable = false;
    }

    public void OnServerDisconect()
    {
        ClosePanel(closeGPSButton);
        OpenPanel(openGPSButton);
        ClosePanel(mainPanel);
        OpenPanel(introPanel);
    }

    public void MakeButtonInteractable(Button button)
    {
        button.interactable = true;
    }
}