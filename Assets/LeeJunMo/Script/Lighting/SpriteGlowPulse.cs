using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteGlowPulse : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float minIntensity = 1.5f;
    [SerializeField] private float maxIntensity = 3.5f;
    [SerializeField] private float speed = 2f;

    // Shader Graph Property Reference 이름 확인해라.
    // 기본적으로 _GlowIntensity 일 가능성이 높다.
    [SerializeField] private string glowIntensityProperty = "_GlowIntensity";

    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(glowIntensityProperty, intensity);
        spriteRenderer.SetPropertyBlock(mpb);
    }
}