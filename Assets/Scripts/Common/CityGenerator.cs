using UnityEngine;
using Unity.AI.Navigation;

public class CityGenerator : MonoBehaviour
{
    public static event System.Action OnCityGenerated;

    [Header("City Layout")]
    [Tooltip("Size of the city grid in the X direction.")]
    public int citySizeX = 20;
    [Tooltip("Size of the city grid in the Z direction.")]
    public int citySizeZ = 20;
    [Tooltip("The size of each city block. Roads and buildings will conform to this size.")]
    public float blockSize = 25f;
    [Tooltip("Determines the road network. E.g., a value of 4 means a road every 4th block.")]
    public int roadInterval = 4;

    [Header("Building Properties")]
    [Tooltip("The minimum height of a building.")]
    public float minBuildingHeight = 10f;
    [Tooltip("The maximum height of a building.")]
    public float maxBuildingHeight = 40f;
    [Tooltip("Material to apply to the generated buildings.")]
    public Material buildingMaterial;

    [Header("Terrain Settings")]
    [Tooltip("Enable to generate hilly terrain.")]
    public bool generateHillyTerrain = true;
    [Tooltip("Maximum height variation of the terrain.")]
    public float terrainHeight = 10f;
    [Tooltip("Scale of the terrain noise. Smaller values create smoother, larger hills.")]
    public float terrainScale = 20f;
    [Tooltip("The size of the empty margin around the city in grid units.")]
    public int marginSize = 5;
    [Tooltip("Material to apply to the generated ground blocks.")]
    public Material groundMaterial;
    [Tooltip("The name of the layer to assign to the generated terrain.")]
    public string terrainLayerName = "Default";

    [Header("NavMesh")]
    [Tooltip("Reference to the NavMeshSurface component for runtime baking.")]
    public NavMeshSurface navMeshSurface;

    private float perlinOffsetX;
    private float perlinOffsetY;

    void Start()
    {
        Debug.Log("CityGenerator: Start() 메소드가 호출되었습니다. 도시 생성을 시작합니다.");
        perlinOffsetX = Random.Range(0f, 1000f);
        perlinOffsetY = Random.Range(0f, 1000f);
        GenerateCity();
    }

    public void GenerateCity()
    {
        if (citySizeX <= 0 || citySizeZ <= 0 || blockSize <= 0)
        {
            Debug.LogError("CityGenerator: City Size 또는 Block Size가 0 이하로 설정되어 도시를 생성할 수 없습니다. 인스펙터 값을 확인해주세요.");
            return;
        }

        Debug.Log($"CityGenerator: 사이즈 {citySizeX}x{citySizeZ}의 도시 생성을 시작합니다. (마진 {marginSize} 포함)");

        GameObject cityParent = new GameObject("Generated City");
        float[,] heightMap = GenerateTerrainMesh(cityParent.transform);

        int buildingsSpawned = 0;
        // Building placement loop considers the margin
        for (int x = 0; x < citySizeX; x++)
        {
            for (int z = 0; z < citySizeZ; z++)
            {
                if (x % roadInterval != 0 && z % roadInterval != 0)
                {
                    // Get height from the heightmap, offsetting by the margin
                    float groundHeight = heightMap[x + marginSize, z + marginSize];
                    // Spawn building, offsetting its position by the margin
                    SpawnBuilding(x + marginSize, z + marginSize, groundHeight, cityParent.transform);
                    buildingsSpawned++;
                }
            }
        }

        if (buildingsSpawned > 0)
        {
            Debug.Log($"CityGenerator: 총 {buildingsSpawned}개의 빌딩 생성을 완료했습니다.");
        }
        else
        {
            Debug.LogWarning("CityGenerator: 생성된 빌딩이 없습니다. Road Interval이 너무 작거나 City Size가 작지 않은지 확인해주세요.");
        }

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("CityGenerator: NavMesh를 성공적으로 빌드했습니다.");
        }
        else
        {
            Debug.LogWarning("CityGenerator: NavMeshSurface가 할당되지 않았습니다. NavMesh를 빌드할 수 없습니다.");
        }

        OnCityGenerated?.Invoke();
    }

    private float[,] GenerateTerrainMesh(Transform parent)
    {
        GameObject terrainObject = new GameObject("Generated Terrain");
        terrainObject.transform.SetParent(parent);

        int layer = LayerMask.NameToLayer(terrainLayerName);
        if (layer == -1)
        {
            Debug.LogWarning($"CityGenerator: Layer named '{terrainLayerName}' not found. Assigning terrain to default layer (0). Please create the layer in Edit > Project Settings > Tags and Layers.", this);
            terrainObject.layer = 0; // Default layer
        }
        else
        {
            terrainObject.layer = layer;
        }

        MeshFilter meshFilter = terrainObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = terrainObject.AddComponent<MeshRenderer>();
        meshRenderer.material = groundMaterial;

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Terrain Mesh";

        int totalSizeX = citySizeX + marginSize * 2;
        int totalSizeZ = citySizeZ + marginSize * 2;
        int vertSizeX = totalSizeX + 1;
        int vertSizeZ = totalSizeZ + 1;

        Vector3[] vertices = new Vector3[vertSizeX * vertSizeZ];
        Vector2[] uvs = new Vector2[vertices.Length];
        float[,] heightMap = new float[vertSizeX, vertSizeZ];

        for (int z = 0; z < vertSizeZ; z++)
        {
            for (int x = 0; x < vertSizeX; x++)
            {
                bool isMargin = x < marginSize || x >= citySizeX + marginSize || z < marginSize || z >= citySizeZ + marginSize;
                float yPos = 0;

                if (!isMargin && generateHillyTerrain)
                {
                    float cityX = x - marginSize;
                    float cityZ = z - marginSize;
                    float perlinX = (cityX + perlinOffsetX) / terrainScale;
                    float perlinZ = (cityZ + perlinOffsetY) / terrainScale;
                    yPos = Mathf.PerlinNoise(perlinX, perlinZ) * terrainHeight;
                }
                
                heightMap[x, z] = yPos;
                vertices[z * vertSizeX + x] = new Vector3(x * blockSize, yPos, z * blockSize);
                uvs[z * vertSizeX + x] = new Vector2((float)x / totalSizeX, (float)z / totalSizeZ);
            }
        }

        int[] triangles = new int[totalSizeX * totalSizeZ * 6];
        int vertIndex = 0;
        int triIndex = 0;
        for (int z = 0; z < totalSizeZ; z++)
        {
            for (int x = 0; x < totalSizeX; x++)
            {
                triangles[triIndex + 0] = vertIndex + 0;
                triangles[triIndex + 1] = vertIndex + vertSizeX + 0;
                triangles[triIndex + 2] = vertIndex + 1;
                triangles[triIndex + 3] = vertIndex + 1;
                triangles[triIndex + 4] = vertIndex + vertSizeX + 0;
                triangles[triIndex + 5] = vertIndex + vertSizeX + 1;

                vertIndex++;
                triIndex += 6;
            }
            vertIndex++;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        terrainObject.AddComponent<MeshCollider>();

        return heightMap;
    }

    private void SpawnBuilding(int x, int z, float groundHeight, Transform parent)
    {
        float xPos = x * blockSize;
        float zPos = z * blockSize;

        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = $"Building_{x}_{z}";
        building.transform.SetParent(parent);

        float buildingHeight = UnityEngine.Random.Range(minBuildingHeight, maxBuildingHeight);
        building.transform.localScale = new Vector3(blockSize * 0.5f, buildingHeight, blockSize * 0.5f);
        building.transform.position = new Vector3(xPos, groundHeight + buildingHeight / 2, zPos);

        if (buildingMaterial != null)
        {
            building.GetComponent<Renderer>().material = buildingMaterial;
        }
    }
}
