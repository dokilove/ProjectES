using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class ArcadeVehicleController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ArcadeVehicleDataSO vehicleData;

    [Header("Movement Tuning")]
    [Tooltip("아크 턴(Arc Turn)의 묵직함을 결정하는 가속/감속 수치입니다.")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 8f;
    private float currentSpeed = 0f; // 현재 속도 캐싱

    [Header("Grounding")]
    [Tooltip("Set this to the layer your ground objects are on.")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 50f;

    [Header("Cameras")]
    public CinemachineCamera backViewCam;    // 백뷰 (평소)
    public CinemachineCamera quarterViewCam; // 쿼터뷰 (전략)

    private CharacterController characterController;
    private InputSystem_Actions playerActions;

    private Vector2 moveInput;
    private Vector3 moveDirection;
    private float verticalVelocity;

    // 카메라 상태 추적용 변수
    private bool isBackViewActive = false;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Vehicle_Arcade.Enable();
        playerActions.Vehicle_Arcade.ChangeCamera.performed += OnChangeCamera;
    }

    private void OnDisable()
    {
        playerActions.Vehicle_Arcade.ChangeCamera.performed -= OnChangeCamera;
        playerActions.Vehicle_Arcade.Disable();
    }

    private void Start()
    {
        UpdateCameraPriorities();
    }

    private void Update()
    {
        moveInput = playerActions.Vehicle_Arcade.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (vehicleData == null) return;

        HandleGravity();
        HandleMovement();
        HandleGroundSnapping();
    }

    private void OnChangeCamera(InputAction.CallbackContext context)
    {
        isBackViewActive = !isBackViewActive;
        UpdateCameraPriorities();
    }

    private void UpdateCameraPriorities()
    {
        if (backViewCam == null || quarterViewCam == null) return;

        if (isBackViewActive)
        {
            backViewCam.Priority = 20;
            quarterViewCam.Priority = 10;
        }
        else
        {
            backViewCam.Priority = 10;
            quarterViewCam.Priority = 20;
        }
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

        // y축 속도만 갱신 (x, z는 HandleMovement에서 덮어씌움)
        moveDirection.y = verticalVelocity;
    }

    private void HandleMovement()
    {
        Transform cameraTransform = Camera.main.transform;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        Vector3 effectiveForward;
        Vector3 effectiveRight;

        float dotProduct = Vector3.Dot(cameraForward, Vector3.down);

        if (dotProduct > 0.9f)
        {
            effectiveForward = cameraTransform.up;
            effectiveRight = cameraTransform.right;
        }
        else
        {
            effectiveForward = cameraForward;
            effectiveRight = cameraRight;
        }

        effectiveForward.y = 0;
        effectiveRight.y = 0;

        effectiveForward.Normalize();
        effectiveRight.Normalize();

        // 목표 이동 방향 벡터
        Vector3 targetMoveVector = (effectiveForward * moveInput.y + effectiveRight * moveInput.x);

        // --- 여기서부터 아크(Arc) 턴 로직 적용 ---
        if (targetMoveVector.sqrMagnitude > 0.01f)
        {
            // 1. [조향] 목표 방향으로 차체를 부드럽게 회전시킵니다.
            Quaternion targetRotation = Quaternion.LookRotation(targetMoveVector.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);

            // 2. [가속] 입력이 있을 때 최고 속도를 향해 서서히 가속합니다.
            float inputMagnitude = Mathf.Clamp01(targetMoveVector.magnitude);
            currentSpeed = Mathf.Lerp(currentSpeed, vehicleData.maxSpeed * inputMagnitude, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // 3. [감속] 입력이 없으면 서서히 멈춥니다.
            currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
        }

        // 4. [이동] 입력 방향(targetMoveVector)이 아닌, "현재 차체의 앞방향(Forward)"으로 속도를 적용합니다.
        Vector3 forwardVelocity = transform.forward * currentSpeed;

        // X, Z 축 이동 방향 갱신 (Y축은 HandleGravity에서 유지 중)
        moveDirection.x = forwardVelocity.x;
        moveDirection.z = forwardVelocity.z;

        // 최종 이동 적용
        characterController.Move(moveDirection * Time.fixedDeltaTime);
    }

    private void HandleGroundSnapping()
    {
        // CharacterController가 있으므로 현재는 비워둠
    }
}