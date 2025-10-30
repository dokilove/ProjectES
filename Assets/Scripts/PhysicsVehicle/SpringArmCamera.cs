using UnityEngine;
using UnityEngine.InputSystem;

public class SpringArmCamera : MonoBehaviour
{
    public Transform target; // 카메라가 따라갈 대상 (자동차)
    public float smoothSpeed = 0.125f; // 카메라 이동의 부드러움 정도
    public float rotationSpeed = 100f; // 카메라 회전 속도

    private InputSystem_Actions playerInputActions;
    private Vector2 cameraLookInput;

    private float currentYaw = 0f;   // Y축 회전 (좌우)
    private float currentPitch = 0f; // X축 회전 (상하)

    private Vector3 initialOffsetFromTarget;

    private float initialYaw;
    private float initialPitch;

    private void Awake()
    {
        playerInputActions = new InputSystem_Actions();

        // CameraLook 액션에 대한 콜백 구독
        playerInputActions.Driver.CameraLook.performed += ctx => cameraLookInput = ctx.ReadValue<Vector2>();
        playerInputActions.Driver.CameraLook.canceled += ctx => cameraLookInput = Vector2.zero;
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("SpringArmCamera: Target is not assigned.", this);
            enabled = false;
            return;
        }

        initialOffsetFromTarget = transform.position - target.position;

        // Extract initial yaw and pitch from the initial rotation relative to the target's forward
        // This assumes the camera starts looking somewhat behind the target
        Vector3 relativeForward = Quaternion.Inverse(target.rotation) * transform.forward;
        initialYaw = Mathf.Atan2(relativeForward.x, relativeForward.z) * Mathf.Rad2Deg;
        initialPitch = Mathf.Asin(relativeForward.y) * Mathf.Rad2Deg;

        currentYaw = initialYaw;
        currentPitch = initialPitch;
    }

    private void OnEnable()
    {
        playerInputActions.Driver.Enable();
    }

    private void OnDisable()
    {
        playerInputActions.Driver.Disable();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 카메라 리그(이 GameObject)의 위치를 타겟에 부드럽게 따라가도록 업데이트
        Vector3 targetPosition = target.position;
        transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime * 10f));

        // 입력에 따라 카메라 회전 처리
        currentYaw += cameraLookInput.x * rotationSpeed * Time.deltaTime;
        currentPitch -= cameraLookInput.y * rotationSpeed * Time.deltaTime; // Y축 반전 (마우스/스틱 조작 직관성)

        // 수직 회전 범위 제한 (카메라가 뒤집히는 것을 방지)
        currentPitch = Mathf.Clamp(currentPitch, -60f, 80f); // 예시 값

        // 카메라 리그에 회전 적용
        float targetYaw = target.eulerAngles.y + currentYaw;
        transform.rotation = Quaternion.Euler(currentPitch, targetYaw, 0f);
    }

    public void ResetCamera()
    {
        if (target == null) return;

        transform.position = target.position + initialOffsetFromTarget;

        currentYaw = initialYaw;
        currentPitch = initialPitch;
        cameraLookInput = Vector2.zero;
    }
}
