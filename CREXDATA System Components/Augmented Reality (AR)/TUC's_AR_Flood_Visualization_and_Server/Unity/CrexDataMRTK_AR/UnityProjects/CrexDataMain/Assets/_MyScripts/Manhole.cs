using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Manhole : MonoBehaviour
{
    private void Start()
    {
        if (ManholeManager.Instance != null)
        {
            ManholeManager.Instance.RegisterManhole(this);
        }
        else
        {
            Debug.LogWarning("ManholeManager instance not found. Manhole will not be managed.", this);
        }
    }

    private void OnDestroy()
    {
        // Unregister from the manager when the object is destroyed.
        if (ManholeManager.Instance != null)
        {
            ManholeManager.Instance.UnregisterManhole(this);
        }
    }
}
