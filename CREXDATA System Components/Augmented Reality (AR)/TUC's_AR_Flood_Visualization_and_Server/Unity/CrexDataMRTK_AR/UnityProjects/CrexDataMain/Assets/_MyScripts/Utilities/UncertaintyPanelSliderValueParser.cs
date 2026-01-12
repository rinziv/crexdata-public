using MixedReality.Toolkit.UX;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UncertaintyPanelSliderValueParser : MonoBehaviour
{
    [SerializeField] private PressableButton _testButton;
    [SerializeField] private Slider _mainSlider;
    [SerializeField] private Slider[] _sliders;

    public AnimationCurve animationCurve;

    void Start()
    {
        if (_testButton != null)
        {
            _testButton.OnClicked.AddListener(OnTestButtonClicked);
        }
    }

    private void OnTestButtonClicked()
    {
        foreach (var slider in _sliders)
        {
            //Randomize slider value between min and max
            SetSliderValue(slider, Random.Range(slider.MinValue, slider.MaxValue));
        }
    }

    public void SetSliderValue(int sliderIndex, float value)
    {
        if (sliderIndex >= 0 && sliderIndex < _sliders.Length && value >= _sliders[sliderIndex].MinValue && value <= _sliders[sliderIndex].MaxValue)
        {
            StartCoroutine(LerpSliderValue(_sliders[sliderIndex], value));
        }
        else
        {
            Debug.LogWarning("Invalid slider index or value out of range.");
        }
    }

    public void SetSliderValue(Slider slider, float value)
    {
        if (slider != null && value >= slider.MinValue && value <= slider.MaxValue)
        {
            StartCoroutine(LerpSliderValue(slider, value));
        }
        else
        {
            Debug.LogWarning("Invalid slider or value out of range.");
        }
    }

    private IEnumerator LerpSliderValue(Slider slider, float targetValue)
    {
        float duration = 1f;
        float elapsed = 0f;
        float startingValue = slider.Value;

        while (elapsed < duration)
        {
            float t = animationCurve.Evaluate(elapsed / duration);
            slider.Value = Mathf.Lerp(startingValue, targetValue, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        slider.Value = targetValue;
    }

    public void TestSetValues()
    {
        SetSliderValue(0, .5f);
        SetSliderValue(1, .7f);
        SetSliderValue(2, .2f);
    }
}
