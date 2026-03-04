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

        if (hitCount > 0)
        {
            // This log is spammy, enable for deep debugging if needed
            // Debug.Log($"[OcclusionFader] Raycast hit {hitCount} objects on the occlusion layer.");
        }

        for (int i = 0; i < hitCount; i++)
        {
            if (hits[i].collider.TryGetComponent<Renderer>(out Renderer renderer))
            {
                if (!currentlyOccluding.Contains(renderer))
                {
                    currentlyOccluding.Add(renderer);
                }
            }
        }

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

        foreach (Renderer renderer in currentlyOccluding)
        {
            if (fadedObjects.All(obj => obj.renderer != renderer))
            {
                Debug.Log($"[OcclusionFader] New occluding object detected: {renderer.name}. Adding to fade list.", renderer.gameObject);
                fadedObjects.Add(new FadingObject(renderer));
            }
        }
        
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

    public FadingObject(Renderer renderer)
    {
        this.renderer = renderer;
        originalMaterials = renderer.sharedMaterials;
        originalColors = new Color[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] != null)
            {
                originalColors[i] = originalMaterials[i].color;
            }
        }
    }

    public bool FadeTo(float targetAlpha, float fadeSpeed)
    {
        bool allMaterialsAtTarget = true;

        if (!materialsCloned)
        {
            Debug.Log($"[FadingObject] Cloning materials for {renderer.name} to enable transparency.", renderer.gameObject);
            renderer.materials = renderer.materials;
            materialsCloned = true;
        }

        for (int i = 0; i < renderer.materials.Length; i++)
        {
            Material mat = renderer.materials[i];
            if (mat == null) continue;

            Color currentColor = mat.color;
            Color targetColor = new Color(originalColors[i].r, originalColors[i].g, originalColors[i].b, targetAlpha);

            if (Mathf.Abs(currentColor.a - targetAlpha) > 0.01f)
            {
                mat.color = Color.Lerp(currentColor, targetColor, fadeSpeed * Time.deltaTime);
                allMaterialsAtTarget = false;

                // This log is very spammy, enable for deep debugging
                // Debug.Log($"[FadingObject] Fading material '{mat.name}' on '{renderer.name}' towards alpha {targetAlpha}. Current alpha: {mat.color.a}", renderer.gameObject);
            }
        }
        return allMaterialsAtTarget;
    }
}