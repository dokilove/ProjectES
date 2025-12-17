using UnityEngine;
using UnityEngine.AI;
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
    
    [Header("Goal Properties")]
    [Tooltip("How close the player needs to be to the goal to trigger the timer.")]
    public float goalRadius = 5f;
    [Tooltip("Minimum time in seconds the player must stay at the goal.")]
    public float minDwellTime = 2f;
    [Tooltip("Maximum time in seconds the player must stay at the goal.")]
    public float maxDwellTime = 5f;

    private List<Goal> activeGoals = new List<Goal>();
    private bool stageCleared = false;
    private bool isGameOver = false;

    // The Goal class now primarily holds state, not creation parameters.
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
        Time.timeScale = 1f;
        UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
    }

    void Start()
    {
        InitializeGoals();
    }

    void InitializeGoals()
    {
        if (player == null)
        {
            Debug.LogError("Player is not assigned in the GoalManager! Please set it up in the Inspector.");
            this.enabled = false;
            return;
        }

        // Find all GameObjects in the scene that are tagged as "Goal"
        GameObject[] goalMarkers = GameObject.FindGameObjectsWithTag("Goal");
        
        if (goalMarkers.Length == 0)
        {
            Debug.LogWarning("No GameObjects with tag 'Goal' found in the scene. The stage may end immediately.");
        }

        Debug.Log($"Found and initializing {goalMarkers.Length} goals.");

        foreach (GameObject marker in goalMarkers)
        {
            // Create the runtime Goal data object
            Goal newGoal = new Goal
            {
                marker = marker,
                position = marker.transform.position,
                requiredDwellTime = UnityEngine.Random.Range(minDwellTime, maxDwellTime),
                dwellTimer = 0f,
                isPlayerInside = false
            };

            // Add a trigger script to the marker and link it to our data object
            GoalTrigger trigger = marker.AddComponent<GoalTrigger>();
            trigger.goal = newGoal;

            // Optional: Adjust collider properties at runtime if needed
            Collider col = marker.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is CapsuleCollider capsule)
                {
                    capsule.radius = goalRadius;
                }
                else if (col is SphereCollider sphere)
                {
                    sphere.radius = goalRadius;
                }
            }

            activeGoals.Add(newGoal);
        }

        CheckForStageClear(); // Check immediately in case there are no goals
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
                
                // Ensure marker and renderer still exist
                if(goal.marker != null && goal.marker.GetComponent<Renderer>() != null)
                {
                    goal.marker.GetComponent<Renderer>().material.color = Color.Lerp(Color.red, Color.green, progress);
                }

                if (goal.dwellTimer >= goal.requiredDwellTime)
                {
                    if(goal.marker != null) Destroy(goal.marker);
                    activeGoals.RemoveAt(i);
                    CheckForStageClear();
                }
            }
            else
            {
                if (goal.dwellTimer > 0)
                {
                    goal.dwellTimer = 0f;
                    if(goal.marker != null && goal.marker.GetComponent<Renderer>() != null)
                    {
                        goal.marker.GetComponent<Renderer>().material.color = Color.red;
                    }
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
        if (isGameOver) return;
        isGameOver = true;
        Time.timeScale = 0f;
        Debug.Log("Game Over triggered!");
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

public class GoalTrigger : MonoBehaviour
{
    public GoalManager.Goal goal; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (goal != null) goal.isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (goal != null) goal.isPlayerInside = false;
        }
    }
}

