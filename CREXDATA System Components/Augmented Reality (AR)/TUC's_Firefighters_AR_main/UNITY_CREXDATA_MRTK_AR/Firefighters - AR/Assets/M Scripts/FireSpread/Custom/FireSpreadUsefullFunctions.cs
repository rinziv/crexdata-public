using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FireSimulation.Enums;

public static class FireSpreadUsefullFunctions
{
    // Adjusts the burn chance based on wind direction
    public static float AdjustBurnChance(float baseChance, WindDirection neighborDirection, WindDirection windDirection, float windStrength)
    {
        if (windDirection == WindDirection.None)
            return baseChance;

        if (neighborDirection == windDirection)
        {
            // Neighbor is in the wind direction
            baseChance += windStrength;
        }
        else if (IsOppositeDirection(neighborDirection, windDirection))
        {
            // Neighbor is opposite to wind direction
            baseChance -= windStrength;
        }
        // Optionally, adjust for perpendicular directions if desired

        // Clamp the burn chance between 0 and 1
        baseChance = Mathf.Clamp(baseChance, 0f, 1f);
        return baseChance;
    }

    // Determines if two directions are opposite
    public static bool IsOppositeDirection(WindDirection dir1, WindDirection dir2)
    {
        if (dir1 == WindDirection.None || dir2 == WindDirection.None)
            return false;

        // Define opposite pairs
        if ((dir1 == WindDirection.North && dir2 == WindDirection.South) ||
            (dir1 == WindDirection.Northeast && dir2 == WindDirection.Southwest) ||
            (dir1 == WindDirection.East && dir2 == WindDirection.West) ||
            (dir1 == WindDirection.Southeast && dir2 == WindDirection.Northwest) ||
            (dir1 == WindDirection.South && dir2 == WindDirection.North) ||
            (dir1 == WindDirection.Southwest && dir2 == WindDirection.Northeast) ||
            (dir1 == WindDirection.West && dir2 == WindDirection.East) ||
            (dir1 == WindDirection.Northwest && dir2 == WindDirection.Southeast))
        {
            return true;
        }

        return false;
    }

    // Converts a direction vector to WindDirection enum
    public static WindDirection GetWindDirectionFromVector(Vector2Int direction)
    {
        if (direction.x == 0 && direction.y == 1)
            return WindDirection.North;
        if (direction.x == 1 && direction.y == 1)
            return WindDirection.Northeast;
        if (direction.x == 1 && direction.y == 0)
            return WindDirection.East;
        if (direction.x == 1 && direction.y == -1)
            return WindDirection.Southeast;
        if (direction.x == 0 && direction.y == -1)
            return WindDirection.South;
        if (direction.x == -1 && direction.y == -1)
            return WindDirection.Southwest;
        if (direction.x == -1 && direction.y == 0)
            return WindDirection.West;
        if (direction.x == -1 && direction.y == 1)
            return WindDirection.Northwest;

        return WindDirection.None;
    }

    
}
