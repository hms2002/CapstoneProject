using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 고블린 전사가 만든 돌진 문맥을 받아 고정 경고선, 돌진 이동, 1회 플레이어 피해를 실행한다.
/// - 패턴 취소, disable, groggy 전환 시 경고와 이동 상태를 정리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinWarrior))]
public sealed class GoblinWarriorChargeRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private GoblinWarrior owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    [Header("Telegraph Clipping")]
    [SerializeField] private LayerMask telegraphWallClipLayers = 1 << 30;
    [SerializeField, Min(3)] private int telegraphWallClipSampleCount = 48;
    [SerializeField, Min(0f)] private float telegraphWallClipSkinWidth = 0.03f;

    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;
    private bool hitTarget;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<GoblinWarrior>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = MakeWarningStyle();
    }

    private void OnDestroy()
    {
        if (warningStyle != null)
            Destroy(warningStyle);
    }

    private void OnDisable()
    {
        Cancel();
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildChargeContext(system, spec, initialTarget, out GoblinWarrior.ChargeContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;
        hitTarget = false;

        try
        {
            float warningSeconds = CombatTimingService.ScaleSeconds(system, context.WarningSeconds, CombatTimingSlot.AttackWarning);
            ShowWarning(context, warningSeconds);
            if (warningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, warningSeconds);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            yield return Dash(context, spec);
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            hitTarget = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
    }

    public void CleanupPresentation()
    {
        HideWarning();
    }

    private IEnumerator Dash(GoblinWarrior.ChargeContext context, AbilitySpec spec)
    {
        Vector2 direction = context.Direction.normalized;
        float speed = context.DashDistance / Mathf.Max(0.01f, context.DashSeconds);
        float duration = Mathf.Max(0.01f, context.DashSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            float deltaTime = Mathf.Min(Time.deltaTime, duration - elapsed);
            Vector2 desiredDelta = direction * (speed * deltaTime);
            Vector2 resolvedDelta = CommonMonsterCombatUtility.ResolveDashWallSlideDelta(
                transform.position,
                desiredDelta,
                context.DashCastRadius,
                context.DashObstacleLayers,
                context.DashWallSkinWidth);
            transform.position += (Vector3)resolvedDelta;
            TryHitTarget(context);
            elapsed += deltaTime;
            yield return null;
        }

        TryHitTarget(context);
    }

    private void ShowWarning(GoblinWarrior.ChargeContext context, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        Vector3 center = (Vector3)context.StartPosition + (Vector3)(context.Direction.normalized * context.DashDistance * 0.5f);
        float angle = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(context.DashDistance, context.WarningWidth),
            angle,
            warningSeconds,
            warningStyle)
            .WithWallClipping(
                telegraphWallClipLayers,
                telegraphWallClipSampleCount,
                telegraphWallClipSkinWidth);

        telegraphService.Show(spec);
    }

    private void HideWarning()
    {
        telegraphService?.HideCurrent();
    }

    private void TryHitTarget(GoblinWarrior.ChargeContext context)
    {
        if (hitTarget)
            return;

        if (CommonMonsterCombatUtility.TryApplyCircleDamage(
                transform.position,
                context.WarningWidth,
                context.TargetLayers,
                gameObject,
                context.HitPayload))
        {
            hitTarget = true;
        }
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private static AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerAreaColors(style);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.72f;
        style.blinkFrequency = 5f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
