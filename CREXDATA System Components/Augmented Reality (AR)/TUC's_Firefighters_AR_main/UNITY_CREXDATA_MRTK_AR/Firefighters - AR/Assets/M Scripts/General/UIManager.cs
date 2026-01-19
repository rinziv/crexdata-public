using MixedReality.Toolkit.UX;
using UnityEngine;

public class UIManager : MonoBehaviour
{


    public void ClosePanel(GameObject panel){
        panel.SetActive(false);
    }

    public void OpenPanel(GameObject panel){
        panel.SetActive(true);
    }

    public void ToglePanel(GameObject panel){
        panel.SetActive(!panel.gameObject.activeSelf);
    }

}