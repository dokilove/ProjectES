using UnityEngine;

/// <summary>
/// A simple component to store the original prefab name on an instantiated GameObject.
/// This helps in identifying the object type even if the prefab link is broken.
/// </summary>
public class BuildingIdentifier : MonoBehaviour
{
    public string prefabName;
}
