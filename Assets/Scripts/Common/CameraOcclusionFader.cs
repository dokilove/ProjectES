using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CameraOcclusionFader : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("The target object the camera is looking at (e.g., the player or vehicle).")]
    public Transform target;

    [Header("Fading Settings")]
    [Tooltip("The layer containing objects that should fade when they block the view.")]
    public LayerMask occlusionLayer;

    [Tooltip("The target alpha value for faded objects.")]
    [Range(0, 1)]
    public float fadedAlpha = 0.3f;

    [Tooltip("The speed at which objects fade in and out.")]
    public float fadeSpeed = 10f;

    private List<FadingObject> fadedObjects = new List<FadingObject>();
    private RaycastHit[] hits = new RaycastHit[10];

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("[OcclusionFader] Target is not assigned!", this);
            return;
        }

        List<Renderer> currentlyOccluding = new List<Renderer>();
        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        int hitCount = Physics.RaycastNonAlloc(transform.position, direction, hits, distance, occlusionLayer);

        // 1. 레이캐스트에 맞은 오브젝트의 '모든 자식 렌더러'를 수집합니다. (구조가 복잡한 모델 대응)
        for (int i = 0; i < hitCount; i++)
        {
            Renderer[] renderers = hits[i].collider.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (!currentlyOccluding.Contains(renderer))
                {
                    currentlyOccluding.Add(renderer);
                }
            }
        }

        // 2. 더 이상 가리지 않는 오브젝트는 불투명하게 되돌립니다.
        for (int i = fadedObjects.Count - 1; i >= 0; i--)
        {
            FadingObject fadingObj = fadedObjects[i];
            if (!currentlyOccluding.Contains(fadingObj.renderer))
            {
                bool fullyOpaque = fadingObj.FadeTo(1.0f, fadeSpeed);
                if (fullyOpaque)
                {
                    fadedObjects.RemoveAt(i);
                }
            }
        }

        // 3. 새로 가리기 시작한 오브젝트를 리스트에 추가합니다.
        foreach (Renderer renderer in currentlyOccluding)
        {
            if (fadedObjects.All(obj => obj.renderer != renderer))
            {
                fadedObjects.Add(new FadingObject(renderer));
            }
        }

        // 4. 가리고 있는 오브젝트들을 투명하게 만듭니다.
        foreach (FadingObject fadingObj in fadedObjects)
        {
            fadingObj.FadeTo(fadedAlpha, fadeSpeed);
        }
    }
}

public class FadingObject
{
    public Renderer renderer;
    private Material[] originalMaterials;
    private Color[] originalColors;
    private bool materialsCloned = false;

    // URP와 Standard 쉐이더의 색상 속성 ID를 미리 캐싱하여 성능을 최적화합니다.
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    public FadingObject(Renderer renderer)
    {
        this.renderer = renderer;
        originalMaterials = renderer.sharedMaterials;
        originalColors = new Color[originalMaterials.Length];

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] != null)
            {
                // URP (_BaseColor) 우선 확인, 없으면 Standard (_Color) 확인
                if (originalMaterials[i].HasProperty(BaseColorID))
                {
                    originalColors[i] = originalMaterials[i].GetColor(BaseColorID);
                }
                else if (originalMaterials[i].HasProperty(ColorID))
                {
                    originalColors[i] = originalMaterials[i].GetColor(ColorID);
                }
                else
                {
                    originalColors[i] = Color.white; // 색상 속성을 못 찾을 경우 기본값
                }
            }
        }
    }

    public bool FadeTo(float targetAlpha, float fadeSpeed)
    {
        bool allMaterialsAtTarget = true;

        if (!materialsCloned)
        {
            // 원본 매터리얼 오염을 막기 위해 인스턴스로 복제합니다.
            renderer.materials = renderer.materials;
            materialsCloned = true;
        }

        for (int i = 0; i < renderer.materials.Length; i++)
        {
            Material mat = renderer.materials[i];
            if (mat == null) continue;

            Color currentColor = Color.white;
            bool hasBaseColor = mat.HasProperty(BaseColorID);
            bool hasColor = mat.HasProperty(ColorID);

            if (hasBaseColor) currentColor = mat.GetColor(BaseColorID);
            else if (hasColor) currentColor = mat.GetColor(ColorID);

            Color targetColor = new Color(originalColors[i].r, originalColors[i].g, originalColors[i].b, targetAlpha);

            // 알파값이 목표치에 도달하지 않았다면 부드럽게 변경 (Lerp)
            if (Mathf.Abs(currentColor.a - targetAlpha) > 0.01f)
            {
                Color newColor = Color.Lerp(currentColor, targetColor, fadeSpeed * Time.deltaTime);

                if (hasBaseColor) mat.SetColor(BaseColorID, newColor);
                else if (hasColor) mat.SetColor(ColorID, newColor);

                allMaterialsAtTarget = false;
            }
            else
            {
                // 소수점 오차 방지를 위해 목표값에 도달하면 강제 고정
                if (hasBaseColor) mat.SetColor(BaseColorID, targetColor);
                else if (hasColor) mat.SetColor(ColorID, targetColor);
            }
        }
        return allMaterialsAtTarget;
    }
}