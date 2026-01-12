using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum POITypeTest
{
    School,
    Hospital,
    Church,
    Station,
    General
}

[CreateAssetMenu(fileName = "New POI", menuName = "POI")]
public class POI : ScriptableObject
{
    [Tooltip("POI coordinates in the format 'latitude, longitude'")]
    public string coordinates;

    public POITypeTest POIType;
    public new string name;
    public string description;
    public float riskLevel;

    [Tooltip("Prefab associated with this POI")]
    public GameObject poiPrefab;


    public double Latitude
    {
        get
        {
            return double.Parse(coordinates.Split(',')[0].Trim());
        }
    }

    public double Longitude
    {
        get
        {
            return double.Parse(coordinates.Split(',')[1].Trim());
        }
    }

}
