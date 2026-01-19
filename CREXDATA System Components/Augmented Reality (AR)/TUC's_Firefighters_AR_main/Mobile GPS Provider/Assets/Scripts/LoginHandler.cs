using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LoginHandler : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text hololensidText;

    public string userName;
    public string hololensID;

    public TMP_Text debugText;

    public void OnNameUpdated()
    {
        string stringTmp = nameText.text;
        stringTmp = stringTmp.Replace("\u200B", "");
        userName = stringTmp;
        Debug.Log("Username updated to: " + userName);
        debugText.text = "Username updated to: " + userName;
    }

    public void OnIdUpdated()
    {
        string stringTmp = hololensidText.text;
        stringTmp = stringTmp.Replace("\u200B", "");
        hololensID = stringTmp;
        Debug.Log("Hololens ID updated to: " + hololensID);
        debugText.text = "Hololens ID updated to: " + hololensID;
    }
}
