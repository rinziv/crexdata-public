using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MixedReality.Toolkit.UX;
using TMPro;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training
{
    /// <summary>
    /// Main training manager that controls the entire training flow.
    /// This system guides users through AR interactions step by step.
    /// </summary>
    public class TrainingManager : MonoBehaviour
    {
        [Header("Training Configuration")]
        [SerializeField] private bool startTrainingOnStart = true;
        [SerializeField] private float sessionTransitionDelay = 2f;

        [Header("UI References")]
        [SerializeField] private GameObject trainingCanvas;
        [SerializeField] private TrainingUI trainingUI;
        [SerializeField] private GameObject handMenu; // Reference to your main hand menu
        [SerializeField] private GameObject mainPanel;

        private StartingConfigurationPanels _startingConfigurationPanels;

        [Header("Training Sessions")]
        [SerializeField] private List<TrainingSession> trainingSessions = new List<TrainingSession>();

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip instructionSound;

        // Private variables
        private int currentSessionIndex = -1;
        private bool isTrainingActive = false;
        private bool isTrainingCompleted = false;
        private TrainingManagerTexts managerTexts;

        // Events
        public event Action OnTrainingStarted;
        public event Action OnTrainingCompleted;
        public event Action<int> OnSessionChanged;
        public event Action<TrainingSession> OnSessionCompleted;

        // Properties
        public bool IsTrainingActive => isTrainingActive;
        public bool IsTrainingCompleted => isTrainingCompleted;
        public TrainingSession CurrentSession => currentSessionIndex >= 0 && currentSessionIndex < trainingSessions.Count ? trainingSessions[currentSessionIndex] : null;
        public int TotalSessions => trainingSessions.Count;
        public int CurrentSessionIndex => currentSessionIndex;

        private void Start()
        {

            TrainingTextManager.Initialize();

            InitializeTrainingManager();

            if (startTrainingOnStart)
            {
                StartTraining();
            }
        }

        private void InitializeTrainingManager()
        {
            _startingConfigurationPanels = FindObjectOfType<StartingConfigurationPanels>();
            // Cache manager texts
            managerTexts = TrainingTextManager.GetManagerTexts();

            // Ensure training canvas is initially hidden
            if (trainingCanvas != null)
            {
                trainingCanvas.SetActive(false);
            }

            // Hide hand menu initially during training
            if (handMenu != null)
            {
                handMenu.SetActive(false);
            }

            // Initialize training UI
            if (trainingUI != null)
            {
                trainingUI.Initialize(this);
            }

            // Initialize all training sessions
            foreach (var session in trainingSessions)
            {
                if (session != null)
                {
                    session.Initialize(this);
                }
            }

            Debug.Log($"[TrainingManager] Initialized with {trainingSessions.Count} training sessions and localization support");
        }

        /// <summary>
        /// Starts the training from the beginning
        /// </summary>
        public void StartTraining()
        {
            if (isTrainingActive)
            {
                Debug.LogWarning("[TrainingManager] Training is already active");
                return;
            }

            Debug.Log("[TrainingManager] Starting training session");

            isTrainingActive = true;
            isTrainingCompleted = false;
            currentSessionIndex = -1;

            // Position and show training canvas
            if (trainingCanvas != null)
            {
                PositionTrainingCanvasInFrontOfUser();
                trainingCanvas.SetActive(true);
            }

            // Hide hand menu during training
            if (handMenu != null)
            {
                handMenu.SetActive(false);
            }

            OnTrainingStarted?.Invoke();

            // Start the first session
            StartCoroutine(TransitionToNextSession());
        }

        /// <summary>
        /// Stops the training and cleans up
        /// </summary>
        public void StopTraining()
        {
            Debug.Log("[TrainingManager] Stopping training session");

            // Stop current session if active
            if (CurrentSession != null && CurrentSession.IsActive)
            {
                CurrentSession.StopSession();
            }

            isTrainingActive = false;

            // Hide training canvas
            if (trainingCanvas != null)
            {
                trainingCanvas.SetActive(false);
            }

            // Show hand menu if training was completed
            if (handMenu != null && isTrainingCompleted)
            {
                handMenu.SetActive(true);
            }
        }

        /// <summary>
        /// Restarts the training from the beginning
        /// </summary>
        public void RestartTraining()
        {
            StopTraining();
            StartCoroutine(DelayedStart());
        }

        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(0.5f);
            StartTraining();
        }

        /// <summary>
        /// Called when the current training session is completed
        /// </summary>
        public void OnCurrentSessionCompleted()
        {
            if (CurrentSession == null) return;

            Debug.Log($"[TrainingManager] Session '{CurrentSession.SessionName}' completed");

            // Play success sound
            PlaySound(successSound);

            // Update UI
            if (trainingUI != null)
            {
                string message = managerTexts?.SessionCompleted ?? "Great job! You've completed the {0} training.";
                trainingUI.ShowSuccess(string.Format(message, CurrentSession.SessionName));
            }

            OnSessionCompleted?.Invoke(CurrentSession);

            // Move to next session after delay
            StartCoroutine(TransitionToNextSession());
        }

        private IEnumerator TransitionToNextSession()
        {
            yield return new WaitForSeconds(sessionTransitionDelay);

            currentSessionIndex++;

            if (currentSessionIndex >= trainingSessions.Count)
            {
                // All sessions completed
                CompleteTraining();
            }
            else
            {
                // Start next session
                StartCurrentSession();
            }
        }

        private void StartCurrentSession()
        {
            var session = CurrentSession;
            if (session == null) return;

            Debug.Log($"[TrainingManager] Starting session {currentSessionIndex + 1}/{trainingSessions.Count}: '{session.SessionName}'");

            OnSessionChanged?.Invoke(currentSessionIndex);

            // Update UI
            if (trainingUI != null)
            {
                trainingUI.UpdateSessionInfo(currentSessionIndex + 1, trainingSessions.Count, session.SessionName);
            }

            // Start the session
            session.StartSession();

            // Play instruction sound
            PlaySound(instructionSound);
        }

        private void CompleteTraining()
        {
            Debug.Log("[TrainingManager] All training sessions completed!");

            isTrainingCompleted = true;
            isTrainingActive = false;

            if (trainingUI != null)
            {
                trainingUI.ShowTrainingComplete();
            }

            // Show hand menu after completing training
            if (handMenu != null)
            {
                handMenu.SetActive(true);
            }

            OnTrainingCompleted?.Invoke();

            // Hide training canvas after a delay
            StartCoroutine(HideTrainingCanvasAfterDelay(5f));
        }

        private IEnumerator HideTrainingCanvasAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (trainingCanvas != null)
            {
                trainingCanvas.SetActive(false);
            }

            //Enable Main Panel
            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
            }
            TutorialDialogTraining.Instance.isTutorialDone = true;
            _startingConfigurationPanels.ShowWelcomePanel();
        }

        /// <summary>
        /// Skips the current session (for debugging)
        /// </summary>
        public void SkipCurrentSession()
        {
            if (CurrentSession != null && CurrentSession.IsActive)
            {
                CurrentSession.StopSession();
                OnCurrentSessionCompleted();
            }
        }

        /// <summary>
        /// Allows restarting training from external scripts
        /// </summary>
        public void ReinitiateTraining()
        {
            RestartTraining();
        }

        /// <summary>
        /// Repositions the training canvas in front of the user (can be called externally if needed)
        /// </summary>
        public void RepositionTrainingCanvas()
        {
            if (trainingCanvas != null && trainingCanvas.activeInHierarchy)
            {
                PositionTrainingCanvasInFrontOfUser();
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Positions the training canvas in front of the user at 1m distance with 30cm offset to the right
        /// </summary>
        private void PositionTrainingCanvasInFrontOfUser()
        {
            if (trainingCanvas == null || Camera.main == null) return;

            Transform cameraTransform = Camera.main.transform;
            Vector3 forwardDirection = cameraTransform.forward;
            Vector3 rightDirection = cameraTransform.right;

            // Calculate position: 1m forward, 0.3m to the right
            Vector3 targetPosition = cameraTransform.position +
                                   forwardDirection * 0.6f +
                                   rightDirection * 0.3f;

            // Set the position
            trainingCanvas.transform.position = targetPosition;

            // Make the canvas face the user (optional - remove if you want it to maintain its original rotation)
            Vector3 directionToUser = cameraTransform.position - trainingCanvas.transform.position;
            directionToUser.y = 0; // Keep it horizontal

            if (directionToUser != Vector3.zero)
            {
                trainingCanvas.transform.rotation = Quaternion.LookRotation(-directionToUser);
            }

            Debug.Log($"[TrainingManager] Positioned training canvas at {targetPosition}, facing user");
        }

        /// <summary>
        /// Updates the current session's progress
        /// </summary>
        public void UpdateSessionProgress(string progressMessage)
        {
            if (trainingUI != null)
            {
                trainingUI.UpdateProgress(progressMessage);
            }
        }

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        public void ShowError(string errorMessage)
        {
            if (trainingUI != null)
            {
                trainingUI.ShowError(errorMessage);
            }
        }

        /// <summary>
        /// Change the training language
        /// </summary>
        /// <param name="language">Target language</param>
        public void SetTrainingLanguage(SystemLanguage language)
        {
            TrainingTextManager.SetLanguage(language);

            // Refresh cached texts
            managerTexts = TrainingTextManager.GetManagerTexts();

            // Refresh UI texts
            if (trainingUI != null)
            {
                trainingUI.Initialize(this); // Re-initialize to refresh cached texts
            }

            // Refresh current session if active
            if (isTrainingActive && CurrentSession != null && CurrentSession.IsActive)
            {
                // Reload session texts
                CurrentSession.Initialize(this);
            }

            Debug.Log($"[TrainingManager] Language changed to: {language}");
        }

        /// <summary>
        /// Get current training language
        /// </summary>
        public SystemLanguage GetCurrentLanguage()
        {
            return TrainingTextManager.GetCurrentLanguage();
        }

        /// <summary>
        /// Switch to English
        /// </summary>
        public void SetEnglish()
        {
            SetTrainingLanguage(SystemLanguage.English);
        }

        /// <summary>
        /// Switch to German
        /// </summary>
        public void SetGerman()
        {
            SetTrainingLanguage(SystemLanguage.German);
        }

        // Editor/Debug methods
#if UNITY_EDITOR
        [ContextMenu("Start Training")]
        private void EditorStartTraining()
        {
            StartTraining();
        }

        [ContextMenu("Stop Training")]
        private void EditorStopTraining()
        {
            StopTraining();
        }

        [ContextMenu("Skip Current Session")]
        private void EditorSkipSession()
        {
            SkipCurrentSession();
        }
#endif
    }
}