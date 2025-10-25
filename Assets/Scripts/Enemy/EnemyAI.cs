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
                if (playerInFOV)
                {
                    lastKnownPlayerPosition = target.position;
                    chaseTimer = chaseDurationAfterLostSight;
                    if (agent.isOnNavMesh) agent.SetDestination(target.position);

                    // Smoothly rotate to face the player
                    Vector3 direction = (target.position - transform.position).normalized;
                    Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
                }
                else
                {
                    chaseTimer -= Time.deltaTime;
                    if (chaseTimer > 0)
                    {
                        if (agent.isOnNavMesh) agent.SetDestination(lastKnownPlayerPosition);
                    }
                    else
                    {
                        currentState = EnemyState.Idle;
                        Debug.Log("Enemy: Lost player, returning to idle.");
                    }
                }
                break;
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

        vertices[0] = Vector3.zero;

        float angleIncrement = viewAngle / segments;
        float currentAngle = -viewAngle / 2;

        for (int i = 0; i <= segments; i++)
        {
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            vertices[i + 1] = direction * radius;
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
