using UnityEngine;
using UnityEngine.InputSystem;

public class ManualFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit Settings")]
    [SerializeField] private float distance = 14f;
    [SerializeField] private float yawSpeed = 120f;
    [SerializeField] private float pitch = 0f; // Pitch is now a serialized field for direct Inspector control
    [SerializeField] private float yOffset = 0f; // Y-axis offset

    // Removed [Header("Smoothing")] and positionSmoothSpeed

    private InputSystem_Actions inputActions;
    private Vector2 cameraInput;

    private float yaw = 0f;
    // pitch is now a serialized field, no longer a private float

    private float initialYaw = 0f;
    private float initialPitch = 0f; // Keep for reset functionality

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Camera_Arcade.CameraMove.performed += OnCameraMove;
        inputActions.Camera_Arcade.CameraMove.canceled += OnCameraMove;
        inputActions.Camera_Arcade.CameraReset.performed += OnCameraReset;
    }

    private void OnCameraMove(InputAction.CallbackContext context)
    {
        cameraInput = context.ReadValue<Vector2>();
    }

    private void OnCameraReset(InputAction.CallbackContext context)
    {
        yaw = initialYaw;
        pitch = initialPitch; // Reset pitch to its initial Inspector value
        cameraInput = Vector2.zero;

        // Construct rotation to avoid gimbal lock
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up); // Yaw around world Y-axis
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, Vector3.right); // Pitch around local X-axis
        Quaternion finalRotation = yawRotation * pitchRotation;

        Vector3 finalPosition = target.position - (finalRotation * Vector3.forward * distance);
        finalPosition.y += yOffset;

        transform.position = finalPosition;
        transform.rotation = finalRotation;
    }

    void OnEnable()
    {
        inputActions.Camera_Arcade.Enable();
    }

    void OnDisable()
    {
        inputActions.Camera_Arcade.Disable();
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("ManualFollowCamera: No target assigned. Please assign one in the inspector.");
            return;
        }

        Vector3 angles = transform.rotation.eulerAngles;
        yaw = angles.y;
        // pitch is now a serialized field, so its initial value comes from the Inspector.
        // We store it for reset functionality.
        initialPitch = pitch; 

        initialYaw = yaw;
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleManualRotation();

        // Construct rotation to avoid gimbal lock
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up); // Yaw around world Y-axis
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, Vector3.right); // Pitch around local X-axis
        Quaternion desiredRotation = yawRotation * pitchRotation;

        Vector3 desiredPosition = target.position - (desiredRotation * Vector3.forward * distance);
        desiredPosition.y += yOffset;

        // Always snap to the desired position and rotation for instant follow
        transform.position = desiredPosition;
        transform.rotation = desiredRotation;
    }

    private void HandleManualRotation()
    {
        // Pitch is now controlled directly in the Inspector, not by input.
        yaw += cameraInput.x * yawSpeed * Time.deltaTime; // Keep yaw input control
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
