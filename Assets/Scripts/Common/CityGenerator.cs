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
    [Tooltip("The size of the empty margin around the city in grid units.")]
    public int marginSize = 5;

    [Header("Building Properties")]
    [Tooltip("The minimum height of a building.")]
    public float minBuildingHeight = 10f;
    [Tooltip("The maximum height of a building.")]
    public float maxBuildingHeight = 40f;
    [Tooltip("Material to apply to the generated buildings.")]
    public Material buildingMaterial;

    [Header("NavMesh")]
    [Tooltip("Reference to the NavMeshSurface component for runtime baking.")]
    public NavMeshSurface navMeshSurface;
    
    void Start()
    {
        Debug.Log("CityGenerator: Start() 메소드가 호출되었습니다. 도시 생성을 시작합니다.");
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

        int buildingsSpawned = 0;
        for (int x = 0; x < citySizeX; x++)
        {
            for (int z = 0; z < citySizeZ; z++)
            {
                bool isRoad = x % roadInterval == 0 || z % roadInterval == 0;
                if (!isRoad)
                {
                    // Ground height is 0 for a flat terrain
                    SpawnBuilding(x, z, 0f, cityParent.transform);
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
