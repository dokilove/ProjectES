using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 3.5f;
    public float attackRange = 1.5f;

    [Header("Field of View")]
    public float viewRadius = 10f;
    [Range(0, 360)]
    public float viewAngle = 90f;

    public LineRenderer fovLineRenderer;

    private NavMeshAgent agent;

    public enum EnemyState { Idle, Chasing }
    private EnemyState currentState = EnemyState.Idle;
    private Vector3 lastKnownPlayerPosition;
    public float chaseDurationAfterLostSight = 3f; // How long to chase after losing sight
    private float chaseTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("EnemyAI: NavMeshAgent component not found on this GameObject.");
            enabled = false; // Disable the script if no NavMeshAgent is found
            return;
        }

        fovLineRenderer = GetComponent<LineRenderer>();
        if (fovLineRenderer == null)
        {
            Debug.LogWarning("EnemyAI: LineRenderer component not found on this GameObject. In-game FOV visualization will not work.");
        }
        else
        {
            fovLineRenderer.enabled = true; // Ensure it's enabled from the start
        }

        agent.speed = moveSpeed;

        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                target = playerObject.transform;
                Debug.Log("EnemyAI: Found player with tag 'Player'.");
            }
            else
            {
                Debug.LogWarning("EnemyAI: Target not set and no GameObject with tag 'Player' found. Please assign a target in the Inspector or ensure player is tagged.");
            }
        }
    }

    void Update()
    {
        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                target = playerObject.transform;
            }
            else
            {
                // If still no target, do nothing
                if (fovLineRenderer != null) fovLineRenderer.enabled = false;
                return;
            }
        }

        bool playerInFOV = CheckFOV();

        switch (currentState)
        {
            case EnemyState.Idle:
                if (playerInFOV)
                {
                    currentState = EnemyState.Chasing;
                    lastKnownPlayerPosition = target.position;
                    chaseTimer = chaseDurationAfterLostSight;
                    if (fovLineRenderer != null) fovLineRenderer.enabled = true;
                    Debug.Log("Enemy: Player detected, starting chase!");
                }
                break;

            case EnemyState.Chasing:
                if (playerInFOV)
                {
                    lastKnownPlayerPosition = target.position;
                    chaseTimer = chaseDurationAfterLostSight; // Reset timer
                    if (agent.isOnNavMesh)
                    {
                        agent.SetDestination(target.position);
                    }
                    // Check if within attack range (optional, for future implementation)
                    if (Vector3.Distance(transform.position, target.position) <= attackRange)
                    {
                        // TODO: Implement attack logic here
                        // Debug.Log("Enemy: Attacking target!");
                    }
                }
                else // Player not in FOV, but still chasing
                {
                    chaseTimer -= Time.deltaTime;
                    if (chaseTimer > 0)
                    {
                        if (agent.isOnNavMesh)
                        {
                            agent.SetDestination(lastKnownPlayerPosition);
                        }
                    }
                    else
                    {
                        currentState = EnemyState.Idle;
                        Debug.Log("Enemy: Lost player, returning to idle.");
                    }
                }
                break;
        }

        DrawFOVInGame();
    }

    private bool CheckFOV()
    {
        if (target == null) return false;

        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // Check if target is within view radius
        if (distanceToTarget < viewRadius)
        {
            // Check if target is within view angle
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
            if (angleToTarget < viewAngle / 2)
            {
                // Check for obstacles using Raycast
                RaycastHit hit;
                if (Physics.Raycast(transform.position, directionToTarget, out hit, viewRadius))
                {
                    if (hit.transform == target)
                    {
                        return true; // Player is in FOV and line of sight is clear
                    }
                }
            }
        }
        return false;
    }

    private void DrawFOVInGame()
    {
        if (fovLineRenderer == null) return;

        int segments = 20; // Number of segments to draw the arc
        Vector3[] points = new Vector3[segments + 2];
        points[0] = transform.position; // Center point

        float currentAngle = -viewAngle / 2;
        for (int i = 0; i <= segments; i++)
        {
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            points[i + 1] = transform.position + direction * viewRadius;
            currentAngle += (viewAngle / segments);
        }

        fovLineRenderer.positionCount = points.Length;
        fovLineRenderer.SetPositions(points);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw view radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // Draw view angle
        Vector3 forward = transform.forward;
        Vector3 leftRayDirection = Quaternion.Euler(0, -viewAngle / 2, 0) * forward;
        Vector3 rightRayDirection = Quaternion.Euler(0, viewAngle / 2, 0) * forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, leftRayDirection * viewRadius);
        Gizmos.DrawRay(transform.position, rightRayDirection * viewRadius);

        // Draw a line to the target if it's visible
        if (target != null)
        {
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            if (distanceToTarget < viewRadius)
            {
                float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
                if (angleToTarget < viewAngle / 2)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, directionToTarget, out hit, viewRadius))
                    {
                        if (hit.transform == target)
                        {
                            Gizmos.color = Color.red; // Player is seen
                            Gizmos.DrawLine(transform.position, target.position);
                        }
                    }
                }
            }
        }
    }
}