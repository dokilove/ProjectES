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

    [Header("Visuals (Scale & Pulse)")]
    [Tooltip("대시 및 차징 시 스케일(크기)이 변할 자동차 모델 (BodyTiltRoot의 자식이어야 함)")]
    public Transform carModel;
    public float minScalePulseSpeed = 2f;
    public float maxScalePulseSpeed = 15f;
    public float scalePulseAmount = 0.05f;
    [Tooltip("How much the car stretches on the Z-axis during a dash.")]
    public float dashStretchAmount = 0.2f;
    [Tooltip("How quickly the car stretches and returns to normal.")]
    public float dashStretchSpeed = 15f;

    // --- ★ 추가된 차체 기울임 (Pitch & Roll) 설정 ---
    [Header("Body Tilt Settings (Visual)")]
    [Tooltip("회전(기울어짐)이 적용될 최상위 모델 루트 (carModel의 부모 역할 권장)")]
    [SerializeField] private Transform bodyTiltRoot;
    [SerializeField] private float pitchSensitivity = 0.2f;
    [SerializeField] private float rollSensitivity = 0.15f;
    [SerializeField] private float tiltSmoothTime = 0.15f;

    // ★ 차징 시 사정없이 떨리는 진동 강도
    [SerializeField] private float maxChargeShakePitch = 3f;
    [SerializeField] private float maxChargeShakeRoll = 3f;

    private float currentPitch;
    private float currentRoll;
    private float pitchVelocity;
    private float rollVelocity;
    private Vector3 lastVelocity;
    private float lastEulerY;

    // ★ 모델의 원래 회전값을 기억할 변수
    private Quaternion initialTiltRotation;
    // ------------------------------------------------

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

        if (bodyTiltRoot == null)
        {
            bodyTiltRoot = carModel;
        }

        if (bodyTiltRoot != null)
        {
            initialTiltRotation = bodyTiltRoot.localRotation;
        }

        // Initialize debug circle renderer
        SetupDebugCircle();
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
        HandleBodyTilt();
    }

    private void LateUpdate()
    {
        DrawDebugCircle();
    }

    private void HandleBodyTilt()
    {
        if (bodyTiltRoot == null) return;
        if (Time.deltaTime > 0)
        {
            Vector3 currentMoveVelocity = characterController.velocity;
            Vector3 localVelocity = transform.InverseTransformDirection(currentMoveVelocity);
            Vector3 localLastVelocity = transform.InverseTransformDirection(lastVelocity);

            float pitchAcceleration = (localVelocity.z - localLastVelocity.z) / Time.deltaTime;
            float targetPitch = -pitchAcceleration * pitchSensitivity;

            float currentEulerY = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastEulerY, currentEulerY) / Time.deltaTime;

            float targetRoll = yawRate * rollSensitivity;

            if (isDashing)
            {
                targetPitch *= 0.3f;
                targetRoll *= 0.3f;
            }

            targetPitch = Mathf.Clamp(targetPitch, -15f, 15f);
            targetRoll = -Mathf.Clamp(targetRoll, -20f, 20f);

            currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, tiltSmoothTime);
            currentRoll = Mathf.SmoothDampAngle(currentRoll, targetRoll, ref rollVelocity, tiltSmoothTime);

            float finalPitch = currentPitch;
            float finalRoll = currentRoll;

            if (isCharging)
            {
                float chargeRatio = Mathf.Clamp01(chargeTimer / vehicleData.maxChargeTime);

                // ★ Random 난수 대신 pulsePhase를 사용하여 스케일 펌핑과 완벽 동기화!
                // Mathf.Cos(pulsePhase)를 쓰면 스케일이 가장 커질 때 차체가 앞으로 쏠리고, 작아질 때 뒤로 쏠립니다.
                // Mathf.Sin(pulsePhase * 2f)를 써서 스케일이 한 번 펌핑될 때 좌우로는 두 번 흔들리게 박자를 쪼갰습니다.
                float shakePitch = Mathf.Cos(pulsePhase) * maxChargeShakePitch * chargeRatio;
                float shakeRoll = Mathf.Sin(pulsePhase * 2f) * maxChargeShakeRoll * chargeRatio;

                finalPitch += shakePitch;
                finalRoll += shakeRoll;
            }

            Quaternion tiltRotation = Quaternion.Euler(finalPitch, 0, finalRoll);
            bodyTiltRoot.localRotation = initialTiltRotation * tiltRotation;

            lastVelocity = currentMoveVelocity;
            lastEulerY = currentEulerY;
        }
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

    private void HandleVisuals()
    {
        if (carModel == null) return;

        if (isCharging)
        {
            float chargeRatio = Mathf.Clamp01(chargeTimer / vehicleData.maxChargeTime);
            float currentPulseSpeed = Mathf.Lerp(minScalePulseSpeed, maxScalePulseSpeed, chargeRatio);

            pulsePhase += currentPulseSpeed * Time.deltaTime;

            // ★ Z축 변형은 빼고, Y축만 위아래로 삐죽삐죽 펌핑!
            float scaleOffsetY = 1.0f + Mathf.Sin(pulsePhase) * scalePulseAmount;
            carModel.localScale = new Vector3(originalScale.x, originalScale.y * scaleOffsetY, originalScale.z);
        }
        else if (isDashing)
        {
            // ★ 돌진 중: Y축은 납작하게(Squash), Z축은 길게(Stretch) 하여 스피드감 극대화!
            float stretch = 1.0f + dashStretchAmount; // ex: 1.2 (앞으로 길어짐)
            float squash = 1.0f / stretch;            // ex: 0.83 (아래로 납작해짐)

            Vector3 targetScale = new Vector3(originalScale.x, originalScale.y * squash, originalScale.z * stretch);
            carModel.localScale = Vector3.Lerp(carModel.localScale, targetScale, dashStretchSpeed * Time.deltaTime);
        }
        else
        {
            // 대시가 끝나면 스무스하게 원래 비율로 복귀
            carModel.localScale = Vector3.Lerp(carModel.localScale, originalScale, dashStretchSpeed * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (vehicleData == null) return;

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

    private void OnDashStart(InputAction.CallbackContext context)
    {
        if (isDashing) return;

        isCharging = true;
        chargeTimer = 0f;
        pulsePhase = 0f;
    }

    private void OnDashRelease(InputAction.CallbackContext context)
    {
        if (!isCharging) return;
        isCharging = false;

        if (chargeTimer < vehicleData.minChargeTime)
        {
            if (carModel != null) carModel.localScale = originalScale;
            return;
        }

        isDashing = true;
        dashDirection = transform.forward;

        // ★ 발진 순간의 타격감: Y축을 순간적으로 납작하게, Z축을 확 길게 세팅해두고 돌진 시작
        if (carModel != null)
        {
            carModel.localScale = new Vector3(originalScale.x, originalScale.y * 0.5f, originalScale.z * 1.5f);
        }

        float chargeRatio = Mathf.Clamp01((chargeTimer - vehicleData.minChargeTime) / (vehicleData.maxChargeTime - vehicleData.minChargeTime));
        dashTimer = Mathf.Lerp(vehicleData.minDashDuration, vehicleData.maxDashDuration, chargeRatio);
        currentDashSpeed = Mathf.Lerp(vehicleData.minDashSpeed, vehicleData.maxDashSpeed, chargeRatio);
    }

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

    private void HandleDash()
    {
        if (dashTimer > 0)
        {
            HandleDashPush();

            if (characterController.isGrounded)
            {
                verticalVelocity = -vehicleData.groundSnapForce;
            }
            else
            {
                verticalVelocity -= vehicleData.extraGravityForce * Time.fixedDeltaTime;
            }

            Vector3 dashMovement = dashDirection * currentDashSpeed;
            dashMovement.y = verticalVelocity;

            characterController.Move(dashMovement * Time.fixedDeltaTime);
            dashTimer -= Time.fixedDeltaTime;
        }
        else
        {
            isDashing = false;
            currentSpeed = 0;
        }
    }

    private void HandleDashPush()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, dashPushRadius, enemyLayer);
        foreach (var hitCollider in hitColliders)
        {
            EnemyAI enemy = hitCollider.GetComponentInParent<EnemyAI>();

            if (enemy != null)
            {
                Vector3 pushDirection = (enemy.transform.position - transform.position).normalized;
                pushDirection.y = 0;
                enemy.ApplyPushForce(pushDirection, dashPushForce);
            }
        }
    }

    // --- ★ 버튼 없는 스마트 자동 후진 로직 적용 ---
    private void HandleMovement()
    {
        Transform cameraTransform = Camera.main.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 targetMoveVector = (cameraForward * moveInput.y + cameraRight * moveInput.x);

        if (isCharging)
        {
            if (targetMoveVector.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetMoveVector.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);
            }

            float chargeTargetSpeed = vehicleData.maxSpeed * vehicleData.chargeMoveSpeedMultiplier * moveInput.magnitude;
            currentSpeed = Mathf.Lerp(currentSpeed, chargeTargetSpeed, acceleration * Time.fixedDeltaTime);
        }
        else // Normal Movement
        {
            if (targetMoveVector.sqrMagnitude > 0.01f)
            {
                float inputMagnitude = Mathf.Clamp01(targetMoveVector.magnitude);
                float finalSpeedTarget = vehicleData.maxSpeed * inputMagnitude; // 기본 목표 전진 속도
                Vector3 finalSteerDirection = targetMoveVector.normalized;      // 기본 조향 방향

                // ★ 스마트 자동 후진 체크 로직
                // 현재 차가 바라보는 방향과 스틱 방향의 내적(Dot) 계산
                float dot = Vector3.Dot(transform.forward, targetMoveVector.normalized);

                // 스틱 방향이 차체 기준 약간 뒤쪽(약 95도 이상)을 향한다면 자동 후진 발동
                if (dot < -0.1f)
                {
                    finalSpeedTarget = -vehicleData.maxSpeed * inputMagnitude; // 목표 속도를 마이너스로!
                    finalSteerDirection = -targetMoveVector.normalized;        // 엉덩이가 타겟을 보게끔 앞머리 방향을 반대로 조향
                }

                // 부드럽게 방향 회전 적용 (전진/후진 상황에 맞춰진 방향으로)
                Quaternion targetRotation = Quaternion.LookRotation(finalSteerDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, vehicleData.rotationSpeed * Time.fixedDeltaTime);

                // 부드럽게 목표 속도로 가감속 (Lerp를 쓰기 때문에 급발진하지 않고 제동 후 마이너스로 내려갑니다)
                currentSpeed = Mathf.Lerp(currentSpeed, finalSpeedTarget, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                // 입력이 없으면 자연스럽게 0으로 감속
                currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
            }
        }

        // 최종 이동 적용 (currentSpeed가 양수면 앞, 음수면 뒤로 물리적으로 이동)
        Vector3 finalVelocity = transform.forward * currentSpeed;
        moveDirection.x = finalVelocity.x;
        moveDirection.z = finalVelocity.z;
        characterController.Move(moveDirection * Time.fixedDeltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, dashPushRadius);
    }
#endif
}