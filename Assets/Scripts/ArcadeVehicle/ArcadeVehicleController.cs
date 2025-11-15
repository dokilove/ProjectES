using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeVehicleController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ArcadeVehicleDataSO vehicleData;

    [Header("Grounding")]
    [Tooltip("Set this to the layer your ground objects are on.")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("How far down to check for ground from the vehicle's starting position.")]
    [SerializeField] private float groundCheckDistance = 50f;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector3 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
    }

    private void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (vehicleData == null) return;

        rb.AddForce(Vector3.down * vehicleData.extraGravityForce, ForceMode.Acceleration);

        HandleMovement();
        HandleGroundSnapping();
    }

    private void HandleMovement()
    {
        rb.angularVelocity = Vector3.zero;

        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraRight = Camera.main.transform.right;
        Vector3 cameraUp = Camera.main.transform.up;

        Vector3 effectiveForward;
        Vector3 effectiveRight;

        // Check if camera is looking almost straight down (pitch ~90 degrees)
        // If dot product of camera's forward and world down is close to 1, it's looking straight down.
        float dotProduct = Vector3.Dot(cameraForward, Vector3.down);

        // Use a threshold (e.g., 0.9f) to determine if the camera is looking mostly straight down.
        if (dotProduct > 0.9f) // Camera is looking mostly straight down (e.g., pitch > ~80 degrees)
        {
            // In this case, 'up' on the stick should map to camera's 'up' (world forward/back)
            // and 'right' on the stick maps to camera's 'right'.
            effectiveForward = cameraUp; // This is world forward/back
            effectiveRight = cameraRight;
        }
        else
        {
            // Normal case: flatten camera's forward and right vectors
            effectiveForward = cameraForward;
            effectiveRight = cameraRight;
        }

        effectiveForward.y = 0; // Always flatten to XZ plane
        effectiveRight.y = 0;   // Always flatten to XZ plane

        effectiveForward.Normalize();
        effectiveRight.Normalize();

        Vector3 currentMoveVector = (effectiveForward * moveInput.y + effectiveRight * moveInput.x);

        if (moveInput.sqrMagnitude > 0.1f)
        {
            moveDirection = currentMoveVector.normalized;
            rb.linearVelocity = moveDirection * vehicleData.maxSpeed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (currentMoveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentMoveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleGroundSnapping()
    {
        if (Physics.Raycast(transform.position, Vector3.down, vehicleData.groundSnapDistance, groundLayer))
        {
            if (rb.linearVelocity.y <= 0)
            {
                rb.AddForce(Vector3.down * vehicleData.groundSnapForce, ForceMode.Acceleration);
            }
        }
    }
}

