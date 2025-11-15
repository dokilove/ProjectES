using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq; // For .FirstOrDefault()

public class CameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("Drag all camera GameObjects you want to switch between here.")]
    public Camera[] cameras;

    private InputSystem_Actions inputActions;
    private int currentCameraIndex = 0;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        // Subscribing to the 'ChangeCamera' action in the 'Vehicle_Arcade' action map
        inputActions.Vehicle_Arcade.ChangeCamera.performed += OnChangeCamera;
    }

    void OnEnable()
    {
        inputActions.Vehicle_Arcade.Enable();
    }

    void OnDisable()
    {
        inputActions.Vehicle_Arcade.Disable();
    }

    void Start()
    {
        if (cameras == null || cameras.Length == 0)
        {
            Debug.LogWarning("CameraSwitcher: No cameras assigned. Please assign cameras in the Inspector.");
            enabled = false; // Disable script if no cameras
            return;
        }

        // Deactivate all cameras except the first one
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].gameObject.SetActive(i == currentCameraIndex);
            }
            else
            {
                Debug.LogWarning($"CameraSwitcher: Camera at index {i} is null. Please check your camera assignments.");
            }
        }
    }

    private void OnChangeCamera(InputAction.CallbackContext context)
    {
        if (cameras == null || cameras.Length <= 1) return;

        // Deactivate current camera
        if (cameras[currentCameraIndex] != null)
        {
            cameras[currentCameraIndex].gameObject.SetActive(false);
        }

        // Move to next camera
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;

        // Activate new camera
        if (cameras[currentCameraIndex] != null)
        {
            cameras[currentCameraIndex].gameObject.SetActive(true);
        }
        else
        {
            // If the next camera is null, try to find the next valid one
            int startIndex = currentCameraIndex;
            do
            {
                currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
                if (cameras[currentCameraIndex] != null)
                {
                    cameras[currentCameraIndex].gameObject.SetActive(true);
                    break;
                }
            } while (currentCameraIndex != startIndex); // Loop until we find a valid camera or come back to start
        }
    }
}
