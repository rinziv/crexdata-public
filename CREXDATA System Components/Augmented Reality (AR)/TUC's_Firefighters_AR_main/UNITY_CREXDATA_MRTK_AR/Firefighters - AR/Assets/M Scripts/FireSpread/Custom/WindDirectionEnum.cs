// WindDirectionEnum.cs
using UnityEngine;

namespace FireSimulation.Enums
{
    // Optionally, you can add [System.Serializable] if you plan to serialize this enum
    [System.Serializable]
    public enum WindDirection
    {
        None,
        North,
        Northeast,
        East,
        Southeast,
        South,
        Southwest,
        West,
        Northwest
    }
}
