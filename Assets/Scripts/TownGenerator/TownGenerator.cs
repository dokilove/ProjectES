using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TownGenerator : MonoBehaviour
{
    [Header("Road Settings")]
    public List<Road> roads = new List<Road>();
    public float roadWidth = 5f;
    public Material roadMaterial;

    [Header("Building Settings")]
    public GameObject[] buildingPrefabs;
    public float buildingOffset = 10f;
    public float buildingSpacing = 20f;
    public Vector2 buildingRandomOffset = new Vector2(0, 5f);
    public Vector2 buildingRandomSpacing = new Vector2(0, 10f);
    public LayerMask terrainLayer; // Assign your terrain layer in the Inspector

    [Header("Building Rows")]
    public int buildingRowsPerSide = 1; // Number of rows on each side of the road
    public float rowSpacing = 5f;       // Spacing between building rows

    [Header("Goal Settings")]
    public GameObject goalMarkerPrefab;
    public List<Vector3> goalPositions = new List<Vector3>();


    private const string BUILDING_CONTAINER_NAME = "[Generated Buildings]";
    private const string GOAL_CONTAINER_NAME = "[Generated Goals]";

    #region Public API

    public void GenerateAll()
    {
        Debug.Log("Starting city generation...");
        GenerateRoad();
        GenerateBuildings();
        GenerateGoals();
        Debug.Log("City generation complete.");
    }

    public List<Vector3> GetPredefinedGoalPositions()
    {
        return goalPositions;
    }

    public Vector3 GetRandomRoadPosition()
    {
        if (roads == null || roads.Count == 0)
        {
            Debug.LogWarning("No roads available to find a random position.");
            return Vector3.zero;
        }

        // Select a random road
        Road randomRoad = roads[Random.Range(0, roads.Count)];
        if (randomRoad.nodes.Count < 2)
        {
            Debug.LogWarning($"Selected road '{randomRoad.name}' has less than 2 nodes.");
            return Vector3.zero;
        }

        // Select a random segment on that road
        int randomSegmentIndex = Random.Range(0, randomRoad.nodes.Count - 1);
        Vector3 p1 = randomRoad.nodes[randomSegmentIndex];
        Vector3 p2 = randomRoad.nodes[randomSegmentIndex + 1];

        // Select a random point along that segment
        float randomT = Random.Range(0f, 1f);
        return Vector3.Lerp(p1, p2, randomT);
    }

    public Bounds GetCityBounds()
    {
        Bounds bounds = new Bounds();
        MeshFilter roadMeshFilter = GetComponent<MeshFilter>();
        if (roadMeshFilter != null && roadMeshFilter.sharedMesh != null)
        {
            bounds = roadMeshFilter.sharedMesh.bounds;
        }

        Transform buildingContainer = transform.Find(BUILDING_CONTAINER_NAME);
        if (buildingContainer != null)
        {
            Renderer[] buildingRenderers = buildingContainer.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in buildingRenderers)
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return bounds;
    }

    #endregion

    #region Road Generation
    public void GenerateRoad()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        if (roads.Count == 0)
        {
            ClearRoads(); // Use the clear method if no roads exist
            return;
        }

        List<CombineInstance> combineInstances = new List<CombineInstance>();

        foreach (var road in roads)
        {
            if (road.nodes.Count < 2) continue;

            Mesh roadMesh = CreateRoadMesh(road);
            CombineInstance combineInstance = new CombineInstance
            {
                mesh = roadMesh,
                transform = Matrix4x4.identity // Vertices are already in local space
            };
            combineInstances.Add(combineInstance);
        }

        Mesh finalMesh = new Mesh();
        finalMesh.name = "CombinedRoadMesh";
        finalMesh.CombineMeshes(combineInstances.ToArray(), true, false); // Merge submeshes, don't use matrices

        meshFilter.mesh = finalMesh;
        meshCollider.sharedMesh = finalMesh;

        if (roadMaterial != null)
        {
            meshRenderer.material = roadMaterial;
        }

        // Automatically assign the "Road" layer to this GameObject
        gameObject.layer = LayerMask.NameToLayer("Road");

#if UNITY_EDITOR
        // Mark the road object as Navigation Static for NavMesh baking
        GameObjectUtility.SetStaticEditorFlags(gameObject, StaticEditorFlags.NavigationStatic);
#endif
    }

    public void ClearRoads()
    {
        roads.Clear(); // Clear the underlying data list

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.mesh = null;
        }
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }
    }

    private Mesh CreateRoadMesh(Road road)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        float currentLength = 0f;

        for (int i = 0; i < road.nodes.Count - 1; i++)
        {
            // Transform world points to local points for the mesh
            Vector3 p1 = transform.InverseTransformPoint(road.nodes[i]);
            Vector3 p2 = transform.InverseTransformPoint(road.nodes[i + 1]);

            // Add a small Y-offset to prevent Z-fighting with the ground plane
            p1.y += 0.001f;
            p2.y += 0.001f;

            float segmentLength = Vector3.Distance(p1, p2);

            Vector3 forward = (p2 - p1).normalized;
            Vector3 side = Vector3.Cross(forward, Vector3.up).normalized;

            vertices.Add(p1 + side * roadWidth * 0.5f); // Left of current node
            vertices.Add(p1 - side * roadWidth * 0.5f); // Right of current node
            vertices.Add(p2 + side * roadWidth * 0.5f); // Left of next node
            vertices.Add(p2 - side * roadWidth * 0.5f); // Right of next node

            uvs.Add(new Vector2(0, currentLength));
            uvs.Add(new Vector2(1, currentLength));
            uvs.Add(new Vector2(0, currentLength + segmentLength));
            uvs.Add(new Vector2(1, currentLength + segmentLength));

            currentLength += segmentLength;

            int vertIndex = i * 4;
            triangles.Add(vertIndex + 0);
            triangles.Add(vertIndex + 2);
            triangles.Add(vertIndex + 1);

            triangles.Add(vertIndex + 1);
            triangles.Add(vertIndex + 2);
            triangles.Add(vertIndex + 3);
        }

        Mesh mesh = new Mesh();
        mesh.name = "RoadMesh_" + road.name;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    #endregion

    #region Goal Generation
    public void GenerateGoals()
    {
        // First, remove any previously generated goal objects to avoid duplication
        Transform existingContainer = transform.Find(GOAL_CONTAINER_NAME);
        if (existingContainer != null)
        {
            DestroyImmediate(existingContainer.gameObject);
        }

        // Now, generate new ones based on the data in the list
        if (goalPositions == null || goalPositions.Count == 0)
        {
            Debug.Log("No goal positions defined. Skipping goal generation.");
            return;
        }

        GameObject goalContainer = new GameObject(GOAL_CONTAINER_NAME);
        goalContainer.transform.SetParent(transform);

        for (int i = 0; i < goalPositions.Count; i++)
        {
            Vector3 pos = goalPositions[i];
            GameObject goalMarker;

            if (goalMarkerPrefab != null)
            {
                goalMarker = Instantiate(goalMarkerPrefab, pos, Quaternion.identity);
            }
            else
            {
                // Create default primitive if no prefab is assigned
                goalMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                goalMarker.transform.position = pos + Vector3.up * 0.05f; // Adjust default primitive position
                goalMarker.transform.localScale = new Vector3(10, 0.1f, 10); // Default size
                // Use .sharedMaterial in editor scripts to avoid creating new material instances.
                goalMarker.GetComponent<Renderer>().sharedMaterial.color = Color.red;
            }

            goalMarker.name = $"GoalMarker_{i}";
            goalMarker.transform.SetParent(goalContainer.transform);

            // Ensure the goal has a trigger collider and the correct tag
            if (goalMarker.GetComponent<Collider>() == null)
            {
                goalMarker.AddComponent<CapsuleCollider>().isTrigger = true;
            }
            else
            {
                goalMarker.GetComponent<Collider>().isTrigger = true;
            }
            goalMarker.tag = "Goal"; // Assign a tag for easy finding at runtime
        }
    }

    public void ClearGoals()
    {
        goalPositions.Clear(); // Clear the underlying data list

        Transform existingContainer = transform.Find(GOAL_CONTAINER_NAME);
        if (existingContainer != null)
        {
            DestroyImmediate(existingContainer.gameObject);
        }
    }
    #endregion

    #region SaveLoad

    public void SaveCity(string path)
    {
        try
        {
            CityData data = new CityData(this);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            Debug.Log($"City data successfully saved to {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save city data: {e.Message}");
        }
    }

    public void LoadCity(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"File not found at {path}");
                return;
            }

            string json = File.ReadAllText(path);
            CityData data = JsonUtility.FromJson<CityData>(json);

            roads = data.roads;
            goalPositions = data.goalPositions;

            // Regenerate the city visuals from the loaded data
            GenerateAll();
            Debug.Log($"City data successfully loaded from {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load city data: {e.Message}");
        }
    }

    #endregion

    #region Building Generation
    public void GenerateBuildings()
    {
        ClearBuildings();

        if (buildingPrefabs == null || buildingPrefabs.Length == 0)
        {
            Debug.LogWarning("Building prefabs array is empty. Cannot generate buildings.");
            return;
        }

        GameObject buildingContainer = new GameObject(BUILDING_CONTAINER_NAME);
        buildingContainer.transform.SetParent(transform);

        // This method is now simplified as city blocks are not being found.
        // It will place buildings along roads without considering blocks.
        foreach (var road in roads)
        {
            if (road.nodes.Count < 2) continue;

            float roadLength = 0;
            for (int i = 0; i < road.nodes.Count - 1; i++)
            {
                roadLength += Vector3.Distance(road.nodes[i], road.nodes[i + 1]);
            }

            float currentDistance = 0;
            while (currentDistance < roadLength)
            {
                GameObject prefabToPlace = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];
                if (prefabToPlace == null)
                {
                    currentDistance += buildingSpacing; // Advance anyway to avoid infinite loop
                    continue;
                }

                // Get approximate size of the building prefab for placement logic
                // Assuming the prefab has a Renderer component
                Renderer prefabRenderer = prefabToPlace.GetComponentInChildren<Renderer>();
                Vector3 buildingSize = prefabRenderer != null ? prefabRenderer.bounds.size : Vector3.one * buildingSpacing; // Fallback size

                GetPathPositionAndDirection(road, currentDistance, out Vector3 position, out Vector3 direction);

                float maxPlacedWidth = 0f;
                for (int i = 0; i < buildingRowsPerSide; i++)
                {
                    float current_row_offset = (roadWidth * 0.5f) + buildingOffset + (i * (buildingSize.x + rowSpacing));

                    // Try placing on the right side
                    float placedWidthRight = PlaceBuilding(prefabToPlace, position, direction, 1, current_row_offset, buildingContainer.transform);
                    // Try placing on the left side
                    float placedWidthLeft = PlaceBuilding(prefabToPlace, position, direction, -1, current_row_offset, buildingContainer.transform);
                    
                    maxPlacedWidth = Mathf.Max(maxPlacedWidth, placedWidthRight, placedWidthLeft);
                }

                // Advance currentDistance by the width of the placed building plus some random spacing
                float advanceAmount = maxPlacedWidth;
                if (advanceAmount == 0) advanceAmount = buildingSize.z; // If nothing placed, use estimated size

                currentDistance += advanceAmount + Random.Range(buildingRandomSpacing.x, buildingRandomSpacing.y);
                if (currentDistance >= roadLength) break;
            }
        }

        CleanupOverlappingBuildings(buildingContainer.transform);
    }

    public void ClearBuildings()
    {
        Transform existingContainer = transform.Find(BUILDING_CONTAINER_NAME);
        if (existingContainer != null)
        {
            DestroyImmediate(existingContainer.gameObject);
        }
    }

    private void GetPathPositionAndDirection(Road road, float distance, out Vector3 position, out Vector3 direction)
    {
        position = road.nodes[0];
        direction = (road.nodes.Count > 1) ? (road.nodes[1] - road.nodes[0]).normalized : Vector3.forward;

        float traveled = 0;
        for (int i = 0; i < road.nodes.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(road.nodes[i], road.nodes[i + 1]);
            if (traveled + segmentLength >= distance)
            {
                float distIntoSegment = distance - traveled;
                direction = (road.nodes[i + 1] - road.nodes[i]).normalized;
                position = road.nodes[i] + direction * distIntoSegment;
                return;
            }
            traveled += segmentLength;
        }
    }

    private float PlaceBuilding(GameObject prefab, Vector3 position, Vector3 direction, int side, float rowOffset, Transform parent)
    {
        if (prefab == null) return 0f;

        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * side;
        Vector3 buildingPosition = position + perpendicular * rowOffset;

        // Get actual building size from prefab
        Renderer prefabRenderer = prefab.GetComponentInChildren<Renderer>();
        if (prefabRenderer == null)
        {
            Debug.LogWarning($"Prefab {prefab.name} is missing a Renderer component. Cannot determine size for placement.");
            return 0f;
        }
        Vector3 buildingSize = prefabRenderer.bounds.size;
        Vector3 buildingHalfExtents = buildingSize * 0.5f; 
        Quaternion buildingRotation = Quaternion.LookRotation(-perpendicular);

        // Adjust building position to be centered on its base, assuming pivot is at bottom-center
        // buildingPosition.y = position.y; // Initial Y from road centerline, will be corrected by raycast

        // Check for overlaps before placing the building
        // Check against roads and other buildings
        if (Physics.CheckBox(buildingPosition, buildingHalfExtents, buildingRotation, LayerMask.GetMask("Road", "Building")))
        {
            // Overlap detected, do not place building here
            return 0f; // Indicate no building was placed
        }

        // Raycast to find the actual ground height
        // Start raycast from above the building's estimated top
        if (Physics.Raycast(buildingPosition + Vector3.up * buildingSize.y, Vector3.down, out RaycastHit hit, buildingSize.y * 2f + 1f, terrainLayer | LayerMask.GetMask("Road")))
        {
            buildingPosition.y = hit.point.y; // Place building on ground, assuming pivot is at bottom-center
        }
        else
        {
            // Fallback if no ground is found (e.g., outside the terrain)
            return 0f;
        }

        GameObject newBuilding = Instantiate(prefab, buildingPosition, buildingRotation);
        newBuilding.transform.SetParent(parent);
        
        int buildingLayerIndex = LayerMask.NameToLayer("Building");
        if (buildingLayerIndex == -1)
        {
            Debug.LogWarning("Layer 'Building' not found. Please create it in Edit -> Project Settings -> Tags and Layers.");
            newBuilding.layer = 0; // Assign to default layer
        }
        else
        {
            newBuilding.layer = buildingLayerIndex; // Assign building to Building layer
        }

#if UNITY_EDITOR
        // Mark the new building as Navigation Static for NavMesh baking
        GameObjectUtility.SetStaticEditorFlags(newBuilding, StaticEditorFlags.NavigationStatic);
#endif
        
        return buildingSize.z; // Return the length of the building along the road direction
    }

    private void CleanupOverlappingBuildings(Transform buildingContainer)
    {
        List<GameObject> buildingsToDestroy = new List<GameObject>();
        // Get all currently placed buildings
        List<GameObject> allPlacedBuildings = new List<GameObject>();
        foreach (Transform child in buildingContainer)
        {
            allPlacedBuildings.Add(child.gameObject);
        }

        int overlapMask = LayerMask.GetMask("Road", "Building");

        foreach (GameObject building in allPlacedBuildings)
        {
            if (building == null) continue; // Might have been destroyed by a previous check

            Collider buildingCollider = building.GetComponent<Collider>();
            if (buildingCollider == null) continue;

            // Check for overlaps with roads OR other buildings
            Collider[] overlaps = Physics.OverlapBox(building.transform.position, buildingCollider.bounds.extents, building.transform.rotation, overlapMask);
            
            bool isOverlappingWithOtherObject = false;
            foreach (Collider overlapCollider in overlaps)
            {
                // If the overlapping collider is not part of the building itself, then it's a real overlap.
                if (overlapCollider.gameObject != building)
                {
                    isOverlappingWithOtherObject = true;
                    break;
                }
            }

            if (isOverlappingWithOtherObject)
            {
                buildingsToDestroy.Add(building);
            }
        }

        foreach (GameObject building in buildingsToDestroy)
        {
            DestroyImmediate(building);
        }
    }
    #endregion
}
