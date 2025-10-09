using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MinimapController : MonoBehaviour
{
    public Transform player;
    public float orthographicSize = 15f; // Controls the zoom level. Smaller is more zoomed in.

    private Camera minimapCamera;

    void Awake()
    {
        minimapCamera = GetComponent<Camera>();
        // Ensure the camera is orthographic
        minimapCamera.orthographic = true;
    }

    void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        // Update the camera's orthographic size (zoom)
        minimapCamera.orthographicSize = orthographicSize;

        // Position the camera above the player
        transform.position = new Vector3(player.position.x, player.position.y + 50f, player.position.z); // Height doesn't affect zoom, but keeps it out of the way

        // Set a fixed top-down rotation
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
