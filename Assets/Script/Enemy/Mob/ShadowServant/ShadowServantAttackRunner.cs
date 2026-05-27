using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - ShadowServant 공격 1회의 경고, 대기, 폭발, 안개 생성을 순서대로 실행한다.
/// - AbilitySpec 취소 토큰과 MobAbilityCoordinator에 종속되어 ASC 생명주기와 함께 정리되도록 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ShadowServant))]
public class ShadowServantAttackRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    // 이 클래스의 책임:
    // ShadowServant 공격 1회의 경고-대기-폭발 실행을 담당하고, 경고 연출 설정은 owner의 AL 패턴 데이터에서 읽어 사용한다.

    public readonly struct AttackContext
    {
        public readonly GameObject TargetObject;
        public readonly Vector3 TargetPoint;
        public readonly Vector3 HitPoint;
        public readonly float WarningDiameter;
        public readonly float DelaySeconds;
        public readonly LayerMask DamageMask;

        public AttackContext(
            GameObject targetObject,
            Vector3 targetPoint,
            Vector3 hitPoint,
            float warningDiameter,
            float delaySeconds,
            LayerMask damageMask)
        {
            TargetObject = targetObject;
            TargetPoint = targetPoint;
            HitPoint = hitPoint;
            WarningDiameter = warningDiameter;
            DelaySeconds = delaySeconds;
            DamageMask = damageMask;
        }
    }

    public const float DefaultAttackDelay = 2f;

    [SerializeField] private ShadowServant owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private readonly HashSet<GameObject> damagedTargets = new();
    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<ShadowServant>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        warningStyle = MakeWarningStyle();
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null)
            yield break;

        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this))
            yield break;

        if (!owner.TryCreateAttackContext(initialTarget, owner.GetAttackPatternData().warningDuration, out AttackContext context))
        {
            abilityCoordinator?.EndRunner(this);
            yield break;
        }

        isRunning = true;
        cancelRequested = false;

        try
        {
            float delaySeconds = CombatTimingService.ScaleSeconds(system, context.DelaySeconds, CombatTimingSlot.AttackWarning);
            ShowWarning(context, delaySeconds);

            if (delaySeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, delaySeconds);

            if (IsCancelled(spec) || cancelRequested || owner.IsDead || IsSuppressed())
                yield break;

            owner.PlayAttackPresentation(context.HitPoint);
            Explode(system, spec, context);
            owner.SpawnFog(context.TargetPoint);
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
    }

    private bool IsSuppressed()
    {
        return abilityCoordinator != null && abilityCoordinator.IsAbilityExecutionSuppressed;
    }

    private void ShowWarning(AttackContext context, float duration)
    {
        if (telegraphService == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            context.HitPoint,
            context.WarningDiameter,
            duration,
            warningStyle);

        telegraphService.Show(spec);
    }

    private void HideWarning()
    {
        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    /// <summary>
    /// 책임 :
    /// - ShadowServant 공격 경고 telegraph가 suppression / death / disable 뒤에도 남지 않게 공통 presentation cleanup 계약으로 정리한다.
    /// - 전투 객체가 runner 구체 타입을 몰라도 시각 자원을 일괄 정리하게 돕는다.
    /// </summary>
    public void CleanupPresentation()
    {
        HideWarning();
    }

    private void Explode(AbilitySystem system, AbilitySpec spec, AttackContext context)
    {
        CombatHitPayload payload = owner.MakeHitPayload(system, spec);
        if (payload == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            context.TargetPoint,
            owner.GetFogRadius(),
            context.DamageMask);

        damagedTargets.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            GameObject hitTarget = CombatTargetResolver2D.ResolveDamageTarget(hit);

            if (hitTarget == null || hitTarget == owner.gameObject)
                continue;

            if (!damagedTargets.Add(hitTarget))
                continue;

            CombatHitPayloadApplier.Apply(hitTarget, payload, hit.ClosestPoint(context.TargetPoint));
        }
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private AttackTelegraphStyle MakeWarningStyle()
    {
        AbilityLogic_ShadowServantAttack.PatternData data = owner != null
            ? owner.GetAttackPatternData()
            : default;

        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerAreaColors(style);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = data.warningBlinkStartNormalized;
        style.blinkFrequency = data.warningBlinkFrequency;
        style.blinkAlphaMin = data.warningBlinkAlphaMin;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    private void OnDestroy()
    {
        if (warningStyle != null)
            Destroy(warningStyle);
    }
}
