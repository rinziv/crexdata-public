using System;
using System.Collections;
using UnityEngine;

namespace FirefighterAR.Training
{
    /// <summary>
    /// Base class for all training sessions.
    /// Each specific training module should inherit from this class.
    /// </summary>
    public abstract class TrainingSession : MonoBehaviour
    {
        [Header("Session Configuration")]
        [SerializeField] protected float timeoutDuration = 60f; // Session timeout in seconds
        [SerializeField] protected bool allowSkipping = true;

        [Header("Localized Content")]
        [SerializeField] protected bool useLocalization = true;

        // Localized text properties (will be populated by derived classes)
        protected string sessionName = "Training Session";
        protected string sessionDescription = "Complete this training task";
        protected string initialInstruction = "Follow the instructions to complete this session";
        protected string completionMessage = "Session completed successfully!";

        // Protected variables
        protected TrainingManager trainingManager;
        protected bool isSessionActive = false;
        protected bool isSessionCompleted = false;
        protected float sessionStartTime;
        protected Coroutine timeoutCoroutine;

        // Events
        public event Action<TrainingSession> OnSessionStarted;
        public event Action<TrainingSession> OnSessionCompleted;
        public event Action<TrainingSession> OnSessionFailed;
        public event Action<TrainingSession, string> OnProgressUpdated;

        // Properties
        public string SessionName => sessionName;
        public string SessionDescription => sessionDescription;
        public bool IsActive => isSessionActive;
        public bool IsCompleted => isSessionCompleted;
        public float ElapsedTime => isSessionActive ? Time.time - sessionStartTime : 0f;
        public float RemainingTime => Mathf.Max(0, timeoutDuration - ElapsedTime);

        /// <summary>
        /// Initialize the training session with the training manager
        /// </summary>
        /// <param name="manager">The training manager controlling this session</param>
        public virtual void Initialize(TrainingManager manager)
        {
            trainingManager = manager;

            // Load localized texts if enabled
            if (useLocalization)
            {
                LoadLocalizedTexts();
            }

            OnSessionInitialize();
        }        /// <summary>
                 /// Starts the training session
                 /// </summary>
        public virtual void StartSession()
        {
            if (isSessionActive)
            {
                Debug.LogWarning($"[{sessionName}] Session is already active");
                return;
            }

            Debug.Log($"[{sessionName}] Starting training session");

            isSessionActive = true;
            isSessionCompleted = false;
            sessionStartTime = Time.time;

            // Start timeout timer
            if (timeoutDuration > 0)
            {
                //timeoutCoroutine = StartCoroutine(SessionTimeoutCoroutine());
            }

            // Update UI with initial instruction
            UpdateProgress(initialInstruction);

            OnSessionStarted?.Invoke(this);
            OnSessionStart();
        }

        /// <summary>
        /// Stops the training session
        /// </summary>
        public virtual void StopSession()
        {
            if (!isSessionActive) return;

            Debug.Log($"[{sessionName}] Stopping training session");

            isSessionActive = false;

            // Stop timeout coroutine
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            OnSessionStop();
        }

        /// <summary>
        /// Completes the training session successfully
        /// </summary>
        protected virtual void CompleteSession()
        {
            if (!isSessionActive || isSessionCompleted) return;

            Debug.Log($"[{sessionName}] Session completed successfully");

            isSessionCompleted = true;
            isSessionActive = false;

            // Stop timeout coroutine
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            UpdateProgress(completionMessage);

            OnSessionCompleted?.Invoke(this);
            OnSessionComplete();

            // Notify training manager
            if (trainingManager != null)
            {
                trainingManager.OnCurrentSessionCompleted();
            }
        }

        /// <summary>
        /// Fails the training session
        /// </summary>
        /// <param name="reason">Reason for failure</param>
        protected virtual void FailSession(string reason = "Session failed")
        {
            if (!isSessionActive) return;

            Debug.LogWarning($"[{sessionName}] Session failed: {reason}");

            isSessionActive = false;

            // Stop timeout coroutine
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            OnSessionFailed?.Invoke(this);
            OnSessionFail(reason);

            // Show error message
            if (trainingManager != null)
            {
                trainingManager.ShowError($"{sessionName} failed: {reason}. Please try again.");
            }
        }

        /// <summary>
        /// Updates the session progress message
        /// </summary>
        /// <param name="progressMessage">The progress message to display</param>
        protected virtual void UpdateProgress(string progressMessage)
        {
            OnProgressUpdated?.Invoke(this, progressMessage);

            if (trainingManager != null)
            {
                trainingManager.UpdateSessionProgress(progressMessage);
            }
        }

        /// <summary>
        /// Session timeout coroutine
        /// </summary>
        private IEnumerator SessionTimeoutCoroutine()
        {
            yield return new WaitForSeconds(timeoutDuration);

            if (isSessionActive)
            {
                FailSession("Session timed out");
            }
        }

        #region Localization Methods

        /// <summary>
        /// Load localized texts for this session type
        /// Override in derived classes to load specific text collections
        /// </summary>
        protected virtual void LoadLocalizedTexts()
        {
            // Base implementation - derived classes should override
        }

        /// <summary>
        /// Called when language changes - reload texts
        /// </summary>
        protected virtual void OnLanguageChanged(SystemLanguage newLanguage)
        {
            if (useLocalization)
            {
                LoadLocalizedTexts();

                // Update UI if session is active
                if (isSessionActive && !string.IsNullOrEmpty(initialInstruction))
                {
                    UpdateProgress(initialInstruction);
                }
            }
        }

        #endregion

        #region Abstract Methods - Override in derived classes

        /// <summary>
        /// Called when the session is initialized
        /// </summary>
        protected virtual void OnSessionInitialize() { }

        /// <summary>
        /// Called when the session starts
        /// </summary>
        protected abstract void OnSessionStart();

        /// <summary>
        /// Called when the session stops
        /// </summary>
        protected virtual void OnSessionStop() { }

        /// <summary>
        /// Called when the session completes successfully
        /// </summary>
        protected virtual void OnSessionComplete() { }

        /// <summary>
        /// Called when the session fails
        /// </summary>
        /// <param name="reason">Reason for failure</param>
        protected virtual void OnSessionFail(string reason) { }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validates that a task has been completed successfully
        /// </summary>
        /// <param name="condition">The condition that must be true for completion</param>
        /// <param name="completionMessage">Message to show on completion</param>
        protected void ValidateTaskCompletion(bool condition, string completionMessage = null)
        {
            if (condition && isSessionActive && !isSessionCompleted)
            {
                if (!string.IsNullOrEmpty(completionMessage))
                {
                    this.completionMessage = completionMessage;
                }
                CompleteSession();
            }
        }

        /// <summary>
        /// Checks if the session should timeout soon and warns the user
        /// </summary>
        protected void CheckTimeoutWarning()
        {
            if (RemainingTime <= 10f && RemainingTime > 9f)
            {
                UpdateProgress($"Hurry up! {Mathf.Ceil(RemainingTime)} seconds remaining!");
            }
        }

        #endregion

        // Unity lifecycle methods
        protected virtual void Start()
        {
            // Localization is handled through TrainingTextManager
        }

        protected virtual void OnDestroy()
        {
            // Cleanup handled by derived classes if needed
        }

        // Update method for derived classes to use
        protected virtual void Update()
        {
            if (isSessionActive)
            {
                //CheckTimeoutWarning();
                OnSessionUpdate();
            }
        }

        /// <summary>
        /// Override this for custom update logic in derived sessions
        /// </summary>
        protected virtual void OnSessionUpdate() { }
    }
}