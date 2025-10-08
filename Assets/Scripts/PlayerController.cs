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
    [SerializeField] private float motorForce = 1500f; // Torque applied to motor wheels
    [SerializeField] private float maxSteerAngle = 30f; // Max steering angle for front wheels
    [SerializeField] private float brakeForce = 3000f; // Brake torque applied to all wheels

    // Input System Actions
    private InputSystem_Actions playerInputActions;
    private float steerInput;
    private float accelerateInput;
    private float brakeInput;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Rigidbody가 Kinematic이 아니고 중력을 사용하는지 확인
        rb.isKinematic = false;
        rb.useGravity = true;

        playerInputActions = new InputSystem_Actions();

        // 입력 액션에 대한 콜백 구독
        playerInputActions.Driver.Steer.performed += ctx => steerInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Steer.canceled += ctx => steerInput = 0f;

        playerInputActions.Driver.Accelerate.performed += ctx => accelerateInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Accelerate.canceled += ctx => accelerateInput = 0f;

        playerInputActions.Driver.Brake.performed += ctx => brakeInput = ctx.ReadValue<float>();
        playerInputActions.Driver.Brake.canceled += ctx => brakeInput = 0f;
    }

    private void OnEnable()
    {
        playerInputActions.Driver.Enable();
    }

    private void OnDisable()
    {
        playerInputActions.Driver.Disable();
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        HandleBraking();
        UpdateWheelPoses();
    }

    private void HandleMotor()
    {
        // 후륜에 모터 토크 적용 (간단화를 위해 RWD)
        rearLeftWheel.motorTorque = accelerateInput * motorForce;
        rearRightWheel.motorTorque = accelerateInput * motorForce;

        // 가속하지 않을 때 모터 토크를 0으로 설정
        if (accelerateInput == 0)
        {
            rearLeftWheel.motorTorque = 0;
            rearRightWheel.motorTorque = 0;
        }
    }

    private void HandleSteering()
    {
        float currentSteerAngle = maxSteerAngle * steerInput;
        frontLeftWheel.steerAngle = currentSteerAngle;
        frontRightWheel.steerAngle = currentSteerAngle;
    }

    private void HandleBraking()
    {
        if (brakeInput > 0.01f)
        {
            // 모든 휠에 브레이크 토크 적용
            frontLeftWheel.brakeTorque = brakeForce;
            frontRightWheel.brakeTorque = brakeForce;
            rearLeftWheel.brakeTorque = brakeForce;
            rearRightWheel.brakeTorque = brakeForce;
        }
        else
        {
            // 브레이크 해제
            frontLeftWheel.brakeTorque = 0f;
            frontRightWheel.brakeTorque = 0f;
            rearLeftWheel.brakeTorque = 0f;
            rearRightWheel.brakeTorque = 0f;
        }
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
}
