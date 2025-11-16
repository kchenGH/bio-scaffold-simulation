using UnityEngine;

public class envControl : MonoBehaviour
{
    [Range(0f, 1f)]
    public float opacity = 1f;

    private Renderer[] renderers;

    void Start()
    {
        // Get all child renderers (including nested)
        renderers = GetComponentsInChildren<Renderer>();

        // Make every material transparent once
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                mat.SetFloat("_Surface", 1); // URP: Transparent
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);

                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
            }
        }
    }

    void Update()
    {
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                Color c = mat.color;
                c.a = opacity;
                mat.color = c;
            }
        }
    }
}
