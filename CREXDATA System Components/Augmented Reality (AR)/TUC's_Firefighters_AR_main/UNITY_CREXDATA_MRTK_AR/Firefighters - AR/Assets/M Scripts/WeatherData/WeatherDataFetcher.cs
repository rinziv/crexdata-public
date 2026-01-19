using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Mapbox.Utils;

public class WeatherDataFetcher : MonoBehaviour
{
    private string weatherAPIUrl = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current_weather=true&hourly=temperature_2m,windspeed_10m,winddirection_10m&forecast_days=1";
    
    public WeatherDataManager weatherDataManager;
    Vector2d targetLocation;

    void Start()
    {   
        if (GPSLocationProvider.Instance != null)
        {   
            SetTargetLocation(GPSLocationProvider.Instance.GetCurrentLocation());
            DataRequest();
        }
        else
        {
            Debug.LogError("GPS Location Provider not found!");
        }
    }

    public void SetTargetLocation(Vector2d location)
    {
        targetLocation = location;
        Debug.Log($"Target location set to: {targetLocation.x}, {targetLocation.y}");
    }

    public void DataRequest(){
        
        StartCoroutine(StreamWeatherData());
    }

    IEnumerator StreamWeatherData()
    {
        double lat = targetLocation.x;
        double lon = targetLocation.y;

        Debug.Log($"Fetching weather data for: {lat}, {lon}");

        string url = string.Format(weatherAPIUrl, lat, lon);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {request.error}");
            }
            else if (weatherDataManager != null)
            {
                Debug.Log(request.downloadHandler.text);
                weatherDataManager.ProcessWeatherData(request.downloadHandler.text);
            }
        }
    }
}

