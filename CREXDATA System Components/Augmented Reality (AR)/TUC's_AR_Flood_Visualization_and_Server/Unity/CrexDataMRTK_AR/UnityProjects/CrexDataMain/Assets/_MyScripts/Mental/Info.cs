using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Info : MonoBehaviour
{
    private string _id;
    public TextMeshProUGUI icon;
    public TextMeshProUGUI label;
    public TextMeshProUGUI id;
    public TextMeshProUGUI timestamp;

    private Material _iconMaterial;

    [SerializeField] private GameObject _disconnectedIcon;

    private void Start()
    {
        _iconMaterial = new Material(icon.fontMaterial);
        icon.fontMaterial = _iconMaterial;
        _disconnectedIcon.SetActive(false);
    }
    public void SetUserId(string id)
    {
        _id = id;
    }
    public string GetUserId() => _id;

    public void SetIconColor(Color color)
    {
        if (_iconMaterial != null)
        {
            _iconMaterial.SetColor("_FaceColor", color);
        }
    }

    public void SetDisconnectedIconActive(bool isActive)
    {
        if (_disconnectedIcon != null)
        {
            _disconnectedIcon.SetActive(isActive);
        }
    }
}