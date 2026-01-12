using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using FirefighterAR.Training;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training.Sessions
{
    /// <summary>
    /// Training session for teaching users how to access and use the hand menu.
    /// Uses MRTK3's HandConstraintPalmUp component and requires poking 3 buttons from 8.
    /// </summary>
    public class HandMenuTrainingSession : TrainingSession
    {
        [Header("Hand Menu Training Settings")]
        [SerializeField] private GameObject handMenu; // Reference to the actual hand menu
        [SerializeField] private HandConstraintPalmUp handConstraintPalmUp;
        [SerializeField] private bool createConstraintIfMissing = true;
        [SerializeField] private int requiredButtonPresses = 3;
        [SerializeField] private int totalMenuButtons = 8;

        [Header("Visual Guidance")]
        [SerializeField] private GameObject palmGuidanceVisual;
        [SerializeField] private GameObject progressIndicator;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip handMenuSound;
        [SerializeField] private AudioClip guidanceSound;
        [SerializeField] private AudioClip buttonPressSound;
        [SerializeField] private AudioClip progressSound;

        // Private variables
        private bool handMenuActivated = false;
        private bool palmDetected = false;
        private int buttonsPressedCount = 0;
        private List<PressableButton> menuButtons = new List<PressableButton>();
        private HashSet<PressableButton> pressedButtons = new HashSet<PressableButton>();
        private Coroutine guidanceCoroutine;
        private Coroutine palmMonitorCoroutine;
        private HandMenuTrainingTexts texts;

        // Training phases
        private enum TrainingPhase
        {
            ShowPalm,
            MenuActivated,
            ButtonInteraction,
            Completed
        }

        private TrainingPhase currentPhase = TrainingPhase.ShowPalm;

        protected override void LoadLocalizedTexts()
        {
            texts = TrainingTextManager.GetHandMenuTexts();
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
            LoadLocalizedTexts();

            // Find or create hand constraint if missing
            if (handConstraintPalmUp == null)
            {
                handConstraintPalmUp = FindObjectOfType<HandConstraintPalmUp>();

                if (handConstraintPalmUp == null && createConstraintIfMissing)
                {
                    CreateHandConstraint();
                }
            }

            // Find hand menu if not assigned
            if (handMenu == null)
            {
                // Look for the hand menu GameObject
                handMenu = FindHandMenuObject();
            }

            // Find all buttons in the hand menu
            if (handMenu != null)
            {
                FindMenuButtons();
            }

            // Initialize visual guidance
            if (palmGuidanceVisual != null)
            {
                palmGuidanceVisual.SetActive(false);
            }

            if (progressIndicator != null)
            {
                progressIndicator.SetActive(false);
            }

            if (trainingManager != null)
            {
                trainingManager.OnTrainingCompleted += () =>
                {

                    // Disable the Hand Menu Parent (if it exists)
                    if (handMenu != null && handMenu.transform.parent != null)
                    {
                        handMenu.transform.parent.gameObject.SetActive(false);
                    }
                };
            }

            Debug.Log($"[HandMenuTrainingSession] Initialized with {menuButtons.Count} menu buttons found");
        }

        protected override void OnSessionStart()
        {
            handMenuActivated = false;
            palmDetected = false;
            buttonsPressedCount = 0;
            pressedButtons.Clear();
            currentPhase = TrainingPhase.ShowPalm;

            // Setup button listeners
            SetupButtonListeners();

            // Start guidance sequence
            guidanceCoroutine = StartCoroutine(ShowGuidanceSequence());

            // Start palm monitoring
            palmMonitorCoroutine = StartCoroutine(MonitorPalmState());

            UpdateProgress(texts?.StartInstruction ?? "HAND MENU ACCESS: Lift your LEFT hand to chest level and turn your palm to face you.");
        }

        protected override void OnSessionStop()
        {
            // Remove button listeners
            RemoveButtonListeners();

            // Hide guidance visuals
            if (palmGuidanceVisual != null)
            {
                palmGuidanceVisual.SetActive(false);
            }

            if (progressIndicator != null)
            {
                progressIndicator.SetActive(false);
            }

            // Stop coroutines
            if (guidanceCoroutine != null)
            {
                StopCoroutine(guidanceCoroutine);
                guidanceCoroutine = null;
            }

            if (palmMonitorCoroutine != null)
            {
                StopCoroutine(palmMonitorCoroutine);
                palmMonitorCoroutine = null;
            }

            // Hide hand menu
            if (handMenu != null)
            {
                handMenu.SetActive(false);
            }

            StopAllCoroutines();
        }

        protected override void OnSessionUpdate()
        {
            // Check if we need to progress to the next phase
            switch (currentPhase)
            {
                case TrainingPhase.ShowPalm:
                    if (handMenu != null && handMenu.activeInHierarchy && !handMenuActivated)
                    {
                        OnHandMenuActivated();
                    }
                    break;

                case TrainingPhase.ButtonInteraction:
                    // Validate completion when we have enough button presses
                    ValidateTaskCompletion(buttonsPressedCount >= requiredButtonPresses);
                    break;
            }
        }

        private void CreateHandConstraint()
        {
            if (handMenu == null) return;

            // Add HandConstraintPalmUp to the hand menu
            handConstraintPalmUp = handMenu.GetComponent<HandConstraintPalmUp>();
            if (handConstraintPalmUp == null)
            {
                handConstraintPalmUp = handMenu.AddComponent<HandConstraintPalmUp>();
            }

            // Configure the hand constraint for palm up detection
            handConstraintPalmUp.FacingCameraTrackingThreshold = 80f;
            handConstraintPalmUp.RequireFlatHand = true;
            handConstraintPalmUp.FlatHandThreshold = 45f;

            Debug.Log("[HandMenuTrainingSession] Created HandConstraintPalmUp component");
        }

        private GameObject FindHandMenuObject()
        {
            // Look for common hand menu names or components
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                string objName = obj.name.ToLower();
                if (objName.Contains("handmenu") ||
                    objName.Contains("hand_menu") ||
                    objName.Contains("hand menu") ||
                    obj.GetComponent<HandConstraintPalmUp>() != null)
                {
                    return obj;
                }
            }

            Debug.LogWarning("[HandMenuTrainingSession] Could not find hand menu automatically");
            return null;
        }

        private void FindMenuButtons()
        {
            menuButtons.Clear();

            if (handMenu == null) return;

            // Find all PressableButton components in the hand menu and its children
            PressableButton[] buttons = handMenu.GetComponentsInChildren<PressableButton>();
            foreach (var button in buttons)
            {
                menuButtons.Add(button);
            }

            Debug.Log($"[HandMenuTrainingSession] Found {menuButtons.Count} buttons in hand menu");
        }

        private void SetupButtonListeners()
        {
            foreach (var button in menuButtons)
            {
                if (button != null)
                {
                    button.OnClicked.AddListener(() => OnMenuButtonPressed(button));
                }
            }
        }

        private void RemoveButtonListeners()
        {
            foreach (var button in menuButtons)
            {
                if (button != null)
                {
                    button.OnClicked.RemoveAllListeners();
                }
            }
        }

        private void OnMenuButtonPressed(PressableButton button)
        {
            if (!isSessionActive || currentPhase != TrainingPhase.ButtonInteraction) return;

            // Only count if this button hasn't been pressed before
            if (!pressedButtons.Contains(button))
            {
                pressedButtons.Add(button);
                buttonsPressedCount++;

                Debug.Log($"[HandMenuTrainingSession] Button pressed! {buttonsPressedCount}/{requiredButtonPresses}");

                // Play button press sound
                if (audioSource != null && buttonPressSound != null)
                {
                    audioSource.PlayOneShot(buttonPressSound);
                }

                // Update progress
                if (buttonsPressedCount < requiredButtonPresses)
                {
                    string progressMessage = texts?.ProgressMessage ?? "Great! Press {0} more buttons to complete the training. ({1}/{2})";
                    UpdateProgress(string.Format(progressMessage, requiredButtonPresses - buttonsPressedCount, buttonsPressedCount, requiredButtonPresses));
                }
                else
                {
                    string completionProgress = texts?.CompletionProgress ?? "Perfect! You've successfully pressed {0} buttons from the hand menu.";
                    UpdateProgress(string.Format(completionProgress, requiredButtonPresses));
                    currentPhase = TrainingPhase.Completed;

                    // Complete after short delay
                    StartCoroutine(CompleteAfterDelay(2f));
                }
            }
        }

        private IEnumerator MonitorPalmState()
        {
            while (isSessionActive && currentPhase == TrainingPhase.ShowPalm)
            {
                // Check if hand menu is now active (indicating palm was detected)
                if (handMenu != null && handMenu.activeInHierarchy && !handMenuActivated)
                {
                    palmDetected = true;
                    UpdateProgress(texts?.MenuActivated ?? "MENU ACTIVATED: The hand menu is now visible and contains your toolkit.");
                }

                yield return new WaitForSeconds(0.2f);
            }

            palmMonitorCoroutine = null;
        }

        private void OnHandMenuActivated()
        {
            handMenuActivated = true;
            currentPhase = TrainingPhase.MenuActivated;

            Debug.Log("[HandMenuTrainingSession] Hand menu activated!");

            // Play success sound
            if (audioSource != null && handMenuSound != null)
            {
                audioSource.PlayOneShot(handMenuSound);
            }

            // Stop guidance
            if (guidanceCoroutine != null)
            {
                StopCoroutine(guidanceCoroutine);
                guidanceCoroutine = null;
            }

            // Hide guidance visuals
            if (palmGuidanceVisual != null)
            {
                palmGuidanceVisual.SetActive(false);
            }

            // Move to button interaction phase
            StartCoroutine(StartButtonInteractionPhase());
        }

        private IEnumerator StartButtonInteractionPhase()
        {
            yield return new WaitForSeconds(1f);

            currentPhase = TrainingPhase.ButtonInteraction;

            string interactionInstruction = texts?.InteractionInstruction ?? "MENU INTERACTION: Practice selecting {0} different buttons using poke or hand ray methods.";
            UpdateProgress(string.Format(interactionInstruction, requiredButtonPresses));

            if (audioSource != null && progressSound != null)
            {
                audioSource.PlayOneShot(progressSound);
            }
        }

        private IEnumerator ShowGuidanceSequence()
        {
            yield return new WaitForSeconds(1f);

            // Show palm guidance visual
            if (palmGuidanceVisual != null)
            {
                palmGuidanceVisual.SetActive(true);

                // Position it in front of the user, slightly to the left
                Transform cameraTransform = Camera.main.transform;
                Vector3 guidancePosition = cameraTransform.position +
                                         cameraTransform.forward * 1.2f +
                                         cameraTransform.right * -0.4f;
                palmGuidanceVisual.transform.position = guidancePosition;
                palmGuidanceVisual.transform.LookAt(cameraTransform);
            }

            yield return new WaitForSeconds(2f);

            // Play guidance sound
            if (audioSource != null && guidanceSound != null)
            {
                audioSource.PlayOneShot(guidanceSound);
            }

            yield return new WaitForSeconds(3f);

            // Update instruction with more detail
            UpdateProgress(texts?.PositioningTechnique ?? "POSITIONING TECHNIQUE: Lift your LEFT hand to chest level and rotate your palm toward your face.");

            yield return new WaitForSeconds(8f);

            // Repeat guidance every 15 seconds if not completed
            while (isSessionActive && currentPhase == TrainingPhase.ShowPalm)
            {
                yield return new WaitForSeconds(15f);

                if (isSessionActive && currentPhase == TrainingPhase.ShowPalm)
                {
                    if (palmDetected)
                    {
                        UpdateProgress(texts?.MaintainPosition ?? "MAINTAIN POSITION: Keep your palm facing toward you.");
                    }
                    else
                    {
                        UpdateProgress(texts?.ReminderMessage ?? "REMINDER: Lift your LEFT hand and position your palm facing you.");
                    }

                    if (audioSource != null && guidanceSound != null)
                    {
                        audioSource.PlayOneShot(guidanceSound);
                    }
                }
            }

            guidanceCoroutine = null;
        }

        private IEnumerator CompleteAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CompleteSession();
        }

        /// <summary>
        /// Public method to set hand menu reference
        /// </summary>
        public void SetHandMenu(GameObject menu)
        {
            handMenu = menu;
            if (handMenu != null)
            {
                FindMenuButtons();
            }
        }

        /// <summary>
        /// Public method to set hand constraint reference
        /// </summary>
        public void SetHandConstraint(HandConstraintPalmUp constraint)
        {
            handConstraintPalmUp = constraint;
        }

        /// <summary>
        /// Public method to configure button press requirements
        /// </summary>
        public void SetButtonRequirements(int requiredPresses, int totalButtons)
        {
            requiredButtonPresses = requiredPresses;
            totalMenuButtons = totalButtons;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RemoveButtonListeners();
            //stop all coroutines
            StopAllCoroutines();
        }
    }
}