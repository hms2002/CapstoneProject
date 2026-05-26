using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class FloweringRuntimeState :
    WeaponAbilityRuntimeState,
    IWeaponDashAugment,
    IAbilityGameplayEventListener,
    IWeaponAbilityHudDurationOverrideProvider
{
    private readonly List<GameObject> transientObjects = new();
    private readonly List<Coroutine> dashCoroutines = new();

    private AbilitySystem abilitySystem;
    private AttributeSet attributeSet;
    private AbilityGameplayEventRelay eventRelay;
    private FloweringRuntimeData runtimeData;
    private FloweringBloomData bloomData;
    private FloweringBloomPresentationController presentation;
    private PlayerStatusRuntime statusRuntime;
    private StatusHandle bloomStatusHandle;
    private GameFlowInputBlocker cutInInputBlocker;
    private bool modifiersApplied;
    private bool eventRegistered;
    private bool cutInPresentationPrepared;

    public bool IsBloomActive => runtimeData != null && runtimeData.IsBloomActive && bloomData != null;

    public static FloweringRuntimeState GetOrAdd(AbilitySystem system)
    {
        if (system == null)
            return null;

        FloweringRuntimeState state = system.GetComponent<FloweringRuntimeState>();
        if (state == null)
            state = system.gameObject.AddComponent<FloweringRuntimeState>();

        state.Bind(system);
        return state;
    }

    public static FloweringRuntimeState ResolveExisting(AbilitySystem system)
    {
        return system != null ? system.GetComponent<FloweringRuntimeState>() : null;
    }

    public void Bind(AbilitySystem system)
    {
        abilitySystem = system;
        attributeSet = system != null ? system.AttributeSet : null;
    }

    public IEnumerator PlayBloomCutIn(AbilitySystem system, AbilitySpec spec, FloweringBloomData data)
    {
        Bind(system);
        cutInPresentationPrepared = false;

        FloweringBloomPresentationController controller = EnsurePresentation(data);
        if (controller == null)
            yield break;

        Animator weaponAnimator = system != null ? system.WeaponAnimator : null;
        AnimatorUpdateMode previousWeaponAnimatorUpdateMode = default;
        bool overrideWeaponAnimatorUpdateMode = false;
        WeaponPresentationRig2D cutInAimPresentationRig = ResolveWeaponPresentationRig(system);
        int cutInAimPresentationToken = BeginCutInAimPresentationOverride(cutInAimPresentationRig, system, data);

        try
        {
            if (weaponAnimator != null && data != null && !string.IsNullOrWhiteSpace(data.CutInAnimationTrigger))
            {
                previousWeaponAnimatorUpdateMode = weaponAnimator.updateMode;
                weaponAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                overrideWeaponAnimatorUpdateMode = true;
            }

            TryPlayCutInAnimation(system, spec, data);
            yield return controller.PlayCutIn(system, spec, data);
        }
        finally
        {
            EndCutInAimPresentationOverride(cutInAimPresentationRig, cutInAimPresentationToken);

            if (overrideWeaponAnimatorUpdateMode && weaponAnimator != null)
                weaponAnimator.updateMode = previousWeaponAnimatorUpdateMode;
        }

        cutInPresentationPrepared = controller == presentation && (spec?.Token == null || !spec.Token.IsCancelled);
    }

    public IEnumerator PlayBloomEndTransition(AbilitySystem system, AbilitySpec spec, FloweringBloomData data)
    {
        Bind(system);
        FloweringBloomPresentationController controller = EnsurePresentation(data);
        if (controller == null)
            yield break;

        yield return controller.PlayBloomEndTransition(spec, data);
    }

    public void AcquireBloomCutInInputBlock()
    {
        cutInInputBlocker ??= GameFlowInputBlocker.GetOrAdd(this);
        cutInInputBlocker?.Acquire();
    }

    public void ReleaseBloomCutInInputBlock()
    {
        if (cutInInputBlocker == null)
            return;

        cutInInputBlocker.Release();
        cutInInputBlocker = null;
    }

    public void BeginBloom(AbilitySystem system, FloweringBloomData data, FloweringRuntimeData ownedRuntimeData)
    {
        bool preserveCutInPresentation = cutInPresentationPrepared && presentation != null;
        ResetBloomState(releasePresentation: !preserveCutInPresentation);
        cutInPresentationPrepared = false;

        Bind(system);
        bloomData = data;
        runtimeData = ownedRuntimeData;

        if (bloomData == null || runtimeData == null)
            return;

        runtimeData.BeginBloom(bloomData.DurationSeconds);
        ApplyModifiers();
        RegisterGameplayEvents();
        EnsurePresentation(bloomData)?.BeginActiveBloom(bloomData);
        RefreshBloomStatus();
    }

    public void EndBloom()
    {
        cutInPresentationPrepared = false;
        ResetBloomState(releasePresentation: true);
    }

    private void ResetBloomState(bool releasePresentation)
    {
        ReleaseBloomCutInInputBlock();
        StopDashCoroutines();
        DestroyTransientObjects();
        RemoveModifiers();
        UnregisterGameplayEvents();
        ReleaseBloomStatus();

        if (runtimeData != null)
            runtimeData.EndBloom();

        runtimeData = null;
        bloomData = null;

        if (releasePresentation && presentation != null)
            presentation.Release();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        EndBloom();
    }

    public override void HandleGameplayEvent(WeaponDefinition weapon, GameplayTag tag, in AbilityEventData data)
    {
        HandleGameplayEvent(tag, data);
    }

    public void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data)
    {
        if (!IsBloomActive || abilitySystem == null || bloomData == null || runtimeData == null)
            return;

        if (abilitySystem.KillConfirmedTag == null || tag != abilitySystem.KillConfirmedTag)
            return;

        if (data.AbilitySystem != null && data.AbilitySystem != abilitySystem)
            return;

        if (!HasKillExtensionRelic())
            return;

        runtimeData.ExtendBloom(bloomData.KillExtensionSeconds);
        RefreshBloomStatus();
    }

    public void ModifyDash(ref float duration, ref float distance)
    {
        if (!IsBloomActive)
            return;

        duration *= bloomData.DashDurationMultiplier;
        distance *= bloomData.DashDistanceMultiplier;
    }

    public void HandleDashStarted(
        AbilitySystem system,
        AbilitySpec spec,
        AbilityDefinition dashAbility,
        Vector2 direction,
        Vector2 startPosition,
        float duration,
        float distance)
    {
        if (!IsBloomActive || system == null)
            return;

        Coroutine coroutine = StartCoroutine(SpawnDashSlashes(system, spec, direction, startPosition, distance));
        dashCoroutines.Add(coroutine);
    }

    public void HandleDashFinished(
        AbilitySystem system,
        AbilitySpec spec,
        AbilityDefinition dashAbility,
        Vector2 direction,
        Vector2 startPosition,
        Vector2 endPosition,
        bool cancelled)
    {
    }

    private void Update()
    {
        if (!IsBloomActive || abilitySystem == null || bloomData == null)
            return;

        RefreshBloomStatus();

        if (bloomData.DashAbility != null)
            abilitySystem.TrySetCooldownRemaining(bloomData.DashAbility, 0f);
    }

    public bool TryGetHudDurationOverride(
        WeaponAbilitySlot slot,
        AbilityDefinition ability,
        out WeaponAbilityHudDurationOverride duration)
    {
        duration = default;

        if (!IsBloomActive || slot != WeaponAbilitySlot.Skill1 || runtimeData == null)
            return false;

        duration = new WeaponAbilityHudDurationOverride(
            runtimeData.RemainingSeconds,
            Mathf.Max(runtimeData.DisplayMaxSeconds, bloomData.DurationSeconds),
            fillBottomToTop: true,
            showText: true);
        return true;
    }

    private void OnDisable()
    {
        EndBloom();
    }

    private FloweringBloomPresentationController EnsurePresentation(FloweringBloomData data)
    {
        if (abilitySystem == null)
            return null;

        if (presentation == null)
            presentation = abilitySystem.GetComponent<FloweringBloomPresentationController>();

        if (presentation == null)
            presentation = abilitySystem.gameObject.AddComponent<FloweringBloomPresentationController>();

        presentation.Initialize(abilitySystem.gameObject, data);
        return presentation;
    }

    private void ApplyModifiers()
    {
        if (attributeSet == null || bloomData == null)
            return;

        RemoveModifiers();

        TryAddModifier(bloomData.NormalDamageAddAttribute, ModifierType.Flat, bloomData.NormalDamageAdd);
        TryAddModifier(bloomData.AttackSpeedBaseAttribute, ModifierType.Flat, bloomData.AttackSpeedBaseAdd);
        TryAddModifier(bloomData.MoveSpeedMulAttribute, ModifierType.Percent, bloomData.MoveSpeedPercent);
        modifiersApplied = true;
    }

    private bool HasKillExtensionRelic()
    {
        if (bloomData == null || bloomData.KillExtensionRequiredTag == null)
            return false;

        TagSystem tagSystem = abilitySystem != null ? abilitySystem.GetComponent<TagSystem>() : null;
        return tagSystem != null && tagSystem.HasTag(bloomData.KillExtensionRequiredTag);
    }

    private void RefreshBloomStatus()
    {
        if (!IsBloomActive || abilitySystem == null || bloomData == null || runtimeData == null)
            return;

        StatusHudDefinition definition = bloomData.BloomStatusDefinition;
        if (definition == null)
            return;

        if (statusRuntime == null)
            statusRuntime = PlayerStatusRuntime.GetOrAdd(abilitySystem.gameObject);

        if (statusRuntime == null)
            return;

        StatusApplyRequest request = new(
            definition,
            "weapon.flowering.bloom",
            remainingTime: runtimeData.RemainingSeconds,
            maxTime: Mathf.Max(runtimeData.DisplayMaxSeconds, bloomData.DurationSeconds),
            isHighlighted: true,
            iconOverride: bloomData.BloomStatusIcon,
            showStacksOverride: false,
            showDurationOverride: true);

        if (bloomStatusHandle.IsValid)
            statusRuntime.UpdateStatus(bloomStatusHandle, request);
        else
            bloomStatusHandle = statusRuntime.Apply(request);
    }

    private void ReleaseBloomStatus()
    {
        if (bloomStatusHandle.IsValid)
            bloomStatusHandle.Release();

        bloomStatusHandle = default;
        statusRuntime = null;
    }

    private void TryAddModifier(AttributeDefinition attribute, ModifierType type, float value)
    {
        if (attribute == null || Mathf.Abs(value) <= 0.0001f)
            return;

        attributeSet.TryAddModifier(attribute, new AttributeModifier(type, value, this));
    }

    private void RemoveModifiers()
    {
        if (!modifiersApplied || attributeSet == null)
            return;

        attributeSet.RemoveModifiersFromSource(this);
        modifiersApplied = false;
    }

    private void RegisterGameplayEvents()
    {
        if (abilitySystem == null || eventRegistered)
            return;

        eventRelay = abilitySystem.GetComponent<AbilityGameplayEventRelay>();
        if (eventRelay == null)
            eventRelay = abilitySystem.gameObject.AddComponent<AbilityGameplayEventRelay>();

        eventRelay.Register(this);
        eventRegistered = true;
    }

    private void UnregisterGameplayEvents()
    {
        if (!eventRegistered)
            return;

        if (eventRelay != null)
            eventRelay.Unregister(this);

        eventRegistered = false;
        eventRelay = null;
    }

    private IEnumerator SpawnDashSlashes(
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 direction,
        Vector2 startPosition,
        float distance)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        int count = bloomData.DashSlashCount;

        for (int i = 0; i < count; i++)
        {
            float delay = i == 0 ? bloomData.DashSlashInitialDelaySeconds : bloomData.DashSlashIntervalSeconds;
            yield return WaitScaled(delay);

            if (!IsBloomActive)
                yield break;

            float t = (i + 1f) / (count + 1f);
            Vector2 center = startPosition + safeDirection * (distance * t);
            float baseAngle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
            float angle = baseAngle + Random.Range(-bloomData.DashSlashAngleJitter, bloomData.DashSlashAngleJitter);

            SpawnDashSlashHitbox(system, spec, center, angle, startPosition);
            SpawnSlashEffect(center, angle);
            SpawnDashSlashParticle(center, angle);
            AbilityAudioRouter.PlayOneShotAtPosition(
                bloomData.DashSlashSound,
                system,
                spec,
                center,
                bloomData);
        }
    }

    private static IEnumerator WaitScaled(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void SpawnDashSlashHitbox(
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 center,
        float angle,
        Vector2 lineOfSightSource)
    {
        if (system == null || bloomData == null)
            return;

        FloweringBloomData data = bloomData;
        GameObject ownerObject = system.gameObject;
        if (ownerObject == null)
            return;

        CombatHitPayload payload = BuildDashSlashPayload(system, spec);
        if (payload == null)
            return;

        GameObject go = new("FloweringDashSlashHitbox");
        transientObjects.Add(go);

        BoxCollider2D hitboxCollider = go.AddComponent<BoxCollider2D>();
        if (hitboxCollider == null)
        {
            transientObjects.Remove(go);
            Destroy(go);
            return;
        }

        FloweringDashSlashHitboxActor actor = go.AddComponent<FloweringDashSlashHitboxActor>();
        if (actor == null)
        {
            transientObjects.Remove(go);
            Destroy(go);
            Debug.LogError("[FloweringRuntimeState] Failed to add FloweringDashSlashHitboxActor.", this);
            return;
        }

        actor.Setup(new FloweringDashSlashHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = ownerObject,
            ignoreTarget = ownerObject,
            lifetime = data.DashSlashActiveTime,
            wallLayers = data.DashSlashWallLayers,
            damageLayers = data.DashSlashHitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = data.DashSlashHitboxSize,
            rotationDegrees = angle,
            lineOfSightSource = lineOfSightSource,
            hitOncePerTarget = true
        });
    }

    private CombatHitPayload BuildDashSlashPayload(AbilitySystem system, AbilitySpec spec)
    {
        if (system == null || system.AttributeSet == null || bloomData == null || bloomData.DashSlashDamageEffect == null)
            return null;

        IStatProvider statProvider = AbilityStatProviderFactory.Create(system);
        float scale = bloomData.DashSlashDamageScale;

        float baseHp = bloomData.DashSlashDamageFormula != null
            ? bloomData.DashSlashDamageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: bloomData.DashSlashLegacyDamage)
            : bloomData.DashSlashLegacyDamage;
        baseHp *= scale;

        float baseKnockback = bloomData.DashSlashKnockbackFormula != null
            ? bloomData.DashSlashKnockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
            : 0f;
        baseKnockback *= scale;

        UnityGAS.DamagePayloadConfig config = bloomData.DashSlashDamageConfig;
        float baseStagger = config != null && config.includeStaggerBuildUp && config.staggerFormula != null
            ? config.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
            : bloomData.DashSlashLegacyStaggerDamage;
        baseStagger *= scale;

        CombatDamageSnapshot snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
            statProvider,
            config,
            baseHp,
            config != null && config.includeStaggerBuildUp ? baseStagger : 0f,
            baseKnockback,
            system.gameObject);

        if (snapshot.FinalHpDamage <= 0f)
            return null;

        return CombatHitPayload.FromSnapshot(
            system,
            spec,
            bloomData.DashSlashDamageEffect,
            bloomData.DashSlashKnockbackEffect,
            snapshot,
            bloomData.DashSlashHitConfirmedTag,
            system.gameObject,
            bloomData.DashSlashHitImpactCueKind);
    }

    private void SpawnSlashEffect(Vector2 center, float angle)
    {
        if (bloomData == null || bloomData.SlashEffectPrefab == null)
            return;

        GameObject effect = Instantiate(bloomData.SlashEffectPrefab, center, Quaternion.Euler(0f, 0f, angle));
        if (effect == null)
            return;

        effect.transform.localScale *= bloomData.SlashEffectScale;
        ApplySlashEffectSprites(effect, bloomData.BloomColor, bloomData.SlashEffectSortingOrderOffset);
        transientObjects.Add(effect);
        Destroy(effect, bloomData.SlashEffectLifetime);
    }

    private void SpawnDashSlashParticle(Vector2 center, float angle)
    {
        if (bloomData == null || bloomData.DashSlashParticlePrefab == null)
            return;

        GameObject particle = Instantiate(bloomData.DashSlashParticlePrefab, center, Quaternion.Euler(0f, 0f, angle));
        if (particle == null)
            return;

        ApplyRendererSortingOrderOffset(particle, bloomData.ParticleSortingOrderOffset);
        transientObjects.Add(particle);
        Destroy(particle, bloomData.ParticleLifetimeFallback);
    }

    private static void ApplySlashEffectSprites(GameObject root, Color color, int sortingOrderOffset)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].color = color;
            renderers[i].sortingOrder += sortingOrderOffset;
        }
    }

    private static void ApplyRendererSortingOrderOffset(GameObject root, int sortingOrderOffset)
    {
        if (root == null || sortingOrderOffset == 0)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sortingOrder += sortingOrderOffset;
    }

    private void StopDashCoroutines()
    {
        for (int i = 0; i < dashCoroutines.Count; i++)
        {
            Coroutine coroutine = dashCoroutines[i];
            if (coroutine != null)
                StopCoroutine(coroutine);
        }

        dashCoroutines.Clear();
    }

    private void DestroyTransientObjects()
    {
        for (int i = 0; i < transientObjects.Count; i++)
        {
            GameObject go = transientObjects[i];
            if (go != null)
                Destroy(go);
        }

        transientObjects.Clear();
    }

    private static void TryPlayCutInAnimation(AbilitySystem system, AbilitySpec spec, FloweringBloomData data)
    {
        if (system == null || spec?.Definition == null || data == null || string.IsNullOrWhiteSpace(data.CutInAnimationTrigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(data.CutInAnimationTrigger), spec.Definition);
    }

    private static int BeginCutInAimPresentationOverride(
        WeaponPresentationRig2D presentationRig,
        AbilitySystem system,
        FloweringBloomData data)
    {
        WeaponAimPresentationSettings settings = data != null ? data.CutInAimPresentation : null;
        if (presentationRig == null || settings == null || settings.Mode == WeaponAimPresentationMode.FollowAim)
            return 0;

        return presentationRig.BeginAimPresentationOverride(
            settings.Mode,
            ResolveAimDirection(system),
            settings.MinimumHoldTime);
    }

    private static void EndCutInAimPresentationOverride(WeaponPresentationRig2D presentationRig, int token)
    {
        if (presentationRig == null || token == 0)
            return;

        presentationRig.EndAimPresentationOverride(token);
    }

    private static WeaponPresentationRig2D ResolveWeaponPresentationRig(AbilitySystem system)
    {
        return system != null ? system.GetComponentInChildren<WeaponPresentationRig2D>(true) : null;
    }

    private static Vector2 ResolveAimDirection(AbilitySystem system)
    {
        if (system == null)
            return Vector2.right;

        Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }
}
