using System.Collections;
using UnityEngine;
using MixedReality.Toolkit.Input;
using MixedReality.Toolkit.Subsystems;
using UnityEngine.XR;
using MixedReality.Toolkit;

namespace FirefighterAR.Training
{
    /// <summary>
    /// Detects when the user shows their left palm facing towards them.
    /// This is used to trigger the hand menu during training.
    /// </summary>
    public class HandMenuDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float palmFacingThreshold = 0.7f; // Dot product threshold for "facing user"
        [SerializeField] private float detectionDuration = 1.5f; // How long palm must be facing user
        [SerializeField] private float detectionDistance = 0.3f; // Maximum distance from head

        [Header("Visual Feedback")]
        [SerializeField] private GameObject palmIndicator; // Optional visual indicator
        [SerializeField] private float indicatorScale = 0.1f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip palmDetectedSound;

        // Private variables
        private bool isPalmFacingUser = false;
        private float palmFacingStartTime;
        private bool hasTriggered = false;
        private Vector3 lastValidPalmPosition;
        private Transform cameraTransform;

        // Events
        public System.Action OnHandMenuTriggered;
        public System.Action<bool> OnPalmFacingChanged;
        public System.Action<float> OnPalmFacingProgress; // 0 to 1 progress

        // Properties
        public bool IsPalmFacingUser => isPalmFacingUser;
        public float FacingProgress => isPalmFacingUser ? Mathf.Clamp01((Time.time - palmFacingStartTime) / detectionDuration) : 0f;
        public bool IsDetectionActive { get; private set; } = true;

        private void Start()
        {
            // Get camera transform (usually the main camera or XR camera)
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null)
            {
                cameraTransform = FindObjectOfType<Camera>()?.transform;
            }

            if (cameraTransform == null)
            {
                Debug.LogError("[HandMenuDetector] No camera found! Hand menu detection will not work.");
                enabled = false;
                return;
            }

            // Initialize palm indicator
            if (palmIndicator != null)
            {
                palmIndicator.SetActive(false);
            }

            Debug.Log("[HandMenuDetector] Initialized");
        }

        private void Update()
        {
            if (!IsDetectionActive) return;

            CheckLeftPalmFacing();
            UpdateVisualFeedback();
        }

        private void CheckLeftPalmFacing()
        {
            var handsSubsystem = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsSubsystem>();
            if (handsSubsystem == null) return;

            // Try to get left hand palm joint
            if (handsSubsystem.TryGetJoint(TrackedHandJoint.Palm, XRNode.LeftHand, out var palmJoint))
            {
                Vector3 palmPosition = palmJoint.Pose.position;
                Vector3 palmNormal = palmJoint.Pose.rotation * Vector3.up; // Palm normal vector

                // Calculate if palm is facing the user
                Vector3 toPalm = (palmPosition - cameraTransform.position).normalized;
                float facingDot = Vector3.Dot(palmNormal, -toPalm);

                // Check distance constraint
                float distanceToHead = Vector3.Distance(palmPosition, cameraTransform.position);
                bool isWithinDistance = distanceToHead <= detectionDistance;

                bool shouldBeFacing = facingDot >= palmFacingThreshold && isWithinDistance;

                if (shouldBeFacing && !isPalmFacingUser)
                {
                    // Started facing user
                    StartPalmFacing(palmPosition);
                }
                else if (!shouldBeFacing && isPalmFacingUser)
                {
                    // Stopped facing user
                    StopPalmFacing();
                }
                else if (shouldBeFacing && isPalmFacingUser)
                {
                    // Continue facing user
                    UpdatePalmFacing(palmPosition);
                }

                lastValidPalmPosition = palmPosition;
            }
            else if (isPalmFacingUser)
            {
                // Hand tracking lost while palm was facing
                StopPalmFacing();
            }
        }

        private void StartPalmFacing(Vector3 palmPosition)
        {
            isPalmFacingUser = true;
            palmFacingStartTime = Time.time;
            hasTriggered = false;

            Debug.Log("[HandMenuDetector] Palm started facing user");
            OnPalmFacingChanged?.Invoke(true);

            // Show visual indicator
            if (palmIndicator != null)
            {
                palmIndicator.SetActive(true);
                palmIndicator.transform.position = palmPosition;
                palmIndicator.transform.localScale = Vector3.one * indicatorScale;
            }
        }

        private void UpdatePalmFacing(Vector3 palmPosition)
        {
            float elapsed = Time.time - palmFacingStartTime;
            float progress = elapsed / detectionDuration;

            OnPalmFacingProgress?.Invoke(progress);

            // Update indicator position and scale
            if (palmIndicator != null)
            {
                palmIndicator.transform.position = palmPosition;
                // Scale indicator based on progress
                float scale = indicatorScale * (1f + progress * 0.5f);
                palmIndicator.transform.localScale = Vector3.one * scale;
            }

            // Check if we've held long enough
            if (elapsed >= detectionDuration && !hasTriggered)
            {
                TriggerHandMenu();
            }
        }

        private void StopPalmFacing()
        {
            isPalmFacingUser = false;

            Debug.Log("[HandMenuDetector] Palm stopped facing user");
            OnPalmFacingChanged?.Invoke(false);
            OnPalmFacingProgress?.Invoke(0f);

            // Hide visual indicator
            if (palmIndicator != null)
            {
                palmIndicator.SetActive(false);
            }
        }

        private void TriggerHandMenu()
        {
            hasTriggered = true;

            Debug.Log("[HandMenuDetector] Hand menu triggered!");

            // Play sound
            if (audioSource != null && palmDetectedSound != null)
            {
                audioSource.PlayOneShot(palmDetectedSound);
            }

            OnHandMenuTriggered?.Invoke();

            // Hide indicator after triggering
            if (palmIndicator != null)
            {
                StartCoroutine(HideIndicatorAfterDelay(1f));
            }
        }

        private IEnumerator HideIndicatorAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (palmIndicator != null)
            {
                palmIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// Enables or disables palm detection
        /// </summary>
        public void SetDetectionActive(bool active)
        {
            IsDetectionActive = active;

            if (!active)
            {
                StopPalmFacing();
            }

            Debug.Log($"[HandMenuDetector] Detection {(active ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Resets the detector state
        /// </summary>
        public void ResetDetector()
        {
            StopPalmFacing();
            hasTriggered = false;
        }

        /// <summary>
        /// Gets the current palm position (if available)
        /// </summary>
        public Vector3 GetCurrentPalmPosition()
        {
            return lastValidPalmPosition;
        }

        /// <summary>
        /// Manually trigger the hand menu (for testing)
        /// </summary>
        [ContextMenu("Trigger Hand Menu")]
        public void ManualTrigger()
        {
            TriggerHandMenu();
        }

        private void UpdateVisualFeedback()
        {
            if (palmIndicator != null && palmIndicator.activeSelf)
            {
                // Add some visual animation (optional)
                float pulse = Mathf.Sin(Time.time * 5f) * 0.1f + 1f;
                palmIndicator.transform.localScale = Vector3.one * (indicatorScale * pulse);

                // Change color based on progress
                var renderer = palmIndicator.GetComponent<Renderer>();
                if (renderer != null)
                {
                    float progress = FacingProgress;
                    Color indicatorColor = Color.Lerp(Color.yellow, Color.green, progress);
                    renderer.material.color = indicatorColor;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraTransform != null)
            {
                // Draw detection sphere
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(cameraTransform.position, detectionDistance);

                // Draw palm position if available
                if (isPalmFacingUser && lastValidPalmPosition != Vector3.zero)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(lastValidPalmPosition, 0.02f);

                    // Draw line from camera to palm
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(cameraTransform.position, lastValidPalmPosition);
                }
            }
        }
    }
}