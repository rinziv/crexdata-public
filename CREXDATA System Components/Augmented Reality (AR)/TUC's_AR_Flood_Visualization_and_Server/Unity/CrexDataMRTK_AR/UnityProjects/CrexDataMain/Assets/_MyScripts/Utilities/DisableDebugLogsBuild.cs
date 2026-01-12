using UnityEngine;

public class DisableDebugLogsBuild : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void DisableDebugLogs()
    {
#if !UNITY_EDITOR
        Debug.unityLogger.logEnabled = false;
#endif
    }
}
