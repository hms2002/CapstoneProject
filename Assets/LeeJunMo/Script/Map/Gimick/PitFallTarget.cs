using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - HoleTrap이 감지한 Collider를 공통 낙하 실행에 필요한 대상 정보로 정규화한다.
/// - 플레이어/보스/낙하 반응 구현체가 서로 다른 후처리를 가져도 같은 PitFallContext로 이어지게 한다.
/// </summary>
public readonly struct PitFallTarget
{
    private const string PlayerTag = "Player";
    private static readonly Vector2 SlimeQueenRespawnPosition = new Vector2(-22f, 4f);

    public readonly AbilitySystem AbilitySystem;
    public readonly SafetyTracker SafetyTracker;
    public readonly Transform Transform;
    public readonly GameObject GameObject;
    public readonly Vector3 RespawnPosition;
    public readonly IPitFallReaction Reaction;
    public readonly PitFallTargetKind Kind;

    private PitFallTarget(
        AbilitySystem abilitySystem,
        SafetyTracker safetyTracker,
        Transform transform,
        Vector3 respawnPosition,
        PitFallTargetKind kind,
        IPitFallReaction reaction = null)
    {
        AbilitySystem = abilitySystem;
        SafetyTracker = safetyTracker;
        Transform = transform;
        GameObject = transform != null ? transform.gameObject : null;
        RespawnPosition = respawnPosition;
        Kind = kind;
        Reaction = reaction;
    }

    public bool IsValid =>
        AbilitySystem != null &&
        Transform != null &&
        GameObject != null;

    public static bool TryCreate(Collider2D collider, out PitFallTarget target)
    {
        if (TryCreatePlayer(collider, out target))
            return true;

        if (TryCreateSlimeQueen(collider, out target))
            return true;

        return TryCreateReactionTarget(collider, out target);
    }

    public static bool TryCreatePlayer(Collider2D collider, out PitFallTarget target)
    {
        target = default;

        GameObject playerObject = ResolvePlayerObject(collider);
        if (playerObject == null)
            return false;

        AbilitySystem abilitySystem = playerObject.GetComponent<AbilitySystem>();
        SafetyTracker safetyTracker = playerObject.GetComponent<SafetyTracker>();
        if (safetyTracker == null)
            return false;

        target = new PitFallTarget(
            abilitySystem,
            safetyTracker,
            playerObject.transform,
            safetyTracker.GetRespawnPosition(),
            PitFallTargetKind.Player);

        return target.IsValid;
    }

    /// <summary>
    /// 책임:
    /// - HoleTrap이 직접 감지한 collider가 플레이어 CombatHurtbox2D인지 확인하고 플레이어 루트로 정규화한다.
    /// - 부모 체인을 타고 허트박스를 찾지 않아 공격 이펙트/센서 자식 collider가 플레이어 낙하로 오인되지 않게 한다.
    /// </summary>
    private static GameObject ResolvePlayerObject(Collider2D collider)
    {
        if (collider == null)
            return null;

        CombatHurtbox2D hurtbox = collider.GetComponent<CombatHurtbox2D>();
        if (hurtbox == null || !hurtbox.OwnsCollider(collider))
            return null;

        GameObject targetRoot = hurtbox.ResolveTargetRoot();
        if (targetRoot == null)
            return null;

        return targetRoot.CompareTag(PlayerTag)
            ? targetRoot
            : null;
    }

    private static bool TryCreateSlimeQueen(Collider2D collider, out PitFallTarget target)
    {
        target = default;

        if (collider == null)
            return false;

        SlimeQueenBossBase slimeQueen = collider.GetComponentInParent<SlimeQueenBossBase>();
        if (slimeQueen == null)
            return false;

        if (!slimeQueen.CanTriggerPitFall)
            return false;

        AbilitySystem abilitySystem = slimeQueen.AbilitySystem != null
            ? slimeQueen.AbilitySystem
            : slimeQueen.GetComponent<AbilitySystem>();

        Vector3 respawnPosition = new Vector3(
            SlimeQueenRespawnPosition.x,
            SlimeQueenRespawnPosition.y,
            slimeQueen.transform.position.z);

        target = new PitFallTarget(
            abilitySystem,
            null,
            slimeQueen.transform,
            respawnPosition,
            PitFallTargetKind.SlimeQueen);

        return target.IsValid;
    }

    /// <summary>
    /// 책임:
    /// - 플레이어/보스 전용 분기가 아닌 전투 오브젝트가 HoleTrap 공통 낙하 파이프라인에 참여하게 한다.
    /// - 대상별 사망, 분열 억제, cleanup 같은 후처리는 IPitFallReaction 구현체에 맡긴다.
    /// </summary>
    private static bool TryCreateReactionTarget(Collider2D collider, out PitFallTarget target)
    {
        target = default;

        if (collider == null)
            return false;

        IPitFallReaction reaction = ResolveReaction(collider);
        if (reaction == null)
            return false;

        Component reactionComponent = reaction as Component;
        if (reactionComponent == null)
            return false;

        AbilitySystem abilitySystem = reactionComponent.GetComponent<AbilitySystem>();
        if (abilitySystem == null)
            abilitySystem = reactionComponent.GetComponentInParent<AbilitySystem>();

        target = new PitFallTarget(
            abilitySystem,
            null,
            reactionComponent.transform,
            reactionComponent.transform.position,
            PitFallTargetKind.ReactionTarget,
            reaction);

        return target.IsValid;
    }

    /// <summary>충돌체 부모 체인에서 낙하 후처리를 받을 구현체를 찾습니다.</summary>
    private static IPitFallReaction ResolveReaction(Collider2D collider)
    {
        if (collider == null)
            return null;

        MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(includeInactive: false);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPitFallReaction reaction)
                return reaction;
        }

        return null;
    }
}

/// <summary>
/// 책임:
/// - HoleTrap이 정규화한 낙하 대상의 큰 분류를 표현한다.
/// - 구덩이 피해량처럼 대상군에 따라 달라지는 authoring 값을 이름/프리팹 체크 없이 결정하게 한다.
/// </summary>
public enum PitFallTargetKind
{
    Player,
    SlimeQueen,
    ReactionTarget
}
