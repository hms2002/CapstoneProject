using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

/// <summary>
/// 책임 :
/// - 장착 중인 파편검 프리팹의 live adapter로서 슬롯의 FragmentBladeRuntimeData와 무기 presentation actor를 연결한다.
/// - ability 발동 성공 이후 조각 소모, 강화 시작, 해제/전환 cleanup 같은 런타임 상태 전이를 처리한다.
/// </summary>
public sealed class FragmentBladeRuntimeState : WeaponAbilityRuntimeState
{
    private const string RecallAttackBlockedTagPath = "State.Attacking.Blocked";

    [Header("Detach Placement")]
    [SerializeField, Min(0f)] private float detachRadius = 1f;
    [SerializeField, Min(1)] private int detachPlacementAttempts = 3;
    [SerializeField] private LayerMask detachObstacleMask;

    [Header("Presentation")]
    [SerializeField] private FragmentBladePresentationActor presentationActor;

    private WeaponInventory2D weaponInventory;
    private TagSystem ownerTags;
    private bool recallAttackBlockTagApplied;
    private int pendingRecallShardCount;

    public FragmentBladeRuntimeData BoundData => GetBoundData();

    private void Awake()
    {
        CacheInventory();
        CacheOwnerTags();
        CachePresentationActor();
    }

    private void Update()
    {
        FragmentBladeRuntimeData data = GetBoundData();
        if (data == null)
            return;

        data.Tick(Time.deltaTime);
        presentationActor?.EnsurePoolSize(data.MaxShardCount);
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        CacheInventory();

        if (previousWeapon != null && previousWeapon != newWeapon)
            CancelActiveShardMotions();
    }

    public override void HandleAbilityActivated(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition activatedAbility)
    {
        if (weapon == null || activatedAbility == null)
            return;

        if (weapon.abilityLoadout is not FragmentBladeLoadout loadout)
            return;

        FragmentBladeRuntimeData data = GetBoundData();
        if (data == null)
            return;

        if (slot == WeaponAbilitySlot.Attack && activatedAbility == loadout.BaseAttack)
        {
            Vector3 dropPosition = ResolveDropPosition();
            if (data.TryDetachShard(dropPosition, Time.time, out FragmentBladeRuntimeData.ShardRuntimeState shard))
                presentationActor?.ShowDetached(shard);
            return;
        }

        if (slot == WeaponAbilitySlot.Skill2 && activatedAbility == loadout.BindEnhanceSkill)
        {
            data.StartSkill2(loadout.Skill2DurationSeconds);
        }
    }

    public override void HandleGameplayEvent(
        WeaponDefinition weapon,
        GameplayTag tag,
        in AbilityEventData eventData)
    {
        if (weapon == null || weapon.abilityLoadout is not FragmentBladeLoadout loadout)
            return;

        FragmentBladeRuntimeData data = GetBoundData();
        if (data == null || !data.IsSkill2Active || data.DetachedShardCount <= 0)
            return;

        if (eventData.Spec?.Definition != loadout.BaseAttack)
            return;

        if (eventData.Causer is GameObject causer && eventData.AbilitySystem != null && causer != eventData.AbilitySystem.gameObject)
            return;

        BeginSkill2Pierce(data, eventData);
    }

    /// <summary>
    /// 책임 :
    /// - 무기 해제, 씬 전환, 사망/스턴 같은 강제 종료 시 조각 이동과 trail을 끊고 runtime 상태를 full bound로 되돌린다.
    /// - visual actor가 붙기 전 단계에서도 gameplay data가 안전한 상태로 복구되게 한다.
    /// </summary>
    public void CancelActiveShardMotions()
    {
        presentationActor?.CancelActiveShardMotions();
        ClearRecallAttackBlockTag();
        pendingRecallShardCount = 0;
        GetBoundData()?.RebindAllShards();
    }

    private void OnDisable()
    {
        CancelActiveShardMotions();
    }

    private void CacheInventory()
    {
        if (weaponInventory == null)
            weaponInventory = GetComponentInParent<WeaponInventory2D>();
    }

    private void CacheOwnerTags()
    {
        if (ownerTags == null)
            ownerTags = GetComponentInParent<TagSystem>();
    }

    private void CachePresentationActor()
    {
        if (presentationActor == null)
            presentationActor = GetComponentInChildren<FragmentBladePresentationActor>(true);
    }

    private FragmentBladeRuntimeData GetBoundData()
    {
        CacheInventory();
        return weaponInventory != null
            ? weaponInventory.ActiveRuntimeData as FragmentBladeRuntimeData
            : null;
    }

    /// <summary>
    /// 책임 :
    /// - Skill1 AD가 성공했을 때 detached 조각을 회수 상태로 바꾸고 actor 이동/피해를 시작한다.
    /// - 회수 가능성 판단은 selection strategy가 먼저 수행하며, 이 메서드는 성공 실행 후 상태 전이에만 집중한다.
    /// </summary>
    public void BeginRecallFromAbility(
        CombatHitPayload recallPayload,
        LayerMask recallDamageLayers,
        GameObject ignoreTarget)
    {
        FragmentBladeRuntimeData data = GetBoundData();
        if (data == null)
            return;

        var recallTargets = data.CollectDetachedShardsForRecall();
        if (recallTargets.Count == 0)
            return;

        BeginRecallAttackBlock(recallTargets.Count);

        for (int i = 0; i < recallTargets.Count; i++)
        {
            FragmentBladeRuntimeData.ShardRuntimeState shard = recallTargets[i];
            if (shard == null)
                continue;

            shard.MarkReturning(shard.WorldPosition);
        }

        if (presentationActor == null)
        {
            CompleteRecallAttackBlock();
            for (int i = 0; i < recallTargets.Count; i++)
                data.CompleteRecall(recallTargets[i]);

            return;
        }

        presentationActor.BeginRecall(
            recallTargets,
            recallPayload,
            recallDamageLayers,
            ignoreTarget,
            (shardId, arrivedPosition) => HandleShardRecallCompleted(data, shardId, arrivedPosition));
    }

    private void HandleShardRecallCompleted(
        FragmentBladeRuntimeData data,
        int shardId,
        Vector3 arrivedPosition)
    {
        FragmentBladeRuntimeData.ShardRuntimeState shard = FindShardById(data, shardId);
        if (shard != null)
        {
            shard.UpdateWorldPosition(arrivedPosition);
            data.CompleteRecall(shard);
        }

        CompleteOneRecallShard();
    }

    private void BeginRecallAttackBlock(int shardCount)
    {
        pendingRecallShardCount = Mathf.Max(0, shardCount);
        if (pendingRecallShardCount <= 0)
            return;

        CacheOwnerTags();
        if (ownerTags == null || recallAttackBlockTagApplied)
            return;

        ownerTags.AddTagByPath(RecallAttackBlockedTagPath);
        recallAttackBlockTagApplied = true;
    }

    private void CompleteOneRecallShard()
    {
        if (pendingRecallShardCount > 0)
            pendingRecallShardCount--;

        if (pendingRecallShardCount <= 0)
            ClearRecallAttackBlockTag();
    }

    private void CompleteRecallAttackBlock()
    {
        pendingRecallShardCount = 0;
        ClearRecallAttackBlockTag();
    }

    private void ClearRecallAttackBlockTag()
    {
        if (!recallAttackBlockTagApplied)
            return;

        CacheOwnerTags();
        ownerTags?.RemoveTagByPath(RecallAttackBlockedTagPath);
        recallAttackBlockTagApplied = false;
    }

    private static FragmentBladeRuntimeData.ShardRuntimeState FindShardById(FragmentBladeRuntimeData data, int shardId)
    {
        if (data == null)
            return null;

        var shards = data.Shards;
        for (int i = 0; i < shards.Count; i++)
        {
            if (shards[i] != null && shards[i].Id == shardId)
                return shards[i];
        }

        return null;
    }

    private void BeginSkill2Pierce(FragmentBladeRuntimeData data, in AbilityEventData eventData)
    {
        if (data == null || eventData.AbilitySystem == null || eventData.Spec?.Definition == null)
            return;

        FragmentBladeAttackData attackData = eventData.Spec.Definition.sourceObject as FragmentBladeAttackData;
        if (attackData == null)
            return;

        var pierceTargets = data.CollectDetachedShardsForRecall();
        if (pierceTargets.Count == 0)
            return;

        Vector3 pierceTargetPosition = ResolvePierceTargetPosition(eventData);
        Vector3 fallbackDirection = ResolvePierceFallbackDirection(eventData, pierceTargetPosition);

        CombatHitPayload payload = FragmentBladeDamageUtility.BuildPayload(
            eventData.AbilitySystem,
            eventData.Spec,
            attackData.DamageConfig,
            attackData.damageEffect,
            attackData.knockbackEffect,
            attackData.damageFormula,
            attackData.knockbackFormula,
            attackData.legacyDamage,
            attackData.legacyStaggerDamage,
            attackData.piercingDamageScale,
            null);

        if (payload == null)
            return;

        for (int i = 0; i < pierceTargets.Count; i++)
        {
            FragmentBladeRuntimeData.ShardRuntimeState shard = pierceTargets[i];
            if (shard == null)
                continue;

            shard.MarkPiercing(shard.WorldPosition);
        }

        presentationActor?.BeginPierce(
            pierceTargets,
            pierceTargetPosition,
            fallbackDirection,
            attackData.piercingOvershootDistance,
            attackData.piercingDurationSeconds,
            payload,
            attackData.hitLayers,
            eventData.AbilitySystem.gameObject,
            (shardId, arrivedPosition) =>
            {
                FragmentBladeRuntimeData.ShardRuntimeState shard = FindShardById(data, shardId);
                if (shard == null || shard.IsBound)
                    return;

                shard.MarkDetachedIdle(arrivedPosition);
            });
    }

    private static Vector3 ResolvePierceTargetPosition(in AbilityEventData eventData)
    {
        if (eventData.Target != null)
            return eventData.Target.transform.position;

        if (eventData.WorldPosition != Vector3.zero)
            return eventData.WorldPosition;

        if (eventData.AbilitySystem != null)
            return eventData.AbilitySystem.transform.position;

        return Vector3.zero;
    }

    private static Vector3 ResolvePierceFallbackDirection(
        in AbilityEventData eventData,
        Vector3 targetPosition)
    {
        Vector3 direction = Vector3.right;

        if (eventData.AbilitySystem != null)
        {
            direction = targetPosition - eventData.AbilitySystem.transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = AbilityAimResolver2D.Resolve(eventData.AbilitySystem.gameObject, Vector2.right);
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        direction.z = 0f;
        return direction.normalized;
    }

    private Vector3 ResolveDropPosition()
    {
        Vector2 origin = transform.position;
        float radius = Mathf.Max(0f, detachRadius);
        int attempts = Mathf.Max(1, detachPlacementAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle;
            if (offset.sqrMagnitude <= 0.0001f)
                offset = Vector2.right;

            Vector2 candidate = origin + offset.normalized * radius;
            if (!Physics2D.Linecast(origin, candidate, detachObstacleMask))
                return candidate;
        }

        return origin;
    }
}
