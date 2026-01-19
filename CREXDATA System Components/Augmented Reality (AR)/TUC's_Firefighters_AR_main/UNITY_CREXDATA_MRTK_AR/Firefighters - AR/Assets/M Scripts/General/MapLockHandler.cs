using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapLockHandler : MonoBehaviour
{    
    // Map Position Lock Handling
    private bool mapLocked = true;
    public Material lockedMaterial;
    public Material unlockedMaterial;
    public GameObject lockIcon;

    public void ToggleMapLock(){

        mapLocked = !mapLocked;
        
        if(mapLocked){
            lockIcon.GetComponent<MeshRenderer>().material = lockedMaterial;
        }
        else{
            lockIcon.GetComponent<MeshRenderer>().material = unlockedMaterial;
        }
    }
}
