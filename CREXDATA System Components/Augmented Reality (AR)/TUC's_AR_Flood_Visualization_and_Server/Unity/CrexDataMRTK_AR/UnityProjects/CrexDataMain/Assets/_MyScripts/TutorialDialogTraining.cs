using FirefighterAR.Training;
using MixedReality.Toolkit.Examples.Demos;
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using UnityEngine;

public class TutorialDialogTraining : MonoBehaviour
{
    public static TutorialDialogTraining Instance { get; private set; }
    public bool isTutorialDone = false;
    private DialogPool DialogPool;
    [SerializeField] private StartingConfigurationPanels _startingConfigurationPanels;
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

    public void SpawnTutorialDialog()
    {
        IDialog dialog = DialogPool.Get()
            .SetHeader("Welcome to the tutorial")
            .SetBody("Proceed to a small training session?")
            .SetPositive("Yes", (args) => SelectLanguageForTraining())
            .SetNegative("Skip", (args) => SkipTutorial());

        SetupDialogBox(dialog);
        dialog.Show();
    }

    public void SelectLanguageForTraining()
    {
        IDialog dialog = DialogPool.Get()
            .SetHeader("Select Language")
            .SetBody("Please select your preferred language for the training sessions.")
            .SetPositive("English", (args) =>
            {
                TrainingTextManager.SetEnglish();
                StartTutorial();
            })
            .SetNegative("German", (args) =>
            {
                TrainingTextManager.SetGerman();
                StartTutorial();
            });

        SetupDialogBox(dialog);
        dialog.Show();
    }

    void SetupDialogBox(IDialog dialog)
    {
        var dialogComponent = dialog as Component;
        if (dialogComponent != null)
        {
            SolverHandler solverHandler = dialogComponent.GetComponent<SolverHandler>();
            Follow followSolver = dialogComponent.GetComponent<Follow>();
            followSolver.MoveLerpTime = 0.2f;
            followSolver.RotateLerpTime = 0.5f;
            followSolver.OrientToControllerDeadZoneDegrees = 25.0f;
            followSolver.IgnoreAngleClamp = false;
            followSolver.MaxViewVerticalDegrees = 20.0f;
            followSolver.MaxViewHorizontalDegrees = 20.0f;
            followSolver.OrientationType = SolverOrientationType.CameraFacing;
            followSolver.ReorientWhenOutsideParameters = false;
            solverHandler.TrackedTargetType = TrackedObjectType.Head;
            followSolver.MinDistance = 0.4f;
            followSolver.MaxDistance = 0.6f;
            followSolver.DefaultDistance = 0.6f;
        }
    }

    public void CompleteTutorial()
    {
        isTutorialDone = true;
        _tutorialPanel.SetActive(false);
        _startingConfigurationPanels.ShowWelcomePanel();
    }

    public void StartTutorial()
    {
        _tutorialPanel.SetActive(true);
    }

    public void SkipTutorial()
    {
        isTutorialDone = true;
        _startingConfigurationPanels.ShowWelcomePanel();
    }
}