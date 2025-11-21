using UnityEngine;
using UnityEngine.UIElements;

// v1.6: Reverted to simpler rotation logic, removed goal-pointing.
public class UIToolkitMinimapController : MonoBehaviour
{
    [Header("UI Setup")]
    [Tooltip("미니맵 UI를 포함하는 UI Document.")]
    public UIDocument uiDocument;
    [Tooltip("미니맵 카메라가 렌더링할 Render Texture.")]
    public RenderTexture minimapTexture;

    [Header("Minimap Camera Settings")]
    [Tooltip("미니맵 카메라의 높이.")]
    public float cameraHeight = 100f;
    [Tooltip("미니맵 카메라의 시야 크기 (Zoom).")]
    public float cameraSize = 25f;

    [Header("Dependencies")]
    [Tooltip("카메라를 관리하는 CameraSwitcher 스크립트.")]
    public CameraSwitcher cameraSwitcher;

    private Transform playerTransform;
    private Camera minimapCamera;
    private Image minimapImage;

    void Start()
    {
        // 플레이어 찾기
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("MinimapController: 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다!");
            this.enabled = false;
            return;
        }
        playerTransform = playerObject.transform;

        // 의존성 스크립트들 찾기
        if (cameraSwitcher == null) cameraSwitcher = FindObjectOfType<CameraSwitcher>();
        if (cameraSwitcher == null)
        {
            Debug.LogError("MinimapController: 씬에서 CameraSwitcher를 찾을 수 없습니다!");
            this.enabled = false;
            return;
        }

        // UI 요소 찾기
        var root = uiDocument.rootVisualElement;
        minimapImage = root.Q<Image>("MinimapImage");

        if (minimapImage == null)
        {
            Debug.LogError("MinimapController: UXML에서 'MinimapImage'를 찾을 수 없습니다!");
            this.enabled = false;
            return;
        }

        // 미니맵 카메라 생성 및 설정
        if (minimapCamera == null)
        {
            GameObject camObj = new GameObject("MinimapCamera");
            minimapCamera = camObj.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            minimapCamera.cullingMask = -1;
        }
        
        minimapCamera.orthographicSize = cameraSize;
        minimapCamera.targetTexture = minimapTexture;

        minimapImage.image = minimapTexture;
    }

    void LateUpdate()
    {
        if (playerTransform == null || minimapCamera == null || cameraSwitcher == null) return;

        // 미니맵 카메라 위치 업데이트
        Vector3 newPos = playerTransform.position;
        newPos.y = cameraHeight;
        minimapCamera.transform.position = newPos;

        Camera activeCam = cameraSwitcher.ActiveCamera;
        if (activeCam == null) return;

        // 이전 버전의 회전 로직으로 복원
        Vector3 referenceForward;
        if (Vector3.Dot(activeCam.transform.forward, Vector3.down) > 0.98f)
        {
            referenceForward = activeCam.transform.up;
        }
        else
        {
            referenceForward = activeCam.transform.forward;
        }

        referenceForward.y = 0;
        if (referenceForward.sqrMagnitude < 0.001f)
        {
            referenceForward = Vector3.forward;
        }
        referenceForward.Normalize();
        
        float mapRotationAngle = Vector3.SignedAngle(Vector3.forward, referenceForward, Vector3.up);
        minimapImage.transform.rotation = Quaternion.Euler(0, 0, -mapRotationAngle);
    }
}
