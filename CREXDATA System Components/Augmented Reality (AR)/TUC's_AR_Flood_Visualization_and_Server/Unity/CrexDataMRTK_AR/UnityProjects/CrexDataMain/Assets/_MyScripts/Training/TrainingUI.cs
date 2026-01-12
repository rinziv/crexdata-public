using UnityEngine;
using TMPro;
using MixedReality.Toolkit.UX;
using System.Collections;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training
{
    /// <summary>
    /// UI controller for the training system.
    /// Handles displaying instructions, progress, and feedback to the user.
    /// </summary>
    public class TrainingUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI sessionCounterText;

        [Header("UI Panels")]
        [SerializeField] private GameObject instructionPanel;
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private GameObject successPanel;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("Buttons")]
        [SerializeField] private PressableButton skipButton;
        [SerializeField] private PressableButton restartButton;
        [SerializeField] private PressableButton continueButton;
        [SerializeField] private PressableButton closeButton;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private Color successColor = Color.green;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color normalColor = Color.white;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float messageDisplayDuration = 3f;

        private TrainingManager trainingManager;
        private CanvasGroup canvasGroup;
        private Coroutine currentMessageCoroutine;
        private TrainingManagerTexts managerTexts;

        /// <summary>
        /// Initialize the training UI with the training manager
        /// </summary>
        public void Initialize(TrainingManager manager)
        {
            trainingManager = manager;

            // Cache manager texts
            managerTexts = TrainingTextManager.GetManagerTexts();

            // Get canvas group for fade animations
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            SetupButtons();
            HideAllPanels();

            Debug.Log("[TrainingUI] Initialized");
        }

        private void SetupButtons()
        {
            // Setup skip button
            if (skipButton != null)
            {
                skipButton.OnClicked.AddListener(() =>
                {
                    if (trainingManager != null)
                    {
                        trainingManager.SkipCurrentSession();
                    }
                });
            }

            // Setup restart button
            if (restartButton != null)
            {
                restartButton.OnClicked.AddListener(() =>
                {
                    if (trainingManager != null)
                    {
                        trainingManager.RestartTraining();
                    }
                });
            }

            // Setup continue button
            if (continueButton != null)
            {
                continueButton.OnClicked.AddListener(() =>
                {
                    HideAllPanels();
                    ShowInstructionPanel();
                });
            }

            // Setup close button
            if (closeButton != null)
            {
                closeButton.OnClicked.AddListener(() =>
                {
                    if (trainingManager != null)
                    {
                        trainingManager.StopTraining();
                    }
                });
            }
        }

        /// <summary>
        /// Updates the session information display
        /// </summary>
        public void UpdateSessionInfo(int currentSession, int totalSessions, string sessionName)
        {
            if (titleText != null)
            {
                titleText.text = sessionName;
            }

            if (sessionCounterText != null)
            {
                sessionCounterText.text = $"Session {currentSession} of {totalSessions}";
            }

            ShowInstructionPanel();
        }

        /// <summary>
        /// Updates the progress message
        /// </summary>
        public void UpdateProgress(string message)
        {
            if (instructionText != null)
            {
                instructionText.text = message;
                instructionText.color = normalColor;
            }

            ShowInstructionPanel();
        }

        /// <summary>
        /// Shows a success message
        /// </summary>
        public void ShowSuccess(string message)
        {
            if (currentMessageCoroutine != null)
            {
                StopCoroutine(currentMessageCoroutine);
            }

            currentMessageCoroutine = StartCoroutine(ShowTemporaryMessage(message, successColor, successPanel));
        }

        /// <summary>
        /// Shows an error message
        /// </summary>
        public void ShowError(string message)
        {
            if (currentMessageCoroutine != null)
            {
                StopCoroutine(currentMessageCoroutine);
            }

            currentMessageCoroutine = StartCoroutine(ShowTemporaryMessage(message, errorColor, errorPanel));
        }

        /// <summary>
        /// Shows the training completion screen
        /// </summary>
        public void ShowTrainingComplete()
        {
            HideAllPanels();

            if (completionPanel != null)
            {
                completionPanel.SetActive(true);
            }

            // Use cached localized texts
            if (titleText != null)
            {
                titleText.text = managerTexts?.TrainingComplete ?? "Training Complete!";
            }

            if (instructionText != null)
            {
                instructionText.text = managerTexts?.TrainingCompleteMessage ?? "Congratulations! You've completed all training sessions. You can now access the hand menu by lifting your left palm towards your face.";
                instructionText.color = successColor;
            }
        }

        private IEnumerator ShowTemporaryMessage(string message, Color textColor, GameObject panel)
        {
            HideAllPanels();

            if (panel != null)
            {
                panel.SetActive(true);
            }

            if (instructionText != null)
            {
                instructionText.text = message;
                instructionText.color = textColor;
            }

            // Fade in
            yield return StartCoroutine(FadeIn());

            // Wait
            yield return new WaitForSeconds(messageDisplayDuration);

            // Fade out and return to instruction panel
            yield return StartCoroutine(FadeOut());
            ShowInstructionPanel();
            yield return StartCoroutine(FadeIn());

            currentMessageCoroutine = null;
        }

        private void HideAllPanels()
        {
            if (instructionPanel != null) instructionPanel.SetActive(false);
            if (progressPanel != null) progressPanel.SetActive(false);
            if (successPanel != null) successPanel.SetActive(false);
            if (errorPanel != null) errorPanel.SetActive(false);
            if (completionPanel != null) completionPanel.SetActive(false);
        }

        private void ShowInstructionPanel()
        {
            HideAllPanels();

            if (instructionPanel != null)
            {
                instructionPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Shows or hides the loading indicator
        /// </summary>
        public void ShowLoadingIndicator(bool show)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(show);
            }
        }

        /// <summary>
        /// Shows or hides the skip button
        /// </summary>
        public void ShowSkipButton(bool show)
        {
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(show);
            }
        }

        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeInDuration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Updates progress with percentage
        /// </summary>
        public void UpdateProgressWithPercentage(string baseMessage, float percentage)
        {
            string message = $"{baseMessage}\nProgress: {Mathf.RoundToInt(percentage * 100)}%";
            UpdateProgress(message);
        }

        /// <summary>
        /// Shows a countdown timer
        /// </summary>
        public void ShowCountdown(int seconds)
        {
            if (progressText != null)
            {
                progressText.text = $"Starting in {seconds}...";
            }
        }

        /// <summary>
        /// Clears the countdown timer
        /// </summary>
        public void ClearCountdown()
        {
            if (progressText != null)
            {
                progressText.text = "";
            }
        }
    }
}