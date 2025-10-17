using UnityEngine;
using UnityEngine.InputSystem;

public class ManualFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit Settings")]
    [SerializeField] private float distance = 14f;
    [SerializeField] private float yawSpeed = 120f;
    [SerializeField] private float pitchSpeed = 120f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-30f, 80f);

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothSpeed = 0.125f;

    private InputSystem_Actions inputActions;
    private Vector2 cameraInput;

    private float yaw = 0f;
    private float pitch = 0f;

    private float initialYaw = 0f;
    private float initialPitch = 0f;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        // Using the new Camera_Arcade action map created by the user
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
        pitch = initialPitch;
        cameraInput = Vector2.zero;

        Quaternion finalRotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 finalPosition = target.position - (finalRotation * Vector3.forward * distance);

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
        pitch = angles.x;

        initialYaw = yaw;
        initialPitch = pitch;
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleManualRotation();

        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 desiredPosition = target.position - (desiredRotation * Vector3.forward * distance);

        if (cameraInput.sqrMagnitude > 0.01f)
        {
            transform.position = desiredPosition; // Snap position while rotating
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed); // Smooth follow
        }

        transform.rotation = desiredRotation;
    }

    private void HandleManualRotation()
    {
        yaw += cameraInput.x * yawSpeed * Time.deltaTime;
        pitch -= cameraInput.y * pitchSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
