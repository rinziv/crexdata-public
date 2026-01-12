using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirefighterAR.Training.Localization
{
    /// <summary>
    /// Centralized localization system for all training text content.
    /// Supports multiple languages with easy extensibility.
    /// </summary>
    [Serializable]
    public class TrainingLocalizations
    {
        [Header("Language Configuration")]
        [SerializeField] private SystemLanguage currentLanguage = SystemLanguage.English;

        // Language-specific text collections
        private Dictionary<SystemLanguage, TrainingTexts> localizedTexts;

        public SystemLanguage CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                currentLanguage = value;
                OnLanguageChanged?.Invoke(value);
            }
        }

        // Events
        public event Action<SystemLanguage> OnLanguageChanged;

        /// <summary>
        /// Initialize the localization system
        /// </summary>
        public void Initialize()
        {
            localizedTexts = new Dictionary<SystemLanguage, TrainingTexts>
            {
                { SystemLanguage.English, GetEnglishTexts() },
                { SystemLanguage.German, GetGermanTexts() }
            };

            Debug.Log($"[TrainingLocalizations] Initialized with {localizedTexts.Count} languages");
        }

        /// <summary>
        /// Get the current language's text collection
        /// </summary>
        public TrainingTexts GetCurrentTexts()
        {
            if (localizedTexts == null)
            {
                Initialize();
            }

            if (localizedTexts.ContainsKey(currentLanguage))
            {
                return localizedTexts[currentLanguage];
            }

            // Fallback to English if current language not available
            Debug.LogWarning($"[TrainingLocalizations] Language {currentLanguage} not available, falling back to English");
            return localizedTexts[SystemLanguage.English];
        }

        /// <summary>
        /// Get text for a specific language
        /// </summary>
        public TrainingTexts GetTextsForLanguage(SystemLanguage language)
        {
            if (localizedTexts == null)
            {
                Initialize();
            }

            return localizedTexts.ContainsKey(language) ? localizedTexts[language] : localizedTexts[SystemLanguage.English];
        }

        /// <summary>
        /// Check if a language is supported
        /// </summary>
        public bool IsLanguageSupported(SystemLanguage language)
        {
            if (localizedTexts == null)
            {
                Initialize();
            }
            return localizedTexts.ContainsKey(language);
        }

        /// <summary>
        /// Get all supported languages
        /// </summary>
        public SystemLanguage[] GetSupportedLanguages()
        {
            if (localizedTexts == null)
            {
                Initialize();
            }

            var languages = new SystemLanguage[localizedTexts.Count];
            localizedTexts.Keys.CopyTo(languages, 0);
            return languages;
        }

        #region Language-Specific Text Collections

        /// <summary>
        /// English text collection
        /// </summary>
        private TrainingTexts GetEnglishTexts()
        {
            return new TrainingTexts
            {
                // =====[ TRAINING MANAGER TEXTS ]=====
                ManagerTexts = new TrainingManagerTexts
                {
                    SessionCompleted = "Great job! You've completed the {0} training.",
                    TrainingComplete = "Training Complete!",
                    TrainingCompleteMessage = "Congratulations! You've completed all training sessions. You can now access the hand menu by lifting your left palm towards your face."
                },

                // =====[ POKE TRAINING SESSION TEXTS ]=====
                PokeTraining = new PokeTrainingTexts
                {
                    SessionName = "Poke Interaction Training",
                    SessionDescription = "Master direct touch interactions with UI elements",
                    InitialInstruction = "Welcome to Poke Interaction Training. Poke is the primary method for interacting with buttons, switches, and UI elements in AR. Use your index finger to directly touch and press the highlighted button 3 times.",
                    CompletionMessage = "Excellent! You have successfully mastered poke interactions. This technique will be essential for operating buttons and controls throughout the Firefighter AR application.",

                    StartInstruction = "POKE INTERACTION: Position yourself close to the button and use your index finger to directly touch and press it 3 times. This is how you will interact with all buttons and controls in the application.",
                    ButtonRepositioned = "Button repositioned. Please poke the highlighted button to continue.",
                    ButtonRepositionedSmooth = "Button smoothly repositioned. Please poke the highlighted button to continue.",
                    ProgressMessage = "Great! Press the button {0} more times to complete the poke interaction training. ({1}/3)",
                    CompletionProgress = "Perfect! You have successfully executed 3 poke interactions. Poke is your primary method for activating buttons, toggles, and interactive elements in close proximity.",
                    TechniqueGuidance = "TECHNIQUE: Extend your index finger and make direct contact with the button surface. You should feel haptic feedback confirming the interaction. This direct touch method ensures precise control.",
                    ReminderMessage = "REMINDER: Move closer to the button and use your index finger to make direct physical contact. Poke interaction requires proximity and direct touch for accurate control.",
                    MultiplePokeInstruction = "Press the button {0} times using poke interaction."
                },

                // =====[ HAND RAY TRAINING SESSION TEXTS ]=====
                HandRayTraining = new HandRayTrainingTexts
                {
                    SessionName = "Hand Ray Interaction Training",
                    SessionDescription = "Master distant object selection using hand ray projection",
                    InitialInstruction = "Welcome to Hand Ray Training. Hand rays allow you to interact with distant objects without moving closer. Point your hand toward the target and use the pinch gesture (thumb to index finger) to select the object 3 times from a distance.",
                    CompletionMessage = "Outstanding! You have mastered hand ray interactions. This technique enables you to operate distant controls, select far objects, and interact with elements beyond your immediate reach in the Firefighter AR environment.",

                    StartInstruction = "HAND RAY INTERACTION: Extend your hand toward the distant target sphere. A ray will project from your hand. Use the pinch gesture (thumb to index finger) to select the object 3 times from a distance.",
                    ProgressMessage = "Excellent! Select the target {0} more times to complete the hand ray interaction training. ({1}/3)",
                    CompletionProgress = "Outstanding! You have successfully used hand ray interaction 3 times to select a distant object. This method is essential for operating equipment, accessing controls, and selecting objects that are out of physical reach.",
                    TechniqueGuidance = "TECHNIQUE: Raise your hand and point at the target cube. Bring your thumb and index finger together in a pinch motion to activate selection. The hand ray extends your interaction range significantly.",
                    ImportantNote = "IMPORTANT: Hand ray interaction is crucial - it allows you to operate distant equipment without leaving your position. Master this technique for effective field operations.",
                    TargetingActive = "TARGETING ACTIVE: Your hand ray is aligned with the target. Execute the pinch gesture (thumb to index finger) to complete the distant selection.",
                    PositioningInstruction = "POSITIONING: Extend your hand and point at the target cube. The hand ray will appear as a line projection from your hand, enabling distant interaction capabilities.",
                    TargetingConfirmed = "TARGETING CONFIRMED: Your hand ray is now aimed at the target. Execute the pinch gesture to complete the selection.",
                    AimRequired = "AIM REQUIRED: Point your hand directly at the highlighted cube to establish targeting with your hand ray.",
                    AimGuidance = "AIM REQUIRED: Point your hand directly at the highlighted cube to establish targeting with your hand ray.",
                    RayDetectedMessage = "Hand ray detected! Now use the pinch gesture to select the target.",
                    NoRayMessage = "Point your hand toward the target to establish your ray connection."
                },

                // =====[ HAND MENU TRAINING SESSION TEXTS ]=====
                HandMenuTraining = new HandMenuTrainingTexts
                {
                    SessionName = "Hand Menu Navigation Training",
                    SessionDescription = "Master the primary interface for accessing application features",
                    InitialInstruction = "Welcome to Hand Menu Training. The hand menu is your primary control interface in the AR application. Access it by lifting your LEFT hand and turning your palm toward your face.",
                    CompletionMessage = "Excellent! You have mastered hand menu access and navigation. The hand menu is your central hub for accessing all tools and features. You can interact with menu items using both poke and hand ray methods.",

                    StartInstruction = "HAND MENU ACCESS: The hand menu contains all primary functions and tools. Lift your LEFT hand to chest level and turn your palm to face you. Hold this position to activate the main interface.",
                    ProgressMessage = "Great! Press {0} more buttons to complete the training. ({1}/{2})",
                    CompletionProgress = "Perfect! You've successfully pressed {0} buttons from the hand menu.",
                    MenuActivated = "MENU ACTIVATED: The hand menu is now visible and contains your toolkit. Proceed to interact with menu buttons using either poke (direct touch) or hand ray (distant selection) methods.",
                    InteractionInstruction = "MENU INTERACTION: The hand menu provides access to different functions. Practice using both interaction methods by selecting {0} different buttons. Use poke for close-range precision or hand ray for distant access.",
                    PalmUpInstruction = "PALM POSITIONING: Turn your LEFT palm upward and toward your face. The hand menu will automatically appear at your wrist when properly positioned.",
                    ButtonSelectionGuidance = "BUTTON SELECTION: Select menu buttons using direct touch (poke interaction) or point and pinch gesture (hand ray interaction). Both methods provide precise control over interface elements.",
                    PositioningTechnique = "POSITIONING TECHNIQUE: Lift your LEFT hand to chest level and rotate your palm toward your face, similar to reading your palm. Maintain this position steadily to activate the firefighter control interface.",
                    MaintainPosition = "MAINTAIN POSITION: Keep your palm facing toward you. The hand menu system is detecting your gesture and will activate the interface momentarily.",
                    ReminderMessage = "REMINDER: The hand menu is your mission-critical interface. Lift your LEFT hand and position your palm facing you to access all firefighting systems and tools.",
                    MenuDetected = "HAND MENU DETECTED: Interface activated successfully. Menu buttons are now accessible for interaction.",
                    MenuLost = "HAND MENU LOST: Menu is no longer visible. Reposition your LEFT hand with palm facing toward you to reactivate the interface.",
                    ButtonPressed = "BUTTON ACTIVATED: Menu selection successful. Continue interacting with available menu options.",
                    TimeoutWarning = "INACTIVITY WARNING: Menu will disappear soon due to inactivity. Interact with menu elements to maintain interface visibility.",
                    TimeoutMessage = "MENU TIMEOUT: Interface has been deactivated due to inactivity. Raise your LEFT hand to reactivate the hand menu system."
                },

                // =====[ POI TRAINING SESSION TEXTS ]=====
                POITraining = new POITrainingTexts
                {
                    SessionName = "Point of Interest Training",
                    SessionDescription = "Learn to scan and gather information from environmental markers",
                    InitialInstruction = "Welcome to POI (Point of Interest) Training. POI markers contain critical information about fire conditions, hazards, victim locations, and available resources. Look at POI markers to access their information.",
                    CompletionMessage = "Excellent! You have mastered POI interactions. These information points will be essential for gathering intelligence about fire conditions, structural hazards, victim locations, and available resources during firefighting operations.",

                    ProgressMessage = "POI DATA ACCESSED: Information retrieved successfully. Continue scanning for {0} more POI markers to complete your environmental assessment training.",
                    ContinueRecon = "CONTINUE RECONNAISSANCE: Maintain situational awareness and locate {0} more POI markers. These information points are essential for effective firefighting operations and safety assessment."
                },

                // =====[ COMMON UI TEXTS ]=====
                CommonUI = new CommonUITexts
                {
                    SessionCounter = "Session {0} of {1}",
                    TimeRemaining = "Hurry up! {0} seconds remaining!",
                    SessionFailed = "{0} failed: {1}. Please try again.",
                    SessionTimedOut = "Session timed out"
                }
            };
        }

        /// <summary>
        /// German text collection
        /// </summary>
        private TrainingTexts GetGermanTexts()
        {
            return new TrainingTexts
            {
                // =====[ TRAINING MANAGER TEXTS ]=====
                ManagerTexts = new TrainingManagerTexts
                {
                    SessionCompleted = "Großartige Arbeit! Sie haben das {0}-Training erfolgreich abgeschlossen.",
                    TrainingComplete = "Training Abgeschlossen!",
                    TrainingCompleteMessage = "Herzlichen Glückwunsch! Sie haben alle Trainingseinheiten erfolgreich abgeschlossen. Sie können nun das Handmenü aufrufen, indem Sie Ihre linke Handfläche zu Ihrem Gesicht heben."
                },

                // =====[ POKE TRAINING SESSION TEXTS ]=====
                PokeTraining = new PokeTrainingTexts
                {
                    SessionName = "Poke-Interaktions-Training",
                    SessionDescription = "Meistern Sie direkte Touch-Interaktionen mit UI-Elementen",
                    InitialInstruction = "Willkommen zum Poke-Interaktions-Training. Poke ist die primäre Methode zur Interaktion mit Schaltflächen, Schaltern und UI-Elementen in AR. Verwenden Sie Ihren Zeigefinger, um die hervorgehobene Schaltfläche 3 Mal direkt zu berühren und zu drücken.",
                    CompletionMessage = "Ausgezeichnet! Sie haben Poke-Interaktionen erfolgreich gemeistert. Diese Technik ist für die Bedienung von Schaltflächen und Steuerelementen in der gesamten Feuerwehr-AR-Anwendung unerlässlich.",

                    StartInstruction = "POKE-INTERAKTION: Positionieren Sie sich nahe zur Schaltfläche und verwenden Sie Ihren Zeigefinger, um sie 3 Mal direkt zu berühren und zu drücken. So interagieren Sie mit allen Schaltflächen und Steuerelementen in der Anwendung.",
                    ButtonRepositioned = "Schaltfläche neu positioniert. Bitte berühren Sie die hervorgehobene Schaltfläche, um fortzufahren.",
                    ButtonRepositionedSmooth = "Schaltfläche sanft neu positioniert. Bitte berühren Sie die hervorgehobene Schaltfläche, um fortzufahren.",
                    ProgressMessage = "Großartig! Drücken Sie die Schaltfläche noch {0} Mal, um das Poke-Interaktions-Training abzuschließen. ({1}/3)",
                    CompletionProgress = "Perfekt! Sie haben erfolgreich 3 Poke-Interaktionen ausgeführt. Poke ist Ihre primäre Methode zur Aktivierung von Schaltflächen, Schaltern und interaktiven Elementen in unmittelbarer Nähe.",
                    TechniqueGuidance = "TECHNIK: Strecken Sie Ihren Zeigefinger aus und stellen Sie direkten Kontakt mit der Schaltflächenoberfläche her. Sie sollten haptisches Feedback spüren, das die Interaktion bestätigt. Diese direkte Berührungsmethode gewährleistet präzise Kontrolle.",
                    ReminderMessage = "ERINNERUNG: Kommen Sie näher zur Schaltfläche und verwenden Sie Ihren Zeigefinger für direkten physischen Kontakt. Poke-Interaktion erfordert Nähe und direkte Berührung für genaue Kontrolle.",
                    MultiplePokeInstruction = "Drücken Sie die Schaltfläche {0} Mal mit Poke-Interaktion."
                },

                // =====[ HAND RAY TRAINING SESSION TEXTS ]=====
                HandRayTraining = new HandRayTrainingTexts
                {
                    SessionName = "Handstrahl-Interaktions-Training",
                    SessionDescription = "Meistern Sie die Auswahl entfernter Objekte mittels Handstrahlprojektion",
                    InitialInstruction = "Willkommen zum Handstrahl-Training. Handstrahlen ermöglichen es Ihnen, mit entfernten Objekten zu interagieren, ohne näher heranzugehen. Richten Sie Ihre Hand auf das Ziel und verwenden Sie die Kneifgeste (Daumen zu Zeigefinger), um das Objekt 3 Mal aus der Entfernung auszuwählen.",
                    CompletionMessage = "Hervorragend! Sie haben Handstrahl-Interaktionen gemeistert. Diese Technik ermöglicht es Ihnen, entfernte Steuerungen zu bedienen, weit entfernte Objekte auszuwählen und mit Elementen außerhalb Ihrer unmittelbaren Reichweite in der Feuerwehr-AR-Umgebung zu interagieren.",

                    StartInstruction = "HANDSTRAHL-INTERAKTION: Strecken Sie Ihre Hand zur entfernten Zielsphäre aus. Ein Strahl wird von Ihrer Hand projiziert. Verwenden Sie die Kneifgeste (Daumen zu Zeigefinger), um das Objekt 3 Mal aus der Entfernung auszuwählen.",
                    ProgressMessage = "Ausgezeichnet! Wählen Sie das Ziel noch {0} Mal aus, um das Handstrahl-Interaktions-Training abzuschließen. ({1}/3)",
                    CompletionProgress = "Hervorragend! Sie haben erfolgreich 3 Mal Handstrahl-Interaktion verwendet, um ein entferntes Objekt auszuwählen. Diese Methode ist unerlässlich für die Bedienung von Ausrüstung, den Zugriff auf Steuerungen und die Auswahl von Objekten außerhalb der physischen Reichweite.",
                    TechniqueGuidance = "TECHNIK: Heben Sie Ihre Hand und zeigen Sie auf den Zielwürfel. Bringen Sie Daumen und Zeigefinger in einer Kneifbewegung zusammen, um die Auswahl zu aktivieren. Der Handstrahl erweitert Ihre Interaktionsreichweite erheblich.",
                    ImportantNote = "WICHTIG: Handstrahl-Interaktion ist entscheidend - sie ermöglicht es Ihnen, entfernte Ausrüstung zu bedienen, ohne Ihre Position zu verlassen. Meistern Sie diese Technik für effektive Feldeinsätze.",
                    TargetingActive = "ZIELERFASSUNG AKTIV: Ihr Handstrahl ist auf das Ziel ausgerichtet. Führen Sie die Kneifgeste (Daumen zu Zeigefinger) aus, um die entfernte Auswahl abzuschließen.",
                    PositioningInstruction = "POSITIONIERUNG: Strecken Sie Ihre Hand aus und zeigen Sie auf den Zielwürfel. Der Handstrahl erscheint als Linienprojektion von Ihrer Hand und ermöglicht entfernte Interaktionsfähigkeiten.",
                    TargetingConfirmed = "ZIELERFASSUNG BESTÄTIGT: Ihr Handstrahl ist nun auf das Ziel gerichtet. Führen Sie die Kneifgeste aus, um die Auswahl abzuschließen.",
                    AimRequired = "ZIELEN ERFORDERLICH: Richten Sie Ihre Hand direkt auf den hervorgehobenen Würfel, um die Zielerfassung mit Ihrem Handstrahl zu etablieren.",
                    AimGuidance = "ZIELEN ERFORDERLICH: Richten Sie Ihre Hand direkt auf den hervorgehobenen Würfel, um die Zielerfassung mit Ihrem Handstrahl zu etablieren.",
                    RayDetectedMessage = "Handstrahl erkannt! Verwenden Sie nun die Kneifgeste, um das Ziel auszuwählen.",
                    NoRayMessage = "Richten Sie Ihre Hand auf das Ziel, um Ihre Strahlverbindung herzustellen."
                },

                // =====[ HAND MENU TRAINING SESSION TEXTS ]=====
                HandMenuTraining = new HandMenuTrainingTexts
                {
                    SessionName = "Handmenü-Navigations-Training",
                    SessionDescription = "Meistern Sie die primäre Schnittstelle für den Zugriff auf Anwendungsfunktionen",
                    InitialInstruction = "Willkommen zum Handmenü-Training. Das Handmenü ist Ihre primäre Steuerschnittstelle in der AR-Anwendung. Greifen Sie darauf zu, indem Sie Ihre LINKE Hand heben und Ihre Handfläche zu Ihrem Gesicht wenden.",
                    CompletionMessage = "Ausgezeichnet! Sie haben Handmenü-Zugriff und -Navigation gemeistert. Das Handmenü ist Ihr zentraler Hub für den Zugriff auf alle Tools und Funktionen. Sie können mit Menüelementen sowohl über Poke- als auch über Handstrahl-Methoden interagieren.",

                    StartInstruction = "HANDMENÜ-ZUGRIFF: Das Handmenü enthält alle primären Funktionen und Tools. Heben Sie Ihre LINKE Hand auf Brusthöhe und wenden Sie Ihre Handfläche zu sich. Halten Sie diese Position, um die Hauptschnittstelle zu aktivieren.",
                    ProgressMessage = "Großartig! Drücken Sie noch {0} Schaltflächen, um das Training abzuschließen. ({1}/{2})",
                    CompletionProgress = "Perfekt! Sie haben erfolgreich {0} Schaltflächen aus dem Handmenü gedrückt.",
                    MenuActivated = "MENÜ AKTIVIERT: Das Handmenü ist nun sichtbar und enthält Ihr Toolkit. Fahren Sie fort, mit Menüschaltflächen zu interagieren, entweder mit Poke (direkte Berührung) oder Handstrahl (entfernte Auswahl) Methoden.",
                    InteractionInstruction = "MENÜ-INTERAKTION: Das Handmenü bietet Zugriff auf verschiedene Funktionen. Üben Sie beide Interaktionsmethoden, indem Sie {0} verschiedene Schaltflächen auswählen. Verwenden Sie Poke für Nahbereichspräzision oder Handstrahl für entfernten Zugriff.",
                    PalmUpInstruction = "HANDHALTUNG: Drehen Sie Ihre LINKE Handfläche nach oben und zu Ihrem Gesicht. Das Handmenü erscheint automatisch an Ihrem Handgelenk bei korrekter Positionierung.",
                    ButtonSelectionGuidance = "SCHALTFLÄCHEN-AUSWAHL: Wählen Sie Menüschaltflächen durch direktes Berühren (Poke-Interaktion) oder Zeigen und Kneifen (Handstrahl-Interaktion). Beide Methoden bieten präzise Kontrolle über Schnittstellenelemente.",
                    PositioningTechnique = "POSITIONIERUNGSTECHNIK: Heben Sie Ihre LINKE Hand auf Brusthöhe und drehen Sie Ihre Handfläche zu Ihrem Gesicht, ähnlich wie beim Lesen Ihrer Handfläche. Halten Sie diese Position stetig, um die Feuerwehr-Steuerschnittstelle zu aktivieren.",
                    MaintainPosition = "POSITION HALTEN: Halten Sie Ihre Handfläche zu sich gewandt. Das Handmenüsystem erkennt Ihre Geste und aktiviert die Schnittstelle in Kürze.",
                    ReminderMessage = "ERINNERUNG: Das Handmenü ist Ihre missionskritische Schnittstelle. Heben Sie Ihre LINKE Hand und positionieren Sie Ihre Handfläche zu sich, um auf alle Feuerwehrsysteme und Tools zuzugreifen.",
                    MenuDetected = "HANDMENÜ ERKANNT: Schnittstelle erfolgreich aktiviert. Menüschaltflächen sind nun für die Interaktion zugänglich.",
                    MenuLost = "HANDMENÜ VERLOREN: Menü ist nicht mehr sichtbar. Positionieren Sie Ihre LINKE Hand mit der Handfläche zu sich, um die Schnittstelle zu reaktivieren.",
                    ButtonPressed = "SCHALTFLÄCHE AKTIVIERT: Menüauswahl erfolgreich. Setzen Sie die Interaktion mit verfügbaren Menüoptionen fort.",
                    TimeoutWarning = "INAKTIVITÄTSWARNUNG: Menü wird bald aufgrund von Inaktivität verschwinden. Interagieren Sie mit Menüelementen, um die Sichtbarkeit der Schnittstelle aufrechtzuerhalten.",
                    TimeoutMessage = "MENÜ-TIMEOUT: Schnittstelle wurde aufgrund von Inaktivität deaktiviert. Heben Sie Ihre LINKE Hand, um das Handmenüsystem zu reaktivieren."
                },

                // =====[ POI TRAINING SESSION TEXTS ]=====
                POITraining = new POITrainingTexts
                {
                    SessionName = "Point-of-Interest-Training",
                    SessionDescription = "Lernen Sie, Umgebungsmarkierungen zu scannen und Informationen zu sammeln",
                    InitialInstruction = "Willkommen zum POI (Point of Interest) Training. POI-Markierungen enthalten wichtige Informationen über Brandbedingungen, Gefahren, Opferstandorte und verfügbare Ressourcen. Schauen Sie auf POI-Markierungen, um auf deren Informationen zuzugreifen.",
                    CompletionMessage = "Ausgezeichnet! Sie haben POI-Interaktionen gemeistert. Diese Informationspunkte sind für das Sammeln von Informationen über Brandbedingungen, strukturelle Gefahren, Opferstandorte und verfügbare Ressourcen während Feuerwehreinsätzen unerlässlich.",

                    ProgressMessage = "POI-DATEN ABGERUFEN: Informationen erfolgreich abgerufen. Scannen Sie weiter nach {0} weiteren POI-Markierungen, um Ihr Umgebungsbewertungstraining abzuschließen.",
                    ContinueRecon = "AUFKLÄRUNG FORTSETZEN: Behalten Sie das Situationsbewusstsein bei und lokalisieren Sie {0} weitere POI-Markierungen. Diese Informationspunkte sind für effektive Feuerwehreinsätze und Sicherheitsbewertung unerlässlich."
                },

                // =====[ COMMON UI TEXTS ]=====
                CommonUI = new CommonUITexts
                {
                    SessionCounter = "Sitzung {0} von {1}",
                    TimeRemaining = "Beeilen Sie sich! {0} Sekunden verbleibend!",
                    SessionFailed = "{0} fehlgeschlagen: {1}. Bitte versuchen Sie es erneut.",
                    SessionTimedOut = "Sitzung ist abgelaufen"
                }
            };
        }

        #endregion
    }

    #region Text Structure Classes

    /// <summary>
    /// Complete text collection for a specific language
    /// </summary>
    [Serializable]
    public class TrainingTexts
    {
        public TrainingManagerTexts ManagerTexts;
        public PokeTrainingTexts PokeTraining;
        public HandRayTrainingTexts HandRayTraining;
        public HandMenuTrainingTexts HandMenuTraining;
        public POITrainingTexts POITraining;
        public CommonUITexts CommonUI;
    }

    [Serializable]
    public class TrainingManagerTexts
    {
        public string SessionCompleted;
        public string TrainingComplete;
        public string TrainingCompleteMessage;
    }

    [Serializable]
    public class PokeTrainingTexts
    {
        public string SessionName;
        public string SessionDescription;
        public string InitialInstruction;
        public string CompletionMessage;
        public string StartInstruction;
        public string ButtonRepositioned;
        public string ButtonRepositionedSmooth;
        public string ProgressMessage;
        public string CompletionProgress;
        public string TechniqueGuidance;
        public string ReminderMessage;
        public string MultiplePokeInstruction;
    }

    [Serializable]
    public class HandRayTrainingTexts
    {
        public string SessionName;
        public string SessionDescription;
        public string InitialInstruction;
        public string CompletionMessage;
        public string StartInstruction;
        public string ProgressMessage;
        public string CompletionProgress;
        public string TechniqueGuidance;
        public string ImportantNote;
        public string TargetingActive;
        public string PositioningInstruction;
        public string TargetingConfirmed;
        public string AimRequired;
        public string AimGuidance;
        public string RayDetectedMessage;
        public string NoRayMessage;
    }

    [Serializable]
    public class HandMenuTrainingTexts
    {
        public string SessionName;
        public string SessionDescription;
        public string InitialInstruction;
        public string CompletionMessage;
        public string StartInstruction;
        public string ProgressMessage;
        public string CompletionProgress;
        public string MenuActivated;
        public string InteractionInstruction;
        public string PalmUpInstruction;
        public string ButtonSelectionGuidance;
        public string PositioningTechnique;
        public string MaintainPosition;
        public string ReminderMessage;
        public string MenuDetected;
        public string MenuLost;
        public string ButtonPressed;
        public string TimeoutWarning;
        public string TimeoutMessage;
    }

    [Serializable]
    public class POITrainingTexts
    {
        public string SessionName;
        public string SessionDescription;
        public string InitialInstruction;
        public string CompletionMessage;
        public string ProgressMessage;
        public string ContinueRecon;
    }

    [Serializable]
    public class CommonUITexts
    {
        public string SessionCounter;
        public string TimeRemaining;
        public string SessionFailed;
        public string SessionTimedOut;
    }

    #endregion
}