using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Image))]
public class GoalIndicator : MonoBehaviour
{
    private Image indicatorImage;
    private Camera mainCamera;
    private RectTransform canvasRectTransform;

    void Start()
    {
        indicatorImage = GetComponent<Image>();
        mainCamera = Camera.main;
        
        // Find the root Canvas to get its RectTransform
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
        }

        if (mainCamera == null || canvasRectTransform == null)
        {
            Debug.LogError("GoalIndicator setup is incomplete. Ensure you have a main camera tagged 'MainCamera' and the indicator is on a Canvas.");
            enabled = false;
        }
    }

    void Update()
    {
        if (GoalManager.Instance == null)
        {
            indicatorImage.enabled = false;
            return;
        }

        List<Vector3> goalPositions = GoalManager.Instance.GetAllGoalPositions();

        if (goalPositions.Count == 0)
        {
            indicatorImage.enabled = false;
            return;
        }

        // Find the closest goal
        Vector3 playerPos = GoalManager.Instance.player.position;
        Vector3 closestGoalPos = Vector3.zero;
        float closestDistSq = float.MaxValue;

        foreach (Vector3 goalPos in goalPositions)
        {
            float distSq = (playerPos - goalPos).sqrMagnitude;
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestGoalPos = goalPos;
            }
        }

        Vector3 targetWorldPos = closestGoalPos;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(targetWorldPos);

        // Check if target is on screen
        bool isOffScreen = screenPoint.z < 0 || screenPoint.x < 0 || screenPoint.x > Screen.width || screenPoint.y < 0 || screenPoint.y > Screen.height;

        indicatorImage.enabled = isOffScreen;

        if (isOffScreen)
        {
            Vector3 cappedTargetScreenPosition = screenPoint;
            if (screenPoint.z < 0)
            {
                cappedTargetScreenPosition *= -1;
            }

            // The direction from the center of the screen to the target
            Vector3 direction = (cappedTargetScreenPosition - new Vector3(Screen.width / 2, Screen.height / 2, 0)).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90); // Assuming the arrow sprite points upwards

            // Calculate the intersection point with the screen borders
            float halfWidth = canvasRectTransform.rect.width / 2;
            float halfHeight = canvasRectTransform.rect.height / 2;
            Vector3 screenCenter = new Vector3(halfWidth, halfHeight, 0);

            float m = direction.y / direction.x;
            
            Vector3 screenBounds = new Vector3(halfWidth * 0.9f, halfHeight * 0.9f, 0);

            float x, y;
            if (Mathf.Abs(m) * screenBounds.x > screenBounds.y)
            {
                // Intersects with top/bottom
                y = Mathf.Sign(direction.y) * screenBounds.y;
                x = y / m;
            }
            else
            {
                // Intersects with left/right
                x = Mathf.Sign(direction.x) * screenBounds.x;
                y = x * m;
            }

            transform.position = screenCenter + new Vector3(x, y, 0);
        }
    }
}