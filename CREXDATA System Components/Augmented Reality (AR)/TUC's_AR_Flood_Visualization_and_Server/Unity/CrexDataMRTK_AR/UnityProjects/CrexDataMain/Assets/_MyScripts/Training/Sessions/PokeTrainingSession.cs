using UnityEngine;
using MixedReality.Toolkit.UX;
using System.Collections;
using MixedReality.Toolkit.Input;
using MixedReality.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;
using FirefighterAR.Training;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training.Sessions
{
    /// <summary>
    /// Training session for teaching users how to poke/press buttons with their finger.
    /// This is the fundamental interaction for UI elements in AR.
    /// </summary>
    public class PokeTrainingSession : TrainingSession
    {
        [Header("Poke Training Settings")]
        [SerializeField] private PressableButton targetButton;
        [SerializeField] private GameObject buttonContainer;
        [SerializeField] private float buttonSpawnDistance = 1.5f;
        [SerializeField] private Vector3 buttonSpawnOffset = Vector3.zero;

        [Header("Visual Guidance")]
        [SerializeField] private GameObject arrowIndicator;
        [SerializeField] private GameObject handGuidanceVisual;
        [SerializeField] private GameObject pokeIndicator;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material normalMaterial;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip buttonPressSound;
        [SerializeField] private AudioClip guidanceSound;

        [Header("Animation Settings")]
        [SerializeField] private AnimationCurve positionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float repositionDuration = 0.8f;
        [SerializeField] private bool useSmoothing = true;

        // Private variables
        private bool buttonPressed = false;
        private Vector3 originalButtonPosition;
        private Renderer buttonRenderer;
        private int pokesRemaining = 3;
        private int totalPresses = 0;
        private Coroutine guidanceCoroutine;
        private Coroutine repositionCoroutine;
        private bool isRepositioning = false;

        private PokeTrainingTexts texts;

        protected override void LoadLocalizedTexts()
        {
            texts = TrainingTextManager.GetPokeTexts();
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

            // Create button if not assigned
            if (targetButton == null)
            {
                CreateTrainingButton();
            }

            // Setup button event
            if (targetButton != null)
            {
                targetButton.OnClicked.AddListener(OnButtonPressed);
                buttonRenderer = targetButton.GetComponent<Renderer>();
                targetButton.DisableInteractorType(typeof(IRayInteractor));
                targetButton.DisableInteractorType(typeof(IGazePinchInteractor));
            }

            // Initialize visual guidance
            if (handGuidanceVisual != null)
            {
                handGuidanceVisual.SetActive(false);
            }

            if (pokeIndicator != null)
            {
                pokeIndicator.SetActive(false);
            }

            if (trainingManager != null)
            {
                trainingManager.OnTrainingCompleted += () =>
                {
                    // Disable the button
                    if (targetButton != null)
                    {
                        buttonContainer.SetActive(false);
                    }
                };
            }
            else
            {
                Debug.LogWarning("[PokeTrainingSession] TrainingManager reference is missing.");
            }
        }

        protected override void OnSessionStart()
        {
            buttonPressed = false;
            pokesRemaining = 3;
            totalPresses = 0;

            // Highlight the button
            HighlightButton(true);

            // Start guidance
            StartCoroutine(ShowGuidanceSequence());

            // Position the button in front of the user
            PositionButton();
            // Enable the button
            if (targetButton != null)
            {
                targetButton.enabled = true;
                if (buttonContainer != null)
                {
                    buttonContainer.SetActive(true);
                }
            }

            if (arrowIndicator != null)
            {
                arrowIndicator.SetActive(true);
                arrowIndicator.GetComponent<DirectionalIndicator>().DirectionalTarget = buttonContainer.transform;
            }

            UpdateProgress(texts?.StartInstruction ?? "POKE INTERACTION: Position yourself close to the button and use your index finger to directly touch and press it 3 times. This is how you will interact with all buttons and controls in the application.");
        }

        protected override void OnSessionStop()
        {
            // Stop all coroutines
            if (guidanceCoroutine != null)
            {
                StopCoroutine(guidanceCoroutine);
                guidanceCoroutine = null;
            }

            if (repositionCoroutine != null)
            {
                StopCoroutine(repositionCoroutine);
                repositionCoroutine = null;
            }

            isRepositioning = false;

            // Hide button and guidance
            if (buttonContainer != null)
            {
                buttonContainer.SetActive(false);
            }

            if (handGuidanceVisual != null)
            {
                handGuidanceVisual.SetActive(false);
            }

            if (pokeIndicator != null)
            {
                pokeIndicator.SetActive(false);
            }

            HighlightButton(false);
        }

        private float lastRepositionTime = 0f;
        private const float REPOSITION_COOLDOWN = 2f; // Prevent too frequent repositioning

        protected override void OnSessionUpdate()
        {
            // Check if button needs repositioning (with cooldown and not during animation)
            if (targetButton != null &&
                !isRepositioning &&
                Time.time - lastRepositionTime > REPOSITION_COOLDOWN &&
                Vector3.Distance(targetButton.transform.position, Camera.main.transform.position) > 1f)
            {
                lastRepositionTime = Time.time;
                RepositionButton();
            }

            // Validate completion
            ValidateTaskCompletion(buttonPressed && pokesRemaining <= 0);
        }

        private void CreateTrainingButton()
        {
            // Create a simple training button if none is assigned
            GameObject buttonObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObj.name = "TrainingPokeButton";
            buttonObj.transform.localScale = new Vector3(0.15f, 0.05f, 0.15f);

            // Add PressableButton component
            targetButton = buttonObj.AddComponent<PressableButton>();

            // Create container
            buttonContainer = new GameObject("PokeButtonContainer");
            buttonObj.transform.SetParent(buttonContainer.transform);

            Debug.Log("[PokeTrainingSession] Created training button");
        }

        private void PositionButton()
        {
            if (targetButton == null || Camera.main == null) return;

            // Position button in front of the user
            Transform cameraTransform = Camera.main.transform;
            Vector3 targetPosition = cameraTransform.position + cameraTransform.forward * buttonSpawnDistance + buttonSpawnOffset;

            buttonContainer.transform.position = targetPosition;
            targetButton.transform.LookAt(cameraTransform);
            targetButton.transform.Rotate(0f, 180f, 0f, Space.Self);

            originalButtonPosition = targetPosition;
        }

        private void RepositionButton()
        {
            // Prevent multiple simultaneous repositioning
            if (isRepositioning || buttonContainer == null || Camera.main == null) return;

            if (useSmoothing)
            {
                StartSmoothRepositioning();
            }
            else
            {
                PositionButton();
                //UpdateProgress(texts?.ButtonRepositioned ?? "Button repositioned. Please poke the highlighted button to continue.");
            }
        }

        private void StartSmoothRepositioning()
        {
            // Stop any existing repositioning
            if (repositionCoroutine != null)
            {
                StopCoroutine(repositionCoroutine);
            }

            repositionCoroutine = StartCoroutine(SmoothRepositionCoroutine());
        }

        private IEnumerator SmoothRepositionCoroutine()
        {
            isRepositioning = true;

            // Calculate new target position
            Transform cameraTransform = Camera.main.transform;
            Vector3 newTargetPosition = cameraTransform.position + cameraTransform.forward * buttonSpawnDistance + buttonSpawnOffset;

            Vector3 startPosition = buttonContainer.transform.position;
            Quaternion startRotation = buttonContainer.transform.rotation;

            // Calculate target rotation (button facing user).
            // The button prefab faces the opposite direction, so rotate an extra 180 degrees to align visuals.
            Quaternion targetRotation = Quaternion.LookRotation(cameraTransform.position - newTargetPosition) * Quaternion.Euler(0f, 180f, 0f);

            float elapsedTime = 0f;

            // Animate the movement
            while (elapsedTime < repositionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / repositionDuration;

                // Use animation curve for smooth easing
                float curveValue = positionCurve.Evaluate(normalizedTime);

                // Interpolate position and rotation
                buttonContainer.transform.position = Vector3.Lerp(startPosition, newTargetPosition, curveValue);
                buttonContainer.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curveValue);

                yield return null; // Wait for next frame
            }

            // Ensure final position is exact
            buttonContainer.transform.position = newTargetPosition;
            buttonContainer.transform.rotation = targetRotation;
            originalButtonPosition = newTargetPosition;

            isRepositioning = false;
            repositionCoroutine = null;

            //UpdateProgress(texts?.ButtonRepositionedSmooth ?? "Button smoothly repositioned. Please poke the highlighted button to continue.");
        }

        private void OnButtonPressed()
        {
            if (!isSessionActive) return;

            pokesRemaining--;
            totalPresses++;

            Debug.Log($"[PokeTrainingSession] Button pressed! {totalPresses}/3");

            // Play success sound
            if (audioSource != null && buttonPressSound != null)
            {
                audioSource.PlayOneShot(buttonPressSound);
            }

            // Update progress based on remaining presses
            if (pokesRemaining > 0)
            {
                string message = texts?.ProgressMessage ?? "Great! Press the button {0} more times to complete the poke interaction training. ({1}/3)";
                UpdateProgress(string.Format(message, pokesRemaining, totalPresses));
            }
            else
            {
                buttonPressed = true;
                UpdateProgress(texts?.CompletionProgress ?? "Perfect! You have successfully executed 3 poke interactions. Poke is your primary method for activating buttons, toggles, and interactive elements in close proximity.");

                // Stop guidance
                if (guidanceCoroutine != null)
                {
                    StopCoroutine(guidanceCoroutine);
                    guidanceCoroutine = null;
                }

                // Hide guidance visuals
                if (handGuidanceVisual != null)
                {
                    handGuidanceVisual.SetActive(false);
                }

                if (pokeIndicator != null)
                {
                    pokeIndicator.SetActive(false);
                }

                // Remove highlight
                HighlightButton(false);

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

            // Show poke indicator
            if (pokeIndicator != null && targetButton != null)
            {
                pokeIndicator.SetActive(true);
                pokeIndicator.transform.position = targetButton.transform.position + Vector3.forward * 0.1f;
            }

            yield return new WaitForSeconds(2f);

            // Play guidance sound
            if (audioSource != null && guidanceSound != null)
            {
                audioSource.PlayOneShot(guidanceSound);
            }

            yield return new WaitForSeconds(3f);

            // Show hand guidance visual
            if (handGuidanceVisual != null)
            {
                handGuidanceVisual.SetActive(true);
                if (targetButton != null)
                {
                    handGuidanceVisual.transform.position = targetButton.transform.position + Vector3.right * 0.2f;
                }
            }

            // Update instruction
            UpdateProgress(texts?.TechniqueGuidance ?? "TECHNIQUE: Extend your index finger and make direct contact with the button surface. You should feel haptic feedback confirming the interaction. This direct touch method ensures precise control.");

            yield return new WaitForSeconds(10f);

            // Hide hand guidance but keep poke indicator
            if (handGuidanceVisual != null)
            {
                handGuidanceVisual.SetActive(false);
            }

            // Repeat guidance every 10 seconds if not completed
            while (isSessionActive && !buttonPressed)
            {
                yield return new WaitForSeconds(10f);

                if (isSessionActive && !buttonPressed)
                {
                    UpdateProgress(texts?.ReminderMessage ?? "REMINDER: Move closer to the button and use your index finger to make direct physical contact. Poke interaction requires proximity and direct touch for accurate control.");

                    if (audioSource != null && guidanceSound != null)
                    {
                        audioSource.PlayOneShot(guidanceSound);
                    }
                }
            }

            guidanceCoroutine = null;
        }

        private void HighlightButton(bool highlight)
        {
            if (buttonRenderer == null) return;

            if (highlight && highlightMaterial != null)
            {
                buttonRenderer.material = highlightMaterial;
            }
            else if (!highlight && normalMaterial != null)
            {
                buttonRenderer.material = normalMaterial;
            }
        }

        public void SetTargetButton(PressableButton button)
        {
            targetButton = button;
            if (targetButton != null)
            {
                targetButton.OnClicked.AddListener(OnButtonPressed);
                buttonRenderer = targetButton.GetComponent<Renderer>();
                buttonContainer = targetButton.gameObject;
            }
        }


        public void SetMultiplePokes(int pokeCount)
        {
            pokesRemaining = pokeCount;
            string template = texts?.MultiplePokeInstruction ?? "Press the button {0} times using poke interaction.";
            initialInstruction = string.Format(template, pokeCount);
        }

        /// <summary>
        /// Configure smooth animation settings
        /// </summary>
        /// <param name="duration">Duration of the repositioning animation</param>
        /// <param name="curve">Animation curve for easing (null to use default)</param>
        /// <param name="enableSmoothing">Enable or disable smooth repositioning</param>
        public void ConfigureAnimation(float duration = 0.8f, AnimationCurve curve = null, bool enableSmoothing = true)
        {
            repositionDuration = Mathf.Clamp(duration, 0.1f, 3f); // Clamp to reasonable values
            useSmoothing = enableSmoothing;

            if (curve != null && curve.keys.Length >= 2)
            {
                positionCurve = curve;
            }
        }

        /// <summary>
        /// Force immediate repositioning (bypasses cooldown and animation)
        /// </summary>
        public void ForceRepositionButton()
        {
            if (repositionCoroutine != null)
            {
                StopCoroutine(repositionCoroutine);
                repositionCoroutine = null;
            }

            isRepositioning = false;
            PositionButton();
            lastRepositionTime = Time.time;
        }
    }
}