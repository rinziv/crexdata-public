using MixedReality.Toolkit.UX;
using System;
using TMPro;
using UnityEngine;

[Serializable]
public class ButtonIconPair
{
    public PressableButton button;
    public TextMeshPro icon;
    public PressableButton activatorButton;

}

public class PinButtonsHelper : MonoBehaviour
{
    public ButtonIconPair[] buttonIconPairs;

    [Header("Pin Button Settings")]
    [Tooltip("The name of the icon to display when the button is pinned (toggled on).")]
    [SerializeField] private string pinnedIconName = "Icon 120";

    [Tooltip("The name of the icon to display when the button is not pinned (toggled off).")]
    [SerializeField] private string unpinnedIconName = "Icon 121";

    [Tooltip("The color of the icon's text when the button is pinned.")]
    [SerializeField] private Color pinnedIconColor = new(191f / 255f, 44f / 255f, 0f, 1f);

    [Tooltip("The color of the icon's text when the button is not pinned.")]
    [SerializeField] private Color unpinnedIconColor = Color.white;

    void Start()
    {
        foreach (var pair in buttonIconPairs)
        {
            if (pair.activatorButton != null)
            {
                pair.activatorButton.IsToggled.OnEntered.AddListener((_) => pair.button.gameObject.SetActive(true));
                pair.activatorButton.IsToggled.OnExited.AddListener((_) => pair.button.gameObject.SetActive(false));
            }
            if (pair.button != null && pair.icon != null)
            {
                SetUpPinButton(pair.button, pair.icon);
            }
        }
    }

    private void SetUpPinButton(PressableButton _pinButton, TextMeshPro _pinIcon)
    {
        _pinButton.gameObject.SetActive(false);
        var fontIconSelector = _pinButton.GetComponentInChildren<FontIconSelector>();

        if (fontIconSelector != null)
        {
            fontIconSelector.CurrentIconName = unpinnedIconName;
        }
        _pinIcon.faceColor = unpinnedIconColor;


        _pinButton.IsToggled.OnEntered.AddListener((args) =>
        {
            if (fontIconSelector != null)
            {
                fontIconSelector.CurrentIconName = pinnedIconName;
            }
            _pinIcon.faceColor = pinnedIconColor;
        });


        _pinButton.IsToggled.OnExited.AddListener((args) =>
        {
            if (fontIconSelector != null)
            {
                fontIconSelector.CurrentIconName = unpinnedIconName;
            }
            _pinIcon.faceColor = unpinnedIconColor;
        });
    }
}
