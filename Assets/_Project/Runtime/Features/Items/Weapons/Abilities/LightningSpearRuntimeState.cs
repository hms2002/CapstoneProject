using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityGAS;

/// <summary>
/// 장착 중인 번개 창의 live 표식 목록, Q/E 실행, 표식 선택 피드백, cleanup을 관리할 책임을 가집니다.
/// </summary>
public sealed class LightningSpearRuntimeState : WeaponAbilityRuntimeState, IWeaponAbilityHudIconOverrideProvider
{
    private readonly struct RecoveredSpearShotRequest
    {
        public readonly Vector2 direction;

        public RecoveredSpearShotRequest(Vector2 direction)
        {
            this.direction = direction;
        }
    }

    // 책임: 회수된 번개창 일제사격의 기준 방향과 발사 요청 목록을 보관한다.
    private sealed class RecoveredSpearVolleyContext
    {
        public readonly Vector2 baseDirection;
        public readonly List<RecoveredSpearShotRequest> shots;

        public RecoveredSpearVolleyContext(Vector2 baseDirection, List<RecoveredSpearShotRequest> shots)
        {
            this.baseDirection = baseDirection;
            this.shots = shots;
        }

        public bool HasShots => shots != null && shots.Count > 0;
    }

    // 책임: 번개창 표식 생성 위치와 해당 방 정보를 함께 전달한다.
    private readonly struct MarkSpawnRequest
    {
        public readonly Vector2 position;
        public readonly MonsterRoomArea2D room;

        public MarkSpawnRequest(Vector2 position, MonsterRoomArea2D room)
        {
            this.position = position;
            this.room = room;
        }
    }

    private readonly List<LightningSpearMarkActor> activeMarks = new List<LightningSpearMarkActor>();
    private readonly List<MarkSpawnRequest> pendingMarkSpawns = new List<MarkSpawnRequest>();
    private readonly List<Tilemap> groundTilemapCache = new List<Tilemap>();
    private readonly List<LightningSpearRecoveredSpearActor> recoveredSpears = new List<LightningSpearRecoveredSpearActor>();
    private readonly List<LightningSpearRecoveredSpearActor> transientRecoveredSpears = new List<LightningSpearRecoveredSpearActor>();
    private readonly List<LightningSpearRecoveredSpearProjectile2D> recoveredProjectiles = new List<LightningSpearRecoveredSpearProjectile2D>();
    private readonly List<LightningSpearRecoverShotTrailEffect> recoveredShotTrails = new List<LightningSpearRecoverShotTrailEffect>();
    private readonly List<GameObject> recoveredShotSpawnEffects = new List<GameObject>();
    private readonly List<Coroutine> recoveredSpearFireRoutines = new List<Coroutine>();
    private readonly Dictionary<LightningSpearMarkActor, GameObject> markHoverRangeIndicators =
        new Dictionary<LightningSpearMarkActor, GameObject>();
    private readonly List<LightningSpearMarkActor> visibleMarkHoverRangeMarks =
        new List<LightningSpearMarkActor>();
    private readonly List<LightningSpearMarkActor> staleMarkHoverRangeMarks =
        new List<LightningSpearMarkActor>();

    private AbilitySystem ownerSystem;
    private WeaponInventory2D weaponInventory;
    private PlayerAim2D aimSource;
    private MovementMotor2D movementMotor;
    private WeaponPresentationRig2D presentationRig;
    private GameObject rushRangeIndicatorInstance;
    private GameObject selectedMarkIndicatorInstance;
    private bool cursorInteractableSet;
    private bool skill1MarkRushHudOverrideActive;
    private bool hasBufferedMarkRushInput;
    private bool isExecutingMarkRush;
    private bool hasCurrentMarkRushDestination;
    private Vector2 currentMarkRushDestination;
    private float currentMarkRushDestinationExpiresAt = -1f;
    private bool hasBufferedMarkRushOrigin;
    private Vector2 bufferedMarkRushOrigin;
    private LightningSpearMarkActor bufferedMarkRushTarget;
    private LightningSpearMarkActor forcedBufferedMarkRushTarget;
    private bool hasForcedBufferedMarkRushOrigin;
    private Vector2 forcedBufferedMarkRushOrigin;
    private float markRushInputBufferExpiresAt = -1f;
    private int recoveredSpearLayoutSideSign = 1;
    private int groundTilemapCacheFrame = -1;
    private int cachedGroundLayer = int.MinValue;

    private void Awake()
    {
        CacheOwnerReferences(null);
    }

    private void Update()
    {
        if (!TryResolveActiveLoadout(out LightningSpearLoadout loadout))
        {
            forcedBufferedMarkRushTarget = null;
            ClearForcedMarkRushOrigin();
            ClearMarkRushInputBuffer();
            ClearFeedback();
            return;
        }

        LightningSpearSkill1Data skill1Data = ResolveSkill1Data(loadout);
        ClearExpiredMarkRushDestination();
        TryConsumeBufferedMarkRush(loadout, skill1Data);
        RefreshMarkFeedback(loadout, skill1Data);
        RefreshRecoveredSpearLayout(skill1Data);
    }

    private void OnDisable()
    {
        isExecutingMarkRush = false;
        ClearMarkRushDestinationOrigin();
        forcedBufferedMarkRushTarget = null;
        ClearForcedMarkRushOrigin();
        ClearMarkRushInputBuffer();
        ClearAllMarks();
        ClearRecoveredSpearState();
        ClearFeedback();
        DestroyFeedbackObjects();
    }

    private void OnDestroy()
    {
        isExecutingMarkRush = false;
        ClearMarkRushDestinationOrigin();
        forcedBufferedMarkRushTarget = null;
        ClearForcedMarkRushOrigin();
        ClearMarkRushInputBuffer();
        ClearAllMarks();
        ClearRecoveredSpearState();
        ClearFeedback();
        DestroyFeedbackObjects();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        CacheOwnerReferences(null);

        if (previousWeapon != newWeapon && previousWeapon?.abilityLoadout is LightningSpearLoadout)
        {
            isExecutingMarkRush = false;
            ClearMarkRushDestinationOrigin();
            forcedBufferedMarkRushTarget = null;
            ClearForcedMarkRushOrigin();
            ClearMarkRushInputBuffer();
            ClearAllMarks();
            ClearRecoveredSpearState();
        }
    }

    public override bool TryHandleAbilityInput(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition ability)
    {
        if (slot != WeaponAbilitySlot.Skill1 || ability == null)
        {
            return false;
        }

        if (IsGameplayInputBlockedByUiOrFlow())
        {
            ClearMarkRushInputBuffer();
            return false;
        }

        CacheOwnerReferences(null);
        if (!TryResolveActiveLoadout(out LightningSpearLoadout loadout) ||
            loadout.MarkRushOrSweep != ability)
        {
            return false;
        }

        bool hasMarkRushOrigin = HasMarkRushDestinationOrigin();
        bool isBasicAttackBusy = IsExecutingBasicAttack(loadout);
        if (!hasMarkRushOrigin && !isBasicAttackBusy)
            return false;

        bool buffered = TryBufferMarkRushInput(slot, ability);
        if (buffered)
        {
            if (isBasicAttackBusy)
                CancelBasicAttack(loadout);

            TryConsumeBufferedMarkRush(loadout, ResolveSkill1Data(loadout));
            return true;
        }

        return false;
    }

    public override void HandleAbilityActivationRejected(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition rejectedAbility)
    {
        TryBufferMarkRushInput(slot, rejectedAbility);
    }

    private bool TryBufferMarkRushInput(WeaponAbilitySlot slot, AbilityDefinition ability)
    {
        if (slot != WeaponAbilitySlot.Skill1 || ability == null)
            return false;

        if (IsGameplayInputBlockedByUiOrFlow())
        {
            ClearMarkRushInputBuffer();
            return false;
        }

        CacheOwnerReferences(null);

        if (!TryResolveActiveLoadout(out LightningSpearLoadout loadout) ||
            loadout.MarkRushOrSweep != ability ||
            ownerSystem == null)
        {
            return false;
        }

        LightningSpearSkill1Data data = ResolveSkill1Data(loadout);
        Vector2 cursorWorld = ResolveCursorWorld(ownerSystem);
        Vector2 origin = ResolveMarkRushInputOrigin();
        LightningSpearMarkActor target = FindSelectableMark(loadout, data, origin, cursorWorld);
        if (target == null)
            return false;

        float bufferSeconds = GetMarkRushInputBufferSeconds(data);
        if (bufferSeconds <= 0f)
            return false;

        hasBufferedMarkRushInput = true;
        hasBufferedMarkRushOrigin = true;
        bufferedMarkRushOrigin = origin;
        bufferedMarkRushTarget = target;
        markRushInputBufferExpiresAt = CalculateMarkRushInputBufferExpiresAt(bufferSeconds);
        return true;
    }

    private bool IsExecutingBasicAttack(LightningSpearLoadout loadout)
    {
        if (ownerSystem == null || loadout == null || loadout.BaseAttack == null)
            return false;

        if (ownerSystem.IsCasting &&
            ownerSystem.CurrentCastSpec != null &&
            ownerSystem.CurrentCastSpec.Definition == loadout.BaseAttack)
        {
            return true;
        }

        return ownerSystem.IsExecuting &&
               ownerSystem.CurrentExecSpec != null &&
               ownerSystem.CurrentExecSpec.Definition == loadout.BaseAttack;
    }

    private void CancelBasicAttack(LightningSpearLoadout loadout)
    {
        if (ownerSystem == null || loadout == null || loadout.BaseAttack == null)
            return;

        if (ownerSystem.IsCasting &&
            ownerSystem.CurrentCastSpec != null &&
            ownerSystem.CurrentCastSpec.Definition == loadout.BaseAttack)
        {
            ownerSystem.CancelCasting(force: true);
        }

        if (ownerSystem.IsExecuting &&
            ownerSystem.CurrentExecSpec != null &&
            ownerSystem.CurrentExecSpec.Definition == loadout.BaseAttack)
        {
            ownerSystem.CancelExecution(force: true);
        }
    }

    public IEnumerator ExecuteSkill1(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        CacheOwnerReferences(system);

        if (system == null || spec == null || loadout == null)
            yield break;

        Vector2 ownerPosition = system.transform.position;
        Vector2 cursorWorld = ResolveCursorWorld(system);
        LightningSpearMarkActor selectedMark =
            ConsumeForcedBufferedMarkRushTarget(loadout, data, ownerPosition);
        if (selectedMark == null)
            selectedMark = FindSelectableMark(loadout, data, ownerPosition, cursorWorld);

        if (selectedMark == null)
        {
            Vector2 direction = ResolveAimDirection(system);
            int facingSideSign = ResolveFacingSideSign(system, direction);
            int aimOverrideToken = BeginAimPresentationOverride(GetNoMarkSweepAimPresentation(data), direction);
            try
            {
                TryPlayAnimationTrigger(system, spec.Definition, GetNoMarkSweepAnimationTrigger(data));
                RecoveredSpearVolleyContext recoveredVolley =
                    BeginRecoveredSpearVolleyDespawn(data, system, spec, direction);
                yield return WaitForAnimationEventOrDelay(
                    system,
                    spec,
                    GetNoMarkSweepHitEventTag(data),
                    GetNoMarkSweepHitEventTimeout(data),
                    GetNoMarkSweepFallbackHitDelay(data));
                Vector2 hitOrigin = system.transform.position;
                LightningSpearHitConfig noMarkSweepHit = GetNoMarkSweepHit(loadout, data);
                Vector2 hitCenter = ResolveHitboxCenter(noMarkSweepHit, system, hitOrigin, direction);
                if (SpawnHitbox(noMarkSweepHit, system, spec, hitOrigin, direction, facingSideSign))
                    PlaySoundAt(data != null ? data.NoMarkSweepHitSound : default, system, spec, hitCenter, data);
                StartRecoveredSpearShotSequence(data, system, spec, recoveredVolley);
            }
            finally
            {
                EndAimPresentationOverride(aimOverrideToken);
            }

            yield break;
        }

        Vector2 rushDirection = (Vector2)selectedMark.transform.position - ownerPosition;
        if (rushDirection.sqrMagnitude <= 0.0001f)
            rushDirection = ResolveAimDirection(system);

        int markRushAimOverrideToken = BeginAimPresentationOverride(GetMarkRushAimPresentation(data), rushDirection);
        try
        {
            isExecutingMarkRush = true;
            SetMarkRushDestinationOrigin(selectedMark.transform.position, loadout, data);
            TryPlayAnimationTrigger(system, spec.Definition, GetMarkRushAnimationTrigger(data));
            yield return ExecuteMarkRush(system, spec, loadout, data, selectedMark);
        }
        finally
        {
            isExecutingMarkRush = false;
            EndAimPresentationOverride(markRushAimOverrideToken);
        }
    }

    public IEnumerator ExecuteSkill2(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data)
    {
        CacheOwnerReferences(system);

        if (system == null || spec == null || loadout == null || GetMarkPrefab(loadout, data) == null)
            yield break;

        int aimOverrideToken = BeginAimPresentationOverride(GetMarkRainAimPresentation(data), ResolveAimDirection(system));
        try
        {
            TryPlayAnimationTrigger(system, spec.Definition, GetMarkRainAnimationTrigger(data));
            yield return WaitForAnimationEventOrDelay(
                system,
                spec,
                GetMarkRainSpawnEventTag(data),
                GetMarkRainSpawnEventTimeout(data),
                GetMarkRainFallbackSpawnDelay(data));

            Vector2 ownerPosition = system.transform.position;
            List<MarkSpawnRequest> markSpawns = GenerateMarkPositions(loadout, data, ownerPosition);
            if (markSpawns.Count > 0)
                PlaySoundAt(data != null ? data.MarkRainSpawnStartSound : default, system, spec, ownerPosition, data);

            for (int i = 0; i < markSpawns.Count; i++)
            {
                MarkSpawnRequest request = markSpawns[i];
                SpawnMark(loadout, data, request.position, request.room, system, spec);
            }
        }
        finally
        {
            EndAimPresentationOverride(aimOverrideToken);
        }
    }

    public void RegisterMark(LightningSpearMarkActor mark)
    {
        if (mark == null || activeMarks.Contains(mark))
            return;

        activeMarks.Add(mark);
    }

    public void UnregisterMark(LightningSpearMarkActor mark)
    {
        if (mark == null)
            return;

        activeMarks.Remove(mark);
        DestroyMarkHoverRangeIndicator(mark);
    }

    private void TryConsumeBufferedMarkRush(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        if (!hasBufferedMarkRushInput)
            return;

        if (IsGameplayInputBlockedByUiOrFlow())
        {
            ClearMarkRushInputBuffer();
            return;
        }

        if (Time.time > markRushInputBufferExpiresAt)
        {
            ClearMarkRushInputBuffer();
            return;
        }

        if (ownerSystem == null || ownerSystem.IsBusy || loadout == null || loadout.MarkRushOrSweep == null)
            return;

        if (!IsSkill1Ready(loadout))
            return;

        Vector2 ownerPosition = ownerSystem.transform.position;
        if (bufferedMarkRushTarget == null || !bufferedMarkRushTarget.IsActive)
        {
            ClearMarkRushInputBuffer();
            return;
        }

        Vector2 origin = hasBufferedMarkRushOrigin ? bufferedMarkRushOrigin : ownerPosition;
        if (!CanRushToMark(loadout, data, bufferedMarkRushTarget, origin))
        {
            ClearMarkRushInputBuffer();
            return;
        }

        forcedBufferedMarkRushTarget = bufferedMarkRushTarget;
        hasForcedBufferedMarkRushOrigin = true;
        forcedBufferedMarkRushOrigin = origin;
        if (ownerSystem.TryActivateAbility(loadout.MarkRushOrSweep))
        {
            ClearMarkRushInputBuffer();
        }
        else
        {
            forcedBufferedMarkRushTarget = null;
            ClearForcedMarkRushOrigin();
        }
    }

    private void ClearMarkRushInputBuffer()
    {
        hasBufferedMarkRushInput = false;
        hasBufferedMarkRushOrigin = false;
        bufferedMarkRushOrigin = Vector2.zero;
        bufferedMarkRushTarget = null;
        markRushInputBufferExpiresAt = -1f;
    }

    private float CalculateMarkRushInputBufferExpiresAt(float bufferSeconds)
    {
        float expiresAt = Time.time + bufferSeconds;
        if (HasMarkRushDestinationOrigin())
            expiresAt = Mathf.Max(expiresAt, currentMarkRushDestinationExpiresAt);

        return expiresAt;
    }

    private Vector2 ResolveMarkRushInputOrigin()
    {
        if (HasMarkRushDestinationOrigin())
            return currentMarkRushDestination;

        return ownerSystem != null
            ? (Vector2)ownerSystem.transform.position
            : Vector2.zero;
    }

    private void SetMarkRushDestinationOrigin(
        Vector2 destination,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        hasCurrentMarkRushDestination = true;
        currentMarkRushDestination = destination;
        currentMarkRushDestinationExpiresAt =
            Time.time +
            GetMarkRushInputBufferSeconds(data) +
            GetMarkRushInternalDelay(loadout, data) +
            0.25f;
    }

    private bool HasMarkRushDestinationOrigin()
    {
        return hasCurrentMarkRushDestination &&
               (isExecutingMarkRush || Time.time <= currentMarkRushDestinationExpiresAt);
    }

    private void ClearExpiredMarkRushDestination()
    {
        if (!hasCurrentMarkRushDestination || isExecutingMarkRush)
            return;

        if (Time.time <= currentMarkRushDestinationExpiresAt)
            return;

        ClearMarkRushDestinationOrigin();
    }

    private void ClearMarkRushDestinationOrigin()
    {
        hasCurrentMarkRushDestination = false;
        currentMarkRushDestination = Vector2.zero;
        currentMarkRushDestinationExpiresAt = -1f;
    }

    private LightningSpearMarkActor ConsumeForcedBufferedMarkRushTarget(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        Vector2 ownerPosition)
    {
        LightningSpearMarkActor target = forcedBufferedMarkRushTarget;
        forcedBufferedMarkRushTarget = null;
        Vector2 validationOrigin = hasForcedBufferedMarkRushOrigin
            ? forcedBufferedMarkRushOrigin
            : ownerPosition;
        ClearForcedMarkRushOrigin();

        if (target == null || !target.IsActive)
            return null;

        return CanRushToMark(loadout, data, target, validationOrigin)
            ? target
            : null;
    }

    private void ClearForcedMarkRushOrigin()
    {
        hasForcedBufferedMarkRushOrigin = false;
        forcedBufferedMarkRushOrigin = Vector2.zero;
    }

    public void HandleMarkActivated(
        LightningSpearMarkActor mark,
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout)
    {
        if (mark == null || system == null || spec == null || loadout == null)
            return;

        LightningSpearSkill2Data data = ResolveSkill2Data(loadout);
        Vector2 hitOrigin = mark.transform.position;
        LightningSpearHitConfig landingHit = GetLandingHit(loadout, data);
        if (SpawnHitbox(landingHit, system, spec, hitOrigin, Vector2.right, 1))
        {
            PlaySoundAt(
                data != null ? data.MarkRainLandingHitSound : default,
                system,
                spec,
                ResolveHitboxCenter(landingHit, system, hitOrigin, Vector2.right),
                data);
        }
    }

    public bool TryGetHudIconOverride(WeaponAbilitySlot slot, AbilityDefinition ability, out Sprite icon)
    {
        icon = null;

        if (slot != WeaponAbilitySlot.Skill1 || ability == null || !skill1MarkRushHudOverrideActive)
            return false;

        if (!TryResolveActiveLoadout(out LightningSpearLoadout loadout) ||
            loadout.MarkRushOrSweep != ability)
        {
            return false;
        }

        LightningSpearSkill1Data data = ResolveSkill1Data(loadout);
        icon = data != null ? data.MarkRushHudIcon : null;
        return icon != null;
    }

    private IEnumerator ExecuteMarkRush(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        LightningSpearMarkActor mark)
    {
        Vector2 start = system.transform.position;
        Vector2 destination = mark.transform.position;
        Vector2 delta = destination - start;
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.0001f ? delta / distance : ResolveAimDirection(system);
        int facingSideSign = ResolveFacingSideSign(system, direction);

        try
        {
            mark.Consume();
            PlaySoundAt(data != null ? data.MarkRushStartSound : default, system, spec, start, data);
            LightningSpearHitConfig markRushHit = GetMarkRushHit(loadout, data);
            bool markRushEffectHandlesHitboxes = SpawnMarkRushEffect(
                loadout,
                data,
                system,
                spec,
                markRushHit,
                start,
                destination,
                direction,
                facingSideSign);
            MoveOwnerToMark(system, destination);
            AddRecoveredSpear(data, system.transform, system, spec);

            float hitDelay = GetMarkRushArrivalHitDelay(loadout, data);
            if (hitDelay > 0f)
                yield return new WaitForSeconds(hitDelay);

            if (!markRushEffectHandlesHitboxes)
                SpawnHitbox(markRushHit, system, spec, destination, direction, facingSideSign);
            PlaySoundAt(data != null ? data.MarkRushArrivalSound : default, system, spec, destination, data);

            RefreshMarkFeedback(loadout, data);
        }
        finally
        {
            QueueSkill1CooldownReset(system, spec, loadout, data);
        }
    }

    private void QueueSkill1CooldownReset(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        ResetSkill1Cooldown(system, spec, loadout, data);

        if (isActiveAndEnabled && gameObject.activeInHierarchy)
            StartCoroutine(ResetSkill1CooldownNextFrame(system, spec, loadout, data));
    }

    private IEnumerator ResetSkill1CooldownNextFrame(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        yield return null;

        ResetSkill1Cooldown(system, spec, loadout, data);
    }

    private static void ResetSkill1Cooldown(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (system == null || loadout == null)
            return;

        AbilityDefinition ability = loadout.MarkRushOrSweep != null
            ? loadout.MarkRushOrSweep
            : spec?.Definition;

        system.TrySetCooldownRemaining(ability, 0f);
        system.SetNextActivationDelay(spec, GetMarkRushInternalDelay(loadout, data));
    }

    private void MoveOwnerToMark(AbilitySystem system, Vector2 destination)
    {
        if (movementMotor != null)
        {
            movementMotor.WarpTo(destination);
            return;
        }

        if (system == null)
            return;

        system.transform.position = new Vector3(destination.x, destination.y, system.transform.position.z);
    }

    private bool SpawnMarkRushEffect(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearHitConfig hitConfig,
        Vector2 start,
        Vector2 destination,
        Vector2 direction,
        int facingSideSign)
    {
        LightningSpearDashStabTrailEffect prefab = GetMarkRushTrailEffectPrefab(loadout, data);
        if (prefab == null)
            return false;

        LightningSpearDashStabTrailEffect effect =
            UnityEngine.Object.Instantiate(prefab);
        if (effect == null)
            return false;

        return effect.PlayMarkRush(
            start,
            destination,
            hitConfig,
            system,
            spec,
            direction,
            facingSideSign,
            GetMarkRushArrivalHitDelay(loadout, data));
    }

    private void AddRecoveredSpear(
        LightningSpearSkill1Data data,
        Transform ownerTransform,
        AbilitySystem system,
        AbilitySpec spec)
    {
        if (data == null || ownerTransform == null || data.RecoveredSpearPrefab == null)
            return;

        int maxCount = data.RecoveredSpearMaxCount;
        if (maxCount <= 0)
            return;

        PruneRecoveredSpears();

        while (recoveredSpears.Count >= maxCount)
            RemoveRecoveredSpearAt(0, data);

        int newIndex = recoveredSpears.Count;
        int newCount = recoveredSpears.Count + 1;
        int sideSign = ResolveFacingSideSign(ownerSystem, ResolveAimDirection(ownerSystem));
        recoveredSpearLayoutSideSign = sideSign;
        Vector2 spawnOffset = CalculateRecoveredSpearOffset(data, newIndex, newCount, sideSign);
        Vector2 spawnPosition = (Vector2)ownerTransform.position + spawnOffset;

        LightningSpearRecoveredSpearActor actor = Instantiate(
            data.RecoveredSpearPrefab,
            new Vector3(spawnPosition.x, spawnPosition.y, ownerTransform.position.z),
            Quaternion.identity);
        if (actor == null)
            return;

        actor.Initialize(
            ownerTransform,
            spawnOffset,
            CalculateRecoveredSpearStockAngle(data, newIndex, newCount, sideSign),
            data.RecoveredSpearStockVisualForwardOffset,
            data.RecoveredSpearSpawnFallbackSeconds,
            data.RecoveredSpearDespawnFallbackSeconds,
            data.RecoveredSpearMoveTweenSeconds,
            data.RecoveredSpearFloatAmplitude,
            data.RecoveredSpearFloatDuration,
            data.RecoveredSpearFollowSmoothTime,
            data.RecoveredSpearWarpSnapDistance);
        recoveredSpears.Add(actor);
        PlaySoundAt(data.RecoveredSpearSpawnSound, system, spec, spawnPosition, data);
        ApplyRecoveredSpearLayout(data);
    }

    private RecoveredSpearVolleyContext BeginRecoveredSpearVolleyDespawn(
        LightningSpearSkill1Data data,
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 aimDirection)
    {
        if (data == null || system == null || data.RecoveredSpearProjectilePrefab == null)
            return null;

        PruneRecoveredSpears();
        if (recoveredSpears.Count == 0)
            return null;

        List<LightningSpearRecoveredSpearActor> volleySpears =
            new List<LightningSpearRecoveredSpearActor>(recoveredSpears);
        recoveredSpears.Clear();
        for (int i = 0; i < volleySpears.Count; i++)
            TrackTransientRecoveredSpear(volleySpears[i]);

        Vector2 baseDirection = aimDirection.sqrMagnitude > 0.0001f
            ? aimDirection.normalized
            : ResolveAimDirection(system);
        int sideSign = ResolveFacingSideSign(system, baseDirection);
        SortRecoveredSpearVolleyForDespawn(volleySpears, sideSign);
        int shotCount = CountValidRecoveredSpears(volleySpears);
        if (shotCount == 0)
            return null;

        int shotIndex = 0;
        var shots = new List<RecoveredSpearShotRequest>(shotCount);
        for (int i = 0; i < volleySpears.Count; i++)
        {
            LightningSpearRecoveredSpearActor actor = volleySpears[i];
            if (actor == null)
                continue;

            float angle = CalculateRecoveredSpearShotAngle(data, shotIndex, shotCount, sideSign);
            Vector2 shotDirection = RotateDirection(baseDirection, angle);
            shots.Add(new RecoveredSpearShotRequest(shotDirection));
            shotIndex++;
        }

        Coroutine routine = null;
        routine = StartCoroutine(CoDespawnRecoveredSpears(
            data,
            system,
            spec,
            volleySpears,
            () => recoveredSpearFireRoutines.Remove(routine)));
        recoveredSpearFireRoutines.Add(routine);

        return new RecoveredSpearVolleyContext(baseDirection, shots);
    }

    private IEnumerator CoDespawnRecoveredSpears(
        LightningSpearSkill1Data data,
        AbilitySystem system,
        AbilitySpec spec,
        List<LightningSpearRecoveredSpearActor> volleySpears,
        System.Action onComplete)
    {
        if (volleySpears == null || volleySpears.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        float interval = data != null ? data.RecoveredSpearShotInterval : 0f;
        int startedCount = 0;
        int totalCount = CountValidRecoveredSpears(volleySpears);
        for (int i = 0; i < volleySpears.Count; i++)
        {
            LightningSpearRecoveredSpearActor actor = volleySpears[i];
            if (actor == null)
                continue;

            float despawnFallbackSeconds = data != null ? data.RecoveredSpearDespawnFallbackSeconds : 0f;
            PlaySoundAt(data != null ? data.RecoveredSpearDespawnSound : default, system, spec, actor.transform.position, data);
            actor.PlayDespawnAndDestroy(despawnFallbackSeconds);
            startedCount++;

            if (interval > 0f && startedCount < totalCount)
                yield return new WaitForSeconds(interval);
        }

        onComplete?.Invoke();
    }

    private void StartRecoveredSpearShotSequence(
        LightningSpearSkill1Data data,
        AbilitySystem system,
        AbilitySpec spec,
        RecoveredSpearVolleyContext volley)
    {
        if (data == null || system == null || spec == null || volley == null || !volley.HasShots)
            return;

        Coroutine routine = null;
        routine = StartCoroutine(CoFireRecoveredSpearShots(
            data,
            system,
            spec,
            volley,
            () => recoveredSpearFireRoutines.Remove(routine)));
        recoveredSpearFireRoutines.Add(routine);
    }

    private IEnumerator CoFireRecoveredSpearShots(
        LightningSpearSkill1Data data,
        AbilitySystem system,
        AbilitySpec spec,
        RecoveredSpearVolleyContext volley,
        System.Action onComplete)
    {
        float interval = data != null ? data.RecoveredSpearShotInterval : 0f;
        float releaseDelay = data != null ? data.RecoveredSpearShotReleaseDelay : 0f;
        List<RecoveredSpearShotRequest> shots = volley != null ? volley.shots : null;
        if (shots == null || shots.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        for (int i = 0; i < shots.Count; i++)
        {
            RecoveredSpearShotRequest shot = shots[i];
            Vector2 spawnPosition = CalculateRecoveredSpearShotSpawnPosition(
                data,
                system,
                volley.baseDirection,
                shot.direction);
            SpawnRecoveredSpearShotSpawnEffect(data, spawnPosition, shot.direction);
            PlaySoundAt(data != null ? data.RecoveredSpearShotSpawnSound : default, system, spec, spawnPosition, data);

            if (releaseDelay > 0f)
                yield return new WaitForSeconds(releaseDelay);

            PlaySoundAt(data != null ? data.RecoveredSpearShotFireSound : default, system, spec, spawnPosition, data);
            SpawnRecoveredShotTrail(data, spawnPosition, shot.direction);
            SpawnRecoveredSpearProjectile(data, system, spec, spawnPosition, shot.direction);

            if (interval > 0f && i < shots.Count - 1)
                yield return new WaitForSeconds(interval);
        }

        onComplete?.Invoke();
    }

    private void SpawnRecoveredSpearShotSpawnEffect(
        LightningSpearSkill1Data data,
        Vector2 spawnPosition,
        Vector2 direction)
    {
        if (data == null || data.RecoveredSpearShotSpawnEffectPrefab == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float angle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
        GameObject effect = Instantiate(
            data.RecoveredSpearShotSpawnEffectPrefab,
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle));
        if (effect == null)
            return;

        recoveredShotSpawnEffects.Add(effect);
        HitboxVisualAnimatorPlayer player = effect.GetComponentInChildren<HitboxVisualAnimatorPlayer>(true);
        bool playerDestroysRoot = false;
        if (player != null)
        {
            player.Play();
            playerDestroysRoot = player.DestroyOnComplete && player.gameObject == effect;
        }

        float animationDuration = player != null ? player.CurrentClipDuration : 0f;
        float fallback = Mathf.Max(data.RecoveredSpearShotSpawnEffectLifetimeFallback, animationDuration);
        if (!playerDestroysRoot && fallback > 0f)
            Destroy(effect, fallback);
    }

    private void SpawnRecoveredShotTrail(
        LightningSpearSkill1Data data,
        Vector2 spawnPosition,
        Vector2 direction)
    {
        if (data == null || data.RecoveredShotTrailEffectPrefab == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 endPosition = PredictRecoveredShotEnd(data, spawnPosition, safeDirection);

        LightningSpearRecoverShotTrailEffect trail = Instantiate(
            data.RecoveredShotTrailEffectPrefab,
            spawnPosition,
            Quaternion.identity);
        if (trail == null)
            return;

        recoveredShotTrails.Add(trail);
        trail.Destroyed += HandleRecoveredShotTrailDestroyed;
        trail.Configure(data.RecoveredShotSliceMaxDistance);
        trail.Play(spawnPosition, endPosition);
    }

    private Vector2 PredictRecoveredShotEnd(
        LightningSpearSkill1Data data,
        Vector2 spawnPosition,
        Vector2 direction)
    {
        float maxDistance = Mathf.Max(
            0.01f,
            data.RecoveredSpearProjectileSpeed * data.RecoveredSpearProjectileLifetime);
        LightningSpearHitConfig hitConfig = data.RecoveredSpearProjectileHit;
        LayerMask wallLayers = hitConfig != null ? hitConfig.WallLayers : default;

        if (wallLayers.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(spawnPosition, direction, maxDistance, wallLayers);
            if (hit.collider != null)
                return hit.point;
        }

        return spawnPosition + direction * maxDistance;
    }

    private void SpawnRecoveredSpearProjectile(
        LightningSpearSkill1Data data,
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 spawnPosition,
        Vector2 direction)
    {
        if (data == null || system == null || spec == null || data.RecoveredSpearProjectilePrefab == null)
            return;

        LightningSpearHitConfig hitConfig = data.RecoveredSpearProjectileHit;
        if (hitConfig == null)
            return;

        CombatHitPayload payload = hitConfig.BuildPayload(system, spec);
        if (payload == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : ResolveAimDirection(system);
        float safetyLifetime =
            data.RecoveredSpearProjectileSpawnFallbackSeconds +
            data.RecoveredSpearProjectileLifetime +
            data.RecoveredSpearProjectileStuckLifetime +
            data.RecoveredSpearProjectileDespawnFallbackSeconds +
            1f;

        var context = new ProjectileAttackSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = Mathf.Max(0.1f, safetyLifetime),
            wallLayers = hitConfig.WallLayers,
            damageLayers = hitConfig.HitLayers,
            hitPayload = payload,
            direction = safeDirection,
            speed = data.RecoveredSpearProjectileSpeed
        };

        LightningSpearRecoveredSpearProjectile2D projectile = Instantiate(
            data.RecoveredSpearProjectilePrefab,
            spawnPosition,
            Quaternion.identity);
        if (projectile == null)
            return;

        recoveredProjectiles.Add(projectile);
        projectile.Destroyed += HandleRecoveredProjectileDestroyed;
        projectile.Setup(
            context,
            spawnPosition,
            data.RecoveredSpearProjectileLifetime,
            data.RecoveredSpearProjectileSpawnFallbackSeconds,
            data.RecoveredSpearProjectileStuckLifetime,
            data.RecoveredSpearProjectileDespawnFallbackSeconds);
    }

    private void SpawnMark(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 position,
        MonsterRoomArea2D room,
        AbilitySystem system,
        AbilitySpec spec)
    {
        LightningSpearMarkActor markPrefab = GetMarkPrefab(loadout, data);
        LightningSpearMarkActor mark = Instantiate(markPrefab, position, Quaternion.identity);
        if (mark == null)
            return;

        RegisterMark(mark);
        PlaySoundAt(data != null ? data.MarkRainMarkSpawnSound : default, system, spec, position, data);
        mark.Initialize(
            this,
            room,
            GetMarkLifetimeSeconds(loadout, data),
            GetMarkRainDelay(loadout, data),
            system,
            spec,
            loadout);
    }

    private List<MarkSpawnRequest> GenerateMarkPositions(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 ownerPosition)
    {
        pendingMarkSpawns.Clear();

        int targetCount = GetMarkRainCount(loadout, data);
        if (targetCount <= 0)
            return pendingMarkSpawns;

        MonsterRoomArea2D ownerRoom = FindRoomContaining(ownerPosition);
        int sampleCount = Mathf.Max(GetCandidateSamples(loadout, data), targetCount * 12);
        for (int i = 0; i < sampleCount && pendingMarkSpawns.Count < targetCount; i++)
        {
            Vector2 candidate = SampleFallbackCandidate(ownerPosition, GetFallbackCombatRadius(loadout, data));

            if (!ValidateMarkCandidate(loadout, data, ownerPosition, ownerRoom, candidate))
                continue;

            MonsterRoomArea2D markRoom = ownerRoom != null ? ownerRoom : FindRoomContaining(candidate);
            pendingMarkSpawns.Add(new MarkSpawnRequest(candidate, markRoom));
        }

        return pendingMarkSpawns;
    }

    private static Vector2 SampleFallbackCandidate(Vector2 origin, float radius)
    {
        return origin + Random.insideUnitCircle * Mathf.Max(0.01f, radius);
    }

    private bool ValidateMarkCandidate(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 ownerPosition,
        MonsterRoomArea2D ownerRoom,
        Vector2 candidate)
    {
        if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y))
            return false;

        if (Vector2.Distance(ownerPosition, candidate) > GetFallbackCombatRadius(loadout, data))
            return false;

        if (ownerRoom != null && !ownerRoom.Contains(candidate))
            return false;

        if (HasPlacementBlocker(ownerPosition, candidate, loadout.MarkRushBodyRadius, loadout.StrictRushBlockMask))
            return false;

        if (Vector2.Distance(ownerPosition, candidate) < GetMinPlayerDistance(loadout, data))
            return false;

        if (!IsLandingValid(loadout, data, candidate))
            return false;

        if (!HasRequiredSpacing(loadout, data, candidate))
            return false;

        return true;
    }

    private bool HasRequiredSpacing(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 candidate)
    {
        float minSpacing = GetMinMarkSpacing(loadout, data);
        if (minSpacing <= 0f)
            return true;

        float minSpacingSqr = minSpacing * minSpacing;
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (((Vector2)mark.transform.position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }

        for (int i = 0; i < pendingMarkSpawns.Count; i++)
        {
            if ((pendingMarkSpawns[i].position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }

        return true;
    }

    private static void TryPlayAnimationTrigger(AbilitySystem system, AbilityDefinition definition, string trigger)
    {
        if (system == null || definition == null || string.IsNullOrWhiteSpace(trigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(trigger), definition);
    }

    private static IEnumerator WaitForAnimationEventOrDelay(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayTag eventTag,
        float eventTimeout,
        float fallbackDelay)
    {
        if (system == null || spec == null)
            yield break;

        if (eventTag != null)
        {
            yield return AbilityTasks.WaitGameplayEvent(
                system,
                spec,
                eventTag,
                onReceived: null,
                timeout: eventTimeout,
                predicate: data => data.Spec == spec);
            yield break;
        }

        if (fallbackDelay > 0f)
            yield return AbilityTasks.WaitDelay(system, spec, fallbackDelay);
    }

    private static LightningSpearSkill1Data ResolveSkill1Data(LightningSpearLoadout loadout)
    {
        return loadout != null && loadout.MarkRushOrSweep != null
            ? loadout.MarkRushOrSweep.sourceObject as LightningSpearSkill1Data
            : null;
    }

    private static LightningSpearSkill2Data ResolveSkill2Data(LightningSpearLoadout loadout)
    {
        return loadout != null && loadout.MarkRain != null
            ? loadout.MarkRain.sourceObject as LightningSpearSkill2Data
            : null;
    }

    private static string GetMarkRushAnimationTrigger(LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushAnimationTrigger : null;
    }

    private static string GetNoMarkSweepAnimationTrigger(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepAnimationTrigger : null;
    }

    private static WeaponAimPresentationSettings GetMarkRushAimPresentation(LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushAimPresentation : null;
    }

    private static WeaponAimPresentationSettings GetNoMarkSweepAimPresentation(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepAimPresentation : null;
    }

    private static GameplayTag GetNoMarkSweepHitEventTag(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepHitEventTag : null;
    }

    private static float GetNoMarkSweepHitEventTimeout(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepHitEventTimeout : 0f;
    }

    private static float GetNoMarkSweepFallbackHitDelay(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepFallbackHitDelay : 0f;
    }

    private static float GetCursorSelectRadius(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.CursorSelectRadius : loadout.CursorSelectRadius;
    }

    private static float GetMarkRushRange(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushRange : loadout.MarkRushRange;
    }

    private static float GetMarkRushBodyRadius(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushBodyRadius : loadout.MarkRushBodyRadius;
    }

    private static float GetMarkRushArrivalHitDelay(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushArrivalHitDelay : loadout.MarkRushArrivalHitDelay;
    }

    private static float GetMarkRushInternalDelay(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushInternalDelay : loadout.MarkRushInternalDelay;
    }

    private static float GetMarkRushInputBufferSeconds(LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushInputBufferSeconds : 0.35f;
    }

    private static LightningSpearDashStabTrailEffect GetMarkRushTrailEffectPrefab(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (data != null && data.MarkRushTrailEffectPrefab != null)
            return data.MarkRushTrailEffectPrefab;

        return loadout != null ? loadout.MarkRushTrailEffectPrefab : null;
    }

    private static LightningSpearHitConfig GetMarkRushHit(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (data != null && data.MarkRushHit != null)
            return data.MarkRushHit;

        return loadout != null ? loadout.MarkRushHit : null;
    }

    private static LightningSpearHitConfig GetNoMarkSweepHit(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (data != null && data.NoMarkSweepHit != null)
            return data.NoMarkSweepHit;

        return loadout != null ? loadout.NoMarkSweepHit : null;
    }

    private static string GetMarkRainAnimationTrigger(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainAnimationTrigger : null;
    }

    private static WeaponAimPresentationSettings GetMarkRainAimPresentation(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainAimPresentation : null;
    }

    private static GameplayTag GetMarkRainSpawnEventTag(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainSpawnEventTag : null;
    }

    private static float GetMarkRainSpawnEventTimeout(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainSpawnEventTimeout : 0f;
    }

    private static float GetMarkRainFallbackSpawnDelay(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainFallbackSpawnDelay : 0f;
    }

    private static LightningSpearMarkActor GetMarkPrefab(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data)
    {
        if (data != null && data.MarkPrefab != null)
            return data.MarkPrefab;

        return loadout != null ? loadout.MarkPrefab : null;
    }

    private static float GetMarkLifetimeSeconds(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkLifetimeSeconds : loadout.MarkLifetimeSeconds;
    }

    private static int GetMarkRainCount(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainCount : loadout.MarkRainCount;
    }

    private static float GetMarkRainDelay(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainDelay : loadout.MarkRainDelay;
    }

    private static float GetFallbackCombatRadius(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.FallbackCombatRadius : loadout.FallbackCombatRadius;
    }

    private static float GetMinPlayerDistance(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MinPlayerDistance : loadout.MinPlayerDistance;
    }

    private static float GetMinMarkSpacing(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MinMarkSpacing : loadout.MinMarkSpacing;
    }

    private static float GetLandingProbeRadius(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.LandingProbeRadius : loadout.LandingProbeRadius;
    }

    private static int GetCandidateSamples(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.CandidateSamples : loadout.CandidateSamples;
    }

    private static LightningSpearHitConfig GetLandingHit(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data)
    {
        if (data != null && data.LandingHit != null)
            return data.LandingHit;

        return loadout != null ? loadout.LandingHit : null;
    }

    private static void PlaySoundAt(
        SoundRef sound,
        AbilitySystem system,
        AbilitySpec spec,
        Vector3 position,
        Object sourceObject)
    {
        AbilityAudioRouter.PlayOneShotAtPosition(sound, system, spec, position, sourceObject);
    }

    private Vector2 ResolveHitboxCenter(
        LightningSpearHitConfig hitConfig,
        AbilitySystem system,
        Vector2 origin,
        Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : ResolveAimDirection(system);
        float forwardOffset = hitConfig != null ? hitConfig.ForwardOffset : 0f;
        return origin + safeDirection * forwardOffset;
    }

    private bool SpawnHitbox(
        LightningSpearHitConfig hitConfig,
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 origin,
        Vector2 direction,
        int facingSideSignOverride = 0,
        HashSet<int> sharedHitTargetIds = null)
    {
        if (hitConfig == null || !hitConfig.HasHitbox || system == null || spec == null)
            return false;

        CombatHitPayload payload = hitConfig.BuildPayload(system, spec);
        if (payload == null)
            return false;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : ResolveAimDirection(system);
        Vector2 center = ResolveHitboxCenter(hitConfig, system, origin, safeDirection);
        int visualSideSign = facingSideSignOverride != 0
            ? (facingSideSignOverride < 0 ? -1 : 1)
            : ResolveFacingSideSign(system, safeDirection);

        MeleeHitboxActor hitbox = Instantiate(hitConfig.HitboxPrefab, center, Quaternion.identity);
        if (hitbox == null)
            return false;

        var context = new MeleeHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = hitConfig.ActiveTime,
            wallLayers = hitConfig.WallLayers,
            damageLayers = hitConfig.HitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = hitConfig.HitboxSize,
            hitOncePerTarget = true,
            destroyOnFirstHit = false,
            direction = safeDirection,
            flipVisualX = visualSideSign < 0,
            sharedHitTargetIds = sharedHitTargetIds
        };

        hitbox.Setup(context);
        return true;
    }

    private void RefreshMarkFeedback(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        if (loadout == null || ownerSystem == null)
        {
            ClearFeedback();
            return;
        }

        bool skillReady = IsSkill1Ready(loadout);
        Vector2 ownerPosition = ownerSystem.transform.position;
        bool keepRushRangeVisible = HasMarkRushDestinationOrigin() || hasBufferedMarkRushInput;
        Vector2 rushRangeOrigin = hasBufferedMarkRushInput && hasBufferedMarkRushOrigin
            ? bufferedMarkRushOrigin
            : keepRushRangeVisible
                ? ResolveMarkRushInputOrigin()
                : ownerPosition;
        Vector2 cursorWorld = ResolveCursorWorld(ownerSystem);
        LightningSpearMarkActor selected = skillReady
            ? FindSelectableMark(loadout, data, ownerPosition, cursorWorld)
            : null;
        bool hasActiveMark = false;
        visibleMarkHoverRangeMarks.Clear();

        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (!mark.IsActive)
            {
                mark.SetFeedback(false, false);
                continue;
            }

            hasActiveMark = true;
            visibleMarkHoverRangeMarks.Add(mark);
            UpdateMarkHoverRangeIndicator(loadout, data, mark, true);
            bool canRushToMark =
                (skillReady || keepRushRangeVisible) &&
                CanRushToMark(loadout, data, mark, rushRangeOrigin);
            mark.SetFeedback(canRushToMark, mark == selected);
        }

        PruneMarkHoverRangeIndicators();
        UpdateRangeIndicator(loadout, data, (skillReady || keepRushRangeVisible) && hasActiveMark);
        UpdateSelectedMarkIndicator(loadout, selected);
        UpdateCursorFeedback(selected != null);
        skill1MarkRushHudOverrideActive = selected != null;
    }

    private void ClearFeedback()
    {
        skill1MarkRushHudOverrideActive = false;

        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            mark.SetFeedback(false, false);
        }

        if (rushRangeIndicatorInstance != null)
            rushRangeIndicatorInstance.SetActive(false);

        if (selectedMarkIndicatorInstance != null)
            selectedMarkIndicatorInstance.SetActive(false);

        SetAllMarkHoverRangeIndicatorsActive(false);
        UpdateCursorFeedback(false);
    }

    private void UpdateRangeIndicator(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        bool active)
    {
        if (!active || loadout == null || ownerSystem == null || loadout.RushRangeIndicatorPrefab == null)
        {
            if (rushRangeIndicatorInstance != null)
                rushRangeIndicatorInstance.SetActive(false);
            return;
        }

        if (rushRangeIndicatorInstance == null)
            rushRangeIndicatorInstance = Instantiate(loadout.RushRangeIndicatorPrefab);

        rushRangeIndicatorInstance.transform.SetParent(null, true);
        rushRangeIndicatorInstance.transform.position = ownerSystem.transform.position;
        rushRangeIndicatorInstance.transform.rotation = Quaternion.identity;
        rushRangeIndicatorInstance.transform.localScale = Vector3.one;

        if (rushRangeIndicatorInstance.TryGetComponent(out LightningSpearRushRangeIndicator rangeIndicator))
            rangeIndicator.SetRadius(GetMarkRushRange(loadout, data));
        else
            rushRangeIndicatorInstance.transform.localScale = Vector3.one * (GetMarkRushRange(loadout, data) * 2f);

        rushRangeIndicatorInstance.SetActive(true);
    }

    private void UpdateMarkHoverRangeIndicator(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        LightningSpearMarkActor mark,
        bool active)
    {
        if (!active || loadout == null || mark == null || loadout.MarkHoverRangeIndicatorPrefab == null)
        {
            DestroyMarkHoverRangeIndicator(mark);
            return;
        }

        if (!markHoverRangeIndicators.TryGetValue(mark, out GameObject indicator) || indicator == null)
        {
            indicator = Instantiate(loadout.MarkHoverRangeIndicatorPrefab);
            markHoverRangeIndicators[mark] = indicator;
        }

        indicator.transform.SetParent(null, true);
        indicator.transform.position = mark.transform.position;
        indicator.transform.rotation = Quaternion.identity;
        indicator.transform.localScale = Vector3.one;

        float radius = GetCursorSelectRadius(loadout, data);
        if (indicator.TryGetComponent(out LightningSpearRushRangeIndicator rangeIndicator))
            rangeIndicator.SetRadius(radius);
        else
            indicator.transform.localScale = Vector3.one * (radius * 2f);

        indicator.SetActive(true);
    }

    private void PruneMarkHoverRangeIndicators()
    {
        staleMarkHoverRangeMarks.Clear();

        foreach (KeyValuePair<LightningSpearMarkActor, GameObject> entry in markHoverRangeIndicators)
        {
            LightningSpearMarkActor mark = entry.Key;
            if (mark == null || !mark.IsActive || !visibleMarkHoverRangeMarks.Contains(mark))
                staleMarkHoverRangeMarks.Add(mark);
        }

        for (int i = 0; i < staleMarkHoverRangeMarks.Count; i++)
            DestroyMarkHoverRangeIndicator(staleMarkHoverRangeMarks[i]);

        staleMarkHoverRangeMarks.Clear();
        visibleMarkHoverRangeMarks.Clear();
    }

    private void SetAllMarkHoverRangeIndicatorsActive(bool active)
    {
        foreach (KeyValuePair<LightningSpearMarkActor, GameObject> entry in markHoverRangeIndicators)
        {
            if (entry.Value != null)
                entry.Value.SetActive(active);
        }
    }

    private void DestroyMarkHoverRangeIndicator(LightningSpearMarkActor mark)
    {
        if (ReferenceEquals(mark, null) || !markHoverRangeIndicators.TryGetValue(mark, out GameObject indicator))
            return;

        markHoverRangeIndicators.Remove(mark);
        if (indicator != null)
            Destroy(indicator);
    }

    private void DestroyAllMarkHoverRangeIndicators()
    {
        foreach (KeyValuePair<LightningSpearMarkActor, GameObject> entry in markHoverRangeIndicators)
        {
            if (entry.Value != null)
                Destroy(entry.Value);
        }

        markHoverRangeIndicators.Clear();
        visibleMarkHoverRangeMarks.Clear();
        staleMarkHoverRangeMarks.Clear();
    }

    private void UpdateSelectedMarkIndicator(LightningSpearLoadout loadout, LightningSpearMarkActor selected)
    {
        if (selected == null || loadout == null || loadout.SelectedMarkIndicatorPrefab == null)
        {
            if (selectedMarkIndicatorInstance != null)
                selectedMarkIndicatorInstance.SetActive(false);
            return;
        }

        if (selectedMarkIndicatorInstance == null)
            selectedMarkIndicatorInstance = Instantiate(loadout.SelectedMarkIndicatorPrefab);

        selectedMarkIndicatorInstance.transform.position = selected.transform.position;
        selectedMarkIndicatorInstance.SetActive(true);
    }

    private void UpdateCursorFeedback(bool active)
    {
        if (cursorInteractableSet == active)
            return;

        cursorInteractableSet = active;
        MouseCursorPlayback.SetInteractable(this, active);
    }

    private void DestroyFeedbackObjects()
    {
        if (rushRangeIndicatorInstance != null)
        {
            Destroy(rushRangeIndicatorInstance);
            rushRangeIndicatorInstance = null;
        }

        if (selectedMarkIndicatorInstance != null)
        {
            Destroy(selectedMarkIndicatorInstance);
            selectedMarkIndicatorInstance = null;
        }

        DestroyAllMarkHoverRangeIndicators();
    }

    private LightningSpearMarkActor FindSelectableMark(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        Vector2 ownerPosition,
        Vector2 cursorWorld)
    {
        LightningSpearMarkActor bestMark = null;
        float cursorSelectRadius = GetCursorSelectRadius(loadout, data);
        float selectRadiusSqr = cursorSelectRadius * cursorSelectRadius;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (!mark.IsActive)
                continue;

            Vector2 markPosition = mark.transform.position;
            float cursorDistanceSqr = (markPosition - cursorWorld).sqrMagnitude;
            if (cursorDistanceSqr > selectRadiusSqr)
                continue;

            if (!CanRushToMark(loadout, data, mark, ownerPosition))
                continue;

            if (cursorDistanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = cursorDistanceSqr;
            bestMark = mark;
        }

        return bestMark;
    }

    private bool CanRushToMark(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        LightningSpearMarkActor mark,
        Vector2 ownerPosition)
    {
        if (loadout == null || mark == null || !mark.IsActive)
            return false;

        Vector2 markPosition = mark.transform.position;
        if (Vector2.Distance(ownerPosition, markPosition) > GetMarkRushRange(loadout, data))
            return false;

        if (!IsLandingValid(loadout, ResolveSkill2Data(loadout), markPosition))
            return false;

        MonsterRoomArea2D ownerRoom = FindRoomContaining(ownerPosition);
        MonsterRoomArea2D markRoom = ResolveMarkRoom(mark, markPosition);
        int blockerMask;

        if (ownerRoom != null)
        {
            if (markRoom != ownerRoom)
                return false;

            blockerMask = loadout.HardBlockMask;
        }
        else
        {
            blockerMask = loadout.StrictRushBlockMask;
        }

        return !HasBlocker(ownerPosition, markPosition, GetMarkRushBodyRadius(loadout, data), blockerMask);
    }

    private MonsterRoomArea2D ResolveMarkRoom(LightningSpearMarkActor mark, Vector2 markPosition)
    {
        if (mark != null && mark.RoomArea != null && mark.RoomArea.Contains(markPosition))
            return mark.RoomArea;

        return FindRoomContaining(markPosition);
    }

    private bool IsLandingValid(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 position)
    {
        if (loadout == null)
            return false;

        float radius = GetLandingProbeRadius(loadout, data);

        if (!HasGroundTileAt(position) && !HasRequiredGroundOverlap(loadout, position, radius))
            return false;

        int blockedMask = loadout.LandingBlockedMask;
        if (blockedMask != 0 &&
            Physics2D.OverlapCircle(position, radius, blockedMask) != null)
        {
            return false;
        }

        return true;
    }

    private bool HasGroundTileAt(Vector2 position)
    {
        List<Tilemap> groundTilemaps = ResolveGroundTilemaps();
        if (groundTilemaps.Count == 0)
            return false;

        for (int i = 0; i < groundTilemaps.Count; i++)
        {
            Tilemap tilemap = groundTilemaps[i];
            if (!IsUsableGroundTilemap(tilemap))
                continue;

            if (tilemap.HasTile(tilemap.WorldToCell(position)))
                return true;
        }

        return false;
    }

    private bool HasRequiredGroundOverlap(LightningSpearLoadout loadout, Vector2 position, float radius)
    {
        int requiredMask = loadout != null ? loadout.RequiredGroundLayers.value : 0;
        if (requiredMask == 0)
            return false;

        return Physics2D.OverlapCircle(position, radius, requiredMask) != null;
    }

    private List<Tilemap> ResolveGroundTilemaps()
    {
        if (groundTilemapCacheFrame == Time.frameCount)
            return groundTilemapCache;

        groundTilemapCacheFrame = Time.frameCount;
        groundTilemapCache.Clear();

        int groundLayer = ResolveGroundLayer();
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        Tilemap[] tilemaps = Object.FindObjectsOfType<Tilemap>();
#endif
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (!IsUsableGroundTilemap(tilemap))
                continue;

            if (tilemap.gameObject.layer != groundLayer)
                continue;

            groundTilemapCache.Add(tilemap);
        }

        return groundTilemapCache;
    }

    private int ResolveGroundLayer()
    {
        if (cachedGroundLayer != int.MinValue)
            return cachedGroundLayer;

        int groundLayer = LayerMask.NameToLayer("Ground");
        cachedGroundLayer = groundLayer >= 0 ? groundLayer : 7;
        return cachedGroundLayer;
    }

    private static bool IsUsableGroundTilemap(Tilemap tilemap)
    {
        return tilemap != null && tilemap.isActiveAndEnabled && tilemap.gameObject.activeInHierarchy;
    }

    private static bool HasBlocker(Vector2 start, Vector2 end, float radius, int layerMask)
    {
        if (layerMask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
        if (radius > 0f)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, direction, distance, layerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (IsInitialPlacementOverlapMovingAway(start, direction, collider, hits[i]))
                    continue;

                return true;
            }

            return false;
        }

        return Physics2D.Linecast(start, end, layerMask).collider != null;
    }

    private static bool HasPlacementBlocker(Vector2 start, Vector2 end, float radius, int layerMask)
    {
        if (layerMask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
        if (radius <= 0f)
            return Physics2D.Linecast(start, end, layerMask).collider != null;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, direction, distance, layerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider == null)
                continue;

            if (IsInitialPlacementOverlapMovingAway(start, direction, collider, hits[i]))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsInitialPlacementOverlapMovingAway(
        Vector2 start,
        Vector2 direction,
        Collider2D collider,
        RaycastHit2D hit)
    {
        if (hit.distance > 0.001f && hit.fraction > 0.0001f)
            return false;

        Vector2 closest = collider.ClosestPoint(start);
        Vector2 awayFromCollider = start - closest;
        if (awayFromCollider.sqrMagnitude <= 0.000001f)
            return false;

        return Vector2.Dot(direction, awayFromCollider.normalized) >= -0.05f;
    }

    private MonsterRoomArea2D FindRoomContaining(Vector2 position)
    {
        MonsterRoomArea2D[] rooms = FindRooms();
        for (int i = 0; i < rooms.Length; i++)
        {
            MonsterRoomArea2D room = rooms[i];
            if (room != null && room.Contains(position))
                return room;
        }

        return null;
    }

    private static MonsterRoomArea2D[] FindRooms()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<MonsterRoomArea2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<MonsterRoomArea2D>();
#endif
    }

    private bool IsSkill1Ready(LightningSpearLoadout loadout)
    {
        if (ownerSystem == null || loadout == null || loadout.MarkRushOrSweep == null)
            return false;

        return ownerSystem.GetCooldownRemaining(loadout.MarkRushOrSweep) <= 0f &&
               ownerSystem.GetNextActivationRemaining(loadout.MarkRushOrSweep) <= 0f;
    }

    private static bool IsGameplayInputBlockedByUiOrFlow()
    {
        if (DialoguePlayback.IsPlaying)
            return true;

        if (UiInteractionStateQuery.HasBlockingUI())
            return true;

        if (SceneTransitionPlayback.IsTransitionActive)
            return true;

        return LoadingPresentationQuery.IsActiveLoadingPresentation;
    }

    private Vector2 ResolveCursorWorld(AbilitySystem system)
    {
        Camera camera = Camera.main;
        if (camera != null)
            return InputActionQuery.GetPointerWorldPosition(camera, 0f);

        if (aimSource != null)
            return aimSource.MouseWorld;

        return system != null ? (Vector2)system.transform.position : Vector2.zero;
    }

    private Vector2 ResolveAimDirection(AbilitySystem system)
    {
        Vector2 direction = AbilityAimResolver2D.Resolve(
            system != null ? system.gameObject : gameObject,
            Vector2.right);

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    private int BeginAimPresentationOverride(WeaponAimPresentationSettings settings, Vector2 direction)
    {
        if (settings == null || settings.Mode == WeaponAimPresentationMode.FollowAim)
            return 0;

        CacheOwnerReferences(ownerSystem);

        if (presentationRig == null)
            return 0;

        return presentationRig.BeginAimPresentationOverride(
            settings.Mode,
            direction,
            settings.MinimumHoldTime);
    }

    private void EndAimPresentationOverride(int token)
    {
        if (token == 0 || presentationRig == null)
            return;

        presentationRig.EndAimPresentationOverride(token);
    }

    private int ResolveFacingSideSign(AbilitySystem system, Vector2 fallbackDirection)
    {
        CacheOwnerReferences(system);

        if (presentationRig != null)
        {
            presentationRig.RefreshNow();
            return presentationRig.CurrentSideSign < 0 ? -1 : 1;
        }

        if (aimSource != null && system != null)
        {
            float deltaX = aimSource.MouseWorld.x - system.transform.position.x;
            if (deltaX < -0.0001f)
                return -1;

            if (deltaX > 0.0001f)
                return 1;
        }

        return fallbackDirection.x < -0.0001f ? -1 : 1;
    }

    private bool TryResolveActiveLoadout(out LightningSpearLoadout loadout)
    {
        CacheOwnerReferences(null);

        WeaponDefinition activeWeapon = weaponInventory != null ? weaponInventory.ActiveWeapon : null;
        loadout = activeWeapon != null ? activeWeapon.abilityLoadout as LightningSpearLoadout : null;
        return loadout != null && ownerSystem != null;
    }

    private void CacheOwnerReferences(AbilitySystem explicitSystem)
    {
        if (explicitSystem != null)
            ownerSystem = explicitSystem;

        if (ownerSystem == null)
            ownerSystem = GetComponentInParent<AbilitySystem>();

        if (weaponInventory == null && ownerSystem != null)
            weaponInventory = ownerSystem.GetComponent<WeaponInventory2D>();

        if (aimSource == null && ownerSystem != null)
            aimSource = ownerSystem.GetComponent<PlayerAim2D>();

        if (movementMotor == null && ownerSystem != null)
            movementMotor = ownerSystem.GetComponent<MovementMotor2D>();

        if (presentationRig == null)
        {
            presentationRig = GetComponentInParent<WeaponPresentationRig2D>();
            if (presentationRig == null && ownerSystem != null)
                presentationRig = ownerSystem.GetComponentInChildren<WeaponPresentationRig2D>(true);
        }
    }

    private void ClearAllMarks()
    {
        DestroyAllMarkHoverRangeIndicators();

        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark != null)
                Destroy(mark.gameObject);
        }

        activeMarks.Clear();
    }

    private void ClearRecoveredSpearState()
    {
        StopRecoveredSpearFireRoutines();
        ClearRecoveredSpears();
        ClearRecoveredProjectiles();
        ClearRecoveredShotTrails();
        ClearRecoveredShotSpawnEffects();
    }

    private void StopRecoveredSpearFireRoutines()
    {
        for (int i = 0; i < recoveredSpearFireRoutines.Count; i++)
        {
            Coroutine routine = recoveredSpearFireRoutines[i];
            if (routine != null)
                StopCoroutine(routine);
        }

        recoveredSpearFireRoutines.Clear();
    }

    private void ClearRecoveredSpears()
    {
        for (int i = recoveredSpears.Count - 1; i >= 0; i--)
        {
            LightningSpearRecoveredSpearActor actor = recoveredSpears[i];
            DestroyRecoveredSpearActor(actor);
        }

        recoveredSpears.Clear();

        for (int i = transientRecoveredSpears.Count - 1; i >= 0; i--)
        {
            LightningSpearRecoveredSpearActor actor = transientRecoveredSpears[i];
            DestroyRecoveredSpearActor(actor);
        }

        transientRecoveredSpears.Clear();
    }

    private void ClearRecoveredProjectiles()
    {
        for (int i = recoveredProjectiles.Count - 1; i >= 0; i--)
        {
            LightningSpearRecoveredSpearProjectile2D projectile = recoveredProjectiles[i];
            if (projectile != null)
            {
                projectile.Destroyed -= HandleRecoveredProjectileDestroyed;
                Destroy(projectile.gameObject);
            }
        }

        recoveredProjectiles.Clear();
    }

    private void ClearRecoveredShotTrails()
    {
        for (int i = recoveredShotTrails.Count - 1; i >= 0; i--)
        {
            LightningSpearRecoverShotTrailEffect trail = recoveredShotTrails[i];
            if (trail != null)
            {
                trail.Destroyed -= HandleRecoveredShotTrailDestroyed;
                Destroy(trail.gameObject);
            }
        }

        recoveredShotTrails.Clear();
    }

    private void ClearRecoveredShotSpawnEffects()
    {
        for (int i = recoveredShotSpawnEffects.Count - 1; i >= 0; i--)
        {
            GameObject effect = recoveredShotSpawnEffects[i];
            if (effect != null)
                Destroy(effect);
        }

        recoveredShotSpawnEffects.Clear();
    }

    private void PruneRecoveredSpears()
    {
        for (int i = recoveredSpears.Count - 1; i >= 0; i--)
        {
            if (recoveredSpears[i] == null)
                recoveredSpears.RemoveAt(i);
        }
    }

    private void TrackTransientRecoveredSpear(LightningSpearRecoveredSpearActor actor)
    {
        if (actor == null || transientRecoveredSpears.Contains(actor))
            return;

        transientRecoveredSpears.Add(actor);
        actor.Destroyed += HandleRecoveredSpearDestroyed;
    }

    private void HandleRecoveredSpearDestroyed(LightningSpearRecoveredSpearActor actor)
    {
        if (actor == null)
            return;

        actor.Destroyed -= HandleRecoveredSpearDestroyed;
        recoveredSpears.Remove(actor);
        transientRecoveredSpears.Remove(actor);
    }

    private void DestroyRecoveredSpearActor(LightningSpearRecoveredSpearActor actor)
    {
        if (actor == null)
            return;

        actor.Destroyed -= HandleRecoveredSpearDestroyed;
        Destroy(actor.gameObject);
    }

    private void RemoveRecoveredSpearAt(int index, LightningSpearSkill1Data data)
    {
        if (index < 0 || index >= recoveredSpears.Count)
            return;

        LightningSpearRecoveredSpearActor actor = recoveredSpears[index];
        recoveredSpears.RemoveAt(index);

        if (actor != null)
        {
            TrackTransientRecoveredSpear(actor);
            float fallback = data != null ? data.RecoveredSpearDespawnFallbackSeconds : 0f;
            actor.PlayDespawnAndDestroy(fallback);
        }

        ApplyRecoveredSpearLayout(data);
    }

    private void ApplyRecoveredSpearLayout(LightningSpearSkill1Data data)
    {
        if (data == null)
            return;

        PruneRecoveredSpears();
        int count = recoveredSpears.Count;
        int sideSign = ResolveFacingSideSign(ownerSystem, ResolveAimDirection(ownerSystem));
        recoveredSpearLayoutSideSign = sideSign;
        for (int i = 0; i < count; i++)
        {
            LightningSpearRecoveredSpearActor actor = recoveredSpears[i];
            if (actor == null)
                continue;

            actor.SetFollowSettings(data.RecoveredSpearFollowSmoothTime, data.RecoveredSpearWarpSnapDistance);
            actor.SetLayout(
                CalculateRecoveredSpearOffset(data, i, count, sideSign),
                CalculateRecoveredSpearStockAngle(data, i, count, sideSign),
                data.RecoveredSpearStockVisualForwardOffset,
                data.RecoveredSpearMoveTweenSeconds);
        }
    }

    private void RefreshRecoveredSpearLayout(LightningSpearSkill1Data data)
    {
        if (data == null || recoveredSpears.Count == 0)
            return;

        int sideSign = ResolveFacingSideSign(ownerSystem, ResolveAimDirection(ownerSystem));
        if (sideSign == recoveredSpearLayoutSideSign)
            return;

        ApplyRecoveredSpearLayout(data);
    }

    private void HandleRecoveredProjectileDestroyed(LightningSpearRecoveredSpearProjectile2D projectile)
    {
        if (projectile == null)
            return;

        projectile.Destroyed -= HandleRecoveredProjectileDestroyed;
        recoveredProjectiles.Remove(projectile);
    }

    private void HandleRecoveredShotTrailDestroyed(LightningSpearRecoverShotTrailEffect trail)
    {
        if (trail == null)
            return;

        trail.Destroyed -= HandleRecoveredShotTrailDestroyed;
        recoveredShotTrails.Remove(trail);
    }

    private static Vector2 CalculateRecoveredSpearOffset(
        LightningSpearSkill1Data data,
        int index,
        int count,
        int sideSign)
    {
        if (data == null || count <= 0)
            return Vector2.zero;

        float centeredIndex = index - (count - 1) * 0.5f;
        int safeSideSign = sideSign < 0 ? -1 : 1;
        Vector2 baseOffset = data.RecoveredSpearBaseOffset;
        baseOffset.x *= safeSideSign;
        float backX = -safeSideSign * data.RecoveredSpearBackOffset;
        float spreadX = centeredIndex * data.RecoveredSpearSpacing * safeSideSign;
        return baseOffset + Vector2.right * (backX + spreadX);
    }

    private static float CalculateRecoveredSpearStockAngle(
        LightningSpearSkill1Data data,
        int index,
        int count,
        int sideSign)
    {
        if (data == null)
            return 0f;

        int safeSideSign = sideSign < 0 ? -1 : 1;
        return -CalculateRecoveredSpearFanAngle(
            index,
            count,
            data.RecoveredSpearStockAngleStep,
            data.RecoveredSpearStockMaxFanAngle) * safeSideSign;
    }

    private static float CalculateRecoveredSpearFanAngle(
        int index,
        int count,
        float angleStep,
        float maxSpread)
    {
        if (count <= 1)
            return 0f;

        float unclampedSpread = Mathf.Max(0f, angleStep) * (count - 1);
        float spread = maxSpread > 0f ? Mathf.Min(unclampedSpread, maxSpread) : unclampedSpread;
        float step = spread / (count - 1);
        return (index - (count - 1) * 0.5f) * step;
    }

    private static float CalculateRecoveredSpearShotAngle(
        LightningSpearSkill1Data data,
        int index,
        int count,
        int sideSign)
    {
        if (data == null)
            return 0f;

        int safeSideSign = sideSign < 0 ? -1 : 1;
        return -CalculateRecoveredSpearFanAngle(
            index,
            count,
            data.RecoveredSpearAngleStep,
            data.RecoveredSpearMaxFanAngle) * safeSideSign;
    }

    private static Vector2 CalculateRecoveredSpearShotSpawnPosition(
        LightningSpearSkill1Data data,
        AbilitySystem system,
        Vector2 baseDirection,
        Vector2 shotDirection)
    {
        Vector2 ownerPosition = system != null ? (Vector2)system.transform.position : Vector2.zero;
        Vector2 safeBaseDirection = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector2.right;
        Vector2 safeShotDirection = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : safeBaseDirection;
        float pivotForwardOffset = data != null ? data.RecoveredSpearShotPivotForwardOffset : 0f;
        float innerRadius = data != null ? data.RecoveredSpearShotInnerRadius : 0f;
        Vector2 pivot = ownerPosition + safeBaseDirection * pivotForwardOffset;
        return pivot + safeShotDirection * innerRadius;
    }

    private static void SortRecoveredSpearVolleyForDespawn(
        List<LightningSpearRecoveredSpearActor> volleySpears,
        int sideSign)
    {
        if (volleySpears == null || volleySpears.Count <= 1)
            return;

        int safeSideSign = sideSign < 0 ? -1 : 1;
        volleySpears.Sort((left, right) =>
        {
            if (left == right)
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            float leftX = left.CurrentPosition.x;
            float rightX = right.CurrentPosition.x;
            return safeSideSign > 0
                ? rightX.CompareTo(leftX)
                : leftX.CompareTo(rightX);
        });
    }

    private static int CountValidRecoveredSpears(List<LightningSpearRecoveredSpearActor> volleySpears)
    {
        if (volleySpears == null)
            return 0;

        int count = 0;
        for (int i = 0; i < volleySpears.Count; i++)
        {
            if (volleySpears[i] != null)
                count++;
        }

        return count;
    }

    private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            safeDirection.x * cos - safeDirection.y * sin,
            safeDirection.x * sin + safeDirection.y * cos).normalized;
    }
}
