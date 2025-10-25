using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System;

public class GoalManager : MonoBehaviour
{
    public static GoalManager Instance { get; private set; }

    [Header("Core Setup")]
    [Tooltip("The player's vehicle transform.")]
    public Transform player;
    [Tooltip("A BoxCollider defining the area where goals can spawn.")]
    public BoxCollider spawnArea;
    [Tooltip("Reference to the CityGenerator script.")]
    public CityGenerator cityGenerator;

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
    public float minSpawnDistanceFromPlayer = 15f;
    [Tooltip("Minimum distance a goal can spawn from other goals.")]
    public float minSpawnDistanceFromOtherGoals = 10f;
    [Tooltip("How many times to try finding a valid spawn position before giving up.")]
    private int maxSpawnAttempts = 25;

    private List<Goal> activeGoals = new List<Goal>();
    private bool stageCleared = false;
    private bool isGameOver = false;
    private List<Vector3> roadPositions = new List<Vector3>();

    public class Goal
    {
        public GameObject marker;
        public Vector3 position;
        public float requiredDwellTime;
        public float dwellTimer;
        public bool isPlayerInside;
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
        // Reset time scale on awake, just in case it was left at 0
        Time.timeScale = 1f;
        UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
    }

    void Start()
    {
        if (player == null || spawnArea == null)
        {
            Debug.LogError("Player or SpawnArea is not assigned in the GoalManager! Please set them up in the Inspector.");
            this.enabled = false;
            return;
        }

        if (cityGenerator != null)
        {
            PopulateRoadPositions();
        }

        SpawnInitialGoals();
    }

    void Update()
    {
        if (stageCleared || isGameOver) return;

        for (int i = activeGoals.Count - 1; i >= 0; i--)
        {
            Goal goal = activeGoals[i];

            if (goal.isPlayerInside)
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
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
        else if (isGameOver)
        {
            GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 200, 100), "Game Over");
            if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2 - 15, 100, 30), "재시작"))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return; // Prevent multiple triggers
        isGameOver = true;
        Time.timeScale = 0f; // Pause the game
        Debug.Log("Game Over triggered!");
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
        Vector3 goalPosition = Vector3.zero;
        bool positionFound = false;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 potentialPosition;
            if (roadPositions.Count > 0)
            {
                potentialPosition = roadPositions[UnityEngine.Random.Range(0, roadPositions.Count)];
            }
            else
            {
                Bounds bounds = spawnArea.bounds;
                potentialPosition = new Vector3(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    spawnArea.transform.position.y,
                    UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
                );
            }

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
        
        CapsuleCollider collider = goalMarker.GetComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = goalRadius;

        goalMarker.transform.localScale = new Vector3(goalRadius * 2, 0.1f, goalRadius * 2);
        goalMarker.transform.position = goalPosition;
        goalMarker.GetComponent<Renderer>().material.color = Color.red;

        Goal newGoal = new Goal
        {
            marker = goalMarker,
            position = goalPosition,
            requiredDwellTime = UnityEngine.Random.Range(minDwellTime, maxDwellTime),
            dwellTimer = 0f,
            isPlayerInside = false
        };

        activeGoals.Add(newGoal);

        GoalTrigger trigger = goalMarker.AddComponent<GoalTrigger>();
        trigger.goal = newGoal;
    }

    void CheckForStageClear()
    {
        if (activeGoals.Count == 0)
        {
            stageCleared = true;
            Debug.Log("Stage Cleared!");
        }
    }

    void PopulateRoadPositions()
    {
        roadPositions.Clear();
        for (int x = 0; x < cityGenerator.citySizeX; x++)
        {
            for (int z = 0; z < cityGenerator.citySizeZ; z++)
            {
                if (x % cityGenerator.roadInterval == 0 || z % cityGenerator.roadInterval == 0)
                {
                    float xPos = x * cityGenerator.blockSize;
                    float zPos = z * cityGenerator.blockSize;
                    roadPositions.Add(new Vector3(xPos, spawnArea.transform.position.y, zPos));
                }
            }
        }
    }

    public List<Vector3> GetAllGoalPositions()
    {
        return activeGoals.Select(g => g.position).ToList();
    }
}

public class GoalTrigger : MonoBehaviour
{
    public GoalManager.Goal goal; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            goal.isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            goal.isPlayerInside = false;
        }
    }
}