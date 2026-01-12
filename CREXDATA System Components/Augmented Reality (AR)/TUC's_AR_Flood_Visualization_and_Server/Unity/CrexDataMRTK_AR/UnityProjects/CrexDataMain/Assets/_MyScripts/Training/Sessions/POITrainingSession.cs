using UnityEngine;
using MixedReality.Toolkit.UX;
using System.Collections;
using MixedReality.Toolkit;

namespace FirefighterAR.Training.Sessions
{
    /// <summary>
    /// Example training session for Point of Interest (POI) interactions.
    /// This demonstrates how to create additional training modules.
    /// </summary>
    public class POITrainingSession : TrainingSession
    {
        [Header("POI Training Settings")]
        [SerializeField] private GameObject[] trainingPOIs;
        [SerializeField] private int requiredPOIInteractions = 2;
        [SerializeField] private float poiSpawnRadius = 3f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject poiHighlight;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material normalMaterial;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip poiInteractionSound;
        [SerializeField] private AudioClip guidanceSound;

        // Private variables
        private int poisInteracted = 0;
        private Coroutine guidanceCoroutine;

        protected override void OnSessionInitialize()
        {
            sessionName = "Point of Interest Training";
            sessionDescription = "Master interaction with environmental information systems";
            initialInstruction = $"Welcome to Point of Interest Training. POIs provide critical information about locations, hazards, and resources in your environment. Locate and interact with {requiredPOIInteractions} POI markers using either poke or hand ray methods.";
            completionMessage = "Excellent! You have mastered POI interactions. These information points will be essential for gathering intelligence about fire conditions, structural hazards, victim locations, and available resources during firefighting operations.";

            // Create training POIs if not assigned
            if (trainingPOIs == null || trainingPOIs.Length == 0)
            {
                CreateTrainingPOIs();
            }

            // Setup POI interactions
            SetupPOIInteractions();
        }

        protected override void OnSessionStart()
        {
            poisInteracted = 0;

            // Activate and position POIs
            ActivatePOIs();

            // Start guidance
            StartCoroutine(ShowGuidanceSequence());

            UpdateProgress($"POI IDENTIFICATION: Scan your environment for glowing cylindrical markers. These represent critical information points. Interact with {requiredPOIInteractions - poisInteracted} POIs using your trained interaction methods.");
        }

        protected override void OnSessionStop()
        {
            // Deactivate POIs
            DeactivatePOIs();

            // Stop guidance
            if (guidanceCoroutine != null)
            {
                StopCoroutine(guidanceCoroutine);
                guidanceCoroutine = null;
            }
        }

        protected override void OnSessionUpdate()
        {
            // Validate completion
            ValidateTaskCompletion(poisInteracted >= requiredPOIInteractions);
        }

        private void CreateTrainingPOIs()
        {
            trainingPOIs = new GameObject[requiredPOIInteractions + 1]; // Create one extra

            for (int i = 0; i < trainingPOIs.Length; i++)
            {
                GameObject poi = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                poi.name = $"TrainingPOI_{i}";
                poi.transform.localScale = new Vector3(0.3f, 0.1f, 0.3f);

                // Add interactable component
                var interactable = poi.AddComponent<StatefulInteractable>();

                // Add POI component (you might have a custom POI component)
                var poiComponent = poi.AddComponent<TrainingPOI>();
                poiComponent.Initialize(this, i);

                trainingPOIs[i] = poi;
                poi.SetActive(false);
            }

            Debug.Log($"[POITrainingSession] Created {trainingPOIs.Length} training POIs");
        }

        private void SetupPOIInteractions()
        {
            foreach (var poi in trainingPOIs)
            {
                if (poi != null)
                {
                    var poiComponent = poi.GetComponent<TrainingPOI>();
                    if (poiComponent != null)
                    {
                        poiComponent.OnInteracted += OnPOIInteracted;
                    }
                }
            }
        }

        private void ActivatePOIs()
        {
            if (Camera.main == null) return;

            Transform cameraTransform = Camera.main.transform;

            for (int i = 0; i < trainingPOIs.Length; i++)
            {
                if (trainingPOIs[i] != null)
                {
                    // Position POI in a circle around the user
                    float angle = (i / (float)trainingPOIs.Length) * 2f * Mathf.PI;
                    Vector3 position = cameraTransform.position +
                                     new Vector3(Mathf.Cos(angle), Random.Range(-0.5f, 1f), Mathf.Sin(angle)) * poiSpawnRadius;

                    trainingPOIs[i].transform.position = position;
                    trainingPOIs[i].SetActive(true);

                    // Apply highlight material
                    var renderer = trainingPOIs[i].GetComponent<Renderer>();
                    if (renderer != null && highlightMaterial != null)
                    {
                        renderer.material = highlightMaterial;
                    }
                }
            }
        }

        private void DeactivatePOIs()
        {
            foreach (var poi in trainingPOIs)
            {
                if (poi != null)
                {
                    poi.SetActive(false);
                }
            }
        }

        private void OnPOIInteracted(int poiIndex)
        {
            if (!isSessionActive) return;

            poisInteracted++;

            Debug.Log($"[POITrainingSession] POI {poiIndex} interacted. Total: {poisInteracted}/{requiredPOIInteractions}");

            // Play interaction sound
            if (audioSource != null && poiInteractionSound != null)
            {
                audioSource.PlayOneShot(poiInteractionSound);
            }

            // Update progress
            int remaining = requiredPOIInteractions - poisInteracted;
            if (remaining > 0)
            {
                UpdateProgress($"POI DATA ACCESSED: Information retrieved successfully. Continue scanning for {remaining} more POI markers to complete your environmental assessment training.");
            }
            else
            {
                UpdateProgress("ENVIRONMENTAL ASSESSMENT COMPLETE: All POI data has been successfully retrieved. You are now proficient in gathering critical field intelligence.");
            }

            // Deactivate the interacted POI
            if (poiIndex < trainingPOIs.Length && trainingPOIs[poiIndex] != null)
            {
                StartCoroutine(DeactivatePOIAfterDelay(trainingPOIs[poiIndex], 1f));
            }
        }

        private IEnumerator DeactivatePOIAfterDelay(GameObject poi, float delay)
        {
            // Change to normal material first
            var renderer = poi.GetComponent<Renderer>();
            if (renderer != null && normalMaterial != null)
            {
                renderer.material = normalMaterial;
            }

            yield return new WaitForSeconds(delay);
            poi.SetActive(false);
        }

        private IEnumerator ShowGuidanceSequence()
        {
            yield return new WaitForSeconds(1f);

            // Play guidance sound
            if (audioSource != null && guidanceSound != null)
            {
                audioSource.PlayOneShot(guidanceSound);
            }

            yield return new WaitForSeconds(2f);

            UpdateProgress("INFORMATION GATHERING: Look around your environment to locate glowing POI markers. These contain vital intelligence about hazards, resources, and tactical information. Use either poke (close range) or hand ray (distance) interaction methods.");

            yield return new WaitForSeconds(5f);

            UpdateProgress("OPERATIONAL CONTEXT: In real firefighting scenarios, POIs will indicate fire conditions, structural integrity, victim locations, water sources, and evacuation routes. Practice accessing this critical information now.");

            // Repeat guidance every 30 seconds if not completed
            while (isSessionActive && poisInteracted < requiredPOIInteractions)
            {
                yield return new WaitForSeconds(30f);

                if (isSessionActive && poisInteracted < requiredPOIInteractions)
                {
                    int remaining = requiredPOIInteractions - poisInteracted;
                    UpdateProgress($"CONTINUE RECONNAISSANCE: Maintain situational awareness and locate {remaining} more POI markers. These information points are essential for effective firefighting operations and safety assessment.");

                    if (audioSource != null && guidanceSound != null)
                    {
                        audioSource.PlayOneShot(guidanceSound);
                    }
                }
            }

            guidanceCoroutine = null;
        }

        /// <summary>
        /// Public method to set custom POIs
        /// </summary>
        public void SetTrainingPOIs(GameObject[] pois)
        {
            trainingPOIs = pois;
            SetupPOIInteractions();
        }

        /// <summary>
        /// Public method to set required interaction count
        /// </summary>
        public void SetRequiredInteractions(int count)
        {
            requiredPOIInteractions = count;
            initialInstruction = $"Find and interact with {requiredPOIInteractions} Points of Interest (POIs) in your environment.";
        }
    }

    /// <summary>
    /// Simple POI component for training purposes
    /// </summary>
    public class TrainingPOI : MonoBehaviour
    {
        private POITrainingSession trainingSession;
        private int poiIndex;
        private StatefulInteractable interactable;

        public System.Action<int> OnInteracted;

        public void Initialize(POITrainingSession session, int index)
        {
            trainingSession = session;
            poiIndex = index;

            // Get or add interactable component
            interactable = GetComponent<StatefulInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<StatefulInteractable>();
            }

            // Setup interaction events
            interactable.OnClicked.AddListener(HandleInteraction);
        }

        private void HandleInteraction()
        {
            OnInteracted?.Invoke(poiIndex);
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.OnClicked.RemoveListener(HandleInteraction);
            }
        }
    }
}