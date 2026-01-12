using UnityEngine;
using MixedReality.Toolkit.SpatialManipulation;
using Mapbox.Unity.MeshGeneration.Factories;
public class MyDialogBoxSetup : MonoBehaviour
{
    private SolverHandler solverHandler;
    private Follow followSolver;
    //TO BE FIXED
    void Awake()
    {
        try
        {
            // Get the SolverHandler component
            solverHandler = GetComponent<SolverHandler>();
            followSolver = GetComponent<Follow>();
            if (TutorialDialog.Instance != null && solverHandler != null)
            {
                if (!TutorialDialogTraining.Instance.isTutorialDone)
                {
                    SetupForTutorial();
                }
                else
                {
                    SetupForMap();
                }
            }


            followSolver.MoveLerpTime = 0.2f;
            followSolver.RotateLerpTime = 0.5f;
            followSolver.OrientToControllerDeadZoneDegrees = 25.0f;
            followSolver.IgnoreAngleClamp = false;
            followSolver.MaxViewVerticalDegrees = 20.0f;
            followSolver.MaxViewHorizontalDegrees = 20.0f;
            followSolver.OrientationType = SolverOrientationType.CameraFacing;
            followSolver.ReorientWhenOutsideParameters = false;

        }
        catch (System.Exception)
        {
            Debug.LogWarning("Follow Solver not found");
            throw;
        }
    }

    public void UpdatePosition()
    {
        if (solverHandler != null && RoutingManager.Instance.selectedPOI != null)
        {
            if (solverHandler.TrackedTargetType == TrackedObjectType.Head)
            {
                SetupForMap();
            }
            solverHandler.TransformOverride = RoutingManager.Instance.selectedPOI;
        }
    }

    private void SetupForTutorial()
    {
        solverHandler.TrackedTargetType = TrackedObjectType.Head;
        followSolver.MinDistance = 0.4f;
        followSolver.MaxDistance = 0.6f;
        followSolver.DefaultDistance = 0.6f;
    }

    public void SetupForDrone()
    {
        solverHandler.TrackedTargetType = TrackedObjectType.Head;
        solverHandler.AdditionalOffset = Vector3.zero;
        followSolver.MinDistance = 0.4f;
        followSolver.MaxDistance = 0.6f;
        followSolver.DefaultDistance = 0.6f;
    }
    private void SetupForMap()
    {
        solverHandler.TrackedTargetType = TrackedObjectType.CustomOverride;
        solverHandler.AdditionalOffset = new Vector3(0, 0.12f, -0.12f);
        followSolver.MinDistance = 0.1f;
        followSolver.MaxDistance = 0.15f;
        followSolver.DefaultDistance = 0.1f;
    }
}