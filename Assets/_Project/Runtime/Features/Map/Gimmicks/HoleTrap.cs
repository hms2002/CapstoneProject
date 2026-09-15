using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 구덩이 trigger에 닿은 유효 대상을 PitFallTarget으로 정규화하고 공통 낙하 실행을 시작한다.
/// - 대상별 후처리와 낙하 연출 세부 구현은 PitFallExecutor와 IPitFallReaction에 위임한다.
/// </summary>
public class HoleTrap : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float trapDamage = 10f;
    [SerializeField] private float playerTrapDamage = 1f;
    [SerializeField] private float trapDuration = 1.0f;

    [Header("GAS References")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GameplayEffect fallingEffect;

    [Header("Ignore Settings")]
    [Tooltip("플레이어 이외의 대상에 적용할 낙하 면제 태그입니다. 플레이어는 대시 중에도 낙하합니다.")]
    [SerializeField] private GameplayTag ignoreTag;
    [Tooltip("켜져 있으면 ignoreTag가 직접 부여된 경우만 무시하고, 하위 태그로 인한 부모 closure는 무시하지 않습니다.")]
    [SerializeField] private bool useExactIgnoreTag = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private readonly HashSet<GameObject> activeTargets = new();
    private Collider2D[] pitColliders;

    private void Awake()
    {
        pitColliders = GetComponents<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    private void CheckAndActivateTrap(Collider2D collision)
    {
        if (!TryBuildFallContext(collision, out PitFallContext context))
            return;

        if (!TryRegisterActiveTarget(context.TargetObject))
            return;

        StartCoroutine(ApplyTrapRoutine(context));
    }

    private bool TryBuildFallContext(Collider2D collision, out PitFallContext context)
    {
        context = default;

        if (!PitFallTarget.TryCreate(collision, out PitFallTarget target))
        {
            return false;
        }

        if (target.Kind == PitFallTargetKind.Player && !ContainsPlayerFoot(target.SafetyTracker.FootPosition))
            return false;

        if (target.Reaction != null && !target.Reaction.CanReactToPitFall(this))
        {
            LogDebug($"ignored target: reaction rejected. target={target.GameObject.name}, reaction={target.Reaction}");
            return false;
        }

        if (ignoreTag != null && target.Kind != PitFallTargetKind.Player)
        {
            TagSystem tagSystem = target.AbilitySystem.TagSystem;
            bool hasIgnoreTag = tagSystem != null && HasIgnoreTag(tagSystem);
            if (hasIgnoreTag)
            {
                LogDebug($"ignored target: has ignore tag. target={target.GameObject.name}, tag={ignoreTag.name}, exact={useExactIgnoreTag}");
                return false;
            }
        }

        Vector3 fallCenter = PitFallPositionResolver.ResolveFallCenter(target.Transform.position, gameObject);
        float damage = ResolveTrapDamage(target);

        context = new PitFallContext(
            target.AbilitySystem,
            target.SafetyTracker,
            target.Transform,
            gameObject,
            damageEffect,
            fallingEffect,
            damage,
            trapDuration,
            fallCenter,
            target.RespawnPosition,
            this,
            target.Reaction,
            logDebug);

        LogDebug($"context built. target={target.GameObject.name}, kind={target.Kind}, damage={damage:0.###}, duration={trapDuration:0.###}, respawn={target.RespawnPosition}, reaction={target.Reaction}");
        return context.IsValid;
    }

    /// <summary>
    /// 책임:
    /// - 정규화된 낙하 대상 분류에 따라 구덩이 피해량을 결정한다.
    /// - 플레이어 한정 피해량을 이름/태그 재검사 없이 공식 target kind로 분기한다.
    /// </summary>
    private float ResolveTrapDamage(PitFallTarget target)
    {
        return target.Kind == PitFallTargetKind.Player
            ? Mathf.Max(0f, playerTrapDamage)
            : Mathf.Max(0f, trapDamage);
    }

    private bool ContainsPlayerFoot(Vector2 foot)
    {
        foreach (Collider2D pit in pitColliders)
            if (PlayerPitFootprint2D.IsInside(pit, foot, PlayerPitFootprint2D.FallInset))
                return true;
        return false;
    }

    /// <summary>
    /// 책임:
    /// - 함정 무시 태그를 exact 또는 closure 포함 방식 중 인스펙터 정책에 맞게 판정한다.
    /// - 지속형 이동 기술의 하위 차단 태그가 대시 부모 태그로 과포괄되는 문제를 막는다.
    /// </summary>
    private bool HasIgnoreTag(TagSystem tagSystem)
    {
        if (tagSystem == null || ignoreTag == null)
            return false;

        return useExactIgnoreTag
            ? tagSystem.HasExplicitTag(ignoreTag)
            : tagSystem.HasTag(ignoreTag);
    }

    private IEnumerator ApplyTrapRoutine(PitFallContext context)
    {
        try
        {
            yield return PitFallExecutor.Execute(context);
        }
        finally
        {
            UnregisterActiveTarget(context.TargetObject);
        }
    }

    /// <summary>
    /// 책임:
    /// - 같은 대상이 OnTriggerStay로 낙하 처리를 중복 시작하지 않도록 대상별 실행 잠금을 건다.
    /// - 구덩이 전체가 아니라 대상 단위로 잠가 여러 몬스터가 동시에 떨어질 수 있게 한다.
    /// </summary>
    private bool TryRegisterActiveTarget(GameObject targetObject)
    {
        if (targetObject == null)
            return false;

        bool registered = activeTargets.Add(targetObject);
        if (!registered)
            LogDebug($"ignored target: already falling. target={targetObject.name}");

        return registered;
    }

    /// <summary>낙하 처리가 끝난 대상을 실행 잠금 목록에서 제거합니다.</summary>
    private void UnregisterActiveTarget(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        activeTargets.Remove(targetObject);
    }

    private void LogDebug(string message)
    {
        if (!logDebug)
            return;

        Debug.Log($"[HoleTrap] {name}: {message}", this);
    }

}

/// <summary>Responsibility: share the inset foot-area test between player walking and falling, independent of body size.</summary>
public static class PlayerPitFootprint2D
{
    public const float FallInset = 0.1f;
    // Walking stops at the outer boundary; falling keeps its independent inset.
    public const float WalkingInset = 0f;

    public static Vector2 GetExitDirection(Collider2D pit, Vector2 foot)
    {
        Vector2 exit = Vector2.zero;
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2 offset = new Vector2(x, y);
                if (!pit.OverlapPoint(foot + offset * FallInset)) exit += offset;
            }
        return exit.normalized;
    }

    public static bool IsInside(Collider2D pit, Vector2 foot, float inset)
    {
        if (pit == null || !pit.enabled || !pit.gameObject.activeInHierarchy || !pit.OverlapPoint(foot))
            return false;
        if (inset <= 0f)
            return true;
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (!pit.OverlapPoint(foot + new Vector2(x * inset, y * inset)))
                    return false;
        return true;
    }
}
