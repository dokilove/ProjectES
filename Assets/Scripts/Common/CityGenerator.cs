using UnityEngine;
using Unity.AI.Navigation;

public class CityGenerator : MonoBehaviour
{
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

        Debug.Log($"CityGenerator: 사이즈 {citySizeX}x{citySizeZ}의 도시 생성을 시작합니다.");

        GameObject cityParent = new GameObject("Generated City");

        int buildingsSpawned = 0;
        for (int x = 0; x < citySizeX; x++)
        {
            for (int z = 0; z < citySizeZ; z++)
            {
                if (x % roadInterval == 0 || z % roadInterval == 0)
                {
                    continue;
                }
                
                SpawnBuilding(x, z, cityParent.transform);
                buildingsSpawned++;
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
    }

    private void SpawnBuilding(int x, int z, Transform parent)
    {
        float xPos = x * blockSize;
        float zPos = z * blockSize;

        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = $"Building_{x}_{z}";
        building.transform.SetParent(parent);

        float buildingHeight = UnityEngine.Random.Range(minBuildingHeight, maxBuildingHeight);
        // X와 Z 스케일을 줄여서 도로 폭을 확보합니다.
        building.transform.localScale = new Vector3(blockSize * 0.5f, buildingHeight, blockSize * 0.5f);
        building.transform.position = new Vector3(xPos, buildingHeight / 2, zPos);

        if (buildingMaterial != null)
        {
            building.GetComponent<Renderer>().material = buildingMaterial;
        }
    }
}