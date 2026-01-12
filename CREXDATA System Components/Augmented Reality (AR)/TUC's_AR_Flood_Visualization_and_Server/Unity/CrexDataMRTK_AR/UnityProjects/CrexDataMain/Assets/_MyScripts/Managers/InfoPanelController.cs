using UnityEngine;
using TMPro;

public class InfoPanelController : MonoBehaviour
{
    private Animator animator;
    public TextMeshProUGUI panelText; // Reference to the TextMeshPro component
    public GameObject panel; // Reference to the panel GameObject

    void Start()
    {
        animator = GetComponent<Animator>();
        // Ensure the panel starts hidden
        animator.SetBool("IsVisible", false);
        panel.SetActive(false);
    }

    public void ShowInfoPanel(string infoText)
    {
        animator.SetBool("IsVisible", true);
        UpdateInfo(infoText);
    }

    public void HideInfoPanel()
    {
        animator.SetBool("IsVisible", false);
    }
    // Method to be called by the animation event to activate the panel
    public void ActivatePanel()
    {
        panel.SetActive(true);
    }

    // Method to be called by the animation event to deactivate the panel
    public void DeactivatePanel()
    {
        panel.SetActive(false);
    }
    private void UpdateInfo(string infoText)
    {
        panelText.text = infoText;
    }
}
