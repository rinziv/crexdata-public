using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro; // Ensure you are using TextMeshProUGUI for UI elements

public class FireSpreadSparkController : MonoBehaviour
{
    public FireSimulationLoader fireSpreadSimulation;

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

    private int maxTime = 60; // Maximum time in seconds
    private int stateTime = 0; // Current time in seconds


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

        maxTime = fireSpreadSimulation.maxTime / 60; // Assuming maxTime is in seconds
    }

    public void PlayStopRoutine()
    {
        isPlaying = !isPlaying;
        UpdatePlayButtonText();

        if (isPlaying)
        {
            // If at the end of the timeline, reset to the beginning to play
            if (stateTime >= maxTime - 1)
            {
                stateTime = 0;
                UpdateCurrentTimeAndTextDisplay(); // Update display for the reset state
                fireSpreadSimulation.UpdateVisualization(stateTime * 60); // Update grid for the reset state
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

            if (stateTime >= maxTime - 1)
            {
                // Reached the end of the timeline
                // stateTime = 0; // Loop back to the beginning
                
                PlayStopRoutine(); // Stop instead of looping
                yield break;
            }
            else
            {
                stateTime++;
            }
            UpdateCurrentTimeAndTextDisplay();
            fireSpreadSimulation.UpdateVisualization(stateTime * 60);
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
        int newStateTime = stateTime + step;

        if (newStateTime >= maxTime)
        {
            stateTime = 0; // Wrap to the beginning
        }
        else if (newStateTime < 0)
        {
            stateTime = maxTime - 1; // Wrap to the end
        }
        else
        {
            stateTime = newStateTime;
        }

        UpdateCurrentTimeAndTextDisplay();
        fireSpreadSimulation.UpdateVisualization(stateTime * 60);
    }

    private void UpdateCurrentTimeAndTextDisplay()
    {
        if (fireSpreadSimulation == null || timePeriodText == null) return;

        // Calculate currentTime directly from baseTime and the grid's stateTime (number of hours passed)
        currentTime = baseTime.AddMinutes(stateTime);
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