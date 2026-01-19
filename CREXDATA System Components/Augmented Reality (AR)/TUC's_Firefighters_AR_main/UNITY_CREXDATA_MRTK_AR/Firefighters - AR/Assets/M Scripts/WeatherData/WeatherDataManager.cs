using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Mapbox.Unity.Map;
using Mapbox.Map;
using MixedReality.Toolkit.UX;

public class WeatherDataManager : MonoBehaviour
{

    // Target location weather data
    public WeatherDataFetcher weatherDataFetcher;
    public bool targetLocationSet = false;
    public GameObject targetLocationMarker;
    public AbstractMap map;
    
    // Check if marker is moved
    private float time = 0.0f;
    private Vector3 positionLast;
    public float minDistance;


    [Header("UI References")]
    public TMP_Text temperatureText;
    public TMP_Text windspeedText;
    public TMP_Text winddirectionText;
    public TMP_Text timeText;
    public WindArrowManager arrowManager;
    public MixedReality.Toolkit.UX.Slider timeSlider;

    public WeatherDataPack currentWeather { get; private set; }
    public List<WeatherDataPack> hourlyForecasts { get; private set; } = new List<WeatherDataPack>();

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;

        if(time > 0.4f && targetLocationSet){

            // Calculate the distance between the previous and current potision of the marker.
            float distance = Vector2.Distance(positionLast, targetLocationMarker.transform.position);
            //Debug.Log(distance);
            if(distance > minDistance){
                positionLast = targetLocationMarker.transform.position;
                weatherDataFetcher.SetTargetLocation(map.WorldToGeoPosition(targetLocationMarker.transform.position));
                weatherDataFetcher.DataRequest();   
            }

            time = 0.0f;
        }
    }

    public void ProcessWeatherData(string json)
    {
        try
        {
            WeatherResponse response = JsonUtility.FromJson<WeatherResponse>(json);

            if (response == null || response.current_weather == null || response.hourly == null)
            {
                Debug.LogError("Invalid weather data");
                return;
            }

            // Store current weather correctly
            currentWeather = new WeatherDataPack(
                response.current_weather.time, // Use the actual time
                response.current_weather.temperature,
                response.current_weather.windspeed,
                response.current_weather.winddirection
            );
            
            // Store hourly forecast
            hourlyForecasts.Clear();
            for (int i = 0; i < response.hourly.time.Count; i++)
            {
                hourlyForecasts.Add(new WeatherDataPack(
                    response.hourly.time[i],
                    response.hourly.temperature_2m[i],
                    response.hourly.windspeed_10m[i],
                    response.hourly.winddirection_10m[i]
                ));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Processing error: {e.Message}");
        }
        
        UpdateWeatherInfo();
    }

    public void UpdateWeatherInfo()
    {
        WeatherDataPack selectedWeather;  
        int hour = (int)timeSlider.Value;

        if(hour == 0)
            selectedWeather = currentWeather;
        else
            selectedWeather = hourlyForecasts[hour - 1];

        if(selectedWeather != null){

            // Calculate uncertainties as 10% of each value
            float temperatureUncertainty = selectedWeather.temperature * 0.1f * (float)hour/5;
            float windSpeedUncertainty = selectedWeather.windSpeed * 0.1f  * (float)hour/5;
            float windDirectionUncertainty = 10.0f * (float)hour/5;

            // Update the UI elements including uncertainty (formatted to 1 decimal place)
            temperatureText.text = $"Temperature: {selectedWeather.temperature}°C (±{temperatureUncertainty:F1}°C)";
            windspeedText.text = $"Wind Speed: {selectedWeather.windSpeed} km/h (±{windSpeedUncertainty:F1} km/h)";
            winddirectionText.text = $"Wind Direction: {ConvertDegreesToCompass(selectedWeather.windDirection)} (±{windDirectionUncertainty:F1}°)";

            Debug.Log($"Wind Direction: {selectedWeather.windDirection}° ({ConvertDegreesToCompass(selectedWeather.windDirection)})");
            // Format timestamp to "time - date"
            string time = selectedWeather.timestamp.Split('T')[1];  // Extract time part
            string date = selectedWeather.timestamp.Split('T')[0];  // Extract date part
            timeText.text = $"Time: {time} | Date: {date}";  // Display formatted time and date
        
            UpdateArrow(selectedWeather.windDirection); 
        }
        
    }

    void UpdateArrow(float direction)
    {
        if (arrowManager != null)
            arrowManager.UpdateArrowDirections(direction);
        else
            Debug.LogWarning("Wind arrow manager missing!");
    }

    string ConvertDegreesToCompass(int degrees)
    {
        string[] compass = {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
        };
        return compass[Mathf.RoundToInt((degrees % 360) / 22.5f) % 16];
    }

    [System.Serializable]
    public class WeatherDataPack
    {
        public string timestamp;
        public float temperature;
        public float windSpeed;
        public int windDirection;

        public WeatherDataPack(string time, float temp, float ws, int wd)
        {
            timestamp = time;
            temperature = temp;
            windSpeed = ws;
            windDirection = wd;
        }
    }

    [System.Serializable]
    public class WeatherResponse
    {
        public CurrentWeather current_weather;
        public HourlyForecast hourly;
    }

    [System.Serializable]
    public class CurrentWeather
    {
        public string time; 
        public int interval;
        public float temperature;
        public float windspeed;
        public int winddirection;
        public int is_day;
        public int weathercode;
    }

    [System.Serializable]
    public class HourlyForecast
    {
        public List<string> time;
        public List<float> temperature_2m;
        public List<float> windspeed_10m;
        public List<int> winddirection_10m;
    }

    public void CloseTargetLocation()
    {
        if (targetLocationSet)
        {
            weatherDataFetcher.SetTargetLocation(GPSLocationProvider.Instance.GetCurrentLocation());
            weatherDataFetcher.DataRequest();   
            targetLocationMarker.SetActive(false);
            targetLocationSet = false;
        }
    }

    public void ToggleTargetLocation()
    {
        if (targetLocationSet)
        {
            weatherDataFetcher.SetTargetLocation(GPSLocationProvider.Instance.GetCurrentLocation());
            weatherDataFetcher.DataRequest();   
            targetLocationMarker.SetActive(false);
            targetLocationSet = false;
        }
        else
        {
            targetLocationMarker.SetActive(true);
            targetLocationSet = true;
        }
    }
}
