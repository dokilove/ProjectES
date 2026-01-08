using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CityData
{
    public List<Road> roads;
    public List<Vector3> goalPositions;

    public CityData(TownGenerator generator)
    {
        roads = generator.roads;
        goalPositions = generator.goalPositions;
    }
}
