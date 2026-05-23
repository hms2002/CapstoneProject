using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BookPixelRevealPresentation : MonoBehaviour
{
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int PixelColumnsId = Shader.PropertyToID("_PixelColumns");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("References")]
    [SerializeField] private Graphic overlayGraphic;
    [SerializeField] private CanvasGroup interactionGate;
    [SerializeField] private Material revealMaterialTemplate;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float firstOpenDuration = 0.2f;
    [SerializeField, Min(0f)] private float categoryTransitionDuration = 0.14f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Pixel Reveal")]
    [SerializeField] private Color overlayColor = new Color(0.04f, 0.035f, 0.08f, 0.96f);
    [SerializeField, Min(8f)] private float pixelColumns = 58f;
    [SerializeField, Range(0f, 0.35f)] private float noiseStrength = 0.18f;
    [SerializeField] private string revealShaderName = "UI/Book Pixel Reveal Overlay";

    private Material runtimeMaterial;
    private Coroutine activeRoutine;
    private bool usesRevealShader;

    public bool IsPlaying => activeRoutine != null;

    private void Awake()
    {
        ResolveReferences();
        HideOverlayImmediate();
    }

    private void OnDisable()
    {
        CancelAndHide();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);

            runtimeMaterial = null;
        }
    }

    public void PlayFirstOpen(Action onComplete = null)
    {
        PlayReveal(firstOpenDuration, null, onComplete);
    }

    public void PlayCategoryTransition(Action swapContent, Action onComplete = null)
    {
        PlayReveal(categoryTransitionDuration, swapContent, onComplete);
    }

    public void CancelAndHide()
    {
        StopActiveRoutine();
        ApplyReveal(1f);
        HideOverlayImmediate();
        SetInteractionEnabled(true);
    }

    private void PlayReveal(float duration, Action swapContent, Action onComplete)
    {
        StopActiveRoutine();
        ResolveReferences();
        EnsureRuntimeMaterial();

        if (overlayGraphic == null)
        {
            swapContent?.Invoke();
            onComplete?.Invoke();
            return;
        }

        overlayGraphic.gameObject.SetActive(true);
        ApplyReveal(0f);
        SetInteractionEnabled(false);

        swapContent?.Invoke();

        if (!gameObject.activeInHierarchy || duration <= 0f)
        {
            ApplyReveal(1f);
            HideOverlayImmediate();
            SetInteractionEnabled(true);
            onComplete?.Invoke();
            return;
        }

        activeRoutine = StartCoroutine(PlayRevealRoutine(duration, onComplete));
    }

    private IEnumerator PlayRevealRoutine(float duration, Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyReveal(SmoothStep(t));
            yield return null;
        }

        ApplyReveal(1f);
        HideOverlayImmediate();
        SetInteractionEnabled(true);
        activeRoutine = null;
        onComplete?.Invoke();
    }

    private void ApplyReveal(float reveal)
    {
        if (overlayGraphic == null)
            return;

        reveal = Mathf.Clamp01(reveal);
        Color color = overlayColor;

        if (usesRevealShader && runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(RevealId, reveal);
            runtimeMaterial.SetFloat(PixelColumnsId, Mathf.Max(8f, pixelColumns));
            runtimeMaterial.SetFloat(NoiseStrengthId, noiseStrength);
            runtimeMaterial.SetFloat(AspectId, ResolveAspect());
            runtimeMaterial.SetColor(ColorId, overlayColor);
            color.a = overlayColor.a;
        }
        else
        {
            color.a = Mathf.Lerp(overlayColor.a, 0f, reveal);
        }

        overlayGraphic.color = color;
    }

    private float ResolveAspect()
    {
        RectTransform rect = overlayGraphic != null ? overlayGraphic.transform as RectTransform : null;
        if (rect == null)
            return 1f;

        Rect r = rect.rect;
        if (r.height <= 0.001f)
            return 1f;

        return Mathf.Max(0.1f, r.width / r.height);
    }

    private void EnsureRuntimeMaterial()
    {
        if (overlayGraphic == null)
            return;

        if (runtimeMaterial == null)
        {
            Shader revealShader = revealMaterialTemplate != null
                ? revealMaterialTemplate.shader
                : Shader.Find(revealShaderName);

            if (revealShader != null)
            {
                runtimeMaterial = revealMaterialTemplate != null
                    ? new Material(revealMaterialTemplate)
                    : new Material(revealShader);
            }
        }

        usesRevealShader = runtimeMaterial != null && runtimeMaterial.HasProperty(RevealId);
        if (runtimeMaterial != null)
            overlayGraphic.material = runtimeMaterial;
    }

    private void HideOverlayImmediate()
    {
        if (overlayGraphic == null)
            return;

        overlayGraphic.gameObject.SetActive(false);
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (interactionGate == null)
            return;

        interactionGate.interactable = enabled;
        interactionGate.blocksRaycasts = enabled;
    }

    private void ResolveReferences()
    {
        if (overlayGraphic == null)
            overlayGraphic = GetComponentInChildren<Graphic>(true);

        if (interactionGate == null)
            interactionGate = GetComponentInParent<CanvasGroup>();
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
