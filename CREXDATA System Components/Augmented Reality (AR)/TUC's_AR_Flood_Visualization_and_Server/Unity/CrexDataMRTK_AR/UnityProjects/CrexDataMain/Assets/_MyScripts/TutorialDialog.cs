using MixedReality.Toolkit.Examples.Demos;
using MixedReality.Toolkit.UX;
using UnityEngine;

public class TutorialDialog : MonoBehaviour
{
    public static TutorialDialog Instance { get; private set; }
    public bool isTutorialDone = false;
    private DialogPool DialogPool;
    //[SerializeField] private StartingConfigurationPanels _startingConfigurationPanels;
    [SerializeField] private GameObject _tutorialPanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        DialogPool = GetComponent<DialogPool>();
    }

    void Start()
    {
        InspectorDrivenDialog dialog = GetComponent<InspectorDrivenDialog>();
        SpawnTutorialDialog();
    }

    public void SpawnTutorialDialog()
    {
        IDialog dialog = DialogPool.Get()
            .SetHeader("Welcome to the tutorial")
            .SetBody("Proceed to the Tutorial?")
            .SetPositive("Yes", (args) => StartTutorial())
            .SetNegative("Skip", (args) => SkipTutorial());

        dialog.Show();
    }

    public void CompleteTutorial()
    {
        isTutorialDone = true;
        _tutorialPanel.SetActive(false);
        //Debug.Log("Tutorial has been marked as completed.");
        //_startingConfigurationPanels.ShowWelcomePanel();
        gameObject.GetComponent<TutorialDialogTraining>().SpawnTutorialDialog();
    }

    public void StartTutorial()
    {
        _tutorialPanel.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
        _tutorialPanel.transform.rotation = Camera.main.transform.rotation;
        _tutorialPanel.SetActive(true);
        //Debug.Log("Tutorial has been started.");
    }

    public void SkipTutorial()
    {
        isTutorialDone = true;
        //Debug.Log("Tutorial has been skipped.");
        //_startingConfigurationPanels.ShowWelcomePanel();
        gameObject.GetComponent<TutorialDialogTraining>().SpawnTutorialDialog();
    }
}