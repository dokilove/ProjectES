using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 4.2f;
    public float attackRange = 1.5f;

    [Header("Field of View")]
    public float viewRadius = 10f;
    [Range(0, 360)]
    public float viewAngle = 90f;
    [Tooltip("The material for the base FOV mesh.")]
    public Material baseFovMaterial;
    [Tooltip("The MeshFilter of the child object used for the detection FOV.")]
    public MeshFilter detectionFovMeshFilter;

    [Header("Detection")]
    [Tooltip("Time in seconds for the detection mesh to fully expand.")]
    public float detectionDuration = 1.0f;
    [Tooltip("How fast the enemy turns to face the player while chasing.")]
    public float rotationSpeed = 10f;

    [Tooltip("한 번 추적을 시작하면 절대 멈추지 않을지 여부입니다.")]
    public bool neverStopChasing = false;

    private NavMeshAgent agent;

    // Base FOV
    private Mesh baseFovMesh;
    private MeshFilter baseFovMeshFilter;

    // Detection FOV
    private Mesh detectionFovMesh;
    
    private float detectionProgress; // 0 = not detected, 1 = fully detected

    public enum EnemyState { Idle, Detecting, Chasing }
    private EnemyState currentState = EnemyState.Idle;
    
    private Vector3 lastKnownPlayerPosition;
    public float chaseDurationAfterLostSight = 3f;
    private float chaseTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("EnemyAI: NavMeshAgent component not found.", this);
            enabled = false;
            return;
        }

        SetupBaseFOV();
        SetupDetectionFOV();

        agent.speed = moveSpeed;
        FindPlayer();
    }

    void Update()
    {
        if (target == null)
        {
            FindPlayer();
            if (target == null)
            {
                // If still no target, clear meshes and do nothing
                if (baseFovMesh != null) baseFovMesh.Clear();
                if (detectionFovMesh != null) detectionFovMesh.Clear();
                return;
            }
        }

        bool playerInFOV = CheckFOV();
        HandleStateMachine(playerInFOV);

        DrawBaseFOVMesh();
        DrawDetectionFOVMesh();
    }

    private void SetupBaseFOV()
    {
        baseFovMeshFilter = GetComponent<MeshFilter>();
        if (baseFovMeshFilter == null)
        {
            baseFovMeshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer baseFovMeshRenderer = GetComponent<MeshRenderer>();
        if (baseFovMeshRenderer == null)
        {
            baseFovMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        baseFovMesh = new Mesh { name = "BaseFOVMesh" };
        baseFovMeshFilter.mesh = baseFovMesh;

        if (baseFovMaterial != null)
        {
            baseFovMeshRenderer.material = baseFovMaterial;
        }
        else
        {
            Debug.LogWarning("EnemyAI: Base FOV Material is not set.", this);
        }
    }

    private void SetupDetectionFOV()
    {
        if (detectionFovMeshFilter == null)
        {
            Debug.LogError("EnemyAI: Detection FOV Mesh Filter is not assigned in the Inspector!", this);
            enabled = false;
            return;
        }
        detectionFovMesh = new Mesh { name = "DetectionFOVMesh" };
        detectionFovMeshFilter.mesh = detectionFovMesh;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
        }
    }

    private void HandleStateMachine(bool playerInFOV)
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                if (playerInFOV)
                {
                    currentState = EnemyState.Detecting;
                    Debug.Log("Enemy: Player spotted, beginning detection...");
                    lastKnownPlayerPosition = target.position;
                }
                else
                {
                    // Slowly decrease detection progress if player is not in sight
                    detectionProgress = Mathf.Max(0, detectionProgress - (1 / detectionDuration) * Time.deltaTime);
                }
                break;

            case EnemyState.Detecting:
                if (playerInFOV)
                {
                    lastKnownPlayerPosition = target.position;
                    // Increase detection progress
                    detectionProgress = Mathf.Min(1, detectionProgress + (1 / detectionDuration) * Time.deltaTime);

                    // Check if the expanding detection radius has reached the player
                    float currentDetectionRadius = viewRadius * detectionProgress;
                    if (Vector3.Distance(transform.position, target.position) <= currentDetectionRadius)
                    {
                        currentState = EnemyState.Chasing;
                        lastKnownPlayerPosition = target.position;
                        chaseTimer = chaseDurationAfterLostSight;
                        Debug.Log("Enemy: Detection complete, starting chase!");
                    }
                }
                else
                {
                    // If player is lost during detection, go back to idle to handle decay
                    currentState = EnemyState.Idle;
                    Debug.Log("Enemy: Player lost during detection, returning to idle.");
                }
                break;

            case EnemyState.Chasing:
                // 'neverStopChasing' 모드이거나 플레이어가 시야에 있으면, 항상 최신 위치로 추적
                if (neverStopChasing || playerInFOV)
                {
                    lastKnownPlayerPosition = target.position;
                    chaseTimer = chaseDurationAfterLostSight; // 타이머 리셋 (기존 로직을 위해)
                    if (agent.isOnNavMesh) agent.SetDestination(target.position);

                    // Smoothly rotate to face the player
                    RotateTowardsPosition(target.position);
                }
                else // 'neverStopChasing'가 아니고, 플레이어가 시야에 없을 때만 기존 로직 실행
                {
                    chaseTimer -= Time.deltaTime;
                    if (chaseTimer > 0)
                    {
                        // 시야를 놓쳤지만 아직 추적 유예 시간일 때
                        if (agent.isOnNavMesh) agent.SetDestination(lastKnownPlayerPosition);
                        RotateTowardsPosition(lastKnownPlayerPosition);
                    }
                    else
                    {
                        // 추적 실패, Idle 상태로 복귀
                        currentState = EnemyState.Idle;
                        if (agent.isOnNavMesh) agent.ResetPath();
                        Debug.Log("Enemy: Lost player, returning to idle.");
                    }
                }
                break;
        }

        if (!playerInFOV && currentState != EnemyState.Chasing)
        {
            TryRotateTowardPlayerWhenNearby();
        }
    }

    private bool CheckFOV()
    {
        if (target == null) return false;

        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget < viewRadius)
        {
            if (Vector3.Angle(transform.forward, directionToTarget) < viewAngle / 2)
            {
                if (Physics.Raycast(transform.position, directionToTarget, out RaycastHit hit, viewRadius))
                {
                    return hit.transform == target;
                }
            }
        }
        return false;
    }

    private void DrawBaseFOVMesh()
    {
        DrawFOVMesh(baseFovMesh, viewRadius);
    }

    private void DrawDetectionFOVMesh()
    {
        float currentDetectionRadius = viewRadius * detectionProgress;
        DrawFOVMesh(detectionFovMesh, currentDetectionRadius);
    }

    private void DrawFOVMesh(Mesh mesh, float radius)
    {
        if (radius <= 0)
        {
            mesh.Clear();
            return;
        }

        int segments = 20;
        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[segments * 3];

        float yOffset = 0.1f; // Z-파이팅 방지를 위한 수직 오프셋
        Vector3 verticalOffset = Vector3.up * yOffset;

        vertices[0] = verticalOffset;

        float angleIncrement = viewAngle / segments;
        float currentAngle = -viewAngle / 2;

        for (int i = 0; i <= segments; i++)
        {
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            vertices[i + 1] = direction * radius + verticalOffset;
            currentAngle += angleIncrement;
        }

        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    private void RotateTowardsPosition(Vector3 worldPosition)
    {
        Vector3 flatDirection = worldPosition - transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(flatDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }

    private void TryRotateTowardPlayerWhenNearby()
    {
        if (target == null)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget > viewRadius)
        {
            return;
        }

        RotateTowardsPosition(target.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 leftRayDirection = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
        Vector3 rightRayDirection = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, leftRayDirection * viewRadius);
        Gizmos.DrawRay(transform.position, rightRayDirection * viewRadius);

        if (target != null && CheckFOV())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            GoalManager.Instance.TriggerGameOver();
        }
    }
}
