using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class BlockType
{
    public string name = "Default Block";
    [Tooltip("블록 한 변에 들어갈 건물 수. 1=1x1, 2=2x2 등")]
    [Min(1)]
    public int buildingsPerBlockSide = 1;
    [Tooltip("이 블록 타입이 선택될 가중치. 높을수록 선택될 확률이 높아집니다.")]
    [Range(1, 100)]
    public int weight = 10;
}

public class CityGenerator : MonoBehaviour
{
    public static event System.Action OnCityGenerated;

    [Header("City Layout")]
    [Tooltip("도시 그리드의 X축 크기 (도로 간격의 배수).")]
    public int citySizeX = 20;
    [Tooltip("도시 그리드의 Z축 크기 (도로 간격의 배수).")]
    public int citySizeZ = 20;
    [Tooltip("각 도시 셀의 크기.")]
    public float blockSize = 25f;
    [Tooltip("도로 네트워크를 결정합니다. 예: 4는 4번째 셀마다 도로를 의미합니다.")]
    public int roadInterval = 4;
    [Tooltip("도시 주변의 빈 여백 크기 (셀 단위).")]
    public int marginSize = 5;

    [Header("Block General Properties")]
    [Tooltip("블록이 생성될 확률 (0.0 ~ 1.0).")]
    [Range(0f, 1f)]
    public float blockDensity = 0.9f;
    [Tooltip("블록 내 건물 사이의 간격 (0.0 ~ 0.5).")]
    [Range(0f, 0.5f)]
    public float buildingPadding = 0.1f;
    [Tooltip("블록 위치의 흔들림 정도. 값이 클수록 블록이 더 많이 어긋납니다.")]
    public float blockPositionJitter = 2.5f;
    [Tooltip("블록이 회전할 최대 각도 (도).")]
    [Range(0f, 45f)]
    public float blockRotationRange = 10f;
    [Tooltip("회전 시 충돌을 피하기 위한 안전 마진. 클수록 블록이 작아집니다.")]
    [Range(0f, 0.5f)]
    public float rotationSafetyMargin = 0.2f;


    [Header("Block Variety")]
    [Tooltip("생성할 블록의 종류와 비율(가중치)을 설정합니다.")]
    public List<BlockType> blockTypes;
    private int totalWeight;

    [Header("Building Properties")]
    [Tooltip("건물의 최소 높이.")]
    public float minBuildingHeight = 10f;
    [Tooltip("건물의 최대 높이.")]
    public float maxBuildingHeight = 40f;
    [Tooltip("생성된 건물에 적용할 재질.")]
    public Material buildingMaterial;

    [Header("Diagonal Roads")]
    [Tooltip("생성할 주요 대각선 도로의 수.")]
    public int numberOfDiagonalRoads = 2;
    [Tooltip("대각선 도로의 너비.")]
    public float diagonalRoadWidth = 20f;

    [Header("NavMesh")]
    [Tooltip("런타임 베이킹을 위한 NavMeshSurface 컴포넌트 참조.")]
    public NavMeshSurface navMeshSurface;
    
    IEnumerator Start()
    {
        PrepareBlockTypes();
        Debug.Log("CityGenerator: Start() 메소드가 호출되었습니다. 도시 생성을 시작합니다.");
        yield return StartCoroutine(GenerateCity());
    }

    private void PrepareBlockTypes()
    {
        totalWeight = 0;
        if (blockTypes == null || blockTypes.Count == 0)
        {
            Debug.LogWarning("CityGenerator: Block Types가 설정되지 않았습니다. 기본값으로 1x1 블록을 생성합니다.");
            blockTypes = new List<BlockType> { new BlockType() };
        }

        foreach (var blockType in blockTypes)
        {
            totalWeight += blockType.weight;
        }
    }

    private BlockType GetRandomBlockType()
    {
        if (totalWeight == 0) return blockTypes[0];

        int randomWeight = Random.Range(0, totalWeight);
        foreach (var blockType in blockTypes)
        {
            if (randomWeight < blockType.weight)
            {
                return blockType;
            }
            randomWeight -= blockType.weight;
        }
        return blockTypes[blockTypes.Count - 1];
    }

    private IEnumerator GenerateCity()
    {
        if (citySizeX <= 0 || citySizeZ <= 0 || blockSize <= 0)
        {
            Debug.LogError("CityGenerator: City Size 또는 Block Size가 0 이하로 설정되어 도시를 생성할 수 없습니다. 인스펙터 값을 확인해주세요.");
            yield break;
        }

        GameObject cityParent = new GameObject("Generated City");
        int totalBuildingsSpawned = 0;

        for (int blockX = 0; blockX < citySizeX / roadInterval; blockX++)
        {
            for (int blockZ = 0; blockZ < citySizeZ / roadInterval; blockZ++)
            {
                if (Random.value < blockDensity)
                {
                    BlockType selectedType = GetRandomBlockType();
                    totalBuildingsSpawned += SpawnBuildingsInBlock(blockX, blockZ, selectedType, cityParent.transform);
                }
            }
        }
        
        if (totalBuildingsSpawned > 0)
        {
            Debug.Log($"CityGenerator: 총 {totalBuildingsSpawned}개의 빌딩 생성을 완료했습니다.");
            Physics.SyncTransforms();
            CarveDiagonalRoads(cityParent.transform);
        }
        else
        {
            Debug.LogWarning("CityGenerator: 생성된 빌딩이 없습니다. 설정 값을 확인해주세요.");
        }

        yield return null;

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

    private int SpawnBuildingsInBlock(int blockX, int blockZ, BlockType blockType, Transform parent)
    {
        float totalBlockSize = roadInterval * blockSize;
        float blockCenterX = (blockX * totalBlockSize) + (totalBlockSize / 2f) + (marginSize * blockSize);
        float blockCenterZ = (blockZ * totalBlockSize) + (totalBlockSize / 2f) + (marginSize * blockSize);
        Vector3 blockCenter = new Vector3(blockCenterX, 0, blockCenterZ);

        blockCenter.x += Random.Range(-blockPositionJitter, blockPositionJitter);
        blockCenter.z += Random.Range(-blockPositionJitter, blockPositionJitter);

        Quaternion blockRotation = Quaternion.Euler(0, Random.Range(-blockRotationRange, blockRotationRange), 0);

        float blockInnerSize = (roadInterval - 1) * blockSize;
        float safeAreaSize = blockInnerSize * (1 - rotationSafetyMargin);

        int buildingsPerSide = blockType.buildingsPerBlockSide;
        float buildingSlotSize = safeAreaSize / buildingsPerSide;
        float actualBuildingSize = buildingSlotSize * (1 - (buildingPadding * 2));
        float paddingOffset = buildingSlotSize * buildingPadding;

        int spawnedCount = 0;
        for (int x = 0; x < buildingsPerSide; x++)
        {
            for (int z = 0; z < buildingsPerSide; z++)
            {
                float localX = (x * buildingSlotSize) + paddingOffset + (actualBuildingSize / 2f) - (safeAreaSize / 2f);
                float localZ = (z * buildingSlotSize) + paddingOffset + (actualBuildingSize / 2f) - (safeAreaSize / 2f);
                Vector3 localPos = new Vector3(localX, 0, localZ);

                Vector3 rotatedLocalPos = blockRotation * localPos;

                Vector3 finalPos = blockCenter + rotatedLocalPos;
                
                SpawnSingleBuilding(finalPos, actualBuildingSize, parent, blockRotation);
                spawnedCount++;
            }
        }
        return spawnedCount;
    }

    private void SpawnSingleBuilding(Vector3 centerPosition, float size, Transform parent, Quaternion blockRotation)
    {
        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.transform.SetParent(parent);

        building.layer = LayerMask.NameToLayer("Buildings");

        Quaternion individualRotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
        building.transform.rotation = blockRotation * individualRotation;

        float buildingHeight = Random.Range(minBuildingHeight, maxBuildingHeight);
        float width = size * Random.Range(0.85f, 1.0f);
        float depth = size * Random.Range(0.85f, 1.0f);

        building.transform.localScale = new Vector3(width, buildingHeight, depth);
        centerPosition.y = buildingHeight / 2f;
        building.transform.position = centerPosition;

        if (buildingMaterial != null)
        {
            building.GetComponent<Renderer>().material = buildingMaterial;
        }
    }

    private void CarveDiagonalRoads(Transform parent)
    {
        Debug.Log($"CityGenerator: {numberOfDiagonalRoads}개의 대각선 도로 생성을 시작합니다.");
        
        HashSet<GameObject> buildingsToDestroy = new HashSet<GameObject>();

        for (int i = 0; i < numberOfDiagonalRoads; i++)
        {
            int startEdge = Random.Range(0, 4);
            int endEdge = (startEdge + Random.Range(1, 4)) % 4;

            Vector3 startPoint = GetPointOnEdge(startEdge);
            Vector3 endPoint = GetPointOnEdge(endEdge);

            Debug.DrawLine(startPoint, endPoint, Color.red, 10f);

            Vector3 path = endPoint - startPoint;
            float distance = path.magnitude;
            Vector3 direction = path.normalized;

            for (float step = 0; step < distance; step += diagonalRoadWidth / 4)
            {
                Vector3 currentPos = startPoint + direction * step;
                currentPos.y = (minBuildingHeight + maxBuildingHeight) / 4;

                Collider[] colliders = Physics.OverlapSphere(currentPos, diagonalRoadWidth / 2f);

                foreach (var collider in colliders)
                {
                    if (collider.transform.parent == parent)
                    {
                        buildingsToDestroy.Add(collider.gameObject);
                    }
                }
            }
        }

        foreach (var building in buildingsToDestroy)
        {
            if (building != null)
            {
                Destroy(building);
            }
        }
        Debug.Log($"CityGenerator: {buildingsToDestroy.Count}개의 건물을 제거하여 대각선 도로를 생성했습니다.");
    }

    private Vector3 GetPointOnEdge(int edge)
    {
        float worldXMin = marginSize * blockSize;
        float worldXMax = (citySizeX + marginSize) * blockSize;
        float worldZMin = marginSize * blockSize;
        float worldZMax = (citySizeZ + marginSize) * blockSize;

        float randomX = Random.Range(worldXMin, worldXMax);
        float randomZ = Random.Range(worldZMin, worldZMax);

        switch (edge)
        {
            case 0: return new Vector3(worldXMin, 0, randomZ);
            case 1: return new Vector3(worldXMax, 0, randomZ);
            case 2: return new Vector3(randomX, 0, worldZMin);
            case 3: return new Vector3(randomX, 0, worldZMax);
        }
        return Vector3.zero;
    }

    public Bounds GetCityBounds()
    {
        float worldXMin = marginSize * blockSize;
        float worldXMax = (citySizeX + marginSize) * blockSize;
        float worldZMin = marginSize * blockSize;
        float worldZMax = (citySizeZ + marginSize) * blockSize;

        Vector3 center = new Vector3((worldXMin + worldXMax) / 2f, 50f, (worldZMin + worldZMax) / 2f);
        Vector3 size = new Vector3(worldXMax - worldXMin, 100f, worldZMax - worldZMin);
        return new Bounds(center, size);
    }
}
