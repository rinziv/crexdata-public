using UnityEngine;
using MixedReality.Toolkit.UX;
using MixedReality.Toolkit.Input;
using System.Collections;
using MixedReality.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;
using FirefighterAR.Training;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training.Sessions
{
    /// <summary>
    /// Training session for teaching users how to use hand ray interactions.
    /// This teaches the "point and pinch" gesture for distant object interaction.
    /// </summary>
    public class HandRayTrainingSession : TrainingSession
    {
        [Header("Hand Ray Training Settings")]
        [SerializeField] private StatefulInteractable targetInteractable;
        [SerializeField] private GameObject targetContainer;
        [SerializeField] private float targetSpawnDistance = 2.5f;
        [SerializeField] private Vector3 targetSpawnOffset = Vector3.up * 0.5f;

        [Header("Visual Guidance")]
        [SerializeField] private GameObject arrowIndicator;
        [SerializeField] private GameObject handRayGuidanceVisual;
        [SerializeField] private GameObject pinchIndicator;
        [SerializeField] private GameObject rayVisualization;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material normalMaterial;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip rayInteractionSound;
        [SerializeField] private AudioClip guidanceSound;

        // Private variables
        private bool targetSelected = false;
        private bool handRayDetected = false;
        private int selectionsRemaining = 3;
        private int totalSelections = 0;
        private Renderer targetRenderer;
        private Coroutine guidanceCoroutine;
        private Coroutine rayDetectionCoroutine;
        private HandRayTrainingTexts texts;

        protected override void LoadLocalizedTexts()
        {
            texts = TrainingTextManager.GetHandRayTexts();
            if (texts != null)
            {
                sessionName = texts.SessionName;
                sessionDescription = texts.SessionDescription;
                initialInstruction = texts.InitialInstruction;
                completionMessage = texts.CompletionMessage;
            }
        }

        protected override void OnSessionInitialize()
        {
            // Localized texts are loaded in LoadLocalizedTexts method

            // Create target if not assigned
            if (targetInteractable == null)
            {
                CreateTrainingTarget();
            }

            // Setup interactable events
            if (targetInteractable != null)
            {
                targetInteractable.OnClicked.AddListener(OnTargetSelected);
                targetRenderer = targetInteractable.GetComponent<Renderer>();
                targetInteractable.DisableInteractorType(typeof(IPokeInteractor));
                targetInteractable.DisableInteractorType(typeof(IGazePinchInteractor));
                targetInteractable.DisableInteractorType(typeof(IGrabInteractor));
            }

            // Initialize visual guidance
            if (handRayGuidanceVisual != null)
            {
                handRayGuidanceVisual.SetActive(false);
            }

            if (pinchIndicator != null)
            {
                pinchIndicator.SetActive(false);
            }

            if (rayVisualization != null)
            {
                rayVisualization.SetActive(false);
            }

            if (trainingManager != null)
            {
                trainingManager.OnTrainingCompleted += () =>
                {
                    //Disable the interactable
                    if (targetInteractable != null)
                    {
                        targetContainer.SetActive(false);
                    }

                };
            }
        }

        protected override void OnSessionStart()
        {
            targetSelected = false;
            handRayDetected = false;
            selectionsRemaining = 3;
            totalSelections = 0;

            // Position the target
            PositionTarget();

            // Enable the target
            if (targetContainer != null)
            {
                targetContainer.SetActive(true);
            }

            if (arrowIndicator != null)
            {
                arrowIndicator.SetActive(true);
                arrowIndicator.GetComponent<DirectionalIndicator>().DirectionalTarget = targetContainer.transform;
            }

            if (targetInteractable != null)
            {
                targetInteractable.enabled = true;

            }

            // Start guidance sequence
            guidanceCoroutine = StartCoroutine(ShowGuidanceSequence());

            // Start ray detection
            rayDetectionCoroutine = StartCoroutine(DetectHandRayUsage());

            UpdateProgress(texts?.StartInstruction ?? "HAND RAY INTERACTION: Extend your hand toward the distant target sphere. A ray will project from your hand. Use the pinch gesture (thumb to index finger) to select the object 3 times from a distance.");
        }

        protected override void OnSessionStop()
        {
            // Hide target and guidance
            if (targetContainer != null)
            {
                targetContainer.SetActive(false);
            }

            if (handRayGuidanceVisual != null)
            {
                handRayGuidanceVisual.SetActive(false);
            }

            if (pinchIndicator != null)
            {
                pinchIndicator.SetActive(false);
            }

            if (rayVisualization != null)
            {
                rayVisualization.SetActive(false);
            }

            // Stop coroutines
            if (guidanceCoroutine != null)
            {
                StopCoroutine(guidanceCoroutine);
                guidanceCoroutine = null;
            }

            if (rayDetectionCoroutine != null)
            {
                StopCoroutine(rayDetectionCoroutine);
                rayDetectionCoroutine = null;
            }



            HighlightTarget(false);
        }

        protected override void OnSessionUpdate()
        {
            // Validate completion
            ValidateTaskCompletion(targetSelected);
        }

        private void CreateTrainingTarget()
        {
            // Create a simple training target if none is assigned
            GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetObj.name = "TrainingRayTarget";
            targetObj.transform.localScale = Vector3.one * 0.2f;

            // Add StatefulInteractable component
            targetInteractable = targetObj.AddComponent<StatefulInteractable>();

            // Create container
            targetContainer = new GameObject("RayTargetContainer");
            targetObj.transform.SetParent(targetContainer.transform);

            Debug.Log("[HandRayTrainingSession] Created training target");
        }

        private void PositionTarget()
        {
            if (targetContainer == null || Camera.main == null) return;

            // Position target at a distance from the user
            Transform cameraTransform = Camera.main.transform;
            Vector3 targetPosition = cameraTransform.position + cameraTransform.forward * targetSpawnDistance + targetSpawnOffset;

            targetContainer.transform.position = targetPosition;
            targetContainer.transform.LookAt(cameraTransform);
        }

        private void OnTargetSelected()
        {
            if (!isSessionActive) return;

            selectionsRemaining--;
            totalSelections++;

            Debug.Log($"[HandRayTrainingSession] Target selected! {totalSelections}/3");

            // Play success sound
            if (audioSource != null && rayInteractionSound != null)
            {
                audioSource.PlayOneShot(rayInteractionSound);
            }

            // Update progress based on remaining selections
            if (selectionsRemaining > 0)
            {
                string message = texts?.ProgressMessage ?? "Excellent! Select the target {0} more times to complete the hand ray interaction training. ({1}/3)";
                UpdateProgress(string.Format(message, selectionsRemaining, totalSelections));
            }
            else
            {
                targetSelected = true;
                UpdateProgress(texts?.CompletionProgress ?? "Outstanding! You have successfully used hand ray interaction 3 times to select a distant object. This method is essential for operating equipment, accessing controls, and selecting objects that are out of physical reach.");

                // Stop guidance
                if (guidanceCoroutine != null)
                {
                    StopCoroutine(guidanceCoroutine);
                    guidanceCoroutine = null;
                }

                if (rayDetectionCoroutine != null)
                {
                    StopCoroutine(rayDetectionCoroutine);
                    rayDetectionCoroutine = null;
                }

                // Hide guidance visuals
                if (handRayGuidanceVisual != null)
                {
                    handRayGuidanceVisual.SetActive(false);
                }

                if (pinchIndicator != null)
                {
                    pinchIndicator.SetActive(false);
                }

                if (rayVisualization != null)
                {
                    rayVisualization.SetActive(false);
                }

                // Remove highlight
                HighlightTarget(false);

                if (normalMaterial != null)
                {
                    targetRenderer.material = normalMaterial;
                }

                if (arrowIndicator != null)
                {
                    arrowIndicator.SetActive(false);
                }

                // Complete after short delay
                StartCoroutine(CompleteAfterDelay(1.5f));
            }
        }

        private IEnumerator CompleteAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CompleteSession();
        }

        private IEnumerator ShowGuidanceSequence()
        {
            yield return new WaitForSeconds(1f);

            // Show ray visualization
            if (rayVisualization != null && targetContainer != null)
            {
                rayVisualization.SetActive(true);
                // Position ray visualization between camera and target
                Vector3 midPoint = Vector3.Lerp(Camera.main.transform.position, targetContainer.transform.position, 0.5f);
                rayVisualization.transform.position = midPoint;
                rayVisualization.transform.LookAt(targetContainer.transform);
            }

            yield return new WaitForSeconds(2f);

            // Play guidance sound
            if (audioSource != null && guidanceSound != null)
            {
                audioSource.PlayOneShot(guidanceSound);
            }

            yield return new WaitForSeconds(3f);

            // Show hand guidance visual
            if (handRayGuidanceVisual != null)
            {
                handRayGuidanceVisual.SetActive(true);
                // Position it near the user's expected hand position
                Transform cameraTransform = Camera.main.transform;
                Vector3 handPosition = cameraTransform.position + cameraTransform.right * -0.3f + cameraTransform.forward * 0.5f;
                handRayGuidanceVisual.transform.position = handPosition;
            }

            // Show pinch indicator
            if (pinchIndicator != null)
            {
                pinchIndicator.SetActive(true);
                if (handRayGuidanceVisual != null)
                {
                    pinchIndicator.transform.position = handRayGuidanceVisual.transform.position + Vector3.up * 0.1f;
                }
            }

            // Update instruction
            UpdateProgress(texts?.TechniqueGuidance ?? "TECHNIQUE: Raise your hand and point at the target cube. Bring your thumb and index finger together in a pinch motion to activate selection. The hand ray extends your interaction range significantly.");

            yield return new WaitForSeconds(10f);

            // Update instruction with more detail
            UpdateProgress(texts?.ImportantNote ?? "IMPORTANT: Hand ray interaction is crucial - it allows you to operate distant equipment without leaving your position. Master this technique for effective field operations.");

            // Repeat guidance every 15 seconds if not completed
            while (isSessionActive && !targetSelected)
            {
                yield return new WaitForSeconds(15f);

                if (isSessionActive && !targetSelected)
                {
                    if (handRayDetected)
                    {
                        UpdateProgress(texts?.TargetingActive ?? "TARGETING ACTIVE: Your hand ray is aligned with the target. Execute the pinch gesture (thumb to index finger) to complete the distant selection.");
                    }
                    else
                    {
                        UpdateProgress(texts?.PositioningInstruction ?? "POSITIONING: Extend your hand and point at the target cube. The hand ray will appear as a line projection from your hand, enabling distant interaction capabilities.");
                    }

                    if (audioSource != null && guidanceSound != null)
                    {
                        audioSource.PlayOneShot(guidanceSound);
                    }
                }
            }

            guidanceCoroutine = null;
        }

        private IEnumerator DetectHandRayUsage()
        {
            while (isSessionActive && !targetSelected)
            {
                // Check if hand rays are active
                var handRays = PlayspaceUtilities.XROrigin.GetComponentsInChildren<MRTKRayInteractor>(true);

                bool rayActive = false;
                foreach (var rayInteractor in handRays)
                {
                    if (rayInteractor.gameObject.activeInHierarchy && rayInteractor.enabled)
                    {
                        // Check if ray interactor has valid targets or is hovering
                        if (rayInteractor.hasHover && rayInteractor.interactablesHovered.Count > 0)
                        {
                            foreach (var hoveredInteractable in rayInteractor.interactablesHovered)
                            {
                                if (ReferenceEquals(hoveredInteractable, targetInteractable))
                                {
                                    rayActive = true;
                                    break;
                                }
                            }
                            if (rayActive) break;
                        }
                    }
                }

                if (rayActive && !handRayDetected)
                {
                    handRayDetected = true;
                    HighlightTarget(true);
                    UpdateProgress(texts?.TargetingConfirmed ?? "TARGETING CONFIRMED: Your hand ray is now aimed at the target. Execute the pinch gesture to complete the selection.");
                }
                else if (!rayActive && handRayDetected)
                {
                    handRayDetected = false;
                    HighlightTarget(false);
                    UpdateProgress(texts?.AimRequired ?? "AIM REQUIRED: Point your hand directly at the highlighted cube to establish targeting with your hand ray.");
                }

                yield return new WaitForSeconds(0.1f);
            }

            rayDetectionCoroutine = null;
        }

        private void HighlightTarget(bool highlight)
        {
            if (targetRenderer == null) return;

            if (highlight && highlightMaterial != null)
            {
                targetRenderer.material = highlightMaterial;
            }
            else if (!highlight && normalMaterial != null)
            {
                if (normalMaterial != null)
                {
                    Material whiteMat = new Material(normalMaterial);
                    whiteMat.color = Color.white;
                    targetRenderer.material = whiteMat;
                }
                else
                {
                    targetRenderer.material = new Material(Shader.Find("Standard")) { color = Color.white };
                }

            }
        }

        // Public method to set target manually
        public void SetTargetInteractable(StatefulInteractable interactable)
        {
            targetInteractable = interactable;
            if (targetInteractable != null)
            {
                targetInteractable.OnClicked.AddListener(OnTargetSelected);
                targetRenderer = targetInteractable.GetComponent<Renderer>();
                targetContainer = targetInteractable.gameObject;
            }
        }
    }
}