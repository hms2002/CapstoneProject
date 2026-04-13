using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class WitchShieldVisualController : MonoBehaviour
{
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

    private const float DefaultPresentationLifetimeSeconds = 1f;

    [Header("Shield Outline")]
    [SerializeField] private WitchShieldController shieldController;
    [SerializeField] private SpriteRenderer ownerSpriteRenderer;
    [SerializeField] private float radiusX = 1.25f;
    [SerializeField] private float radiusY = 1.65f;
    [SerializeField] private float lineWidth = 0.12f;
    [SerializeField] private int segmentCount = 40;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.15f, 0f);

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
    [SerializeField] private ShieldAnimationSettings activateAnimation = new ShieldAnimationSettings();
    [SerializeField] private ShieldAnimationSettings breakAnimation = new ShieldAnimationSettings();

    [Header("Shield Break Presentation")]
    [SerializeField] private GameObject shieldBreakParticlePrefab;
    [SerializeField] private Vector3 shieldBreakParticleLocalOffset = new Vector3(0f, 0.15f, -0.02f);
    [SerializeField] [Min(0f)] private float shieldBreakParticleLifetimeOverrideSeconds;
    [SerializeField] private bool useUnscaledShieldBreakParticleTime;
    [SerializeField] private Vector3 shieldBreakParticleScaleMultiplier = Vector3.one;
    [SerializeField] private float shieldBreakParticleRotationOffsetZ;
    [SerializeField] private CameraShakeHook shieldBreakCameraShake = CameraShakeHook.Create(0.2f, 1f, 0.32f, 0.05f);

    private LineRenderer lineRenderer;
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
        ResolveCoreReferences();
        EnsureLineRenderer();
        ApplySorting();
        ApplyShieldVisualPose();
        HideImmediate();
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
        if (lineRenderer == null || !lineRenderer.enabled || lastCurrentStage <= 0)
            return;

        float ratio = lastMaxStage > 0 ? (float)lastCurrentStage / lastMaxStage : 0f;
        float pulse = 0.88f + (Mathf.Sin(Time.time * 5.5f) * 0.08f);
        Color color = GetShieldColor(ratio);
        color.a *= pulse;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

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

        lastCurrentStage = currentStage;
        lastMaxStage = Mathf.Max(1, maxStage);

        if (currentStage > 0)
            ShowShield(currentStage, lastMaxStage, justActivated);
        else
            HideImmediate();
    }

    private void OnShieldBroken()
    {
        if (breakRoutine != null)
            StopCoroutine(breakRoutine);

        SetOptionalShieldVisualVisible(true);
        PlayConfiguredAnimation(breakAnimation);
        SpawnPresentationPrefab(
            shieldBreakParticlePrefab,
            shieldBreakParticleLocalOffset,
            shieldBreakParticleRotationOffsetZ,
            shieldBreakParticleScaleMultiplier,
            shieldBreakParticleLifetimeOverrideSeconds,
            useUnscaledShieldBreakParticleTime);
        shieldBreakCameraShake.TryPlay(
            source: gameObject,
            fallbackDirection: Vector3.up,
            debugReason: "WitchShield.Break");

        breakRoutine = StartCoroutine(PlayBreakRoutine());
    }

    private void ShowShield(int currentStage, int maxStage, bool playActivatePresentation)
    {
        EnsureLineRenderer();
        ApplySorting();
        BuildEllipse(radiusX, radiusY);

        float ratio = maxStage > 0 ? (float)currentStage / maxStage : 0f;
        float widthScale = Mathf.Lerp(0.52f, 1f, ratio);
        float resolvedWidth = lineWidth * widthScale;
        Color color = GetShieldColor(ratio);

        lineRenderer.enabled = true;
        lineRenderer.startWidth = resolvedWidth;
        lineRenderer.endWidth = resolvedWidth;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        EnsureShieldVisualInstance();
        ApplyShieldVisualPose();
        ApplyShieldVisual(currentStage, maxStage);

        if (playActivatePresentation)
            PlayConfiguredAnimation(activateAnimation);
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
        EnsureLineRenderer();
        ApplySorting();

        float duration = 0.22f;
        float elapsed = 0f;
        float startRadiusX = radiusX * 0.94f;
        float startRadiusY = radiusY * 0.94f;
        float endRadiusX = radiusX * 1.24f;
        float endRadiusY = radiusY * 1.24f;
        Color startColor = new Color(1f, 0.86f, 0.72f, 0.92f);
        Color endColor = new Color(1f, 0.18f, 0.18f, 0f);

        lineRenderer.enabled = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            BuildEllipse(
                Mathf.Lerp(startRadiusX, endRadiusX, eased),
                Mathf.Lerp(startRadiusY, endRadiusY, eased));

            float width = Mathf.Lerp(lineWidth * 1.35f, 0.01f, eased);
            Color color = Color.Lerp(startColor, endColor, eased);

            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            yield return null;
        }

        HideImmediate();
        breakRoutine = null;
    }

    private void HideImmediate()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        SetOptionalShieldVisualVisible(false);
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer != null)
            return;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.positionCount = Mathf.Max(8, segmentCount);
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
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

        if (lineRenderer != null)
        {
            lineRenderer.sortingLayerID = ownerSpriteRenderer.sortingLayerID;
            lineRenderer.sortingOrder = ownerSpriteRenderer.sortingOrder + 2;
        }

        SpriteRenderer visualRenderer = GetShieldVisualSpriteRenderer();
        if (visualRenderer != null)
        {
            visualRenderer.sortingLayerID = ownerSpriteRenderer.sortingLayerID;
            visualRenderer.sortingOrder = ownerSpriteRenderer.sortingOrder + shieldVisualSortingOrderOffset;
        }
    }

    private void BuildEllipse(float ellipseRadiusX, float ellipseRadiusY)
    {
        if (lineRenderer == null)
            return;

        int count = Mathf.Max(8, segmentCount);
        if (lineRenderer.positionCount != count)
            lineRenderer.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float normalized = (float)i / count;
            float angle = normalized * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                Mathf.Cos(angle) * ellipseRadiusX,
                Mathf.Sin(angle) * ellipseRadiusY,
                0f) + localOffset;
            lineRenderer.SetPosition(i, point);
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

    private void SpawnPresentationPrefab(
        GameObject prefab,
        Vector3 localOffset,
        float rotationOffsetZ,
        Vector3 scaleMultiplier,
        float lifetimeOverrideSeconds,
        bool useUnscaledTime)
    {
        if (prefab == null)
            return;

        Transform anchor = ResolveVisualTransform();
        if (anchor == null)
            anchor = transform;

        Vector3 position = anchor.TransformPoint(localOffset);
        Quaternion rotation = anchor.rotation * Quaternion.Euler(0f, 0f, rotationOffsetZ);
        GameObject instance = Instantiate(prefab, position, rotation);
        if (instance == null)
            return;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
        ConfigureSpawnedPresentation(instance, useUnscaledTime);

        float lifetime = ResolvePresentationLifetime(instance, lifetimeOverrideSeconds);
        if (lifetime > 0f)
            Destroy(instance, lifetime);
    }

    private static void ConfigureSpawnedPresentation(GameObject instance, bool useUnscaledTime)
    {
        if (instance == null)
            return;

        instance.SetActive(true);

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (useUnscaledTime)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            particleSystem.Play(withChildren: true);
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            animationComponent.Play();
        }
    }

    private static float ResolvePresentationLifetime(GameObject instance, float lifetimeOverrideSeconds)
    {
        if (lifetimeOverrideSeconds > 0f)
            return lifetimeOverrideSeconds;

        float particleLifetime = ResolveParticleLifetime(instance);
        if (particleLifetime > 0f)
            return particleLifetime;

        float animationLifetime = ResolveAnimatorLifetime(instance);
        if (animationLifetime > 0f)
            return animationLifetime;

        return DefaultPresentationLifetimeSeconds;
    }

    private static float ResolveParticleLifetime(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        if (particleSystems == null || particleSystems.Length == 0)
            return 0f;

        float maxLifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            if (main.loop)
                return DefaultPresentationLifetimeSeconds;

            float startDelay = ResolveCurveMax(main.startDelay);
            float startLifetime = ResolveCurveMax(main.startLifetime);
            maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
        }

        return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
    }

    private static float ResolveAnimatorLifetime(GameObject instance)
    {
        float maxLifetime = 0f;

        Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                if (clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, clip.length);
            }
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            foreach (AnimationState state in animationComponent)
            {
                if (state?.clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, state.clip.length);
            }
        }

        return maxLifetime > 0f ? maxLifetime + 0.05f : 0f;
    }

    private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
            _ => Mathf.Max(curve.constant, curve.constantMax)
        };
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
}
