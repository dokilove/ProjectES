using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
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

    [Header("Spawning")]
    [Tooltip("The layer mask for the ground, used for placing the enemy correctly on the terrain.")]
    public LayerMask groundLayer;

    private NavMeshAgent agent;
    private TownGenerator townGenerator;

    // Base FOV
    private Mesh baseFovMesh;
    private MeshFilter baseFovMeshFilter;

    // Detection FOV
    private Mesh detectionFovMesh;
    
    private float detectionProgress; // 0 = not detected, 1 = fully detected

    public enum EnemyState { Patrol, Detecting, Chasing }
    private EnemyState currentState = EnemyState.Patrol;
    
    private Vector3 lastKnownPlayerPosition;
    public float chaseDurationAfterLostSight = 3f;
    private float chaseTimer;

    void OnEnable()
    {
        // This event is no longer needed as we will place the agent using NavMesh
        // CityGenerator.OnCityGenerated += PlaceOnGround;
    }

    void OnDisable()
    {
        // CityGenerator.OnCityGenerated -= PlaceOnGround;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        townGenerator = FindObjectOfType<TownGenerator>();

        if (townGenerator == null)
        {
            Debug.LogError("EnemyAI: TownGenerator not found in the scene!", this);
            enabled = false;
            return;
        }

        SetupBaseFOV();
        SetupDetectionFOV();

        agent.speed = moveSpeed;
        FindPlayer();
        
        // Check if the agent is placed on a valid NavMesh area in the editor.
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"Enemy '{gameObject.name}' is not placed on a baked NavMesh. It will not be able to move. Please place it on a valid NavMesh area in the Scene Editor.", this);
            return; // Stop further execution if it cannot patrol.
        }

        // Start patrolling from the current position.
        GoToNewPatrolPoint();
    }

    void Update()
    {
        if (target == null)
        {
            FindPlayer();
            if (target == null)
            {
                // If still no target, just keep patrolling
                if (currentState != EnemyState.Patrol) currentState = EnemyState.Patrol;
            }
        }

        bool playerInFOV = target != null && CheckFOV();
        HandleStateMachine(playerInFOV);

        DrawBaseFOVMesh();
        DrawDetectionFOVMesh();
    }

    private void HandleStateMachine(bool playerInFOV)
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                // Check if we've reached the destination
                if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance)
                {
                    GoToNewPatrolPoint();
                }

                if (playerInFOV)
                {
                    currentState = EnemyState.Detecting;
                    Debug.Log("Enemy: Player spotted, beginning detection...");
                    lastKnownPlayerPosition = target.position;
                }
                break;

            case EnemyState.Detecting:
                if (playerInFOV)
                {
                    lastKnownPlayerPosition = target.position;
                    detectionProgress = Mathf.Min(1, detectionProgress + (1 / detectionDuration) * Time.deltaTime);

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
                    // If player is lost during detection, go back to patrol and decay detection
                    currentState = EnemyState.Patrol;
                    detectionProgress = Mathf.Max(0, detectionProgress - (1 / detectionDuration) * Time.deltaTime);
                    Debug.Log("Enemy: Player lost during detection, returning to patrol.");
                }
                break;

            case EnemyState.Chasing:
                if (neverStopChasing || playerInFOV)
                {
                    lastKnownPlayerPosition = target.position;
                    chaseTimer = chaseDurationAfterLostSight;
                    if (agent.isOnNavMesh) agent.SetDestination(target.position);
                    RotateTowardsPosition(target.position);
                }
                else
                {
                    chaseTimer -= Time.deltaTime;
                    if (chaseTimer > 0)
                    {
                        if (agent.isOnNavMesh) agent.SetDestination(lastKnownPlayerPosition);
                        RotateTowardsPosition(lastKnownPlayerPosition);
                    }
                    else
                    {
                        currentState = EnemyState.Patrol;
                        GoToNewPatrolPoint(); // Start patrolling again
                        Debug.Log("Enemy: Lost player, returning to patrol.");
                    }
                }
                break;
        }
    }

    private void GoToNewPatrolPoint()
    {
        if (townGenerator == null || !agent.isOnNavMesh) return;

        Vector3 randomPoint = townGenerator.GetRandomRoadPosition();
        
        // Find the nearest point on the NavMesh to the random road position
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            Debug.LogWarning("Could not find a valid NavMesh point near the random road position. Trying again.", this);
            // Optionally, try again or find another point
        }
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

        Vector3 centerVertex = Vector3.up * 0.1f;
        vertices[0] = centerVertex;

        float angleIncrement = viewAngle / segments;
        float currentAngle = -viewAngle / 2;

        for (int i = 0; i <= segments; i++)
        {
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            vertices[i + 1] = centerVertex + direction * radius;
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
            // Assuming a GoalManager exists to handle game over
            // GoalManager.Instance.TriggerGameOver();
            Debug.Log("Game Over!");
        }
    }
}
