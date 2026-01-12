// Copyright (c) Mixed Reality Toolkit Contributors
// Licensed under the BSD 3-Clause

// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using MixedReality.Toolkit.Input;
using MixedReality.Toolkit.UX;
using UnityEngine;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Helper script to implement the demo scene hand menu actions.
    /// </summary>
    [AddComponentMenu("MRTK/Examples/MiniMenu")]
    internal class MiniMenu : MonoBehaviour
    {

        [SerializeField]
        private StatefulInteractable rayToggle;

        [SerializeField]
        private StatefulInteractable EnableCalibrate, EnableFIltering;

        [SerializeField]
        private GameObject profilerObject;

        /// <summary>
        /// A Unity event function that is called when an enabled script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            rayToggle.ForceSetToggled(GetHandRaysActive());
            rayToggle.OnClicked.AddListener(() =>
            {
                // Debug.Log("RayToggle clicked. IsToggled: " + rayToggle.IsToggled);
                SetHandRaysActive(rayToggle.IsToggled);
            });

        }

        /// <summary>
        /// Toggle hand rays.
        /// </summary>
        public void SetHandRaysActive(bool value)
        {
            var handRays = PlayspaceUtilities.XROrigin.GetComponentsInChildren<MRTKRayInteractor>(true);

            foreach (var interactor in handRays)
            {
                interactor.gameObject.SetActive(value);
            }
        }

        /// <summary>
        /// Get if all hand rays in the scene are active.
        /// </summary>
        private bool GetHandRaysActive()
        {
            bool active = true;
            var handRays = PlayspaceUtilities.XROrigin.GetComponentsInChildren<MRTKRayInteractor>(true);

            foreach (var interactor in handRays)
            {
                active &= interactor.gameObject.activeSelf;
                if (!active)
                {
                    break;
                }
            }

            return active;
        }

        /// <summary>
        /// Toggle gaze pinch interactors.
        /// </summary>
        public void SetGazePinchActive(bool value)
        {
            var gazePinchInteractors = PlayspaceUtilities.XROrigin.GetComponentsInChildren<GazePinchInteractor>(true);

            foreach (var interactor in gazePinchInteractors)
            {
                interactor.gameObject.SetActive(value);
            }
        }

        /// <summary>
        /// Get if all pinch interactors in the scene are active.
        /// </summary>
        private bool GetGazePinchActive()
        {
            bool active = true;
            var gazePinchInteractors = PlayspaceUtilities.XROrigin.GetComponentsInChildren<GazePinchInteractor>(true);

            foreach (var interactor in gazePinchInteractors)
            {
                active &= interactor.gameObject.activeSelf;
                if (!active)
                {
                    break;
                }
            }

            return active;
        }

        /// <summary>
        /// Toggle perf overlay.
        /// </summary>
        public void SetPerfOverlayActive(bool value)
        {
            if (profilerObject != null)
            {
                profilerObject.SetActive(value);
            }
        }
    }
}
#pragma warning restore CS1591
