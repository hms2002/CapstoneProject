using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Wizard 산탄 공격의 준비 대기, 발사 호출, runner 생명주기를 관리한다.
/// - 실제 투사체 생성과 피해 payload 구성은 Wizard 본체에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Wizard))]
public class WizardScatterShotRunner : MonoBehaviour, IMobPatternRunner
{
    [SerializeField] private Wizard owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private MonoBehaviour telegraphService;

    [Header("Telegraph Clipping")]
    [SerializeField] private LayerMask telegraphWallClipLayers = 1 << 30;
    [SerializeField, Min(3)] private int telegraphWallClipSampleCount = 48;
    [SerializeField, Min(0f)] private float telegraphWallClipSkinWidth = 0.03f;

    private AttackTelegraphStyle scatterTelegraphStyle;
    private IAttackTelegraphPresenter telegraphPresenter;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<Wizard>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        telegraphPresenter = AttackTelegraphPresenterResolver.Resolve(telegraphService, this);

        scatterTelegraphStyle = MakeScatterTelegraphStyle();
    }

    private void OnDestroy()
    {
        if (scatterTelegraphStyle != null)
            Destroy(scatterTelegraphStyle);
    }

    private void OnDisable()
    {
        HideTelegraph();
    }

    /// <summary>마법사의 산탄 발사를 한 번 실행합니다.</summary>
    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildShotContext(system, spec, initialTarget, out Wizard.ScatterShotContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            float prepareSeconds = CombatTimingService.ScaleSeconds(system, context.PrepareSeconds, CombatTimingSlot.AttackWarning);
            ShowTelegraph(context, prepareSeconds);
            owner.PlayAttackPrepareAnimation();
            if (prepareSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, prepareSeconds);

            if (cancelRequested || owner.IsDead || IsCancelled(spec)) yield break;

            HideTelegraph();
            owner.PlayAttackAnimation();
            owner.FireScatterShot(context);
            yield return null;
        }
        finally
        {
            HideTelegraph();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    /// <summary>실행 중인 산탄 공격을 취소 상태로 바꿉니다.</summary>
    public void Cancel()
    {
        cancelRequested = true;
        HideTelegraph();
    }

    /// <summary>
    /// 책임:
    /// - Wizard 산탄 공격의 발사 방향과 퍼짐 각도를 플레이어에게 미리 보여준다.
    /// - 실제 투사체 생성과 분리해 경고 표시 생명주기만 관리한다.
    /// </summary>
    private void ShowTelegraph(Wizard.ScatterShotContext context, float duration)
    {
        if (telegraphPresenter == null)
            return;

        if (context.Direction.sqrMagnitude <= 0.0001f)
            return;

        float angleDeg = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateSector(
            context.Origin,
            context.TelegraphRange,
            context.TelegraphAngle,
            angleDeg,
            duration,
            scatterTelegraphStyle)
            .WithWallClipping(
                telegraphWallClipLayers,
                telegraphWallClipSampleCount,
                telegraphWallClipSkinWidth);

        telegraphPresenter.Show(spec);
    }

    /// <summary>현재 표시 중인 Wizard 산탄 경고를 즉시 숨깁니다.</summary>
    private void HideTelegraph()
    {
        telegraphPresenter?.HideCurrent();
    }

    /// <summary>어빌리티 취소 여부를 확인합니다.</summary>
    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    /// <summary>Wizard 산탄 공격의 표준 위험 부채꼴 경고 스타일을 만듭니다.</summary>
    private static AttackTelegraphStyle MakeScatterTelegraphStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerAreaColors(style);
        style.progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.65f;
        style.blinkFrequency = 7f;
        style.blinkAlphaMin = 0.62f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
