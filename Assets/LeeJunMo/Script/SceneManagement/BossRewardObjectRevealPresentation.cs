using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 보스 처치 보상 포탈/오브젝트가 등장할 때 마스크, 파티클, 입력 잠금, 카메라 흔들림을 묶어 reveal 연출을 재생한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Boss Reward Portal Reveal Presentation")]
public sealed class BossRewardObjectRevealPresentation : MonoBehaviour
{
    private static readonly Color GizmoParticleColor = new Color(1f, 0.82f, 0.12f, 0.95f);
    private static readonly Color GizmoMaskColor = new Color(0.1f, 0.85f, 1f, 0.9f);
    private static readonly Color GizmoRevealStartColor = new Color(1f, 0.45f, 0.05f, 0.9f);
    private static readonly Color GizmoRevealEndColor = new Color(0.25f, 1f, 0.35f, 0.9f);
    private static readonly Color GizmoRevealPathColor = new Color(1f, 1f, 1f, 0.65f);
    private const float GizmoPointRadius = 0.08f;

    [Header("Reveal")]
    [SerializeField] private Transform revealRoot;
    [SerializeField] private SpriteMask revealMask;
    [SerializeField] private SpriteRenderer[] maskedRenderers;
    [SerializeField] private Vector3 startLocalOffset = new Vector3(0f, -1f, 0f);
    [SerializeField, Min(0f)] private float revealDurationSeconds = 0.75f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool applyMaskDuringReveal = true;
    [SerializeField] private bool disableMaskAfterReveal = true;

    [Header("Particles")]
    [SerializeField] private ParticleSystem[] loopDustParticles;
    [SerializeField] private bool stopLoopDustOnComplete = true;
    [SerializeField] private ParticleSystem[] burstDustParticles;
    [SerializeField] private bool clearParticlesBeforePlay = true;
    [SerializeField] private Transform particleSpawnAnchor;
    [SerializeField] private Vector3 particleSpawnLocalOffset;
    [SerializeField] private bool parentSpawnedParticlesToAnchor;
    [SerializeField, Min(0f)] private float spawnedParticleDestroyDelay = 2f;

    [Header("Interaction Lock")]
    [SerializeField] private Collider2D[] collidersToDisableDuringReveal;

    [Header("Scene Mask Isolation")]
    [SerializeField] private bool isolateGlobalVisionMasksDuringReveal = true;

    [Header("Reveal Shake")]
    [SerializeField] private bool playLoopCameraShake = true;
    [SerializeField] private CameraShakeHook loopCameraShake = CameraShakeHook.Create(0.035f, 1f, 0f, 0.18f);
    [SerializeField] private bool playCompleteCameraShake = true;
    [SerializeField] private CameraShakeHook completeCameraShake = CameraShakeHook.Create(0.16f, 1f, 0f, 0f);
    [SerializeField] private bool shakeRevealRootDuringReveal = true;
    [SerializeField] private Vector3 revealRootShakeAmplitude = new Vector3(0.035f, 0.018f, 0f);
    [SerializeField, Min(0f)] private float revealRootShakeFrequency = 24f;

    private readonly List<RendererMaskState> rendererMaskStates = new();
    private readonly List<ColliderState> colliderStates = new();
    private readonly List<SpriteMaskRangeState> globalVisionMaskStates = new();
    private readonly List<ParticleSystem> spawnedLoopDustParticles = new();
    private readonly List<ParticleSystem> spawnedBurstDustParticles = new();
    private readonly List<ParticleTransformState> sceneParticleTransformStates = new();
    private Coroutine revealRoutine;
    private GameFlowInputBlocker inputBlocker;
    private SpriteMask activeRevealMask;
    private SpriteMask runtimeRevealMask;
    private Vector3 revealRootFinalLocalPosition;
    private float revealPlaybackElapsedSeconds;
    private bool hasRevealRootState;
    private bool hasMaskState;
    private bool revealMaskWasEnabled;

    public void PlayReveal()
    {
        if (!isActiveAndEnabled)
            return;

        StopRevealRoutine(complete: true);
        StopLoopDustParticles(clear: true);
        DestroySpawnedParticles(immediate: true);
        revealRoutine = StartCoroutine(PlayRevealRoutine());
    }

    private void Reset()
    {
        revealMask = GetComponentInChildren<SpriteMask>(includeInactive: true);
        maskedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        collidersToDisableDuringReveal = GetComponentsInChildren<Collider2D>(includeInactive: true);
    }

    private void OnDisable()
    {
        StopRevealRoutine(complete: true);
        StopLoopDustParticles(clear: true);
        DestroySpawnedParticles(immediate: true);
    }

    private IEnumerator PlayRevealRoutine()
    {
        AcquireInputBlocker();
        CaptureRevealState();
        ApplyRevealStart();
        ApplyRevealProgress(0f, 0f);
        PlayRevealStartParticles();

        float duration = Mathf.Max(0f, revealDurationSeconds);
        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float normalized = Mathf.Clamp01(elapsed / duration);
                ApplyRevealProgress(normalized, elapsed);
                PlayLoopCameraShakeIfNeeded(normalized);
                elapsed += ResolveDeltaTime();
                yield return null;
            }
        }

        ApplyRevealProgress(1f, duration);
        PlayRevealCompleteParticles();
        CompleteReveal();
    }

    private void CaptureRevealState()
    {
        activeRevealMask = null;
        runtimeRevealMask = null;

        Transform root = ResolveRevealRoot();
        if (root != null)
        {
            revealRootFinalLocalPosition = root.localPosition;
            hasRevealRootState = true;
        }

        if (revealMask != null)
        {
            revealMaskWasEnabled = revealMask.enabled;
            hasMaskState = true;
        }

        CaptureRendererMaskStates();
        CaptureColliderStates();
    }

    private void CaptureRendererMaskStates()
    {
        rendererMaskStates.Clear();
        if ((revealMask == null && revealRoot == null) || maskedRenderers == null)
            return;

        for (int i = 0; i < maskedRenderers.Length; i++)
        {
            SpriteRenderer renderer = maskedRenderers[i];
            if (renderer == null)
                continue;

            rendererMaskStates.Add(new RendererMaskState(renderer, renderer.maskInteraction));
        }
    }

    private void CaptureColliderStates()
    {
        colliderStates.Clear();
        if (collidersToDisableDuringReveal == null)
            return;

        for (int i = 0; i < collidersToDisableDuringReveal.Length; i++)
        {
            Collider2D collider = collidersToDisableDuringReveal[i];
            if (collider == null)
                continue;

            colliderStates.Add(new ColliderState(collider, collider.enabled));
        }
    }

    private void ApplyRevealStart()
    {
        activeRevealMask = ResolvePlaybackRevealMask();

        if (applyMaskDuringReveal && activeRevealMask != null)
        {
            if (rendererMaskStates.Count > 0)
                RestrictGlobalVisionMasksForReveal();

            activeRevealMask.enabled = true;
            if (revealMask != null && activeRevealMask != revealMask)
                revealMask.enabled = false;

            for (int i = 0; i < rendererMaskStates.Count; i++)
            {
                SpriteRenderer renderer = rendererMaskStates[i].Renderer;
                if (renderer != null)
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        SetRevealCollidersEnabled(false);
    }

    private void ApplyRevealProgress(float normalized, float elapsedSeconds)
    {
        revealPlaybackElapsedSeconds = Mathf.Max(0f, elapsedSeconds);

        if (!hasRevealRootState)
            return;

        Transform root = ResolveRevealRoot();
        if (root == null)
            return;

        float curveValue = revealCurve != null
            ? Mathf.Clamp01(revealCurve.Evaluate(Mathf.Clamp01(normalized)))
            : Mathf.Clamp01(normalized);
        Vector3 basePosition = Vector3.LerpUnclamped(
            revealRootFinalLocalPosition + startLocalOffset,
            revealRootFinalLocalPosition,
            curveValue);
        root.localPosition = basePosition + ResolveRevealRootShakeOffset(normalized);
    }

    private void CompleteReveal()
    {
        PlayCompleteCameraShakeIfNeeded();
        if (stopLoopDustOnComplete)
            StopLoopDustParticles(clear: false);

        RestoreRevealState(complete: true);
        ReleaseInputBlocker();
        revealRoutine = null;
    }

    private void PlayLoopCameraShakeIfNeeded(float normalized)
    {
        if (!playLoopCameraShake || !ShouldPlayRevealMotionShake())
            return;

        float envelope = ResolveRevealMotionEnvelope(normalized);
        if (envelope <= 0f)
            return;

        loopCameraShake.TryPlay(gameObject, Vector3.up, envelope, "Boss reward gate reveal loop");
    }

    private void PlayCompleteCameraShakeIfNeeded()
    {
        if (!playCompleteCameraShake || !ShouldPlayRevealMotionShake())
            return;

        completeCameraShake.TryPlay(gameObject, Vector3.up, 1f, "Boss reward gate reveal complete");
    }

    private Vector3 ResolveRevealRootShakeOffset(float normalized)
    {
        if (!shakeRevealRootDuringReveal ||
            !ShouldPlayRevealMotionShake() ||
            revealRootShakeAmplitude == Vector3.zero ||
            revealRootShakeFrequency <= 0f)
        {
            return Vector3.zero;
        }

        float envelope = ResolveRevealMotionEnvelope(normalized);
        if (envelope <= 0f)
            return Vector3.zero;

        float angle = revealPlaybackElapsedSeconds * revealRootShakeFrequency * Mathf.PI * 2f;
        return new Vector3(
            Mathf.Sin(angle) * revealRootShakeAmplitude.x,
            Mathf.Cos(angle * 1.37f) * revealRootShakeAmplitude.y,
            Mathf.Sin(angle * 0.73f) * revealRootShakeAmplitude.z) * envelope;
    }

    private static float ResolveRevealMotionEnvelope(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clamped * 6f));
        float release = 1f - Mathf.SmoothStep(0.85f, 1f, clamped);
        return attack * release;
    }

    private float ResolveDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void AcquireInputBlocker()
    {
        if (inputBlocker != null && inputBlocker.IsBlocking)
            return;

        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
    }

    private void ReleaseInputBlocker()
    {
        inputBlocker?.Release();
        inputBlocker = null;
    }

    private bool ShouldPlayRevealMotionShake()
    {
        return hasRevealRootState && ResolveRevealRoot() != null && ResolveFirstMaskedRenderer() != null;
    }

    private void StopRevealRoutine(bool complete)
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        RestoreRevealState(complete);
        ReleaseInputBlocker();
    }

    private void RestoreRevealState(bool complete)
    {
        if (hasRevealRootState)
        {
            Transform root = ResolveRevealRoot();
            if (root != null)
                root.localPosition = complete ? revealRootFinalLocalPosition : root.localPosition;
        }

        RestoreRendererMasks();
        RestoreMaskState();
        RestoreGlobalVisionMaskRanges();
        SetRevealCollidersEnabled(true);
        RestoreSceneParticleTransforms();
        hasRevealRootState = false;
    }

    private void RestoreRendererMasks()
    {
        for (int i = 0; i < rendererMaskStates.Count; i++)
        {
            RendererMaskState state = rendererMaskStates[i];
            if (state.Renderer != null)
                state.Renderer.maskInteraction = state.MaskInteraction;
        }

        rendererMaskStates.Clear();
    }

    private void RestoreMaskState()
    {
        if (revealMask != null && hasMaskState)
            revealMask.enabled = applyMaskDuringReveal && disableMaskAfterReveal ? false : revealMaskWasEnabled;

        if (runtimeRevealMask != null)
        {
            Destroy(runtimeRevealMask.gameObject);
            runtimeRevealMask = null;
        }

        activeRevealMask = null;
        hasMaskState = false;
    }

    private void RestrictGlobalVisionMasksForReveal()
    {
        globalVisionMaskStates.Clear();
        if (!isolateGlobalVisionMasksDuringReveal)
            return;

        GlobalVisionMaskController[] controllers =
            FindObjectsByType<GlobalVisionMaskController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            GlobalVisionMaskController controller = controllers[i];
            if (controller == null || !controller.isActiveAndEnabled)
                continue;

            if (!TryResolveVisionOverlayRange(controller, out int sortingLayerId, out int sortingOrder))
                continue;

            SpriteMask[] sceneMasks = controller.GetComponentsInChildren<SpriteMask>(includeInactive: true);
            for (int maskIndex = 0; maskIndex < sceneMasks.Length; maskIndex++)
            {
                SpriteMask sceneMask = sceneMasks[maskIndex];
                if (sceneMask == null || IsRevealMask(sceneMask))
                    continue;

                // Player vision masks are restricted too, so reward renderers only respond to the reveal mask.
                globalVisionMaskStates.Add(new SpriteMaskRangeState(sceneMask));
                sceneMask.isCustomRangeActive = true;
                sceneMask.frontSortingLayerID = sortingLayerId;
                sceneMask.backSortingLayerID = sortingLayerId;
                sceneMask.frontSortingOrder = sortingOrder;
                sceneMask.backSortingOrder = sortingOrder;
            }
        }
    }

    private void RestoreGlobalVisionMaskRanges()
    {
        for (int i = 0; i < globalVisionMaskStates.Count; i++)
        {
            SpriteMaskRangeState state = globalVisionMaskStates[i];
            if (state.Mask == null)
                continue;

            state.Mask.isCustomRangeActive = state.IsCustomRangeActive;
            state.Mask.frontSortingLayerID = state.FrontSortingLayerID;
            state.Mask.backSortingLayerID = state.BackSortingLayerID;
            state.Mask.frontSortingOrder = state.FrontSortingOrder;
            state.Mask.backSortingOrder = state.BackSortingOrder;
        }

        globalVisionMaskStates.Clear();
    }

    private static bool TryResolveVisionOverlayRange(
        GlobalVisionMaskController controller,
        out int sortingLayerId,
        out int sortingOrder)
    {
        sortingLayerId = 0;
        sortingOrder = 0;

        if (controller == null)
            return false;

        SpriteRenderer[] renderers = controller.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.maskInteraction == SpriteMaskInteraction.None)
                continue;

            sortingLayerId = renderer.sortingLayerID;
            sortingOrder = renderer.sortingOrder;
            return true;
        }

        return false;
    }

    private void SetRevealCollidersEnabled(bool restore)
    {
        for (int i = 0; i < colliderStates.Count; i++)
        {
            ColliderState state = colliderStates[i];
            if (state.Collider != null)
                state.Collider.enabled = restore ? state.WasEnabled : false;
        }

        if (restore)
            colliderStates.Clear();
    }

    private void PlayRevealStartParticles()
    {
        PlayParticles(loopDustParticles, isLoopDust: true);
    }

    private void PlayRevealCompleteParticles()
    {
        PlayParticles(burstDustParticles, isLoopDust: false);
    }

    private void PlayParticles(ParticleSystem[] particles, bool isLoopDust)
    {
        if (particles == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            ParticleSystem playableParticle = ResolvePlayableParticle(particle, isLoopDust);
            if (playableParticle == null)
                continue;

            ParticleSystem.MainModule main = playableParticle.main;
            main.useUnscaledTime = useUnscaledTime;

            playableParticle.gameObject.SetActive(true);
            if (clearParticlesBeforePlay)
                playableParticle.Clear(withChildren: true);
            playableParticle.Play(withChildren: true);
        }
    }

    private void StopLoopDustParticles(bool clear)
    {
        ParticleSystemStopBehavior stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        if (loopDustParticles != null)
        {
            for (int i = 0; i < loopDustParticles.Length; i++)
            {
                ParticleSystem particle = loopDustParticles[i];
                if (particle != null && particle.gameObject.scene.IsValid())
                    particle.Stop(true, stopBehavior);
            }
        }

        for (int i = 0; i < spawnedLoopDustParticles.Count; i++)
        {
            ParticleSystem particle = spawnedLoopDustParticles[i];
            if (particle == null)
                continue;

            particle.Stop(true, stopBehavior);
            if (clear)
                Destroy(particle.gameObject);
            else
                Destroy(particle.gameObject, spawnedParticleDestroyDelay);
        }

        spawnedLoopDustParticles.Clear();
    }

    private Transform ResolveRevealRoot()
    {
        return revealRoot;
    }

    private SpriteMask ResolvePlaybackRevealMask()
    {
        if (!applyMaskDuringReveal || rendererMaskStates.Count == 0)
            return null;

        Transform root = ResolveRevealRoot();
        if (revealMask != null && (root == null || !revealMask.transform.IsChildOf(root)))
            return revealMask;

        SpriteRenderer sourceRenderer = rendererMaskStates[0].Renderer;
        if (sourceRenderer == null)
            return revealMask;

        runtimeRevealMask = CreateRuntimeRevealMask(root, sourceRenderer);
        return runtimeRevealMask != null ? runtimeRevealMask : revealMask;
    }

    private SpriteMask CreateRuntimeRevealMask(Transform root, SpriteRenderer sourceRenderer)
    {
        if (sourceRenderer == null)
            return null;

        GameObject maskObject = new GameObject($"{name}_RuntimeRevealMask");
        Transform parent = root != null ? root.parent : transform.parent;
        Transform anchor = revealMask != null ? revealMask.transform : sourceRenderer.transform;

        maskObject.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        maskObject.transform.localScale = anchor.lossyScale;
        if (parent != null)
            maskObject.transform.SetParent(parent, worldPositionStays: true);

        SpriteMask mask = maskObject.AddComponent<SpriteMask>();
        if (revealMask != null)
        {
            mask.sprite = revealMask.sprite;
            mask.alphaCutoff = revealMask.alphaCutoff;
            mask.isCustomRangeActive = revealMask.isCustomRangeActive;
            mask.frontSortingLayerID = revealMask.frontSortingLayerID;
            mask.backSortingLayerID = revealMask.backSortingLayerID;
            mask.frontSortingOrder = revealMask.frontSortingOrder;
            mask.backSortingOrder = revealMask.backSortingOrder;
        }
        else
        {
            mask.sprite = sourceRenderer.sprite;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = sourceRenderer.sortingLayerID;
            mask.backSortingLayerID = sourceRenderer.sortingLayerID;
            mask.frontSortingOrder = sourceRenderer.sortingOrder + 1;
            mask.backSortingOrder = sourceRenderer.sortingOrder - 1;
        }

        mask.enabled = false;
        return mask;
    }

    private bool IsRevealMask(SpriteMask mask)
    {
        return mask != null && (mask == revealMask || mask == activeRevealMask || mask == runtimeRevealMask);
    }

    private ParticleSystem ResolvePlayableParticle(ParticleSystem configuredParticle, bool isLoopDust)
    {
        if (configuredParticle == null)
            return null;

        if (configuredParticle.gameObject.scene.IsValid())
        {
            if (isLoopDust)
                PrepareSceneParticleForReveal(configuredParticle);

            return configuredParticle;
        }

        ResolveParticleSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation);
        Transform anchor = ResolveParticleSpawnAnchor();
        Transform parent = parentSpawnedParticlesToAnchor ? anchor : null;

        ParticleSystem instance = Instantiate(configuredParticle, spawnPosition, spawnRotation, parent);
        instance.name = configuredParticle.name;

        if (isLoopDust)
        {
            spawnedLoopDustParticles.Add(instance);
        }
        else
        {
            spawnedBurstDustParticles.Add(instance);
            Destroy(instance.gameObject, ResolveParticleDestroyDelay(instance));
        }

        return instance;
    }

    private void PrepareSceneParticleForReveal(ParticleSystem particle)
    {
        if (particle == null)
            return;

        Transform particleTransform = particle.transform;
        Transform root = ResolveRevealRoot();
        bool shouldDetachFromRevealRoot =
            root != null &&
            particleTransform != root &&
            particleTransform.IsChildOf(root);
        bool shouldApplyConfiguredPose =
            particleSpawnAnchor != null ||
            particleSpawnLocalOffset != Vector3.zero;

        if (!shouldDetachFromRevealRoot && !shouldApplyConfiguredPose)
            return;

        CaptureSceneParticleTransform(particleTransform);

        if (shouldDetachFromRevealRoot)
            particleTransform.SetParent(root.parent, worldPositionStays: true);

        if (shouldApplyConfiguredPose)
        {
            ResolveParticleSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation);
            particleTransform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
    }

    private void CaptureSceneParticleTransform(Transform particleTransform)
    {
        if (particleTransform == null)
            return;

        for (int i = 0; i < sceneParticleTransformStates.Count; i++)
        {
            if (sceneParticleTransformStates[i].Transform == particleTransform)
                return;
        }

        sceneParticleTransformStates.Add(new ParticleTransformState(particleTransform));
    }

    private void RestoreSceneParticleTransforms()
    {
        for (int i = 0; i < sceneParticleTransformStates.Count; i++)
        {
            ParticleTransformState state = sceneParticleTransformStates[i];
            if (state.Transform == null)
                continue;

            state.Transform.SetParent(state.Parent, worldPositionStays: false);
            state.Transform.localPosition = state.LocalPosition;
            state.Transform.localRotation = state.LocalRotation;
            state.Transform.localScale = state.LocalScale;
        }

        sceneParticleTransformStates.Clear();
    }

    private void ResolveParticleSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        Transform anchor = ResolveParticleSpawnAnchor();
        if (anchor == null)
        {
            spawnPosition = transform.position + particleSpawnLocalOffset;
            spawnRotation = transform.rotation;
            return;
        }

        Transform root = ResolveRevealRoot();
        if (Application.isPlaying &&
            hasRevealRootState &&
            root != null &&
            anchor.IsChildOf(root))
        {
            Matrix4x4 endRootMatrix = ComposeWorldMatrix(root, revealRootFinalLocalPosition);
            Matrix4x4 anchorMatrix = ResolveEndMatrixForTransform(anchor, root, endRootMatrix);
            spawnPosition = anchorMatrix.MultiplyPoint3x4(particleSpawnLocalOffset);
            spawnRotation = anchor.rotation;
            return;
        }

        spawnPosition = anchor.TransformPoint(particleSpawnLocalOffset);
        spawnRotation = anchor.rotation;
    }

    private Transform ResolveParticleSpawnAnchor()
    {
        if (particleSpawnAnchor != null)
            return particleSpawnAnchor;

        Transform root = ResolveRevealRoot();
        return root != null ? root : transform;
    }

    private float ResolveParticleDestroyDelay(ParticleSystem particle)
    {
        if (particle == null)
            return spawnedParticleDestroyDelay;

        ParticleSystem.MainModule main = particle.main;
        float lifetime = main.startLifetime.constantMax;
        return Mathf.Max(spawnedParticleDestroyDelay, main.duration + lifetime);
    }

    private void DestroySpawnedParticles(bool immediate)
    {
        DestroySpawnedParticles(spawnedLoopDustParticles, immediate);
        DestroySpawnedParticles(spawnedBurstDustParticles, immediate);
    }

    private void DestroySpawnedParticles(List<ParticleSystem> particles, bool immediate)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            if (immediate)
                Destroy(particle.gameObject);
            else
                Destroy(particle.gameObject, spawnedParticleDestroyDelay);
        }

        particles.Clear();
    }

    private void OnDrawGizmos()
    {
        DrawRevealGizmos(drawLabels: false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawRevealGizmos(drawLabels: true);
    }

    private void DrawRevealGizmos(bool drawLabels)
    {
        DrawRevealPathGizmos(drawLabels);
        DrawParticleSpawnGizmo(drawLabels);
        DrawRevealMaskGizmo(drawLabels);
    }

    private void DrawRevealPathGizmos(bool drawLabels)
    {
        Transform root = ResolveRevealRoot();
        if (root == null || !TryResolveRevealRootMatrices(
                out Matrix4x4 startRootMatrix,
                out Matrix4x4 endRootMatrix,
                out Vector3 startPosition,
                out Vector3 endPosition))
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = GizmoRevealPathColor;
        Gizmos.DrawLine(startPosition, endPosition);
        Gizmos.color = GizmoRevealStartColor;
        Gizmos.DrawWireSphere(startPosition, GizmoPointRadius * 1.5f);
        Gizmos.color = GizmoRevealEndColor;
        Gizmos.DrawWireSphere(endPosition, GizmoPointRadius * 1.5f);
        Gizmos.color = previousColor;

        if (drawLabels)
        {
            DrawGizmoLabel(startPosition, "Reveal Start", GizmoRevealStartColor);
            DrawGizmoLabel(endPosition, "Reveal End", GizmoRevealEndColor);
        }

        if (maskedRenderers == null)
            return;

        bool labelsDrawn = false;
        for (int i = 0; i < maskedRenderers.Length; i++)
        {
            SpriteRenderer renderer = maskedRenderers[i];
            if (renderer == null)
                continue;

            Matrix4x4 endMatrix = ResolveEndMatrixForTransform(renderer.transform, root, endRootMatrix);
            Matrix4x4 startMatrix = ResolveStartMatrixForTransform(renderer.transform, root, startRootMatrix);
            DrawSpriteBoundsGizmo(startMatrix, renderer.sprite, GizmoRevealStartColor);
            DrawSpriteBoundsGizmo(endMatrix, renderer.sprite, GizmoRevealEndColor);

            Vector3 startCenter = ResolveSpriteCenterWorld(startMatrix, renderer.sprite);
            Vector3 endCenter = ResolveSpriteCenterWorld(endMatrix, renderer.sprite);
            previousColor = Gizmos.color;
            Gizmos.color = GizmoRevealPathColor;
            Gizmos.DrawLine(startCenter, endCenter);
            Gizmos.color = previousColor;

            if (drawLabels && !labelsDrawn)
            {
                DrawGizmoLabel(startCenter, "Masked Start", GizmoRevealStartColor);
                DrawGizmoLabel(endCenter, "Masked End", GizmoRevealEndColor);
                labelsDrawn = true;
            }
        }
    }

    private void DrawParticleSpawnGizmo(bool drawLabels)
    {
        if (!HasParticleReferences() && particleSpawnAnchor == null)
            return;

        Vector3 spawnPosition = ResolveParticleSpawnGizmoPosition();
        Transform anchor = ResolveParticleSpawnAnchor();

        Color previousColor = Gizmos.color;
        Gizmos.color = GizmoParticleColor;
        Gizmos.DrawSphere(spawnPosition, GizmoPointRadius);
        Gizmos.DrawWireSphere(spawnPosition, GizmoPointRadius * 2.25f);
        if (anchor != null)
            Gizmos.DrawLine(anchor.position, spawnPosition);
        Gizmos.color = previousColor;

        if (drawLabels)
            DrawGizmoLabel(spawnPosition, "Particle Spawn", GizmoParticleColor);
    }

    private void DrawRevealMaskGizmo(bool drawLabels)
    {
        if (!applyMaskDuringReveal ||
            !TryResolveRevealMaskGizmoSource(out Transform sourceTransform, out Sprite sourceSprite, out string label))
        {
            return;
        }

        Transform root = ResolveRevealRoot();
        Matrix4x4 endRootMatrix = Matrix4x4.identity;
        TryResolveRevealRootMatrices(out _, out endRootMatrix, out _, out _);
        Matrix4x4 sourceMatrix = ResolveEndMatrixForTransform(sourceTransform, root, endRootMatrix);
        DrawSpriteBoundsGizmo(sourceMatrix, sourceSprite, GizmoMaskColor);
        if (drawLabels)
            DrawGizmoLabel(ResolveSpriteCenterWorld(sourceMatrix, sourceSprite), label, GizmoMaskColor);
    }

    private bool HasParticleReferences()
    {
        return HasParticleReference(loopDustParticles) || HasParticleReference(burstDustParticles);
    }

    private static bool HasParticleReference(ParticleSystem[] particles)
    {
        if (particles == null)
            return false;

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null)
                return true;
        }

        return false;
    }

    private Vector3 ResolveParticleSpawnGizmoPosition()
    {
        Transform anchor = ResolveParticleSpawnAnchor();
        if (anchor == null)
            return transform.position + particleSpawnLocalOffset;

        Transform root = ResolveRevealRoot();
        Matrix4x4 endRootMatrix = Matrix4x4.identity;
        TryResolveRevealRootMatrices(out _, out endRootMatrix, out _, out _);
        Matrix4x4 anchorMatrix = ResolveEndMatrixForTransform(anchor, root, endRootMatrix);
        return anchorMatrix.MultiplyPoint3x4(particleSpawnLocalOffset);
    }

    private bool TryResolveRevealMaskGizmoSource(
        out Transform sourceTransform,
        out Sprite sourceSprite,
        out string label)
    {
        if (revealMask != null)
        {
            sourceTransform = revealMask.transform;
            sourceSprite = revealMask.sprite;
            Transform root = ResolveRevealRoot();
            label = root != null && revealMask.transform.IsChildOf(root)
                ? "Runtime Mask Clone"
                : "Reveal Mask";
            return true;
        }

        SpriteRenderer sourceRenderer = ResolveFirstMaskedRenderer();
        if (sourceRenderer != null)
        {
            sourceTransform = sourceRenderer.transform;
            sourceSprite = sourceRenderer.sprite;
            label = "Runtime Mask";
            return true;
        }

        sourceTransform = null;
        sourceSprite = null;
        label = string.Empty;
        return false;
    }

    private SpriteRenderer ResolveFirstMaskedRenderer()
    {
        if (maskedRenderers == null)
            return null;

        for (int i = 0; i < maskedRenderers.Length; i++)
        {
            if (maskedRenderers[i] != null)
                return maskedRenderers[i];
        }

        return null;
    }

    private bool TryResolveRevealRootMatrices(
        out Matrix4x4 startMatrix,
        out Matrix4x4 endMatrix,
        out Vector3 startPosition,
        out Vector3 endPosition)
    {
        Transform root = ResolveRevealRoot();
        if (root == null)
        {
            startMatrix = Matrix4x4.identity;
            endMatrix = Matrix4x4.identity;
            startPosition = Vector3.zero;
            endPosition = Vector3.zero;
            return false;
        }

        Vector3 endLocalPosition = Application.isPlaying && hasRevealRootState
            ? revealRootFinalLocalPosition
            : root.localPosition;
        Vector3 startLocalPosition = endLocalPosition + startLocalOffset;
        startMatrix = ComposeWorldMatrix(root, startLocalPosition);
        endMatrix = ComposeWorldMatrix(root, endLocalPosition);
        startPosition = startMatrix.MultiplyPoint3x4(Vector3.zero);
        endPosition = endMatrix.MultiplyPoint3x4(Vector3.zero);
        return true;
    }

    private static Matrix4x4 ComposeWorldMatrix(Transform target, Vector3 localPosition)
    {
        Matrix4x4 localMatrix = Matrix4x4.TRS(localPosition, target.localRotation, target.localScale);
        return target.parent != null
            ? target.parent.localToWorldMatrix * localMatrix
            : localMatrix;
    }

    private static Matrix4x4 ResolveStartMatrixForTransform(
        Transform target,
        Transform root,
        Matrix4x4 startRootMatrix)
    {
        if (target == null)
            return Matrix4x4.identity;

        if (root != null && target.IsChildOf(root))
            return startRootMatrix * root.worldToLocalMatrix * target.localToWorldMatrix;

        return target.localToWorldMatrix;
    }

    private static Matrix4x4 ResolveEndMatrixForTransform(
        Transform target,
        Transform root,
        Matrix4x4 endRootMatrix)
    {
        if (target == null)
            return Matrix4x4.identity;

        if (root != null && target.IsChildOf(root))
            return endRootMatrix * root.worldToLocalMatrix * target.localToWorldMatrix;

        return target.localToWorldMatrix;
    }

    private static void DrawSpriteBoundsGizmo(Matrix4x4 matrix, Sprite sprite, Color color)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = matrix;
        Gizmos.color = color;

        if (sprite != null)
            Gizmos.DrawWireCube(sprite.bounds.center, sprite.bounds.size);
        else
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.5f);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static Vector3 ResolveSpriteCenterWorld(Matrix4x4 matrix, Sprite sprite)
    {
        return matrix.MultiplyPoint3x4(sprite != null ? sprite.bounds.center : Vector3.zero);
    }

    private static void DrawGizmoLabel(Vector3 position, string text, Color color)
    {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
        style.normal.textColor = color;
        UnityEditor.Handles.Label(position, text, style);
#endif
    }

    private readonly struct RendererMaskState
    {
        public RendererMaskState(SpriteRenderer renderer, SpriteMaskInteraction maskInteraction)
        {
            Renderer = renderer;
            MaskInteraction = maskInteraction;
        }

        public SpriteRenderer Renderer { get; }
        public SpriteMaskInteraction MaskInteraction { get; }
    }

    private readonly struct SpriteMaskRangeState
    {
        public SpriteMaskRangeState(SpriteMask mask)
        {
            Mask = mask;
            IsCustomRangeActive = mask.isCustomRangeActive;
            FrontSortingLayerID = mask.frontSortingLayerID;
            BackSortingLayerID = mask.backSortingLayerID;
            FrontSortingOrder = mask.frontSortingOrder;
            BackSortingOrder = mask.backSortingOrder;
        }

        public SpriteMask Mask { get; }
        public bool IsCustomRangeActive { get; }
        public int FrontSortingLayerID { get; }
        public int BackSortingLayerID { get; }
        public int FrontSortingOrder { get; }
        public int BackSortingOrder { get; }
    }

    private readonly struct ColliderState
    {
        public ColliderState(Collider2D collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider2D Collider { get; }
        public bool WasEnabled { get; }
    }

    private readonly struct ParticleTransformState
    {
        public ParticleTransformState(Transform transform)
        {
            Transform = transform;
            Parent = transform != null ? transform.parent : null;
            LocalPosition = transform != null ? transform.localPosition : Vector3.zero;
            LocalRotation = transform != null ? transform.localRotation : Quaternion.identity;
            LocalScale = transform != null ? transform.localScale : Vector3.one;
        }

        public Transform Transform { get; }
        public Transform Parent { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }
}
