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
    [Tooltip("Base scale of the terrain noise. Smaller values create smoother, larger features.")]
    public float terrainScale = 20f;
    [Tooltip("The size of the empty margin around the city in grid units.")]
    public int marginSize = 5;
    [Tooltip("Material to apply to the generated ground blocks.")]
    public Material groundMaterial;
    [Tooltip("Physic Material to apply to the generated ground blocks.")]
    public PhysicsMaterial groundPhysicMaterial;
    [Tooltip("The name of the layer to assign to the generated terrain.")]
    public string terrainLayerName = "Default";
    [Tooltip("Number of smoothing iterations for roads.")]
    [Range(0, 5)]
    public int roadSmoothingIterations = 2;

    [Header("Fractal Noise Settings")]
    [Tooltip("Number of noise layers to combine. More octaves = more detail.")]
    [Range(1, 8)]
    public int octaves = 4;
    [Tooltip("Multiplier for frequency per octave. >1")]
    public float lacunarity = 2.0f;
    [Tooltip("Multiplier for amplitude per octave. <1")]
    [Range(0.0f, 1.0f)]
    public float persistence = 0.5f;

    [Header("Terrain Coloring")]
    [Tooltip("Controls the color of the terrain based on its height. Left side is lowest, right side is highest.")]
    public Gradient groundColorGradient;

    [Header("NavMesh")]
    [Tooltip("Reference to the NavMeshSurface component for runtime baking.")]
    public NavMeshSurface navMeshSurface;

    private float perlinOffsetX;
    private float perlinOffsetY;

    void Awake()
    {
        // Initialize a default gradient if one isn't set
        if (groundColorGradient == null || groundColorGradient.colorKeys.Length == 0)
        {
            groundColorGradient = new Gradient();
            groundColorGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.3f, 0.6f, 0.2f), 0.0f), new GradientColorKey(new Color(0.6f, 0.5f, 0.3f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
            );
        }
    }

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

        // 1. Generate base heightmap from Perlin noise
        float[,] heightMap = GenerateHeightMap();

        // 2. Flatten areas for buildings
        FlattenBuildingAreas(heightMap);

        // 3. Smooth the roads
        SmoothRoads(heightMap);

        // 4. Build the actual terrain mesh from the modified heightmap
        BuildTerrainFromHeightMap(heightMap, cityParent.transform);


        int buildingsSpawned = 0;
        for (int x = 0; x < citySizeX; x++)
        {
            for (int z = 0; z < citySizeZ; z++)
            {
                bool isRoad = x % roadInterval == 0 || z % roadInterval == 0;
                if (!isRoad)
                {
                    // Get the flattened ground height for the building plot
                    float groundHeight = GetBuildingPlotHeight(heightMap, x, z);
                    SpawnBuilding(x, z, groundHeight, cityParent.transform);
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

    private float[,] GenerateHeightMap()
    {
        int vertSizeX = citySizeX + marginSize * 2 + 1;
        int vertSizeZ = citySizeZ + marginSize * 2 + 1;
        float[,] heightMap = new float[vertSizeX, vertSizeZ];

        for (int z = 0; z < vertSizeZ; z++)
        {
            for (int x = 0; x < vertSizeX; x++)
            {
                bool isMargin = x < marginSize || x >= citySizeX + marginSize || z < marginSize || z >= citySizeZ + marginSize;
                float yPos = 0;

                if (!isMargin && generateHillyTerrain)
                {
                    float totalNoise = 0;
                    float frequency = 1.0f;
                    float amplitude = 1.0f;
                    float maxAmplitude = 0; // To normalize the result

                    for (int i = 0; i < octaves; i++)
                    {
                        float sampleX = (x - marginSize) / terrainScale * frequency + perlinOffsetX;
                        float sampleZ = (z - marginSize) / terrainScale * frequency + perlinOffsetY;

                        // PerlinNoise returns [0,1]. Let's shift it to [-1, 1] to allow for valleys.
                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ) * 2 - 1;

                        totalNoise += perlinValue * amplitude;

                        maxAmplitude += amplitude;

                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }

                    // Normalize the noise to be between 0 and 1
                    float normalizedNoise = (totalNoise / maxAmplitude + 1) / 2f;
                    yPos = normalizedNoise * terrainHeight;
                }
                heightMap[x, z] = yPos;
            }
        }
        return heightMap;
    }

    private void FlattenBuildingAreas(float[,] heightMap)
    {
        if (!generateHillyTerrain) return;

        for (int z = 0; z < citySizeZ; z++)
        {
            for (int x = 0; x < citySizeX; x++)
            {
                bool isRoad = x % roadInterval == 0 || z % roadInterval == 0;
                if (!isRoad)
                {
                    int mapX = x + marginSize;
                    int mapZ = z + marginSize;

                    // Get the four corner vertices of this building plot
                    float h1 = heightMap[mapX, mapZ];
                    float h2 = heightMap[mapX + 1, mapZ];
                    float h3 = heightMap[mapX, mapZ + 1];
                    float h4 = heightMap[mapX + 1, mapZ + 1];

                    // Calculate the average height
                    float averageHeight = (h1 + h2 + h3 + h4) / 4f;

                    // Set all four corners to the average height to flatten the plot
                    heightMap[mapX, mapZ] = averageHeight;
                    heightMap[mapX + 1, mapZ] = averageHeight;
                    heightMap[mapX, mapZ + 1] = averageHeight;
                    heightMap[mapX + 1, mapZ + 1] = averageHeight;
                }
            }
        }
    }

    private void SmoothRoads(float[,] heightMap)
    {
        if (!generateHillyTerrain || roadSmoothingIterations <= 0) return;

        int vertSizeX = citySizeX + marginSize * 2 + 1;
        int vertSizeZ = citySizeZ + marginSize * 2 + 1;
        
        for(int i = 0; i < roadSmoothingIterations; i++)
        {
            float[,] tempHeightMap = (float[,])heightMap.Clone();

            for (int z = marginSize; z < citySizeZ + marginSize; z++)
            {
                for (int x = marginSize; x < citySizeX + marginSize; x++)
                {
                    int cityX = x - marginSize;
                    int cityZ = z - marginSize;
                    bool isRoad = cityX % roadInterval == 0 || cityZ % roadInterval == 0;

                    if (isRoad)
                    {
                        float heightSum = 0;
                        int neighborCount = 0;

                        // 3x3 kernel
                        for (int nz = -1; nz <= 1; nz++)
                        {
                            for (int nx = -1; nx <= 1; nx++)
                            {
                                int neighborX = x + nx;
                                int neighborZ = z + nz;

                                if (neighborX >= 0 && neighborX < vertSizeX && neighborZ >= 0 && neighborZ < vertSizeZ)
                                {
                                    heightSum += tempHeightMap[neighborX, neighborZ];
                                    neighborCount++;
                                }
                            }
                        }

                        if (neighborCount > 0)
                        {
                            heightMap[x, z] = heightSum / neighborCount;
                        }
                    }
                }
            }
        }
    }


    private void BuildTerrainFromHeightMap(float[,] heightMap, Transform parent)
    {
        GameObject terrainObject = new GameObject("Generated Terrain");
        terrainObject.transform.SetParent(parent);

        int layer = LayerMask.NameToLayer(terrainLayerName);
        if (layer == -1)
        {
            Debug.LogWarning($"CityGenerator: Layer named '{terrainLayerName}' not found. Assigning to default.", this);
            terrainObject.layer = 0;
        }
        else
        {
            terrainObject.layer = layer;
        }

        MeshFilter meshFilter = terrainObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = terrainObject.AddComponent<MeshRenderer>();
        meshRenderer.material = groundMaterial;

        // 1. Create and populate the Mesh
        Mesh mesh = new Mesh();
        mesh.name = "Procedural Terrain Mesh";

        int vertSizeX = heightMap.GetLength(0);
        int vertSizeZ = heightMap.GetLength(1);
        int totalSizeX = vertSizeX - 1;
        int totalSizeZ = vertSizeZ - 1;

        Vector3[] vertices = new Vector3[vertSizeX * vertSizeZ];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];

        for (int z = 0; z < vertSizeZ; z++)
        {
            for (int x = 0; x < vertSizeX; x++)
            {
                int index = z * vertSizeX + x;
                float yPos = heightMap[x, z];
                vertices[index] = new Vector3(x * blockSize, yPos, z * blockSize);
                uvs[index] = new Vector2((float)x / totalSizeX, (float)z / totalSizeZ);

                // Calculate and assign vertex color based on height
                float normalizedHeight = 0;
                if (terrainHeight > 0)
                {
                    normalizedHeight = Mathf.Clamp01(yPos / terrainHeight);
                }
                colors[index] = groundColorGradient.Evaluate(normalizedHeight);
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
        mesh.colors = colors;
        mesh.RecalculateNormals();

        // 2. Assign the Mesh to MeshFilter
        meshFilter.mesh = mesh;

        // 3. Add and configure MeshCollider
        MeshCollider meshCollider = terrainObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh; // Assign the mesh to the collider
        if (groundPhysicMaterial != null)
        {
            meshCollider.material = groundPhysicMaterial;
        }
    }
    
    private float GetBuildingPlotHeight(float[,] heightMap, int cityX, int cityZ)
    {
        // Since the area is flattened, we can just take the height from one corner.
        return heightMap[cityX + marginSize, cityZ + marginSize];
    }

    private void SpawnBuilding(int cityX, int cityZ, float groundHeight, Transform parent)
    {
        // We now use cityX and cityZ, and offset by margin inside the position calculation
        float xPos = (cityX + marginSize) * blockSize;
        float zPos = (cityZ + marginSize) * blockSize;

        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = $"Building_{cityX}_{cityZ}";
        building.transform.SetParent(parent);

        float buildingHeight = UnityEngine.Random.Range(minBuildingHeight, maxBuildingHeight);
        
        // Center the building on the block
        float buildingXPos = xPos + (blockSize / 2f);
        float buildingZPos = zPos + (blockSize / 2f);

        building.transform.localScale = new Vector3(blockSize * 0.8f, buildingHeight, blockSize * 0.8f); // Slightly smaller than block
        building.transform.position = new Vector3(buildingXPos, groundHeight + buildingHeight / 2, buildingZPos);

        if (buildingMaterial != null)
        {
            building.GetComponent<Renderer>().material = buildingMaterial;
        }
    }
}
