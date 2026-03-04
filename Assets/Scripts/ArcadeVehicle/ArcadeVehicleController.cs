using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class ArcadeVehicleController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ArcadeVehicleDataSO vehicleData;

    [Header("Movement Tuning")]
    [Tooltip("아크 턴(Arc Turn)을 얼마나 부드럽게 시작하고 멈출지 정하는 값입니다.")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 8f;
    private float currentSpeed = 0f;

    [Header("Cameras")]
    public CinemachineCamera backViewCam;
    public CinemachineCamera quarterViewCam;

    [Header("Visuals")]
    [Tooltip("The transform of the car model to be scaled during charge. If null, this GameObject's transform is used.")]
    public Transform carModel;
    public float minScalePulseSpeed = 2f;
    public float maxScalePulseSpeed = 15f;
    public float scalePulseAmount = 0.05f;
    [Tooltip("How much the car stretches on the Z-axis during a dash.")]
    public float dashStretchAmount = 0.2f;
    [Tooltip("How quickly the car stretches and returns to normal.")]
    public float dashStretchSpeed = 15f;

    [Header("Dash Physics")]
    public LayerMask enemyLayer;
    public float dashPushRadius = 3f;
    public float dashPushForce = 50f;

    [Header("Debug")]
    [Tooltip("Shows the dash push radius in the Game view.")]
    public bool showDashRadiusInGame = false;

    private CharacterController characterController;
    private InputSystem_Actions playerActions;

    // Input & State
    private Vector2 moveInput;
    private Vector3 moveDirection;
    private float verticalVelocity;
    private bool isBackViewActive = false;

    // --- Dash State ---
    private bool isCharging;
    private bool isDashing;
    private float chargeTimer;
    private float dashTimer;
    private Vector3 dashDirection;
    private float currentDashSpeed;
    private Vector3 originalScale;
    private float pulsePhase;
    
    // --- Debug ---
    private LineRenderer debugCircleRenderer;
    // --------------------

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Vehicle_Arcade.Enable();
        playerActions.Vehicle_Arcade.ChangeCamera.performed += OnChangeCamera;

        // --- Dash Input Subscription ---
        playerActions.Vehicle_Arcade.ChargingDash.performed += OnDashStart;
        playerActions.Vehicle_Arcade.ChargingDash.canceled += OnDashRelease;
        // -------------------------------
    }

    private void OnDisable()
    {
        playerActions.Vehicle_Arcade.ChangeCamera.performed -= OnChangeCamera;
        
        // --- Dash Input Unsubscription ---
        playerActions.Vehicle_Arcade.ChargingDash.performed -= OnDashStart;
        playerActions.Vehicle_Arcade.ChargingDash.canceled -= OnDashRelease;
        // ---------------------------------

        playerActions.Vehicle_Arcade.Disable();
    }

    private void Start()
    {
        UpdateCameraPriorities();

        // Initialize scale for visual effects
        if (carModel == null)
        {
            carModel = transform; // Default to this transform if not set
        }
        originalScale = carModel.localScale;

        // Initialize debug circle renderer
        SetupDebugCircle();
    }

    private void LateUpdate()
    {
        DrawDebugCircle();
    }

    private void SetupDebugCircle()
    {
        GameObject circleObj = new GameObject("DashRadiusDebugCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;
        circleObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        debugCircleRenderer = circleObj.AddComponent<LineRenderer>();
        debugCircleRenderer.useWorldSpace = false;
        debugCircleRenderer.loop = true;
        debugCircleRenderer.startWidth = 0.05f;
        debugCircleRenderer.endWidth = 0.05f;
        debugCircleRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        debugCircleRenderer.startColor = new Color(1, 0, 0, 0.5f);
        debugCircleRenderer.endColor = new Color(1, 0, 0, 0.5f);
        debugCircleRenderer.positionCount = 0;
    }

    private void DrawDebugCircle()
    {
        if (debugCircleRenderer == null) return;

        if (showDashRadiusInGame)
        {
            const int segments = 36;
            debugCircleRenderer.positionCount = segments + 1;
            
            float angle = 0f;
            for (int i = 0; i < (segments + 1); i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * dashPushRadius;
                float z = Mathf.Cos(Mathf.Deg2Rad * angle) * dashPushRadius;

                debugCircleRenderer.SetPosition(i, new Vector3(x, 0, z));

                angle += (360f / segments);
            }
        }
        else
        {
            debugCircleRenderer.positionCount = 0;
        }
    }

    private void Update()
    {
        // Read move input as long as we are not dashing
        if (!isDashing)
        {
            moveInput = playerActions.Vehicle_Arcade.Move.ReadValue<Vector2>();
        }
        else
        {
            moveInput = Vector2.zero;
        }

        // Handle state timers
        if (isCharging)
        {
            chargeTimer += Time.deltaTime;
        }

        HandleVisuals();
    }

    private void HandleVisuals()
    {
        if (carModel == null) return;

        if (isCharging)
        {
            // Pulsating scale effect with variable speed
            float chargeRatio = Mathf.Clamp01(chargeTimer / vehicleData.maxChargeTime);
            float currentPulseSpeed = Mathf.Lerp(minScalePulseSpeed, maxScalePulseSpeed, chargeRatio);
            
            pulsePhase += currentPulseSpeed * Time.deltaTime;

            float scaleOffset = 1.0f + Mathf.Sin(pulsePhase) * scalePulseAmount;
            carModel.localScale = originalScale * scaleOffset;
        }
        else if (isDashing)
        {
            // Dash squash and stretch effect (Reversed)
            float stretch = 1.0f + dashStretchAmount;
            float squash = 1.0f / stretch;
            Vector3 targetScale = new Vector3(originalScale.x, originalScale.y * stretch, originalScale.z * squash);
            carModel.localScale = Vector3.Lerp(carModel.localScale, targetScale, dashStretchSpeed * Time.deltaTime);
        }
        else
        {
            // Smoothly return to original scale when idle
            carModel.localScale = Vector3.Lerp(carModel.localScale, originalScale, dashStretchSpeed * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (vehicleData == null) return;

        // State machine for movement
        if (isDashing)
        {
            HandleDash();
        }
        else
        {
            HandleGravity();
            HandleMovement();
        }
    }

    // --- Dash Event Handlers ---
    private void OnDashStart(InputAction.CallbackContext context)
    {
        if (isDashing) return; // Can't start charging while already dashing

        isCharging = true;
        chargeTimer = 0f;
        pulsePhase = 0f; // Reset pulse phase
    }

    private void OnDashRelease(InputAction.CallbackContext context)
    {
        if (!isCharging) return;
        isCharging = false;

        // Reset scale immediately
        if (carModel != null)
        {
            carModel.localScale = originalScale;
        }

        // Check for minimum charge time
        if (chargeTimer < vehicleData.minChargeTime)
        {
            return; // Dash fizzles
        }

        isDashing = true;

        // Dash in the direction the vehicle is currently facing
        dashDirection = transform.forward;

        // Calculate charge ratio, considering min charge time
        float chargeRatio = Mathf.Clamp01((chargeTimer - vehicleData.minChargeTime) / (vehicleData.maxChargeTime - vehicleData.minChargeTime));

        // Interpolate dash duration and speed
        dashTimer = Mathf.Lerp(vehicleData.minDashDuration, vehicleData.maxDashDuration, chargeRatio);
        currentDashSpeed = Mathf.Lerp(vehicleData.minDashSpeed, vehicleData.maxDashSpeed, chargeRatio);
    }
    // ---------------------------

    private void OnChangeCamera(InputAction.CallbackContext context)
    {
        isBackViewActive = !isBackViewActive;
        UpdateCameraPriorities();
    }

    private void UpdateCameraPriorities()
    {
        if (backViewCam == null || quarterViewCam == null) return;

        backViewCam.Priority = isBackViewActive ? 20 : 10;
        quarterViewCam.Priority = isBackViewActive ? 10 : 20;
    }

    private void HandleGravity()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = -vehicleData.groundSnapForce;
        }
        else
        {
            verticalVelocity -= vehicleData.extraGravityForce * Time.fixedDeltaTime;
        }
        moveDirection.y = verticalVelocity;
    }

    // --- New Dash Handling Logic ---
    private void HandleDash()
    {
        if (dashTimer > 0)
        {
            // --- Physics Interaction ---
            HandleDashPush();
            // -------------------------

            // Calculate gravity during the dash
            if (characterController.isGrounded)
            {
                verticalVelocity = -vehicleData.groundSnapForce;
            }
            else
            {
                verticalVelocity -= vehicleData.extraGravityForce * Time.fixedDeltaTime;
            }

            // Combine dash movement with gravity
            Vector3 dashMovement = dashDirection * currentDashSpeed; // Use currentDashSpeed
            dashMovement.y = verticalVelocity;

            // Apply movement
            characterController.Move(dashMovement * Time.fixedDeltaTime);
            dashTimer -= Time.fixedDeltaTime;
        }
        else
        {
            // End of dash
            isDashing = false;
            currentSpeed = 0; // Reset speed after dash
        }
    }

    private void HandleDashPush()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, dashPushRadius, enemyLayer);
        if (hitColliders.Length > 0)
        {
            Debug.Log($"[Vehicle] OverlapSphere found {hitColliders.Length} colliders on enemy layer.");
        }
        
        foreach (var hitCollider in hitColliders)
        {
            // Use GetComponentInParent to find EnemyAI script even if collider is on a child object
            EnemyAI enemy = hitCollider.GetComponentInParent<EnemyAI>();

            if (enemy != null)
            {
                Debug.Log($"[Vehicle] Found EnemyAI on '{enemy.name}'. Attempting to push.");
                Vector3 pushDirection = (enemy.transform.position - transform.position).normalized;
                pushDirection.y = 0; // Keep the push horizontal
                
                // Call the public method on the enemy to handle the hit
                enemy.ApplyPushForce(pushDirection, dashPushForce);
            }
            else
            {
                Debug.LogWarning($"[Vehicle] Collider '{hitCollider.name}' is on the enemy layer, but no EnemyAI script found on it or its parents.", hitCollider.gameObject);
            }
        }
    }
    // -------------------------------

    private void HandleMovement()
    {
        // Calculate camera-relative vectors once at the top
        Transform cameraTransform = Camera.main.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        Vector3 targetMoveVector = (cameraForward * moveInput.y + cameraRight * moveInput.x);

        // --- Handle movement based on state ---
        if (isCharging)
        {
            // Allow rotation while charging
            if (targetMoveVector.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetMoveVector.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);
            }

            // Move slowly based on multiplier
            float chargeTargetSpeed = vehicleData.maxSpeed * vehicleData.chargeMoveSpeedMultiplier * moveInput.magnitude;
            currentSpeed = Mathf.Lerp(currentSpeed, chargeTargetSpeed, acceleration * Time.fixedDeltaTime);
        }
        else // Normal Movement
        {
            if (targetMoveVector.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetMoveVector.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);

                float inputMagnitude = Mathf.Clamp01(targetMoveVector.magnitude);
                currentSpeed = Mathf.Lerp(currentSpeed, vehicleData.maxSpeed * inputMagnitude, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
            }
        }

        // Apply final calculated movement
        Vector3 finalVelocity = transform.forward * currentSpeed;
        moveDirection.x = finalVelocity.x;
        moveDirection.z = finalVelocity.z;
        characterController.Move(moveDirection * Time.fixedDeltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw the dash push radius when the object is selected in the editor
        Gizmos.color = new Color(1, 0, 0, 0.5f); // Semi-transparent red
        Gizmos.DrawWireSphere(transform.position, dashPushRadius);
    }
#endif
}
