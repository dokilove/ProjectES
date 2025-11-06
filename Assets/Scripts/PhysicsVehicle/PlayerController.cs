using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Visual Wheels (Optional)")]
    public Transform frontLeftTransform;
    public Transform frontRightTransform;
    public Transform rearLeftTransform;
    public Transform rearRightTransform;

    [Header("Movement Settings")]
    [SerializeField] private WheelSettingsSO wheelSettings; // 휠 설정 ScriptableObject

    [Header("Dependencies")]
    [SerializeField] private SpringArmCamera springArmCamera; // 카메라 리셋을 위해 참조

    [Header("Spawning")]
    [Tooltip("The layer mask for the ground, used for placing the vehicle correctly on the terrain.")]
    public LayerMask groundLayer;

    // Input System Actions
    private InputSystem_Actions playerInputActions;
    private float steerInput;
    private float accelerateInput;
    private float brakeInput;
    private float handbrakeInput;

    private Rigidbody rb;
    private WheelFrictionCurve originalRearSidewaysFriction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Rigidbody가 Kinematic이 아니고 중력을 사용하는지 확인
        rb.isKinematic = false;
        rb.useGravity = true;

        if (wheelSettings == null)
        {
            Debug.LogError("PlayerController: WheelSettingsSO is not assigned!", this);
            enabled = false;
            return;
        }

        playerInputActions = new InputSystem_Actions();

        // 입력 액션에 대한 콜백 구독
        playerInputActions.Driver.Steer.performed += ctx => steerInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Steer.canceled += ctx => steerInput = 0f;

        playerInputActions.Driver.Accelerate.performed += ctx => accelerateInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Accelerate.canceled += ctx => accelerateInput = 0f;

        playerInputActions.Driver.Brake.performed += ctx => brakeInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Brake.canceled += ctx => brakeInput = 0f;

        playerInputActions.Driver.Handbrake.performed += ctx => handbrakeInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Handbrake.canceled += ctx => handbrakeInput = 0f;

        playerInputActions.Driver.CameraReset.performed += OnCameraReset;

        // 드리프트를 위해 원래의 후륜 측면 마찰력 저장
        originalRearSidewaysFriction = rearLeftWheel.sidewaysFriction;

        // 기본 측면 마찰력 설정 (미끄러움 방지)
        SetSidewaysFrictionStiffness(frontLeftWheel, wheelSettings.defaultSidewaysFrictionStiffness);
        SetSidewaysFrictionStiffness(frontRightWheel, wheelSettings.defaultSidewaysFrictionStiffness);
        SetSidewaysFrictionStiffness(rearLeftWheel, wheelSettings.defaultSidewaysFrictionStiffness);
        SetSidewaysFrictionStiffness(rearRightWheel, wheelSettings.defaultSidewaysFrictionStiffness);
    }

    private void Start()
    {
        // Initialization is now handled by the OnCityGenerated event
    }

    private void SetSidewaysFrictionStiffness(WheelCollider wheel, float stiffness)
    {
        var friction = wheel.sidewaysFriction;
        friction.stiffness = stiffness;
        wheel.sidewaysFriction = friction;
    }

    private void OnCameraReset(InputAction.CallbackContext context)
    {
        if (springArmCamera != null)
        {
            springArmCamera.ResetCamera();
        }
    }

    private void OnEnable()
    {
        playerInputActions.Driver.Enable();
        CityGenerator.OnCityGenerated += PlaceOnGround;
    }

    private void OnDisable()
    {
        playerInputActions.Driver.Disable();
        CityGenerator.OnCityGenerated -= PlaceOnGround;
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleSteering();
        HandleHandbrake();
        UpdateWheelPoses();
        ApplySpeedLimit();

        // Stability control for better handling on slopes and corners
        ApplyDownforce();
        ApplyAntiRoll();
    }

    private void HandleMovement()
    {
        // 전진(W)과 후진/브레이크(S) 입력을 -1 ~ 1 범위의 값으로 통합
        float moveInput = accelerateInput - brakeInput;

        // 현재 차량의 전진 방향 속도 (m/s)
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        
        // S키를 눌렀고(moveInput < 0), 차가 앞으로 움직이는 중이면 브레이크 적용
        if (moveInput < 0 && forwardSpeed > 0.1f)
        {
            ApplyBrake();
            ApplyMotorTorque(0); // 브레이크 중에는 모터 토크 비활성화
        }
        else // 그 외의 모든 경우 (전진, 후진, 정지)
        {
            ReleaseBrake();
            ApplyMotorTorque(moveInput * wheelSettings.motorForce);
        }
    }

    private void HandleHandbrake()
    {
        bool aButtonPressed = false;
        if (Gamepad.current != null)
        { 
            aButtonPressed = Gamepad.current.buttonSouth.isPressed;
        }

        // 핸드브레이크 입력이 있을 경우 (키보드 스페이스 또는 컨트롤러 A버튼)
        if (handbrakeInput > 0 || aButtonPressed)
        {
            // 뒷바퀴에만 강한 브레이크를 걸어 잠급니다.
            rearLeftWheel.brakeTorque = wheelSettings.brakeForce * 2f;
            rearRightWheel.brakeTorque = wheelSettings.brakeForce * 2f;

            // 뒷바퀴의 측면 마찰력을 줄여 드리프트를 유도합니다.
            var friction = rearLeftWheel.sidewaysFriction;
            friction.stiffness = wheelSettings.handbrakeFrictionStiffness;
            rearLeftWheel.sidewaysFriction = friction;
            rearRightWheel.sidewaysFriction = friction;
        }
        else
        {
            // 핸드브레이크를 떼면 원래 마찰력으로 복원합니다.
            // 브레이크 토크는 HandleMovement에서 관리하므로 여기서는 마찰력만 복원합니다.
            rearLeftWheel.sidewaysFriction = originalRearSidewaysFriction;
            rearRightWheel.sidewaysFriction = originalRearSidewaysFriction;
        }
    }

    private void ApplyMotorTorque(float torque)
    {
        frontLeftWheel.motorTorque = torque;
        frontRightWheel.motorTorque = torque;
    }

    private void ApplyBrake()
    {
        frontLeftWheel.brakeTorque = wheelSettings.brakeForce;
        frontRightWheel.brakeTorque = wheelSettings.brakeForce;
        rearLeftWheel.brakeTorque = wheelSettings.brakeForce;
        rearRightWheel.brakeTorque = wheelSettings.brakeForce;
    }

    private void ReleaseBrake()
    {
        frontLeftWheel.brakeTorque = 0f;
        frontRightWheel.brakeTorque = 0f;
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;
    }

    private void HandleSteering()
    {
        float currentSteerAngle = wheelSettings.maxSteerAngle * steerInput;
        frontLeftWheel.steerAngle = currentSteerAngle;
        frontRightWheel.steerAngle = currentSteerAngle;
    }

    private void UpdateWheelPoses()
    {
        UpdateWheelPose(frontLeftWheel, frontLeftTransform);
        UpdateWheelPose(frontRightWheel, frontRightTransform);
        UpdateWheelPose(rearLeftWheel, rearLeftTransform);
        UpdateWheelPose(rearRightWheel, rearRightTransform);
    }

    private void UpdateWheelPose(WheelCollider wheelCollider, Transform wheelTransform)
    {
        if (wheelTransform == null) return;

        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);

        wheelTransform.position = pos;
        wheelTransform.rotation = rot * Quaternion.Euler(0, 0, -90); // 휠 모델의 로컬 축 방향에 따라 정확한 회전 값은 달라질 수 있습니다.
    }

    private void ApplySpeedLimit()
    {
        if (rb.linearVelocity.magnitude > wheelSettings.maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * wheelSettings.maxSpeed;
        }
    }

    private void ApplyDownforce()
    {
        // Apply a downward force to increase tire grip, proportional to the vehicle's speed.
        // This helps prevent slipping on slopes and improves stability at high speeds.
        rb.AddForce(-transform.up * wheelSettings.downforce * rb.linearVelocity.magnitude);
    }

    private void ApplyAntiRoll()
    {
        ApplyAntiRollToAxle(frontLeftWheel, frontRightWheel);
        ApplyAntiRollToAxle(rearLeftWheel, rearRightWheel);
    }

    private void ApplyAntiRollToAxle(WheelCollider wheelL, WheelCollider wheelR)
    {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        bool groundedL = wheelL.GetGroundHit(out hit);
        if (groundedL)
        {
            travelL = (-wheelL.transform.InverseTransformPoint(hit.point).y - wheelL.radius) / wheelL.suspensionDistance;
        }

        bool groundedR = wheelR.GetGroundHit(out hit);
        if (groundedR)
        {
            travelR = (-wheelR.transform.InverseTransformPoint(hit.point).y - wheelR.radius) / wheelR.suspensionDistance;
        }

        float antiRollForce = (travelL - travelR) * wheelSettings.antiRollForce;

        if (groundedL)
        {
            rb.AddForceAtPosition(wheelL.transform.up * -antiRollForce, wheelL.transform.position);
        }
        if (groundedR)
        {
            rb.AddForceAtPosition(wheelR.transform.up * antiRollForce, wheelR.transform.position);
        }
    }

    private void PlaceOnGround()
    {
        // Start the ray from high above the vehicle's current position
        Vector3 rayStart = new Vector3(transform.position.x, 1000f, transform.position.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 2000f, groundLayer))
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
            Debug.Log($"Player '{gameObject.name}' placed on ground at {transform.position}", this);
        }
        else
        {
            Debug.LogWarning($"PlaceOnGround: Could not find ground beneath player '{gameObject.name}'. Check ground layer and distance.", this);
        }
    }

    void OnDrawGizmos()
    {
        if (rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(rb.worldCenterOfMass, 0.1f); // Visualize CoM
        }
    }
}