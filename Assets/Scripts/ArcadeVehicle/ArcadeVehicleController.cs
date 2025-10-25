using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeVehicleController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float acceleration = 5f; // Adjusted for Lerp
    [SerializeField] private float deceleration = 25f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Grounding")]
    [Tooltip("Set this to the layer your ground objects are on.")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("How far down to check for ground from the vehicle's starting position.")]
    [SerializeField] private float groundCheckDistance = 50f;

    [Header("Gravity")]
    [Tooltip("Extra gravity force to apply, making the vehicle fall faster. Acts as an acceleration.")]
    [SerializeField] private float extraGravityForce = 20f;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private float currentSpeed;
    private Vector3 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions();

        // We only freeze rotation, allowing the Rigidbody to be affected by gravity on the Y-axis.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        // Position the vehicle on the ground when the game starts.
        PlaceOnGround();
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
        // Read movement input in Update for responsiveness.
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Apply extra gravity every physics step for a less floaty feel.
        rb.AddForce(Vector3.down * extraGravityForce, ForceMode.Acceleration);

        // Apply physics-based movement in FixedUpdate.
        HandleMovement();
    }

    /// <summary>
    /// Casts a ray downwards to find the ground and places the vehicle on top of it.
    /// </summary>
    private void PlaceOnGround()
    {
        // Start the ray from slightly above the vehicle to ensure it doesn't start inside the ground
        Vector3 rayStart = transform.position + Vector3.up * 5f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            // We need the vehicle's height to position it correctly.
            // Using the collider's bounds is a reliable way to do this.
            var vehicleCollider = GetComponent<Collider>();
            if (vehicleCollider != null)
            {
                // The pivot of the vehicle might not be at its base.
                // We use 'bounds.extents.y' which is half the collider's height.
                // This positions the vehicle so its bottom touches the ground.
                float heightOffset = vehicleCollider.bounds.extents.y;
                transform.position = new Vector3(transform.position.x, hit.point.y + heightOffset, transform.position.z);
            }
            else
            {
                // Fallback if there's no collider, just place it slightly above the ground.
                transform.position = new Vector3(transform.position.x, hit.point.y + 0.5f, transform.position.z);
            }
        }
        else
        {
            Debug.LogWarning("PlaceOnGround: Could not find ground beneath the vehicle. Check ground layer and distance.", this);
        }
    }

    private void HandleMovement()
    {
        // We control rotation manually, so we zero out angular velocity.
        rb.angularVelocity = Vector3.zero;

        // Get camera's forward and right vectors, flattened onto the horizontal plane (y=0).
        Vector3 moveForward = Camera.main.transform.forward;
        Vector3 moveRight = Camera.main.transform.right;

        moveForward.y = 0;
        moveRight.y = 0;

        moveForward.Normalize();
        moveRight.Normalize();

        Vector3 currentMoveVector = (moveForward * moveInput.y + moveRight * moveInput.x);

        if (moveInput.sqrMagnitude > 0.1f)
        {
            moveDirection = currentMoveVector.normalized;
            currentSpeed = Mathf.Lerp(currentSpeed, maxSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.fixedDeltaTime);
        }

        // Apply velocity using the stored direction
        rb.linearVelocity = moveDirection * currentSpeed;

        // Ensure the vehicle comes to a complete stop
        if (currentSpeed < 0.01f)
        {
            rb.linearVelocity = Vector3.zero;
            currentSpeed = 0f;
        }

        // If there is movement input, smoothly rotate the vehicle to face the direction of movement.
        if (currentMoveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentMoveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}
