using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class BuildingData
{
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

[System.Serializable]
public class CityData
{
    public List<Road> roads;
    public List<Vector3> goalPositions;
    public List<BuildingData> buildings = new List<BuildingData>();

    private const string BUILDING_CONTAINER_NAME = "[Generated Buildings]";

    // For serialization
    public CityData() { }

    public CityData(TownGenerator generator)
    {
        roads = generator.roads;
        goalPositions = generator.goalPositions;
        buildings = new List<BuildingData>();

        Transform buildingContainer = generator.transform.Find(BUILDING_CONTAINER_NAME);
        if (buildingContainer != null)
        {
            foreach (Transform buildingTransform in buildingContainer)
            {
                GameObject buildingInstance = buildingTransform.gameObject;
                BuildingIdentifier identifier = buildingInstance.GetComponent<BuildingIdentifier>();
                if (identifier != null)
                {
                    Vector3 pos = buildingInstance.transform.position;
                    Quaternion rot = buildingInstance.transform.rotation;
                    Vector3 scl = buildingInstance.transform.localScale;

                    if (IsVector3Valid(pos) && IsQuaternionValid(rot) && IsVector3Valid(scl))
                    {
                        BuildingData buildingData = new BuildingData
                        {
                            prefabName = identifier.prefabName,
                            position = pos,
                            rotation = rot,
                            scale = scl
                        };
                        buildings.Add(buildingData);
                    }
                    else
                    {
                        Debug.LogError($"Building '{buildingInstance.name}' under '{BUILDING_CONTAINER_NAME}' has invalid transform values (NaN or Infinity) and will not be saved.", buildingInstance);
                    }
                }
                else
                {
                    Debug.LogWarning($"GameObject '{buildingInstance.name}' under '{BUILDING_CONTAINER_NAME}' is missing a 'BuildingIdentifier' component and will not be saved.", buildingInstance);
                }
            }
        }
    }

    private bool IsVector3Valid(Vector3 vec)
    {
        return !float.IsNaN(vec.x) && !float.IsNaN(vec.y) && !float.IsNaN(vec.z) &&
               !float.IsInfinity(vec.x) && !float.IsInfinity(vec.y) && !float.IsInfinity(vec.z);
    }

    private bool IsQuaternionValid(Quaternion q)
    {
        return !float.IsNaN(q.x) && !float.IsNaN(q.y) && !float.IsNaN(q.z) && !float.IsNaN(q.w) &&
               !float.IsInfinity(q.x) && !float.IsInfinity(q.y) && !float.IsInfinity(q.z) && !float.IsInfinity(q.w);
    }
}