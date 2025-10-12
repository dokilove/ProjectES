using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class GoalManager : MonoBehaviour
{
    public static GoalManager Instance { get; private set; }

    [Header("Core Setup")]
    [Tooltip("The player's vehicle transform.")]
    public Transform player;
    [Tooltip("A BoxCollider defining the area where goals can spawn.")]
    public BoxCollider spawnArea;

    [Header("Goal Properties")]
    [Tooltip("How close the player needs to be to the goal to trigger the timer.")]
    public float goalRadius = 5f;
    [Tooltip("Minimum time in seconds the player must stay at the goal.")]
    public float minDwellTime = 2f;
    [Tooltip("Maximum time in seconds the player must stay at the goal.")]
    public float maxDwellTime = 5f;

    [Header("Spawning Behavior")]
    [Tooltip("Number of goals to spawn at the start of the stage.")]
    public int initialGoalCount = 5;
    [Tooltip("Minimum distance a goal can spawn from the player's start position.")]
    public float minSpawnDistanceFromPlayer = 100f;
    [Tooltip("Minimum distance a goal can spawn from other goals.")]
    public float minSpawnDistanceFromOtherGoals = 100f;
    [Tooltip("How many times to try finding a valid spawn position before giving up.")]
    private int maxSpawnAttempts = 25;

    private List<Goal> activeGoals = new List<Goal>();
    private bool stageCleared = false;

    // Internal class to manage the state of each goal
    private class Goal
    {
        public GameObject marker;
        public Vector3 position;
        public float requiredDwellTime;
        public float dwellTimer;
    }

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
        // Initialize the random seed to ensure different results on each run
        Random.InitState((int)System.DateTime.Now.Ticks);
    }

    void Start()
    {
        if (player == null || spawnArea == null)
        {
            Debug.LogError("Player or SpawnArea is not assigned in the GoalManager! Please set them up in the Inspector.");
            this.enabled = false; // Disable script if not set up
            return;
        }

        SpawnInitialGoals();
    }

    void Update()
    {
        if (stageCleared) return;

        // Iterate backwards to safely remove items from the list
        for (int i = activeGoals.Count - 1; i >= 0; i--)
        {
            Goal goal = activeGoals[i];
            
            float distance = Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(goal.position.x, goal.position.z));

            if (distance < goalRadius)
            {
                goal.dwellTimer += Time.deltaTime;

                float progress = goal.dwellTimer / goal.requiredDwellTime;
                goal.marker.GetComponent<Renderer>().material.color = Color.Lerp(Color.red, Color.green, progress);

                if (goal.dwellTimer >= goal.requiredDwellTime)
                {
                    Destroy(goal.marker);
                    activeGoals.RemoveAt(i);
                    CheckForStageClear();
                }
            }
            else
            {
                if (goal.dwellTimer > 0)
                {
                    goal.dwellTimer = 0f;
                    goal.marker.GetComponent<Renderer>().material.color = Color.red;
                }
            }
        }
    }

    void OnGUI()
    {
        if (stageCleared)
        {
            GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 200, 100), "Stage Cleared!");
            if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2 - 15, 100, 30), "다시 시작"))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    void SpawnInitialGoals()
    {
        for (int i = 0; i < initialGoalCount; i++)
        {
            SpawnNewGoal();
        }
    }

    void SpawnNewGoal()
    {
        Bounds bounds = spawnArea.bounds;
        Vector3 goalPosition = Vector3.zero;
        bool positionFound = false;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 potentialPosition = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                spawnArea.transform.position.y,
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (Vector3.Distance(player.position, potentialPosition) < minSpawnDistanceFromPlayer)
            {
                continue;
            }

            bool tooCloseToAnotherGoal = activeGoals.Any(g => Vector3.Distance(g.position, potentialPosition) < minSpawnDistanceFromOtherGoals);
            if (tooCloseToAnotherGoal)
            {
                continue;
            }

            goalPosition = potentialPosition;
            positionFound = true;
            break;
        }

        if (!positionFound)
        {
            Debug.LogWarning($"Could not find a valid spawn position for goal #{activeGoals.Count + 1} after {maxSpawnAttempts} attempts. Skipping this goal.");
            return;
        }

        GameObject goalMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goalMarker.name = "GoalMarker_" + activeGoals.Count;
        Destroy(goalMarker.GetComponent<CapsuleCollider>());
        
        goalMarker.transform.localScale = new Vector3(goalRadius * 2, 0.1f, goalRadius * 2);
        goalMarker.transform.position = goalPosition;
        goalMarker.GetComponent<Renderer>().material.color = Color.red;

        Goal newGoal = new Goal
        {
            marker = goalMarker,
            position = goalPosition,
            requiredDwellTime = Random.Range(minDwellTime, maxDwellTime),
            dwellTimer = 0f
        };

        activeGoals.Add(newGoal);
    }

    void CheckForStageClear()
    {
        if (activeGoals.Count == 0)
        {
            stageCleared = true;
            Debug.Log("Stage Cleared!");
        }
    }

    public List<Vector3> GetAllGoalPositions()
    {
        return activeGoals.Select(g => g.position).ToList();
    }
}
