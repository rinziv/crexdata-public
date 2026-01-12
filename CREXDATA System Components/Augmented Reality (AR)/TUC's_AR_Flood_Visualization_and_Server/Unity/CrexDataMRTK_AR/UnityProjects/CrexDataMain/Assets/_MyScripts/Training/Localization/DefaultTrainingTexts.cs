using UnityEngine;
using FirefighterAR.Training.Localization;

namespace FirefighterAR.Training.Localization
{
    /// <summary>
    /// Default text fallbacks for when localization system is not available.
    /// This ensures the training system always has text to display.
    /// </summary>
    public static class DefaultTrainingTexts
    {
        /// <summary>
        /// Get default English texts
        /// </summary>
        public static TrainingTexts GetDefault()
        {
            return new TrainingTexts
            {
                ManagerTexts = new TrainingManagerTexts
                {
                    SessionCompleted = "Great job! You've completed the {0} training.",
                    TrainingComplete = "Training Complete!",
                    TrainingCompleteMessage = "Congratulations! You've completed all training sessions. You can now access the hand menu by lifting your left palm towards your face."
                },

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

                HandRayTraining = new HandRayTrainingTexts
                {
                    SessionName = "Hand Ray Interaction Training",
                    SessionDescription = "Master distant object selection using hand ray projection",
                    InitialInstruction = "Welcome to Hand Ray Training. Hand rays allow you to interact with distant objects without moving closer. Point your hand toward the target and use the pinch gesture (thumb to index finger) to select the object 3 times from a distance.",
                    CompletionMessage = "Outstanding! You have mastered hand ray interactions. This technique enables you to operate distant controls, select far objects, and interact with elements beyond your immediate reach in the Firefighter AR environment.",
                    ProgressMessage = "Excellent! Select the target {0} more times to complete the hand ray interaction training. ({1}/3)",
                    CompletionProgress = "Outstanding! You have successfully used hand ray interaction 3 times to select a distant object. This method is essential for operating equipment, accessing controls, and selecting objects that are out of physical reach.",
                    AimGuidance = "AIM REQUIRED: Point your hand directly at the highlighted cube to establish targeting with your hand ray.",
                    RayDetectedMessage = "Hand ray detected! Now use the pinch gesture to select the target.",
                    NoRayMessage = "Point your hand toward the target to establish your ray connection."
                },

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
                    PositioningTechnique = "POSITIONING TECHNIQUE: Lift your LEFT hand to chest level and rotate your palm toward your face, similar to reading your palm. Maintain this position steadily to activate the firefighter control interface.",
                    MaintainPosition = "MAINTAIN POSITION: Keep your palm facing toward you. The hand menu system is detecting your gesture and will activate the interface momentarily.",
                    ReminderMessage = "REMINDER: The hand menu is your mission-critical interface. Lift your LEFT hand and position your palm facing you to access all firefighting systems and tools."
                },

                POITraining = new POITrainingTexts
                {
                    SessionName = "Point of Interest Training",
                    SessionDescription = "Learn to scan and gather information from environmental markers",
                    InitialInstruction = "Welcome to POI (Point of Interest) Training. POI markers contain critical information about fire conditions, hazards, victim locations, and available resources. Look at POI markers to access their information.",
                    CompletionMessage = "Excellent! You have mastered POI interactions. These information points will be essential for gathering intelligence about fire conditions, structural hazards, victim locations, and available resources during firefighting operations.",
                    ProgressMessage = "POI DATA ACCESSED: Information retrieved successfully. Continue scanning for {0} more POI markers to complete your environmental assessment training.",
                    ContinueRecon = "CONTINUE RECONNAISSANCE: Maintain situational awareness and locate {0} more POI markers. These information points are essential for effective firefighting operations and safety assessment."
                },

                CommonUI = new CommonUITexts
                {
                    SessionCounter = "Session {0} of {1}",
                    TimeRemaining = "Hurry up! {0} seconds remaining!",
                    SessionFailed = "{0} failed: {1}. Please try again.",
                    SessionTimedOut = "Session timed out"
                }
            };
        }
    }
}