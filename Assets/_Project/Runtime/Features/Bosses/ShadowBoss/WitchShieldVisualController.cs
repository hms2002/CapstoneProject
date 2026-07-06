using System;
using System.Collections;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class WitchShieldVisualController : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보호막의 시각 연출, 단계별 색상, 보호막 타격/파괴 사운드와 presentation을 관리한다.

    private static readonly SoundRef FallbackShieldDamagedSound = SoundRef.FromKey("sound_shadow_DamagedShield");
    private static readonly SoundRef FallbackShieldBreakSound = SoundRef.FromKey("sound_shadow_BreakShield");

    [Serializable]
    private sealed class ShieldAnimationSettings
    {
        public string triggerName;
        public string stateName;
        [Min(0f)] public float crossFadeDuration = 0.05f;

        public bool HasPlayableConfiguration =>
            !string.IsNullOrWhiteSpace(triggerName) ||
            !string.IsNullOrWhiteSpace(stateName);
    }

    [Header("References")]
    [SerializeField] private WitchShieldController shieldController;
    [SerializeField] private SpriteRenderer ownerSpriteRenderer;

    [Header("Shield Prefab Visual")]
    [SerializeField] private GameObject shieldVisualPrefab;
    [SerializeField] private Transform shieldVisualRoot;
    [SerializeField] private SpriteRenderer shieldVisualSpriteRenderer;
    [SerializeField] private Animator shieldVisualAnimator;
    [SerializeField] private Vector3 shieldVisualLocalOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float shieldVisualLocalRotationZ;
    [SerializeField] private Vector3 shieldVisualLocalScale = Vector3.one;
    [SerializeField] private int shieldVisualSortingOrderOffset = 1;
    [SerializeField] private bool tintShieldVisualWithStageColor = true;
    [SerializeField] [Range(0f, 1f)] private float shieldVisualAlphaMultiplier = 0.72f;
    [SerializeField] private SoundRef shieldActivateSound;
    [SerializeField] private SoundRef shieldDamagedSound;
    [SerializeField] private SoundRef shieldBreakSound;
    [SerializeField] private ShieldAnimationSettings activateAnimation = new ShieldAnimationSettings();
    [SerializeField] private ShieldAnimationSettings breakAnimation = new ShieldAnimationSettings();

    [Header("Shield Break Presentation")]
    [SerializeField] private WorldPresentationHook shieldBreakPresentation;
    [HideInInspector, FormerlySerializedAs("shieldBreakParticlePrefab")]
    [SerializeField] private GameObject legacyShieldBreakParticlePrefab;
    [HideInInspector, FormerlySerializedAs("shieldBreakParticleLocalOffset")]
    [SerializeField] private Vector3 legacyShieldBreakParticleLocalOffset = new Vector3(0f, 0.15f, -0.02f);
    [HideInInspector, FormerlySerializedAs("shieldBreakParticleLifetimeOverrideSeconds")]
    [SerializeField] private float legacyShieldBreakParticleLifetimeOverrideSeconds;
    [HideInInspector, FormerlySerializedAs("useUnscaledShieldBreakParticleTime")]
    [SerializeField] private bool legacyUseUnscaledShieldBreakParticleTime;
    [HideInInspector, FormerlySerializedAs("shieldBreakParticleScaleMultiplier")]
    [SerializeField] private Vector3 legacyShieldBreakParticleScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("shieldBreakParticleRotationOffsetZ")]
    [SerializeField] private float legacyShieldBreakParticleRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("shieldBreakCameraShake")]
    [SerializeField] private CameraShakeHook legacyShieldBreakCameraShake = CameraShakeHook.Create(0.2f, 1f, 0.32f, 0.05f);

    private Coroutine breakRoutine;
    private bool isSubscribedToShieldController;
    private int lastCurrentStage;
    private int lastMaxStage;

    private GameObject spawnedShieldVisualInstance;
    private Transform spawnedShieldVisualRoot;
    private SpriteRenderer spawnedShieldVisualSpriteRenderer;
    private Animator spawnedShieldVisualAnimator;

    private void Awake()
    {
        MigrateLegacyShieldBreakPresentation();
        ResolveCoreReferences();
        CleanupLegacyOutlineVisual();
        ApplySorting();
        ApplyShieldVisualPose();
        HideImmediate();
    }

    private void OnValidate()
    {
        MigrateLegacyShieldBreakPresentation();
    }

    private void OnEnable()
    {
        ResolveCoreReferences();
        TryBindShieldController();
        SyncFromController();
    }

    private void OnDisable()
    {
        UnbindShieldController();
    }

    private void OnDestroy()
    {
        if (spawnedShieldVisualInstance != null)
            Destroy(spawnedShieldVisualInstance);
    }

    private void Update()
    {
        if (isSubscribedToShieldController)
            return;

        ResolveCoreReferences();
        if (TryBindShieldController())
            SyncFromController();
    }

    private void LateUpdate()
    {
        if (lastCurrentStage <= 0)
            return;

        float ratio = lastMaxStage > 0 ? (float)lastCurrentStage / lastMaxStage : 0f;
        float pulse = 0.88f + (Mathf.Sin(Time.time * 5.5f) * 0.08f);

        SpriteRenderer visualRenderer = GetShieldVisualSpriteRenderer();
        if (tintShieldVisualWithStageColor && visualRenderer != null && visualRenderer.enabled)
        {
            Color visualColor = GetShieldColor(ratio);
            visualColor.a *= shieldVisualAlphaMultiplier * pulse;
            visualRenderer.color = visualColor;
        }
    }

    private void ResolveCoreReferences()
    {
        if (shieldController == null)
            shieldController = GetComponent<WitchShieldController>();

        if (ownerSpriteRenderer == null)
            ownerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (shieldVisualRoot == null && shieldVisualSpriteRenderer != null)
            shieldVisualRoot = shieldVisualSpriteRenderer.transform;

        if (shieldVisualRoot == null && shieldVisualAnimator != null)
            shieldVisualRoot = shieldVisualAnimator.transform;

        if (shieldVisualSpriteRenderer == null && shieldVisualRoot != null)
            shieldVisualSpriteRenderer = shieldVisualRoot.GetComponent<SpriteRenderer>();

        if (shieldVisualAnimator == null && shieldVisualRoot != null)
            shieldVisualAnimator = shieldVisualRoot.GetComponent<Animator>();
    }

    private bool TryBindShieldController()
    {
        if (shieldController == null)
            shieldController = GetComponent<WitchShieldController>();

        if (shieldController == null || isSubscribedToShieldController)
            return shieldController != null;

        shieldController.ShieldStageChanged += OnShieldStageChanged;
        shieldController.ShieldBroken += OnShieldBroken;
        isSubscribedToShieldController = true;
        return true;
    }

    private void UnbindShieldController()
    {
        if (!isSubscribedToShieldController || shieldController == null)
            return;

        shieldController.ShieldStageChanged -= OnShieldStageChanged;
        shieldController.ShieldBroken -= OnShieldBroken;
        isSubscribedToShieldController = false;
    }

    private void SyncFromController()
    {
        if (shieldController == null)
        {
            HideImmediate();
            return;
        }

        lastCurrentStage = shieldController.CurrentShieldStage;
        lastMaxStage = Mathf.Max(1, shieldController.MaxShieldStage);

        if (shieldController.HasShield)
            ShowShield(lastCurrentStage, lastMaxStage, playActivatePresentation: false);
        else
            HideImmediate();
    }

    private void OnShieldStageChanged(int currentStage, int maxStage)
    {
        bool justActivated = lastCurrentStage <= 0 && currentStage > 0;
        bool wasDamaged = lastCurrentStage > 0 && currentStage > 0 && currentStage < lastCurrentStage;

        lastCurrentStage = currentStage;
        lastMaxStage = Mathf.Max(1, maxStage);

        if (currentStage > 0)
        {
            if (wasDamaged)
                PlayShieldDamagedSound();

            ShowShield(currentStage, lastMaxStage, justActivated);
        }
        else
            HideImmediate();
    }

    private void OnShieldBroken()
    {
        if (breakRoutine != null)
            StopCoroutine(breakRoutine);

        SetOptionalShieldVisualVisible(true);
        PlayConfiguredAnimation(breakAnimation);
        SoundPlaybackUtility.Play(
            shieldBreakSound.IsSet ? shieldBreakSound : FallbackShieldBreakSound,
            instigator: gameObject,
            causer: gameObject,
            position: transform.position,
            sourceObject: this);
        Transform presentationAnchor = ResolveVisualTransform();
        WorldPresentationPlayback.PlayDeferredAsync(
            shieldBreakPresentation,
            WorldPresentationContext.AtAnchor(
                instigator: gameObject,
                anchor: presentationAnchor != null ? presentationAnchor : transform,
                fallbackDirection: Vector3.up,
                target: null,
                sourceObject: this,
                causer: gameObject));

        breakRoutine = StartCoroutine(PlayBreakRoutine());
    }

    /// <summary>보호막 단계가 감소했을 때 전용 타격 사운드를 재생합니다.</summary>
    private void PlayShieldDamagedSound()
    {
        SoundRef sound = shieldDamagedSound.IsSet ? shieldDamagedSound : FallbackShieldDamagedSound;
        SoundPlaybackUtility.Play(
            sound,
            instigator: gameObject,
            causer: gameObject,
            position: transform.position,
            sourceObject: this);
    }

    private void ShowShield(int currentStage, int maxStage, bool playActivatePresentation)
    {
        ApplySorting();
        CleanupLegacyOutlineVisual();

        EnsureShieldVisualInstance();
        ApplyShieldVisualPose();
        ApplyShieldVisual(currentStage, maxStage);

        if (playActivatePresentation)
        {
            SoundPlaybackUtility.Play(
                shieldActivateSound,
                instigator: gameObject,
                causer: gameObject,
                position: transform.position,
                sourceObject: this);
            PlayConfiguredAnimation(activateAnimation);
        }
    }

    private void ApplyShieldVisual(int currentStage, int maxStage)
    {
        Transform visualTransform = ResolveVisualTransform();
        SpriteRenderer visualRenderer = GetShieldVisualSpriteRenderer();
        Animator visualAnimator = GetShieldVisualAnimator();

        if (visualTransform == null && visualRenderer == null && visualAnimator == null)
            return;

        SetOptionalShieldVisualVisible(true);
        ApplySorting();

        if (visualRenderer != null)
        {
            if (tintShieldVisualWithStageColor)
            {
                float ratio = maxStage > 0 ? (float)currentStage / maxStage : 0f;
                Color color = GetShieldColor(ratio);
                color.a *= shieldVisualAlphaMultiplier;
                visualRenderer.color = color;
            }
        }
    }

    private IEnumerator PlayBreakRoutine()
    {
        ApplySorting();
        CleanupLegacyOutlineVisual();
        yield return new WaitForSeconds(0.22f);

        HideImmediate();
        breakRoutine = null;
    }

    private void HideImmediate()
    {
        CleanupLegacyOutlineVisual();

        SetOptionalShieldVisualVisible(false);
    }

    private void EnsureShieldVisualInstance()
    {
        if (spawnedShieldVisualInstance != null || shieldVisualPrefab == null)
            return;

        spawnedShieldVisualInstance = Instantiate(shieldVisualPrefab, transform);
        spawnedShieldVisualInstance.name = shieldVisualPrefab.name;
        spawnedShieldVisualRoot = spawnedShieldVisualInstance.transform;
        spawnedShieldVisualSpriteRenderer = spawnedShieldVisualInstance.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        spawnedShieldVisualAnimator = spawnedShieldVisualInstance.GetComponentInChildren<Animator>(includeInactive: true);
        ApplyShieldVisualPose();
        SetOptionalShieldVisualVisible(false);
    }

    private void ApplySorting()
    {
        if (ownerSpriteRenderer == null)
            return;

        SpriteRenderer visualRenderer = GetShieldVisualSpriteRenderer();
        if (visualRenderer != null)
        {
            visualRenderer.sortingLayerID = ownerSpriteRenderer.sortingLayerID;
            visualRenderer.sortingOrder = ownerSpriteRenderer.sortingOrder + shieldVisualSortingOrderOffset;
        }
    }

    private void ApplyShieldVisualPose()
    {
        Transform visualTransform = ResolveVisualTransform();
        if (visualTransform == null || visualTransform == transform)
            return;

        visualTransform.localPosition = shieldVisualLocalOffset;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, shieldVisualLocalRotationZ);
        visualTransform.localScale = shieldVisualLocalScale;
    }

    private Transform ResolveVisualTransform()
    {
        if (spawnedShieldVisualRoot != null)
            return spawnedShieldVisualRoot;

        if (shieldVisualRoot != null)
            return shieldVisualRoot;

        if (shieldVisualSpriteRenderer != null)
            return shieldVisualSpriteRenderer.transform;

        if (shieldVisualAnimator != null)
            return shieldVisualAnimator.transform;

        return null;
    }

    private void CleanupLegacyOutlineVisual()
    {
        LineRenderer legacyLineRenderer = GetComponent<LineRenderer>();
        if (legacyLineRenderer == null)
            return;

        legacyLineRenderer.enabled = false;

        if (Application.isPlaying)
            Destroy(legacyLineRenderer);
        else
            DestroyImmediate(legacyLineRenderer);
    }

    private SpriteRenderer GetShieldVisualSpriteRenderer()
    {
        if (spawnedShieldVisualSpriteRenderer != null)
            return spawnedShieldVisualSpriteRenderer;

        if (shieldVisualSpriteRenderer != null)
            return shieldVisualSpriteRenderer;

        if (shieldVisualRoot != null)
            shieldVisualSpriteRenderer = shieldVisualRoot.GetComponent<SpriteRenderer>();

        return shieldVisualSpriteRenderer;
    }

    private Animator GetShieldVisualAnimator()
    {
        if (spawnedShieldVisualAnimator != null)
            return spawnedShieldVisualAnimator;

        if (shieldVisualAnimator != null)
            return shieldVisualAnimator;

        if (shieldVisualRoot != null)
            shieldVisualAnimator = shieldVisualRoot.GetComponent<Animator>();

        return shieldVisualAnimator;
    }

    private void SetOptionalShieldVisualVisible(bool visible)
    {
        Transform visualTransform = ResolveVisualTransform();
        if (visualTransform != null && visualTransform != transform)
            visualTransform.gameObject.SetActive(visible);

        SpriteRenderer visualRenderer = GetShieldVisualSpriteRenderer();
        if (visualRenderer != null)
            visualRenderer.enabled = visible;
    }

    private void PlayConfiguredAnimation(ShieldAnimationSettings settings)
    {
        Animator visualAnimator = GetShieldVisualAnimator();
        if (visualAnimator == null || settings == null || !settings.HasPlayableConfiguration)
            return;

        if (!string.IsNullOrWhiteSpace(settings.triggerName))
            visualAnimator.SetTrigger(settings.triggerName);

        if (string.IsNullOrWhiteSpace(settings.stateName))
            return;

        if (settings.crossFadeDuration > 0f)
            visualAnimator.CrossFadeInFixedTime(settings.stateName, settings.crossFadeDuration);
        else
            visualAnimator.Play(settings.stateName);
    }

    private Color GetShieldColor(float ratio)
    {
        if (ratio >= 0.75f)
            return new Color(0.48f, 0.9f, 1f, 0.92f);

        if (ratio >= 0.5f)
            return new Color(0.56f, 0.84f, 1f, 0.9f);

        if (ratio >= 0.25f)
            return new Color(0.98f, 0.7f, 0.28f, 0.92f);

        return new Color(1f, 0.34f, 0.34f, 0.96f);
    }

    private void MigrateLegacyShieldBreakPresentation()
    {
        if (legacyShieldBreakParticlePrefab != null && !shieldBreakPresentation.particle.HasContent)
        {
            shieldBreakPresentation.particle.prefab = legacyShieldBreakParticlePrefab;
            shieldBreakPresentation.particle.localOffset = legacyShieldBreakParticleLocalOffset;
            shieldBreakPresentation.particle.rotationOffsetZ = legacyShieldBreakParticleRotationOffsetZ;
            shieldBreakPresentation.particle.scaleMultiplier = legacyShieldBreakParticleScaleMultiplier;
            shieldBreakPresentation.particle.lifetimeOverrideSeconds = legacyShieldBreakParticleLifetimeOverrideSeconds;
            shieldBreakPresentation.particle.useUnscaledTime = legacyUseUnscaledShieldBreakParticleTime;
        }

        if (!shieldBreakPresentation.HasShake && legacyShieldBreakCameraShake.amplitude > 0f)
            shieldBreakPresentation.cameraShake = legacyShieldBreakCameraShake;
    }
}
