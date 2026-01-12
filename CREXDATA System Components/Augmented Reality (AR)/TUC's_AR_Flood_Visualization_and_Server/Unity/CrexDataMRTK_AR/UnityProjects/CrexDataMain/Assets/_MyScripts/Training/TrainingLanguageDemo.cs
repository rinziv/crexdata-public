using UnityEngine;
using MixedReality.Toolkit.UX;
using TMPro;

namespace FirefighterAR.Training
{
    /// <summary>
    /// Simple demo component showing how to use the training localization system.
    /// This demonstrates language switching functionality for the training system.
    /// </summary>
    public class TrainingLanguageDemo : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private PressableButton englishButton;
        [SerializeField] private PressableButton germanButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Demo Settings")]
        [SerializeField] private bool initializeOnStart = true;

        private void Start()
        {
            if (initializeOnStart)
            {
                InitializeDemo();
            }
        }

        /// <summary>
        /// Initialize the demo component
        /// </summary>
        public void InitializeDemo()
        {
            // Initialize the text manager
            TrainingTextManager.Initialize();

            // Setup button listeners
            SetupButtons();

            // Update initial display
            UpdateDisplay();

            Debug.Log("[TrainingLanguageDemo] Demo initialized");
        }

        /// <summary>
        /// Setup button event listeners
        /// </summary>
        private void SetupButtons()
        {
            if (englishButton != null)
            {
                englishButton.OnClicked.AddListener(SwitchToEnglish);
            }

            if (germanButton != null)
            {
                germanButton.OnClicked.AddListener(SwitchToGerman);
            }
        }

        /// <summary>
        /// Switch to English language
        /// </summary>
        public void SwitchToEnglish()
        {
            TrainingTextManager.SetEnglish();
            UpdateDisplay();
            Debug.Log("[TrainingLanguageDemo] Switched to English");
        }

        /// <summary>
        /// Switch to German language
        /// </summary>
        public void SwitchToGerman()
        {
            TrainingTextManager.SetGerman();
            UpdateDisplay();
            Debug.Log("[TrainingLanguageDemo] Switched to German");
        }

        /// <summary>
        /// Update the display with current language information
        /// </summary>
        private void UpdateDisplay()
        {
            if (statusText != null)
            {
                SystemLanguage currentLang = TrainingTextManager.GetCurrentLanguage();
                var texts = TrainingTextManager.GetCurrentTexts();

                string langName = currentLang == SystemLanguage.English ? "English" : "Deutsch";
                string sampleText = texts?.PokeTraining?.SessionName ?? "Sample Text";

                statusText.text = $"Language: {langName}\\nSample: {sampleText}";
            }
        }

        /// <summary>
        /// Demonstrate text retrieval for different training types
        /// </summary>
        [ContextMenu("Show Sample Texts")]
        public void ShowSampleTexts()
        {
            var pokeTexts = TrainingTextManager.GetPokeTexts();
            var handRayTexts = TrainingTextManager.GetHandRayTexts();
            var handMenuTexts = TrainingTextManager.GetHandMenuTexts();

            Debug.Log($"[Demo] Poke Training: {pokeTexts?.SessionName}");
            Debug.Log($"[Demo] Hand Ray Training: {handRayTexts?.SessionName}");
            Debug.Log($"[Demo] Hand Menu Training: {handMenuTexts?.SessionName}");
        }

        /// <summary>
        /// Get current language for external components
        /// </summary>
        public SystemLanguage GetCurrentLanguage()
        {
            return TrainingTextManager.GetCurrentLanguage();
        }

        /// <summary>
        /// Check if system is ready
        /// </summary>
        public bool IsSystemReady()
        {
            return TrainingTextManager.GetCurrentTexts() != null;
        }

        #region Context Menu Methods (for testing)

        [ContextMenu("Switch to English")]
        private void TestEnglish()
        {
            SwitchToEnglish();
        }

        [ContextMenu("Switch to German")]
        private void TestGerman()
        {
            SwitchToGerman();
        }

        [ContextMenu("Initialize System")]
        private void TestInitialize()
        {
            InitializeDemo();
        }

        #endregion
    }
}