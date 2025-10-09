using UnityEngine;

public class GoalManager : MonoBehaviour
{
    public static GoalManager Instance { get; private set; }
    public Vector3 CurrentGoalPosition => currentGoalPosition;

    [Tooltip("The player's vehicle transform.")]
    public Transform player;

    [Tooltip("A BoxCollider defining the area where goals can spawn.")]
    public BoxCollider spawnArea;

    [Tooltip("How close the player needs to be to the goal to trigger the timer.")]
    public float goalRadius = 5f;

    [Tooltip("Minimum time in seconds the player must stay at the goal.")]
    public float minDwellTime = 2f;

    [Tooltip("Maximum time in seconds the player must stay at the goal.")]
    public float maxDwellTime = 5f;

    private GameObject currentGoalMarker;
    private Vector3 currentGoalPosition;
    private float requiredDwellTime;
    private float dwellTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        if (player == null || spawnArea == null)
        {
            Debug.LogError("Player or SpawnArea is not assigned in the GoalManager! Please set them up in the Inspector.");
            this.enabled = false; // Disable script if not set up
            return;
        }

        SpawnNewGoal();
    }

    void Update()
    {
        // Check distance on the XZ plane only
        float distance = Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(currentGoalPosition.x, currentGoalPosition.z));

        if (distance < goalRadius)
        {
            dwellTimer += Time.deltaTime;

            // Visual feedback for the timer: lerp color from red to green
            float progress = dwellTimer / requiredDwellTime;
            currentGoalMarker.GetComponent<Renderer>().material.color = Color.Lerp(Color.red, Color.green, progress);

            if (dwellTimer >= requiredDwellTime)
            {
                SpawnNewGoal();
            }
        }
        else
        {
            dwellTimer = 0f; // Reset timer if player leaves
            // Reset color if it's not already red
            if (currentGoalMarker.GetComponent<Renderer>().material.color != Color.red)
            {
                currentGoalMarker.GetComponent<Renderer>().material.color = Color.red;
            }
        }
    }

    void SpawnNewGoal()
    {
        dwellTimer = 0f;
        requiredDwellTime = Random.Range(minDwellTime, maxDwellTime);

        Bounds bounds = spawnArea.bounds;
        // Find a random point within the bounds
        currentGoalPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            spawnArea.transform.position.y, // Place the goal at the same Y level as the spawn area collider
            Random.Range(bounds.min.z, bounds.max.z)
        );

        if (currentGoalMarker == null)
        {
            currentGoalMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            currentGoalMarker.name = "GoalMarker";
            // We don't need the collider on the marker
            Destroy(currentGoalMarker.GetComponent<CapsuleCollider>());
        }
        
        // Make the cylinder flat to look like a circle on the ground
        currentGoalMarker.transform.localScale = new Vector3(goalRadius * 2, 0.1f, goalRadius * 2);
        currentGoalMarker.transform.position = currentGoalPosition;
        // Ensure the marker color is reset to red for the new goal
        currentGoalMarker.GetComponent<Renderer>().material.color = Color.red;

        Debug.Log($"New goal! Go to {currentGoalPosition} and wait for {requiredDwellTime:F1} seconds.");
    }
}
