using UnityEngine;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training
{
    /// <summary>
    /// Text manager that provides easy access to localized training texts.
    /// Acts as a bridge between the training system and localization.
    /// </summary>
    public static class TrainingTextManager
    {
        private static TrainingLocalizations localizations;
        private static SystemLanguage currentLanguage = SystemLanguage.English;

        /// <summary>
        /// Initialize the text manager
        /// </summary>
        public static void Initialize()
        {
            if (localizations == null)
            {
                localizations = new TrainingLocalizations();
                localizations.Initialize();
                Debug.Log("[TrainingTextManager] Initialized with localization support");
            }
        }

        /// <summary>
        /// Set the current language
        /// </summary>
        public static void SetLanguage(SystemLanguage language)
        {
            if (localizations == null) Initialize();

            currentLanguage = language;
            localizations.CurrentLanguage = language;
            Debug.Log($"[TrainingTextManager] Language set to: {language}");
        }

        /// <summary>
        /// Get current language
        /// </summary>
        public static SystemLanguage GetCurrentLanguage()
        {
            return currentLanguage;
        }

        /// <summary>
        /// Get current texts
        /// </summary>
        public static TrainingTexts GetCurrentTexts()
        {
            if (localizations == null) Initialize();
            return localizations.GetCurrentTexts();
        }

        #region Convenience Methods

        /// <summary>
        /// Get poke training texts
        /// </summary>
        public static PokeTrainingTexts GetPokeTexts()
        {
            return GetCurrentTexts()?.PokeTraining;
        }

        /// <summary>
        /// Get hand ray training texts
        /// </summary>
        public static HandRayTrainingTexts GetHandRayTexts()
        {
            return GetCurrentTexts()?.HandRayTraining;
        }

        /// <summary>
        /// Get hand menu training texts
        /// </summary>
        public static HandMenuTrainingTexts GetHandMenuTexts()
        {
            return GetCurrentTexts()?.HandMenuTraining;
        }

        /// <summary>
        /// Get POI training texts
        /// </summary>
        public static POITrainingTexts GetPOITexts()
        {
            return GetCurrentTexts()?.POITraining;
        }

        /// <summary>
        /// Get training manager texts
        /// </summary>
        public static TrainingManagerTexts GetManagerTexts()
        {
            return GetCurrentTexts()?.ManagerTexts;
        }

        /// <summary>
        /// Get common UI texts
        /// </summary>
        public static CommonUITexts GetCommonUITexts()
        {
            return GetCurrentTexts()?.CommonUI;
        }

        #endregion

        #region Language Selection Methods

        /// <summary>
        /// Switch to English
        /// </summary>
        public static void SetEnglish()
        {
            SetLanguage(SystemLanguage.English);
        }

        /// <summary>
        /// Switch to German
        /// </summary>
        public static void SetGerman()
        {
            SetLanguage(SystemLanguage.German);
        }

        /// <summary>
        /// Check if language is supported
        /// </summary>
        public static bool IsLanguageSupported(SystemLanguage language)
        {
            if (localizations == null) Initialize();
            return localizations.IsLanguageSupported(language);
        }

        /// <summary>
        /// Get supported languages
        /// </summary>
        public static SystemLanguage[] GetSupportedLanguages()
        {
            if (localizations == null) Initialize();
            return localizations.GetSupportedLanguages();
        }

        #endregion
    }
}