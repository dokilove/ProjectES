using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class ArcadeVehicleController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ArcadeVehicleDataSO vehicleData;

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

        // 1. 카메라 전환 버튼 이벤트 연결 (performed 사용)
        // 주의: Input Action Asset에 'ChangeCamera' 액션이 만들어져 있어야 합니다.
        playerActions.Vehicle_Arcade.ChangeCamera.performed += OnChangeCamera;
    }

    private void OnDisable()
    {
        // 이벤트 연결 해제 (메모리 누수 방지)
        playerActions.Vehicle_Arcade.ChangeCamera.performed -= OnChangeCamera;
        playerActions.Vehicle_Arcade.Disable();
    }

    private void Start()
    {
        // 게임 시작 시 초기 카메라 상태 적용
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

    // --- [추가된 기능] 카메라 전환 로직 ---
    private void OnChangeCamera(InputAction.CallbackContext context)
    {
        // 버튼을 누를 때마다 상태 반전 (True <-> False)
        isBackViewActive = !isBackViewActive;
        UpdateCameraPriorities();
    }

    private void UpdateCameraPriorities()
    {
        if (backViewCam == null || quarterViewCam == null) return;

        if (isBackViewActive)
        {
            // 백뷰 활성화 (우선순위 높임)
            backViewCam.Priority = 20;
            quarterViewCam.Priority = 10;
        }
        else
        {
            // 쿼터뷰 활성화
            backViewCam.Priority = 10;
            quarterViewCam.Priority = 20;
        }
    }
    // -------------------------------------

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

    private void HandleMovement()
    {
        Transform cameraTransform = Camera.main.transform;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        Vector3 effectiveForward;
        Vector3 effectiveRight;

        // 카메라가 바닥을 보고 있는지 확인 (Dot Product)
        float dotProduct = Vector3.Dot(cameraForward, Vector3.down);

        // 
        // 카메라가 90도 가까이 내려다볼 때의 이동 축 보정 로직
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

        Vector3 horizontalMoveVector = (effectiveForward * moveInput.y + effectiveRight * moveInput.x);

        if (moveInput.sqrMagnitude > 0.1f)
        {
            moveDirection.x = horizontalMoveVector.normalized.x * vehicleData.maxSpeed;
            moveDirection.z = horizontalMoveVector.normalized.z * vehicleData.maxSpeed;
        }
        else
        {
            moveDirection.x = 0;
            moveDirection.z = 0;
        }

        characterController.Move(moveDirection * Time.fixedDeltaTime);

        if (horizontalMoveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalMoveVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleGroundSnapping()
    {
        // CharacterController가 있으므로 현재는 비워둠
    }
}