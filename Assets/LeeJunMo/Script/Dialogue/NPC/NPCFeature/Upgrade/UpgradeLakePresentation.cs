using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[Serializable]
public struct UpgradeLakePresentationSettings
{
    private static readonly int DeepColorMaterialId = Shader.PropertyToID("_DeepColor");
    private static readonly int ShallowColorMaterialId = Shader.PropertyToID("_ShallowColor");
    private static readonly int CausticColorMaterialId = Shader.PropertyToID("_CausticColor");
    private static readonly int BottomTintMaterialId = Shader.PropertyToID("_BottomTint");
    private static readonly int FoamColorMaterialId = Shader.PropertyToID("_FoamColor");
    private static readonly int BottomStrengthMaterialId = Shader.PropertyToID("_BottomStrength");
    private static readonly int BottomTilingMaterialId = Shader.PropertyToID("_BottomTiling");
    private static readonly int BottomDriftMaterialId = Shader.PropertyToID("_BottomDrift");
    private static readonly int HeightTexStrengthMaterialId = Shader.PropertyToID("_HeightTexStrength");
    private static readonly int HeightTexTilingMaterialId = Shader.PropertyToID("_HeightTexTiling");
    private static readonly int HeightTexDriftMaterialId = Shader.PropertyToID("_HeightTexDrift");
    private static readonly int CausticTextureStrengthMaterialId = Shader.PropertyToID("_CausticTextureStrength");
    private static readonly int CausticTextureTilingMaterialId = Shader.PropertyToID("_CausticTextureTiling");
    private static readonly int CausticTextureSpeedMaterialId = Shader.PropertyToID("_CausticTextureSpeed");
    private static readonly int CausticTextureSharpnessMaterialId = Shader.PropertyToID("_CausticTextureSharpness");
    private static readonly int FoamStrengthMaterialId = Shader.PropertyToID("_FoamStrength");
    private static readonly int FoamTilingMaterialId = Shader.PropertyToID("_FoamTiling");
    private static readonly int FoamSpeedMaterialId = Shader.PropertyToID("_FoamSpeed");
    private static readonly int FoamThresholdMaterialId = Shader.PropertyToID("_FoamThreshold");
    private static readonly int AlphaMaterialId = Shader.PropertyToID("_Alpha");
    private static readonly int WaveScaleMaterialId = Shader.PropertyToID("_WaveScale");
    private static readonly int WaveSpeedMaterialId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveStrengthMaterialId = Shader.PropertyToID("_WaveStrength");
    private static readonly int CausticStrengthMaterialId = Shader.PropertyToID("_CausticStrength");
    private static readonly int TopDownDepthBiasMaterialId = Shader.PropertyToID("_TopDownDepthBias");
    private static readonly int ParallaxMaterialId = Shader.PropertyToID("_Parallax");
    private static readonly int BackgroundDistortionMaterialId = Shader.PropertyToID("_BackgroundDistortion");
    private static readonly int InteractionDistortionMaterialId = Shader.PropertyToID("_InteractionDistortion");
    private static readonly int InteractionCompressionMaterialId = Shader.PropertyToID("_InteractionCompression");
    private static readonly int DepthNoiseStrengthMaterialId = Shader.PropertyToID("_DepthNoiseStrength");
    private static readonly int SurfaceNormalStrengthMaterialId = Shader.PropertyToID("_SurfaceNormalStrength");
    private static readonly int SpecularStrengthMaterialId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int EdgeDarkeningMaterialId = Shader.PropertyToID("_EdgeDarkening");

    public bool enabled;
    public bool useUnscaledTime;

    [Header("Surface")]
    public Color deepColor;
    public Color shallowColor;
    public Color causticColor;
    [Range(0f, 1f)] public float surfaceAlpha;
    [Min(0.1f)] public float waveScale;
    [Min(0f)] public float waveSpeed;
    [Range(0f, 0.25f)] public float waveStrength;
    [Range(0f, 1f)] public float causticStrength;
    [Range(0f, 1f)] public float topDownDepthBias;
    [Range(0f, 2f)] public float contentParallax;
    [Range(0f, 0.08f)] public float backgroundDistortion;
    [Range(0f, 0.12f)] public float interactionDistortion;

    [Header("Lake Detail")]
    public Color bottomTint;
    public Color foamColor;
    [Range(0f, 1f)] public float bottomStrength;
    [Min(0.1f)] public float bottomTiling;
    public Vector2 bottomDrift;
    [Range(0f, 1f)] public float heightTextureStrength;
    [Min(0.1f)] public float heightTextureTiling;
    public Vector2 heightTextureDrift;
    [Range(0f, 2f)] public float causticTextureStrength;
    [Min(0.1f)] public float causticTextureTiling;
    public Vector4 causticTextureSpeed;
    [Range(0.25f, 8f)] public float causticTextureSharpness;
    [Range(0f, 1f)] public float foamStrength;
    [Min(0.1f)] public float foamTiling;
    public Vector4 foamSpeed;
    [Range(0f, 1f)] public float foamThreshold;
    [Range(0f, 1f)] public float depthNoiseStrength;
    [Range(0f, 1f)] public float surfaceNormalStrength;
    [Range(0f, 1f)] public float specularStrength;
    [Range(0f, 1f)] public float edgeDarkening;

    [Header("Ripples")]
    public Color rippleColor;
    public bool surfaceInteractionEnabled;
    public bool drawOverlayRipples;
    [Range(0f, 2f)] public float surfaceInteractionStrength;
    [Tooltip("Scales hard ripple compression, color banding, and sharp lighting from interaction ripples. Lower values keep click intensity from creating harsh compressed lines.")]
    [Range(0f, 1f)] public float surfaceCompressionStrength;
    [Range(0f, 2f)] public float surfaceClickRippleIntensity;
    [Min(1f)] public float surfaceRippleRadius;
    [Min(0.5f)] public float surfaceRippleThickness;
    [FormerlySerializedAs("surfaceRippleResidualDuration")]
    [Min(0f)] public float surfaceOuterRippleDuration;
    [FormerlySerializedAs("surfaceRippleResidualStrength")]
    [Range(0f, 1f)] public float surfaceOuterRippleStrength;
    [FormerlySerializedAs("surfaceRippleResidualSpeed")]
    [Min(0f)] public float surfaceOuterRippleSpeed;
    [Tooltip("Seconds used to fade the main ripple into the outer ripple and fade the outer ripple before it ends.")]
    [Min(0f)] public float surfaceRippleFadeOutDuration;
    [Min(0f)] public float surfaceRippleMinInterval;
    [Min(0.05f)] public float rippleDuration;
    [Min(0f)] public float rippleStartRadius;
    [Min(1f)] public float rippleEndRadius;
    [Min(0.5f)] public float rippleThickness;
    [Range(0f, 2f)] public float openRippleIntensity;
    [Range(0f, 2f)] public float hoverRippleIntensity;
    [Range(0f, 2f)] public float purchaseRippleIntensity;
    [Min(0f)] public float ambientRippleInterval;
    [Range(0f, 1f)] public float ambientRippleIntensity;

    [Header("Pointer Wake")]
    public bool pointerWakeEnabled;
    [Range(0f, 2f)] public float pointerWakeIntensity;
    [Min(1f)] public float pointerWakeMinDistance;
    [Min(0.05f)] public float pointerWakeDuration;
    [Min(1f)] public float pointerWakeLength;
    [Min(0.5f)] public float pointerWakeWidth;

    public static UpgradeLakePresentationSettings CreateDefault()
    {
        return new UpgradeLakePresentationSettings
        {
            enabled = true,
            useUnscaledTime = true,
            deepColor = new Color(0.025f, 0.07f, 0.11f, 1f),
            shallowColor = new Color(0.07f, 0.23f, 0.28f, 1f),
            causticColor = new Color(0.62f, 0.95f, 0.87f, 0.75f),
            surfaceAlpha = 0.94f,
            waveScale = 5.5f,
            waveSpeed = 0.32f,
            waveStrength = 0.055f,
            causticStrength = 0.18f,
            topDownDepthBias = 0.18f,
            contentParallax = 0.22f,
            backgroundDistortion = 0.018f,
            interactionDistortion = 0.045f,
            bottomTint = new Color(0.18f, 0.75f, 0.82f, 1f),
            foamColor = new Color(0.72f, 1f, 0.92f, 0.62f),
            bottomStrength = 0.22f,
            bottomTiling = 3.2f,
            bottomDrift = new Vector2(0.012f, -0.007f),
            heightTextureStrength = 0.18f,
            heightTextureTiling = 5.8f,
            heightTextureDrift = new Vector2(-0.028f, 0.018f),
            causticTextureStrength = 0.38f,
            causticTextureTiling = 4.6f,
            causticTextureSpeed = new Vector4(0.05f, -0.032f, -0.038f, 0.044f),
            causticTextureSharpness = 2.6f,
            foamStrength = 0.13f,
            foamTiling = 8.5f,
            foamSpeed = new Vector4(0.018f, 0.012f, -0.021f, 0.015f),
            foamThreshold = 0.52f,
            depthNoiseStrength = 0.28f,
            surfaceNormalStrength = 0.46f,
            specularStrength = 0.26f,
            edgeDarkening = 0.22f,
            rippleColor = new Color(0.72f, 0.95f, 1f, 0.5f),
            surfaceInteractionEnabled = true,
            drawOverlayRipples = false,
            surfaceInteractionStrength = 0.72f,
            surfaceCompressionStrength = 0.25f,
            surfaceClickRippleIntensity = 0.72f,
            surfaceRippleRadius = 230f,
            surfaceRippleThickness = 22f,
            surfaceOuterRippleDuration = 6.2f,
            surfaceOuterRippleStrength = 0.16f,
            surfaceOuterRippleSpeed = 0.94f,
            surfaceRippleFadeOutDuration = 0.85f,
            surfaceRippleMinInterval = 0.28f,
            rippleDuration = 1.05f,
            rippleStartRadius = 14f,
            rippleEndRadius = 210f,
            rippleThickness = 5.5f,
            openRippleIntensity = 1.15f,
            hoverRippleIntensity = 0f,
            purchaseRippleIntensity = 1.35f,
            ambientRippleInterval = 0f,
            ambientRippleIntensity = 0f,
            pointerWakeEnabled = true,
            pointerWakeIntensity = 0.18f,
            pointerWakeMinDistance = 8f,
            pointerWakeDuration = 1.0f,
            pointerWakeLength = 46f,
            pointerWakeWidth = 30f,
        };
    }

    public void ApplySurfaceSettingsTo(Material material)
    {
        if (material == null)
            return;

        SetColorIfPresent(material, DeepColorMaterialId, deepColor);
        SetColorIfPresent(material, ShallowColorMaterialId, shallowColor);
        SetColorIfPresent(material, CausticColorMaterialId, causticColor);
        SetColorIfPresent(material, BottomTintMaterialId, bottomTint);
        SetColorIfPresent(material, FoamColorMaterialId, foamColor);
        SetFloatIfPresent(material, AlphaMaterialId, surfaceAlpha);
        SetFloatIfPresent(material, WaveScaleMaterialId, waveScale);
        SetFloatIfPresent(material, WaveSpeedMaterialId, waveSpeed);
        SetFloatIfPresent(material, WaveStrengthMaterialId, waveStrength);
        SetFloatIfPresent(material, CausticStrengthMaterialId, causticStrength);
        SetFloatIfPresent(material, TopDownDepthBiasMaterialId, topDownDepthBias);
        SetFloatIfPresent(material, ParallaxMaterialId, contentParallax);
        SetFloatIfPresent(material, BackgroundDistortionMaterialId, backgroundDistortion);
        SetFloatIfPresent(material, InteractionDistortionMaterialId, interactionDistortion);
        SetFloatIfPresent(material, InteractionCompressionMaterialId, surfaceCompressionStrength);
        SetFloatIfPresent(material, BottomStrengthMaterialId, bottomStrength);
        SetFloatIfPresent(material, BottomTilingMaterialId, bottomTiling);
        SetVectorIfPresent(material, BottomDriftMaterialId, new Vector4(bottomDrift.x, bottomDrift.y, 0f, 0f));
        SetFloatIfPresent(material, HeightTexStrengthMaterialId, heightTextureStrength);
        SetFloatIfPresent(material, HeightTexTilingMaterialId, heightTextureTiling);
        SetVectorIfPresent(material, HeightTexDriftMaterialId, new Vector4(heightTextureDrift.x, heightTextureDrift.y, 0f, 0f));
        SetFloatIfPresent(material, CausticTextureStrengthMaterialId, causticTextureStrength);
        SetFloatIfPresent(material, CausticTextureTilingMaterialId, causticTextureTiling);
        SetVectorIfPresent(material, CausticTextureSpeedMaterialId, causticTextureSpeed);
        SetFloatIfPresent(material, CausticTextureSharpnessMaterialId, causticTextureSharpness);
        SetFloatIfPresent(material, FoamStrengthMaterialId, foamStrength);
        SetFloatIfPresent(material, FoamTilingMaterialId, foamTiling);
        SetVectorIfPresent(material, FoamSpeedMaterialId, foamSpeed);
        SetFloatIfPresent(material, FoamThresholdMaterialId, foamThreshold);
        SetFloatIfPresent(material, DepthNoiseStrengthMaterialId, depthNoiseStrength);
        SetFloatIfPresent(material, SurfaceNormalStrengthMaterialId, surfaceNormalStrength);
        SetFloatIfPresent(material, SpecularStrengthMaterialId, specularStrength);
        SetFloatIfPresent(material, EdgeDarkeningMaterialId, edgeDarkening);
    }

    public void ReadSurfaceSettingsFrom(Material material)
    {
        if (material == null)
            return;

        deepColor = GetColorIfPresent(material, DeepColorMaterialId, deepColor);
        shallowColor = GetColorIfPresent(material, ShallowColorMaterialId, shallowColor);
        causticColor = GetColorIfPresent(material, CausticColorMaterialId, causticColor);
        bottomTint = GetColorIfPresent(material, BottomTintMaterialId, bottomTint);
        foamColor = GetColorIfPresent(material, FoamColorMaterialId, foamColor);
        surfaceAlpha = GetFloatIfPresent(material, AlphaMaterialId, surfaceAlpha);
        waveScale = GetFloatIfPresent(material, WaveScaleMaterialId, waveScale);
        waveSpeed = GetFloatIfPresent(material, WaveSpeedMaterialId, waveSpeed);
        waveStrength = GetFloatIfPresent(material, WaveStrengthMaterialId, waveStrength);
        causticStrength = GetFloatIfPresent(material, CausticStrengthMaterialId, causticStrength);
        topDownDepthBias = GetFloatIfPresent(material, TopDownDepthBiasMaterialId, topDownDepthBias);
        contentParallax = GetFloatIfPresent(material, ParallaxMaterialId, contentParallax);
        backgroundDistortion = GetFloatIfPresent(material, BackgroundDistortionMaterialId, backgroundDistortion);
        interactionDistortion = GetFloatIfPresent(material, InteractionDistortionMaterialId, interactionDistortion);
        surfaceCompressionStrength = GetFloatIfPresent(material, InteractionCompressionMaterialId, surfaceCompressionStrength);
        bottomStrength = GetFloatIfPresent(material, BottomStrengthMaterialId, bottomStrength);
        bottomTiling = GetFloatIfPresent(material, BottomTilingMaterialId, bottomTiling);
        Vector4 bottomDriftValue = GetVectorIfPresent(material, BottomDriftMaterialId, new Vector4(bottomDrift.x, bottomDrift.y, 0f, 0f));
        bottomDrift = new Vector2(bottomDriftValue.x, bottomDriftValue.y);
        heightTextureStrength = GetFloatIfPresent(material, HeightTexStrengthMaterialId, heightTextureStrength);
        heightTextureTiling = GetFloatIfPresent(material, HeightTexTilingMaterialId, heightTextureTiling);
        Vector4 heightDriftValue = GetVectorIfPresent(material, HeightTexDriftMaterialId, new Vector4(heightTextureDrift.x, heightTextureDrift.y, 0f, 0f));
        heightTextureDrift = new Vector2(heightDriftValue.x, heightDriftValue.y);
        causticTextureStrength = GetFloatIfPresent(material, CausticTextureStrengthMaterialId, causticTextureStrength);
        causticTextureTiling = GetFloatIfPresent(material, CausticTextureTilingMaterialId, causticTextureTiling);
        causticTextureSpeed = GetVectorIfPresent(material, CausticTextureSpeedMaterialId, causticTextureSpeed);
        causticTextureSharpness = GetFloatIfPresent(material, CausticTextureSharpnessMaterialId, causticTextureSharpness);
        foamStrength = GetFloatIfPresent(material, FoamStrengthMaterialId, foamStrength);
        foamTiling = GetFloatIfPresent(material, FoamTilingMaterialId, foamTiling);
        foamSpeed = GetVectorIfPresent(material, FoamSpeedMaterialId, foamSpeed);
        foamThreshold = GetFloatIfPresent(material, FoamThresholdMaterialId, foamThreshold);
        depthNoiseStrength = GetFloatIfPresent(material, DepthNoiseStrengthMaterialId, depthNoiseStrength);
        surfaceNormalStrength = GetFloatIfPresent(material, SurfaceNormalStrengthMaterialId, surfaceNormalStrength);
        specularStrength = GetFloatIfPresent(material, SpecularStrengthMaterialId, specularStrength);
        edgeDarkening = GetFloatIfPresent(material, EdgeDarkeningMaterialId, edgeDarkening);
    }

    public void Sanitize()
    {
        ApplyMissingInteractionDefaults();

        drawOverlayRipples = false;
        waveScale = Mathf.Max(0.1f, waveScale);
        waveSpeed = Mathf.Max(0f, waveSpeed);
        surfaceAlpha = Mathf.Clamp01(surfaceAlpha);
        waveStrength = Mathf.Clamp(waveStrength, 0f, 0.25f);
        causticStrength = Mathf.Clamp01(causticStrength);
        topDownDepthBias = Mathf.Clamp01(topDownDepthBias);
        contentParallax = Mathf.Clamp(contentParallax, 0f, 2f);
        backgroundDistortion = Mathf.Clamp(backgroundDistortion, 0f, 0.08f);
        interactionDistortion = Mathf.Clamp(interactionDistortion, 0f, 0.12f);
        ApplyMissingTextureDefaults();
        bottomStrength = Mathf.Clamp01(bottomStrength);
        bottomTiling = Mathf.Max(0.1f, bottomTiling);
        heightTextureStrength = Mathf.Clamp01(heightTextureStrength);
        heightTextureTiling = Mathf.Max(0.1f, heightTextureTiling);
        causticTextureStrength = Mathf.Clamp(causticTextureStrength, 0f, 2f);
        causticTextureTiling = Mathf.Max(0.1f, causticTextureTiling);
        causticTextureSharpness = Mathf.Clamp(causticTextureSharpness, 0.25f, 8f);
        foamStrength = Mathf.Clamp01(foamStrength);
        foamTiling = Mathf.Max(0.1f, foamTiling);
        foamThreshold = Mathf.Clamp01(foamThreshold);
        depthNoiseStrength = Mathf.Clamp01(depthNoiseStrength);
        surfaceNormalStrength = Mathf.Clamp01(surfaceNormalStrength);
        specularStrength = Mathf.Clamp01(specularStrength);
        edgeDarkening = Mathf.Clamp01(edgeDarkening);
        surfaceInteractionStrength = Mathf.Clamp(surfaceInteractionStrength, 0f, 2f);
        surfaceCompressionStrength = Mathf.Clamp01(surfaceCompressionStrength);
        surfaceClickRippleIntensity = Mathf.Clamp(surfaceClickRippleIntensity, 0f, 2f);
        surfaceRippleRadius = Mathf.Max(1f, surfaceRippleRadius);
        surfaceRippleThickness = Mathf.Max(0.5f, surfaceRippleThickness);
        surfaceOuterRippleDuration = Mathf.Max(0f, surfaceOuterRippleDuration);
        surfaceOuterRippleStrength = Mathf.Clamp01(surfaceOuterRippleStrength);
        surfaceOuterRippleSpeed = Mathf.Max(0f, surfaceOuterRippleSpeed);
        surfaceRippleFadeOutDuration = Mathf.Max(0f, surfaceRippleFadeOutDuration);
        surfaceRippleMinInterval = Mathf.Max(0f, surfaceRippleMinInterval);
        rippleDuration = Mathf.Max(0.05f, rippleDuration);
        rippleStartRadius = Mathf.Max(0f, rippleStartRadius);
        rippleEndRadius = Mathf.Max(rippleStartRadius + 1f, rippleEndRadius);
        rippleThickness = Mathf.Max(0.5f, rippleThickness);
        openRippleIntensity = Mathf.Clamp(openRippleIntensity, 0f, 2f);
        hoverRippleIntensity = 0f;
        purchaseRippleIntensity = Mathf.Clamp(purchaseRippleIntensity, 0f, 2f);
        ambientRippleInterval = 0f;
        ambientRippleIntensity = 0f;
        pointerWakeIntensity = Mathf.Clamp(pointerWakeIntensity, 0f, 2f);
        pointerWakeMinDistance = Mathf.Max(1f, pointerWakeMinDistance);
        pointerWakeDuration = Mathf.Max(0.05f, pointerWakeDuration);
        pointerWakeLength = Mathf.Max(1f, pointerWakeLength);
        pointerWakeWidth = Mathf.Max(0.5f, pointerWakeWidth);
    }

    private void ApplyMissingInteractionDefaults()
    {
        if (!enabled)
            return;

        bool missingSurfaceInteraction =
            !surfaceInteractionEnabled &&
            surfaceInteractionStrength <= 0f &&
            surfaceClickRippleIntensity <= 0f &&
            surfaceRippleRadius <= 0f &&
            surfaceRippleThickness <= 0f;

        if (missingSurfaceInteraction)
        {
            surfaceInteractionEnabled = true;
            drawOverlayRipples = false;
            surfaceInteractionStrength = 0.72f;
            surfaceCompressionStrength = 0.25f;
            surfaceClickRippleIntensity = 0.72f;
            surfaceRippleRadius = 230f;
            surfaceRippleThickness = 22f;
        }

        if (surfaceOuterRippleDuration <= 0f &&
            surfaceOuterRippleStrength <= 0f &&
            surfaceOuterRippleSpeed <= 0f)
        {
            surfaceOuterRippleDuration = 6.2f;
            surfaceOuterRippleStrength = 0.16f;
            surfaceOuterRippleSpeed = 0.94f;
        }
        else if ((Mathf.Approximately(surfaceOuterRippleDuration, 2.4f) &&
                  Mathf.Approximately(surfaceOuterRippleStrength, 0.26f) &&
                  Mathf.Approximately(surfaceOuterRippleSpeed, 0.72f)) ||
                 (Mathf.Approximately(surfaceOuterRippleDuration, 6.2f) &&
                  Mathf.Approximately(surfaceOuterRippleStrength, 0.18f) &&
                  Mathf.Approximately(surfaceOuterRippleSpeed, 0.92f)))
        {
            surfaceOuterRippleDuration = 6.2f;
            surfaceOuterRippleStrength = 0.16f;
            surfaceOuterRippleSpeed = 0.94f;
        }

        if (surfaceRippleMinInterval <= 0f || Mathf.Approximately(surfaceRippleMinInterval, 0.18f))
            surfaceRippleMinInterval = 0.28f;

        if (backgroundDistortion <= 0f && interactionDistortion <= 0f)
        {
            backgroundDistortion = 0.018f;
            interactionDistortion = 0.045f;
        }

        bool missingPointerWake =
            !pointerWakeEnabled &&
            pointerWakeIntensity <= 0f &&
            pointerWakeMinDistance <= 0f &&
            pointerWakeDuration <= 0f &&
            pointerWakeLength <= 0f &&
            pointerWakeWidth <= 0f;

        if (missingPointerWake)
        {
            pointerWakeEnabled = true;
            pointerWakeIntensity = 0.18f;
            pointerWakeMinDistance = 8f;
            pointerWakeDuration = 1.0f;
            pointerWakeLength = 46f;
            pointerWakeWidth = 30f;
        }

        if (pointerWakeMinDistance <= 0f)
            pointerWakeMinDistance = 8f;

        if (pointerWakeDuration <= 0f)
            pointerWakeDuration = 1.0f;

        if (pointerWakeLength <= 0f)
            pointerWakeLength = 46f;

        if (pointerWakeWidth <= 0f)
            pointerWakeWidth = 30f;
    }

    private void ApplyMissingTextureDefaults()
    {
        bool missingTextureDetail =
            bottomTint.maxColorComponent <= 0f &&
            bottomTint.a <= 0f &&
            foamColor.maxColorComponent <= 0f &&
            foamColor.a <= 0f &&
            bottomTiling <= 0f &&
            heightTextureTiling <= 0f &&
            causticTextureTiling <= 0f &&
            foamTiling <= 0f;

        if (!missingTextureDetail)
            return;

        bottomTint = new Color(0.18f, 0.75f, 0.82f, 1f);
        foamColor = new Color(0.72f, 1f, 0.92f, 0.62f);
        bottomStrength = 0.22f;
        bottomTiling = 3.2f;
        bottomDrift = new Vector2(0.012f, -0.007f);
        heightTextureStrength = 0.18f;
        heightTextureTiling = 5.8f;
        heightTextureDrift = new Vector2(-0.028f, 0.018f);
        causticTextureStrength = 0.38f;
        causticTextureTiling = 4.6f;
        causticTextureSpeed = new Vector4(0.05f, -0.032f, -0.038f, 0.044f);
        causticTextureSharpness = 2.6f;
        foamStrength = 0.13f;
        foamTiling = 8.5f;
        foamSpeed = new Vector4(0.018f, 0.012f, -0.021f, 0.015f);
        foamThreshold = 0.52f;
    }

    private static void SetColorIfPresent(Material material, int propertyId, Color value)
    {
        if (material.HasProperty(propertyId))
            material.SetColor(propertyId, value);
    }

    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material.HasProperty(propertyId))
            material.SetFloat(propertyId, value);
    }

    private static void SetVectorIfPresent(Material material, int propertyId, Vector4 value)
    {
        if (material.HasProperty(propertyId))
            material.SetVector(propertyId, value);
    }

    private static Color GetColorIfPresent(Material material, int propertyId, Color fallback)
    {
        return material.HasProperty(propertyId) ? material.GetColor(propertyId) : fallback;
    }

    private static float GetFloatIfPresent(Material material, int propertyId, float fallback)
    {
        return material.HasProperty(propertyId) ? material.GetFloat(propertyId) : fallback;
    }

    private static Vector4 GetVectorIfPresent(Material material, int propertyId, Vector4 fallback)
    {
        return material.HasProperty(propertyId) ? material.GetVector(propertyId) : fallback;
    }
}

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class UpgradeLakePresentation : MonoBehaviour
{
    private const string SurfaceLayerName = "LakeSurface";
    private const string RippleLayerName = "LakeRipples";
    private const int MaxSurfaceRipples = 8;
    private const int WakeBufferSize = 512;
    private const int MaxWakeStampsPerFrame = 16;
    private const float WakePropagation = 38f;
    private const float WakeTextureStrength = 1.15f;
    private const float PointerWakeSpeedReference = 720f;
    private static readonly int InteractionStrengthId = Shader.PropertyToID("_InteractionStrength");
    private static readonly int InteractionCompressionId = Shader.PropertyToID("_InteractionCompression");
    private static readonly int OuterRippleDurationId = Shader.PropertyToID("_OuterRippleDuration");
    private static readonly int OuterRippleStrengthId = Shader.PropertyToID("_OuterRippleStrength");
    private static readonly int OuterRippleSpeedId = Shader.PropertyToID("_OuterRippleSpeed");
    private static readonly int RippleFadeOutDurationId = Shader.PropertyToID("_RippleFadeOutDuration");
    private static readonly int SurfaceAspectId = Shader.PropertyToID("_SurfaceAspect");
    private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");
    private static readonly int ContentOffsetId = Shader.PropertyToID("_ContentOffset");
    private static readonly int SurfaceRippleCountId = Shader.PropertyToID("_SurfaceRippleCount");
    private static readonly int SurfaceRippleDataId = Shader.PropertyToID("_SurfaceRippleData");
    private static readonly int SurfaceRippleExtraId = Shader.PropertyToID("_SurfaceRippleExtra");
    private static readonly int WakeTexId = Shader.PropertyToID("_WakeTex");
    private static readonly int WakeTextureStrengthId = Shader.PropertyToID("_WakeTextureStrength");
    private static readonly int WakeDecayId = Shader.PropertyToID("_WakeDecay");
    private static readonly int WakePropagationId = Shader.PropertyToID("_WakePropagation");
    private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    private static readonly int StampUvId = Shader.PropertyToID("_StampUv");
    private static readonly int StampDirectionId = Shader.PropertyToID("_StampDirection");
    private static readonly int StampIntensityId = Shader.PropertyToID("_StampIntensity");
    private static readonly int StampLengthId = Shader.PropertyToID("_StampLength");
    private static readonly int StampWidthId = Shader.PropertyToID("_StampWidth");
    private static readonly int StampAspectId = Shader.PropertyToID("_StampAspect");
    private static readonly int StampSeedId = Shader.PropertyToID("_StampSeed");

    private const float EditorTestWakeSpacing = 18f;
    private const int EditorTestWakeStampCount = 9;

    private RectTransform viewportRoot;
    private RectTransform contentRoot;
    private Image surfaceImage;
    private UILakeSurfaceImage surfaceLakeImage;
    private UILakeRippleGraphic rippleGraphic;
    private Material surfaceMaterial;
    private Material surfaceMaterialPreset;
    private Material wakeBufferMaterial;
    private RenderTexture wakeReadBuffer;
    private RenderTexture wakeWriteBuffer;
    private UpgradeLakePresentationSettings settings;
    private bool useSurfaceMaterialSettings;
    private bool ownsSurfaceMaterial;
    private bool ownsSurfaceLayer;
    private bool animateSurfaceInEditMode;
    private float nextAmbientRippleTime;
    private float lastSurfaceRippleTime = float.NegativeInfinity;
    private float wakeStampSeed;
    private bool hasLastWakeLocalPosition;
    private bool hasLastWakeEventLocalPosition;
    private Vector2 lastWakeLocalPosition;
    private Vector2 lastWakeEventLocalPosition;
    private bool hasInitialized;
    private readonly List<SurfaceRipple> surfaceRipples = new List<SurfaceRipple>();
    private readonly Vector4[] surfaceRippleData = new Vector4[MaxSurfaceRipples];
    private readonly Vector4[] surfaceRippleExtra = new Vector4[MaxSurfaceRipples];

    private struct SurfaceRipple
    {
        public Vector2 Uv;
        public float StartTime;
        public float Intensity;
        public float Radius;
        public float Duration;
        public float Thickness;
    }

    public void Initialize(
        RectTransform viewport,
        RectTransform content,
        Image lakeSurfaceImage,
        UpgradeLakePresentationSettings presentationSettings,
        Material lakeSurfaceMaterialPreset = null,
        bool useLakeSurfaceMaterialSettings = false,
        bool animateLakeSurfaceInEditMode = false)
    {
        viewportRoot = viewport;
        contentRoot = content;
        settings = presentationSettings;
        settings.Sanitize();
        if (surfaceImage != lakeSurfaceImage)
        {
            RestoreSurfaceImageMaterial();
            surfaceImage = lakeSurfaceImage;
            surfaceLakeImage = surfaceImage as UILakeSurfaceImage;
            ownsSurfaceLayer = false;
        }

        bool nextAnimateSurfaceInEditMode = !Application.isPlaying && animateLakeSurfaceInEditMode;
        if (surfaceMaterialPreset != lakeSurfaceMaterialPreset ||
            animateSurfaceInEditMode != nextAnimateSurfaceInEditMode)
        {
            DestroySurfaceMaterial();
        }

        surfaceMaterialPreset = lakeSurfaceMaterialPreset;
        useSurfaceMaterialSettings = lakeSurfaceMaterialPreset != null && useLakeSurfaceMaterialSettings;
        animateSurfaceInEditMode = nextAnimateSurfaceInEditMode;
        hasInitialized = true;

        EnsureLayers();
        if (Application.isPlaying)
            EnsureWakeBuffer(clear: true);

        ApplySurfaceMaterialProperties();
        ApplyLayerVisibility();
        ScheduleNextAmbientRipple(initialDelay: 0.65f);
    }

    public void PlayOpen()
    {
        if (!CanPlay())
            return;

        EnsureLayers();
        EmitAtViewportLocal(Vector2.zero, settings.openRippleIntensity, force: true);
        ScheduleNextAmbientRipple(initialDelay: 0.8f);
    }

    public void EmitHoverRipple(Vector2 screenPosition, Camera eventCamera)
    {
        return;
    }

    public void EmitPurchaseRipple(RectTransform target)
    {
        if (!CanPlay() || target == null || settings.purchaseRippleIntensity <= 0f)
            return;

        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Vector2 localPosition = viewportRoot.InverseTransformPoint(worldCenter);
        EmitAtViewportLocal(localPosition, settings.purchaseRippleIntensity, force: true);
    }

    private void Update()
    {
        if (!hasInitialized || viewportRoot == null)
            return;

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            TickEditorPreview();
#endif
            return;
        }

        EnsureLayers();
        ApplyLayerVisibility();
        if (!settings.enabled)
            return;

        UpdateWakeSimulation();
        UpdatePointerInteraction();
        UpdateAmbientRipples();
        ApplySurfaceMaterialProperties();
    }

    private void OnDestroy()
    {
        DestroySurfaceMaterial();
        DestroyWakeBufferMaterial();
        ReleaseWakeBuffers();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            RestoreEditorPreviewMaterial();
#endif
    }

    private bool CanPlay()
    {
        return hasInitialized && settings.enabled && viewportRoot != null;
    }

    private void EnsureLayers()
    {
        if (viewportRoot == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying &&
            (UnityEditor.EditorUtility.IsPersistent(this) ||
             UnityEditor.EditorUtility.IsPersistent(viewportRoot)))
        {
            return;
        }
#endif

        RemoveGeneratedSurfaceLayer();
        EnsureSurfaceTarget();
        if (settings.drawOverlayRipples)
            EnsureRippleLayer();

        if (surfaceImage != null &&
            surfaceImage.rectTransform.parent == viewportRoot &&
            surfaceImage.rectTransform != viewportRoot)
        {
            surfaceImage.rectTransform.SetSiblingIndex(0);
        }

        if (rippleGraphic != null && settings.drawOverlayRipples)
            rippleGraphic.rectTransform.SetSiblingIndex(1);
    }

    private void EnsureSurfaceTarget()
    {
        if (surfaceImage == null || surfaceLakeImage == null)
        {
            RectTransform surfaceRect = ResolveSurfaceTargetRect();
            if (surfaceRect == null)
                return;

            Image existingImage = surfaceRect.GetComponent<Image>();
            if (ownsSurfaceLayer)
            {
                surfaceLakeImage = surfaceRect.GetComponent<UILakeSurfaceImage>();
                if (surfaceLakeImage == null)
                    surfaceLakeImage = EnsureLakeSurfaceImage(surfaceRect, existingImage);
            }
            else
            {
                surfaceLakeImage = existingImage as UILakeSurfaceImage;
            }

            surfaceImage = surfaceLakeImage != null ? surfaceLakeImage : existingImage;
            if (surfaceImage == null)
                return;

            if (ownsSurfaceLayer)
            {
                surfaceImage.raycastTarget = false;
                surfaceImage.maskable = true;
                surfaceImage.color = Color.white;
                surfaceImage.type = Image.Type.Simple;
            }
        }

        if (surfaceImage == null)
            return;

        Shader shader = Shader.Find("UI/Lake Surface");
        if (shader == null)
            return;

        EnsureSurfaceMaterial(shader);
        ApplySurfaceMaterialToImage();
    }

    private RectTransform ResolveSurfaceTargetRect()
    {
        if (surfaceImage != null)
            return surfaceImage.rectTransform;

        if (viewportRoot == null)
            return null;

#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.EditorUtility.IsPersistent(viewportRoot))
            return null;
#endif

        RectTransform internalLayer = FindDirectChild(viewportRoot, SurfaceLayerName);
        if (internalLayer == null)
            internalLayer = CreateStretchLayer(SurfaceLayerName, viewportRoot, hidden: true);
        else
            internalLayer.gameObject.hideFlags |= HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;

        ownsSurfaceLayer = true;
        return internalLayer;
    }

    private static UILakeSurfaceImage EnsureLakeSurfaceImage(RectTransform surfaceRect, Image existingImage)
    {
        if (surfaceRect == null)
            return null;

        if (existingImage is UILakeSurfaceImage existingLakeImage)
            return existingLakeImage;

        if (existingImage != null)
            return null;

        Material material = existingImage != null ? existingImage.material : null;
        Color color = existingImage != null ? existingImage.color : Color.white;
        bool raycastTarget = existingImage != null && existingImage.raycastTarget;
        bool maskable = existingImage == null || existingImage.maskable;
        Sprite sprite = existingImage != null ? existingImage.sprite : null;
        Image.Type type = existingImage != null ? existingImage.type : Image.Type.Simple;
        bool preserveAspect = existingImage != null && existingImage.preserveAspect;
        bool fillCenter = existingImage == null || existingImage.fillCenter;
        Image.FillMethod fillMethod = existingImage != null ? existingImage.fillMethod : Image.FillMethod.Radial360;
        int fillOrigin = existingImage != null ? existingImage.fillOrigin : 0;
        float fillAmount = existingImage != null ? existingImage.fillAmount : 1f;
        bool fillClockwise = existingImage == null || existingImage.fillClockwise;

        UILakeSurfaceImage lakeImage = surfaceRect.gameObject.AddComponent<UILakeSurfaceImage>();
        lakeImage.material = material;
        lakeImage.color = color;
        lakeImage.raycastTarget = raycastTarget;
        lakeImage.maskable = maskable;
        lakeImage.sprite = sprite;
        lakeImage.type = type;
        lakeImage.preserveAspect = preserveAspect;
        lakeImage.fillCenter = fillCenter;
        lakeImage.fillMethod = fillMethod;
        lakeImage.fillOrigin = fillOrigin;
        lakeImage.fillAmount = fillAmount;
        lakeImage.fillClockwise = fillClockwise;
        return lakeImage;
    }

    private void EnsureSurfaceMaterial(Shader shader)
    {
        if (surfaceMaterialPreset != null)
        {
            if (Application.isPlaying || animateSurfaceInEditMode)
            {
                if (surfaceMaterial == null || !ownsSurfaceMaterial)
                {
                    DestroySurfaceMaterial();
                    surfaceMaterial = new Material(surfaceMaterialPreset)
                    {
                        name = Application.isPlaying
                            ? $"M_Runtime{surfaceMaterialPreset.name}"
                            : $"M_EditorPreview{surfaceMaterialPreset.name}",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    ownsSurfaceMaterial = true;
                }

                surfaceMaterial.CopyPropertiesFromMaterial(surfaceMaterialPreset);
            }
            else
            {
                DestroySurfaceMaterial();
                surfaceMaterial = surfaceMaterialPreset;
                ownsSurfaceMaterial = false;
            }

            return;
        }

        if (surfaceMaterial != null && ownsSurfaceMaterial)
            return;

        DestroySurfaceMaterial();
        surfaceMaterial = new Material(shader)
        {
            name = "M_RuntimeUpgradeLakeSurface",
            hideFlags = HideFlags.HideAndDontSave,
        };
        ownsSurfaceMaterial = true;
    }

    private void ApplySurfaceMaterialToImage()
    {
        if (surfaceImage == null || surfaceMaterial == null)
            return;

        if (!Application.isPlaying && ownsSurfaceMaterial)
        {
            if (surfaceImage.material != surfaceMaterialPreset)
                surfaceImage.material = surfaceMaterialPreset;

            if (surfaceLakeImage != null)
                surfaceLakeImage.PreviewMaterialOverride = surfaceMaterial;

            surfaceImage.SetMaterialDirty();
            return;
        }

        if (surfaceLakeImage != null)
            surfaceLakeImage.PreviewMaterialOverride = null;

        surfaceImage.material = surfaceMaterial;
    }

    private void EnsureRippleLayer()
    {
        if (rippleGraphic == null)
        {
            RectTransform rippleRect = FindDirectChild(viewportRoot, RippleLayerName);
            if (rippleRect == null)
                rippleRect = CreateStretchLayer(RippleLayerName, viewportRoot);

            rippleGraphic = rippleRect.GetComponent<UILakeRippleGraphic>();
            if (rippleGraphic == null)
                rippleGraphic = rippleRect.gameObject.AddComponent<UILakeRippleGraphic>();
        }

        rippleGraphic.raycastTarget = false;
        rippleGraphic.Configure(
            settings.useUnscaledTime,
            settings.rippleColor,
            settings.rippleDuration,
            settings.rippleStartRadius,
            settings.rippleEndRadius,
            settings.rippleThickness,
            settings.surfaceRippleFadeOutDuration);
    }

    private void ApplyLayerVisibility()
    {
        if (ownsSurfaceLayer && surfaceImage != null && surfaceImage.gameObject.activeSelf != settings.enabled)
            surfaceImage.gameObject.SetActive(settings.enabled);

        bool showOverlayRipples = settings.enabled && settings.drawOverlayRipples;
        if (rippleGraphic != null && rippleGraphic.gameObject.activeSelf != showOverlayRipples)
            rippleGraphic.gameObject.SetActive(showOverlayRipples);
    }

    private void ApplySurfaceMaterialProperties()
    {
        if (surfaceMaterial == null)
            return;

        float now = GetTime();
        PruneSurfaceEvents(now);
        BuildSurfaceShaderArrays();

        if (!useSurfaceMaterialSettings)
            settings.ApplySurfaceSettingsTo(surfaceMaterial);

        if (!Application.isPlaying && useSurfaceMaterialSettings && !animateSurfaceInEditMode)
            return;

        surfaceMaterial.SetFloat(InteractionStrengthId, settings.surfaceInteractionStrength);
        surfaceMaterial.SetFloat(InteractionCompressionId, settings.surfaceCompressionStrength);
        surfaceMaterial.SetFloat(OuterRippleDurationId, settings.surfaceOuterRippleDuration);
        surfaceMaterial.SetFloat(OuterRippleStrengthId, settings.surfaceOuterRippleStrength);
        surfaceMaterial.SetFloat(OuterRippleSpeedId, settings.surfaceOuterRippleSpeed);
        surfaceMaterial.SetFloat(RippleFadeOutDurationId, settings.surfaceRippleFadeOutDuration);
        surfaceMaterial.SetFloat(SurfaceAspectId, GetSurfaceAspect());
        surfaceMaterial.SetFloat(UnscaledTimeId, now);
        surfaceMaterial.SetInt(SurfaceRippleCountId, settings.surfaceInteractionEnabled ? Mathf.Min(surfaceRipples.Count, MaxSurfaceRipples) : 0);
        surfaceMaterial.SetVectorArray(SurfaceRippleDataId, surfaceRippleData);
        surfaceMaterial.SetVectorArray(SurfaceRippleExtraId, surfaceRippleExtra);
        surfaceMaterial.SetTexture(WakeTexId, wakeReadBuffer != null ? wakeReadBuffer : Texture2D.blackTexture);
        surfaceMaterial.SetFloat(WakeTextureStrengthId, settings.surfaceInteractionEnabled ? WakeTextureStrength : 0f);

        Vector2 contentOffset = contentRoot != null ? contentRoot.anchoredPosition : Vector2.zero;
        surfaceMaterial.SetVector(ContentOffsetId, new Vector4(contentOffset.x, contentOffset.y, 0f, 0f));
        ApplySurfaceMaterialToImage();
    }

    private void EmitAtViewportLocal(Vector2 localPosition, float intensity, bool force)
    {
        AddSurfaceRipple(localPosition, intensity, force);

        if (!settings.drawOverlayRipples)
            return;

        EnsureRippleLayer();
        rippleGraphic?.Emit(localPosition, intensity);
    }

    private void AddSurfaceRipple(Vector2 localPosition, float intensity, bool force)
    {
        if (!settings.surfaceInteractionEnabled || intensity <= 0f || !TryLocalToUv(localPosition, out Vector2 uv))
            return;

        float now = GetTime();
        if (!force && settings.surfaceRippleMinInterval > 0f && now - lastSurfaceRippleTime < settings.surfaceRippleMinInterval)
            return;

        if (surfaceRipples.Count >= MaxSurfaceRipples)
            surfaceRipples.RemoveAt(0);

        surfaceRipples.Add(new SurfaceRipple
        {
            Uv = uv,
            StartTime = now,
            Intensity = intensity,
            Radius = PixelsToUvDistance(settings.surfaceRippleRadius),
            Duration = settings.rippleDuration,
            Thickness = PixelsToUvDistance(settings.surfaceRippleThickness),
        });

        lastSurfaceRippleTime = now;
    }

    private void UpdatePointerInteraction()
    {
        if (!settings.surfaceInteractionEnabled)
            return;

        if (!TryScreenToViewportLocal(Input.mousePosition, out Vector2 localPosition))
        {
            hasLastWakeLocalPosition = false;
            hasLastWakeEventLocalPosition = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
            AddSurfaceRipple(localPosition, settings.surfaceClickRippleIntensity, force: false);

        if (!settings.pointerWakeEnabled || settings.pointerWakeIntensity <= 0f)
        {
            lastWakeLocalPosition = localPosition;
            lastWakeEventLocalPosition = localPosition;
            hasLastWakeLocalPosition = true;
            hasLastWakeEventLocalPosition = true;
            return;
        }

        if (!hasLastWakeLocalPosition)
        {
            lastWakeLocalPosition = localPosition;
            lastWakeEventLocalPosition = localPosition;
            hasLastWakeLocalPosition = true;
            hasLastWakeEventLocalPosition = true;
            return;
        }

        if (!hasLastWakeEventLocalPosition)
        {
            lastWakeEventLocalPosition = localPosition;
            hasLastWakeEventLocalPosition = true;
        }

        Vector2 frameDelta = localPosition - lastWakeLocalPosition;
        float frameDistance = frameDelta.magnitude;
        float deltaTime = GetDeltaTime();
        lastWakeLocalPosition = localPosition;

        if (frameDistance < 0.1f)
            return;

        Vector2 eventDelta = localPosition - lastWakeEventLocalPosition;
        float eventDistance = eventDelta.magnitude;
        float sampleDistance = Mathf.Max(1f, settings.pointerWakeMinDistance);
        if (eventDistance < sampleDistance)
            return;

        Vector2 movementDirection = eventDelta.normalized;
        float speed = frameDistance / deltaTime;
        float motion = Mathf.Clamp01(speed / PointerWakeSpeedReference);
        if (motion <= 0.1f)
            return;

        int stampCount = 0;
        while (eventDistance >= sampleDistance && stampCount < MaxWakeStampsPerFrame)
        {
            Vector2 stampPosition = lastWakeEventLocalPosition + movementDirection * sampleDistance;
            AddPointerWakeStamp(stampPosition, movementDirection, motion);
            lastWakeEventLocalPosition = stampPosition;

            eventDelta = localPosition - lastWakeEventLocalPosition;
            eventDistance = eventDelta.magnitude;
            movementDirection = eventDistance > 0.0001f ? eventDelta.normalized : movementDirection;
            stampCount++;
        }
    }

    private void AddPointerWakeStamp(Vector2 localPosition, Vector2 direction, float motion)
    {
        if (!settings.surfaceInteractionEnabled ||
            !settings.pointerWakeEnabled ||
            settings.pointerWakeIntensity <= 0f ||
            !TryLocalToUv(localPosition, out Vector2 uv))
        {
            return;
        }

        if (!EnsureWakeBuffer(clear: false))
            return;

        Vector2 stampDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        wakeBufferMaterial.SetVector(StampUvId, new Vector4(uv.x, uv.y, 0f, 0f));
        wakeBufferMaterial.SetVector(StampDirectionId, new Vector4(stampDirection.x, stampDirection.y, 0f, 0f));
        wakeBufferMaterial.SetFloat(StampIntensityId, settings.pointerWakeIntensity * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(motion)));
        wakeBufferMaterial.SetFloat(StampLengthId, PixelsToUvDistance(settings.pointerWakeLength));
        wakeBufferMaterial.SetFloat(StampWidthId, PixelsToUvDistance(settings.pointerWakeWidth));
        wakeBufferMaterial.SetFloat(StampAspectId, GetSurfaceAspect());
        wakeBufferMaterial.SetFloat(StampSeedId, wakeStampSeed);
        wakeStampSeed += 1.618f;

        Graphics.Blit(wakeReadBuffer, wakeWriteBuffer, wakeBufferMaterial, 1);
        SwapWakeBuffers();
    }

    private void PruneSurfaceEvents(float now)
    {
        for (int i = surfaceRipples.Count - 1; i >= 0; i--)
        {
            if (now - surfaceRipples[i].StartTime > surfaceRipples[i].Duration + settings.surfaceOuterRippleDuration)
                surfaceRipples.RemoveAt(i);
        }

    }

    private void BuildSurfaceShaderArrays()
    {
        for (int i = 0; i < MaxSurfaceRipples; i++)
        {
            if (i < surfaceRipples.Count)
            {
                SurfaceRipple ripple = surfaceRipples[i];
                surfaceRippleData[i] = new Vector4(ripple.Uv.x, ripple.Uv.y, ripple.StartTime, ripple.Intensity);
                surfaceRippleExtra[i] = new Vector4(ripple.Radius, ripple.Duration, ripple.Thickness, 0f);
            }
            else
            {
                surfaceRippleData[i] = Vector4.zero;
                surfaceRippleExtra[i] = Vector4.zero;
            }
        }

    }

    private bool EnsureWakeBuffer(bool clear)
    {
        if (!settings.surfaceInteractionEnabled || !settings.pointerWakeEnabled)
            return wakeReadBuffer != null && wakeWriteBuffer != null && wakeBufferMaterial != null;

        if (wakeBufferMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/UI/Lake Wake Buffer");
            if (shader == null)
                return false;

            wakeBufferMaterial = new Material(shader)
            {
                name = "M_RuntimeUpgradeLakeWakeBuffer",
                hideFlags = HideFlags.DontSave,
            };
        }

        bool created = false;
        if (wakeReadBuffer == null || wakeWriteBuffer == null ||
            wakeReadBuffer.width != WakeBufferSize || wakeWriteBuffer.width != WakeBufferSize)
        {
            ReleaseWakeBuffers();
            wakeReadBuffer = CreateWakeRenderTexture("RT_UpgradeLakeWake_A");
            wakeWriteBuffer = CreateWakeRenderTexture("RT_UpgradeLakeWake_B");
            created = true;
        }

        if (created || clear)
        {
            ClearWakeBuffer(wakeReadBuffer);
            ClearWakeBuffer(wakeWriteBuffer);
        }

        return wakeReadBuffer != null && wakeWriteBuffer != null && wakeBufferMaterial != null;
    }

    private void UpdateWakeSimulation()
    {
        if (wakeReadBuffer == null && !EnsureWakeBuffer(clear: false))
            return;

        if (wakeBufferMaterial == null || wakeReadBuffer == null || wakeWriteBuffer == null)
            return;

        float deltaTime = GetDeltaTime();
        float duration = Mathf.Max(0.05f, settings.pointerWakeDuration);
        float decay = Mathf.Log(10f) / duration;
        wakeBufferMaterial.SetFloat(WakeDecayId, decay);
        wakeBufferMaterial.SetFloat(WakePropagationId, WakePropagation);
        wakeBufferMaterial.SetFloat(DeltaTimeId, deltaTime);

        Graphics.Blit(wakeReadBuffer, wakeWriteBuffer, wakeBufferMaterial, 0);
        SwapWakeBuffers();
    }

    private RenderTexture CreateWakeRenderTexture(string textureName)
    {
        RenderTexture texture = new RenderTexture(WakeBufferSize, WakeBufferSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
        {
            name = textureName,
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
        };
        texture.Create();
        return texture;
    }

    private void ClearWakeBuffer(RenderTexture texture)
    {
        if (texture == null)
            return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }

    private void SwapWakeBuffers()
    {
        RenderTexture temp = wakeReadBuffer;
        wakeReadBuffer = wakeWriteBuffer;
        wakeWriteBuffer = temp;
    }

    private void ReleaseWakeBuffers()
    {
        ReleaseWakeBuffer(ref wakeReadBuffer);
        ReleaseWakeBuffer(ref wakeWriteBuffer);
    }

    private void ReleaseWakeBuffer(ref RenderTexture texture)
    {
        if (texture == null)
            return;

        texture.Release();
        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);

        texture = null;
    }

    private void DestroyWakeBufferMaterial()
    {
        if (wakeBufferMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(wakeBufferMaterial);
        else
            DestroyImmediate(wakeBufferMaterial);

        wakeBufferMaterial = null;
    }

    private void DestroySurfaceMaterial()
    {
        if (surfaceMaterial == null)
            return;

        RestoreSurfaceImageMaterial();

        if (ownsSurfaceMaterial)
        {
            if (Application.isPlaying)
                Destroy(surfaceMaterial);
            else
                DestroyImmediate(surfaceMaterial);
        }

        surfaceMaterial = null;
        ownsSurfaceMaterial = false;
    }

    private void RestoreSurfaceImageMaterial()
    {
        if (surfaceImage == null)
            return;

        if (surfaceLakeImage != null)
            surfaceLakeImage.PreviewMaterialOverride = null;

        surfaceImage.material = surfaceMaterialPreset;
        surfaceImage.SetMaterialDirty();
        surfaceImage.SetVerticesDirty();
    }

    private void UpdateAmbientRipples()
    {
        if (settings.ambientRippleInterval <= 0f || settings.ambientRippleIntensity <= 0f)
            return;

        float now = GetTime();
        if (now < nextAmbientRippleTime)
            return;

        Rect rect = viewportRoot.rect;
        if (rect.width <= 1f || rect.height <= 1f)
            return;

        Vector2 localPosition = new Vector2(
            UnityEngine.Random.Range(rect.xMin + rect.width * 0.16f, rect.xMax - rect.width * 0.16f),
            UnityEngine.Random.Range(rect.yMin + rect.height * 0.16f, rect.yMax - rect.height * 0.16f));

        EmitAtViewportLocal(localPosition, settings.ambientRippleIntensity, force: false);
        ScheduleNextAmbientRipple(initialDelay: UnityEngine.Random.Range(settings.ambientRippleInterval * 0.72f, settings.ambientRippleInterval * 1.35f));
    }

    private void ScheduleNextAmbientRipple(float initialDelay)
    {
        float now = GetTime();
        nextAmbientRippleTime = now + Mathf.Max(0.05f, initialDelay);
    }

    private bool TryScreenToViewportLocal(Vector2 screenPosition, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        if (viewportRoot == null)
            return false;

        Camera eventCamera = GetEventCamera();
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewportRoot, screenPosition, eventCamera))
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRoot, screenPosition, eventCamera, out localPosition);
    }

    private bool TryLocalToUv(Vector2 localPosition, out Vector2 uv)
    {
        uv = Vector2.zero;
        if (viewportRoot == null)
            return false;

        Rect rect = viewportRoot.rect;
        if (rect.width <= 1f || rect.height <= 1f)
            return false;

        uv = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPosition.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPosition.y));

        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private float PixelsToUvDistance(float pixels)
    {
        if (viewportRoot == null)
            return 0f;

        Rect rect = viewportRoot.rect;
        float size = Mathf.Max(1f, rect.height);
        return Mathf.Max(0f, pixels) / size;
    }

    private float GetSurfaceAspect()
    {
        if (viewportRoot == null)
            return 1f;

        Rect rect = viewportRoot.rect;
        return rect.height > 1f ? Mathf.Max(0.0001f, rect.width / rect.height) : 1f;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private float GetTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (float)(UnityEditor.EditorApplication.timeSinceStartup % 100000.0);
#endif

        return settings.useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private float GetDeltaTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return 1f / 30f;
#endif

        float deltaTime = settings.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Max(deltaTime, 0.0001f);
    }

#if UNITY_EDITOR
    public void TickEditorPreview()
    {
        if (Application.isPlaying || !hasInitialized || viewportRoot == null)
            return;

        EnsureLayers();
        ApplyLayerVisibility();
        if (!settings.enabled)
            return;

        if (wakeReadBuffer != null || wakeWriteBuffer != null)
            UpdateWakeSimulation();

        ApplySurfaceMaterialProperties();
        if (surfaceImage != null && !ownsSurfaceMaterial)
            surfaceImage.SetMaterialDirty();
    }

    public void RestoreEditorPreviewMaterial()
    {
        RestoreEditorPreviewMaterial(null);
    }

    public void RestoreEditorPreviewMaterial(Material materialPresetOverride)
    {
        if (Application.isPlaying)
            return;

        if (UnityEditor.EditorUtility.IsPersistent(this) ||
            (viewportRoot != null && UnityEditor.EditorUtility.IsPersistent(viewportRoot)))
        {
            return;
        }

        if (materialPresetOverride != null)
            surfaceMaterialPreset = materialPresetOverride;

        animateSurfaceInEditMode = false;

        Material previewMaterial = ownsSurfaceMaterial ? surfaceMaterial : null;
        surfaceMaterial = surfaceMaterialPreset;
        ownsSurfaceMaterial = false;

        RestoreSurfaceImageMaterial();

        if (previewMaterial != null)
            DestroyImmediate(previewMaterial);
    }

    public void EmitEditorRipplePreview()
    {
        if (Application.isPlaying || !hasInitialized || viewportRoot == null)
            return;

        EnsureLayers();
        float intensity = settings.surfaceClickRippleIntensity > 0f ? settings.surfaceClickRippleIntensity : 0.72f;
        EmitAtViewportLocal(Vector2.zero, intensity, force: true);
        TickEditorPreview();
    }

    public void EmitEditorWakePreview()
    {
        if (Application.isPlaying || !hasInitialized || viewportRoot == null)
            return;

        EnsureLayers();
        if (!EnsureWakeBuffer(clear: false))
            return;

        Vector2 direction = new Vector2(1f, 0.18f).normalized;
        float spacing = Mathf.Max(EditorTestWakeSpacing, settings.pointerWakeMinDistance);
        float centerOffset = (EditorTestWakeStampCount - 1) * spacing * 0.5f;
        for (int i = 0; i < EditorTestWakeStampCount; i++)
        {
            Vector2 localPosition = direction * ((i * spacing) - centerOffset);
            AddPointerWakeStamp(localPosition, direction, 1f);
        }

        TickEditorPreview();
    }

    public void ClearEditorInteractionPreview()
    {
        if (Application.isPlaying)
            return;

        surfaceRipples.Clear();
        ClearWakeBuffer(wakeReadBuffer);
        ClearWakeBuffer(wakeWriteBuffer);
        TickEditorPreview();
    }
#endif

    private static RectTransform FindDirectChild(RectTransform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child as RectTransform;
        }

        return null;
    }

    private void RemoveGeneratedSurfaceLayer()
    {
        RectTransform generatedSurface = FindDirectChild(viewportRoot, SurfaceLayerName);
        if (generatedSurface == null || generatedSurface.childCount > 0)
            return;

        if ((generatedSurface.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0)
            return;

        bool hasLakeImage = generatedSurface.GetComponent<UILakeSurfaceImage>() != null;
        bool looksLikeLegacyGeneratedImage =
            generatedSurface.GetComponent<Image>() != null &&
            generatedSurface.GetComponent<CanvasRenderer>() != null &&
            generatedSurface.GetComponents<Component>().Length <= 3;

        if (!hasLakeImage && !looksLikeLegacyGeneratedImage)
            return;

        if (surfaceImage != null && surfaceImage.transform == generatedSurface)
        {
            surfaceImage = null;
            surfaceLakeImage = null;
        }

        if (Application.isPlaying)
            Destroy(generatedSurface.gameObject);
        else
            DestroyImmediate(generatedSurface.gameObject);
    }

    private static RectTransform CreateStretchLayer(string layerName, RectTransform parent, bool hidden = false)
    {
        GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer));
        if (hidden)
            layer.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;

        RectTransform rect = layer.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        return rect;
    }
}
