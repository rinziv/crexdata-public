using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro; // Ensure you are using TextMeshProUGUI for UI elements

public class FireSpreadController : MonoBehaviour
{
    public FireSpreadGrid fireSpreadGrid;

    // UI Elements
    public Button playStopButton;
    public TextMeshPro timePeriodText; 

    // Time Variables
    private DateTime baseTime;
    private DateTime currentTime;
    private bool isPlaying = false;

    // The step interval (in seconds) for auto-advancing when in Play mode
    [SerializeField] private float stepInterval = 1f;

    private Coroutine playRoutine;
    public TextMeshPro playStopButtonTextComponent; 
    private WaitForSeconds playWaitInstruction; 

    void Start()
    {
        // Initialize time
        DateTime now = DateTime.Now;
        baseTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        // Initialize cached WaitForSeconds
        playWaitInstruction = new WaitForSeconds(stepInterval);

        // Initial UI setup
        UpdateCurrentTimeAndTextDisplay();
        UpdatePlayButtonText();
    }

    public void PlayStopRoutine()
    {
        isPlaying = !isPlaying;
        UpdatePlayButtonText();

        if (isPlaying)
        {
            if (fireSpreadGrid.maxTime <= 0)
            {
                Debug.LogWarning("Cannot play: fireSpreadGrid.maxTime is not positive.");
                isPlaying = false; // Revert state
                UpdatePlayButtonText();
                return;
            }

            // If at the end of the timeline, reset to the beginning to play
            if (fireSpreadGrid.stateTime >= fireSpreadGrid.maxTime - 1)
            {
                fireSpreadGrid.stateTime = 0;
                UpdateCurrentTimeAndTextDisplay(); // Update display for the reset state
                fireSpreadGrid.UpdateVisualization(); // Update grid for the reset state
            }
            playRoutine = StartCoroutine(PlaySequenceRoutine());
        }
        else
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
        }
    }

    private IEnumerator PlaySequenceRoutine()
    {
        while (isPlaying)
        {
            yield return playWaitInstruction;

            if (fireSpreadGrid.stateTime >= fireSpreadGrid.maxTime - 1)
            {
                // Reached the end of the timeline
                // fireSpreadGrid.stateTime = 0; // Loop back to the beginning
                
                PlayStopRoutine(); // Stop instead of looping
                yield break;
            }
            else
            {
                fireSpreadGrid.stateTime++;
            }
            UpdateCurrentTimeAndTextDisplay();
            fireSpreadGrid.UpdateVisualization();
        }
    }

    public void NextTimePeriod()
    {
        if (isPlaying)
        {
            PlayStopRoutine(); // Stop playback before manual step
        }
        AdvanceTimeStep(1);
    }

    public void PreviousTimePeriod()
    {
        if (isPlaying)
        {
            PlayStopRoutine(); // Stop playback before manual step
        }
        AdvanceTimeStep(-1);
    }

    private void AdvanceTimeStep(int step)
    {
        if (fireSpreadGrid.maxTime <= 0)
        {
            Debug.LogWarning("Cannot change time period: fireSpreadGrid.maxTime is not positive.");
            return;
        }

        int newStateTime = fireSpreadGrid.stateTime + step;

        if (newStateTime >= fireSpreadGrid.maxTime)
        {
            fireSpreadGrid.stateTime = 0; // Wrap to the beginning
        }
        else if (newStateTime < 0)
        {
            fireSpreadGrid.stateTime = fireSpreadGrid.maxTime - 1; // Wrap to the end
        }
        else
        {
            fireSpreadGrid.stateTime = newStateTime;
        }

        UpdateCurrentTimeAndTextDisplay();
        fireSpreadGrid.UpdateVisualization();
    }

    private void UpdateCurrentTimeAndTextDisplay()
    {
        if (fireSpreadGrid == null || timePeriodText == null) return;

        // Calculate currentTime directly from baseTime and the grid's stateTime (number of hours passed)
        currentTime = baseTime.AddHours(fireSpreadGrid.stateTime);
        string displayString = currentTime.ToString("HH:mm dd-MM-yy");
        timePeriodText.text = $"Time: {displayString}";
    }

    private void UpdatePlayButtonText()
    {
        if (playStopButtonTextComponent != null)
        {
            playStopButtonTextComponent.text = isPlaying ? "=" : "Δ ";
        }
    }
}