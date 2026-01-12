using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    // The name of the scene you want to load (must be in Build Settings!)
    [SerializeField] private string sceneToLoad = "MainScene";

    // Static reference if you want a singleton approach
    public static SceneChanger Instance { get; private set; }

    private void Awake()
    {
        // Optional Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // Ensure this manager (and anything else on it) isn't destroyed
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void LoadNextScene()
    {
        // Loads the specified scene in "single" mode, 
        // which unloads the current one but keeps DontDestroyOnLoad objects alive.
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    // If you ever need to load a scene additively, you can do:
    public void LoadSceneAdditively()
    {
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Additive);
    }
}
