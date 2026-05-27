using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Bishop이 만든 직선 마법 문맥을 받아 경고 표시, 폭발 표시, 피해 판정을 실행한다.
/// - 패턴 취소/정리 시 남은 경고와 폭발 표시를 회수한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Bishop))]
public class BishopLineBlastRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    /// <summary>
    /// 책임:
    /// - AL이 소유한 Bishop 폭발 VFX authoring 값을 Runner에 전달하기 위한 불변 실행 설정이다.
    /// - Runner가 ScriptableObject 직렬화 데이터에 직접 의존하지 않도록 경계를 만든다.
    /// </summary>
    public readonly struct BlastEffectConfig
    {
        public readonly GameObject Prefab;
        public readonly float ScaleMultiplier;
        public readonly bool AlignToLine;
        public readonly float FallbackLifetime;

        public BlastEffectConfig(GameObject prefab, float scaleMultiplier, bool alignToLine, float fallbackLifetime)
        {
            Prefab = prefab;
            ScaleMultiplier = scaleMultiplier;
            AlignToLine = alignToLine;
            FallbackLifetime = Mathf.Max(0.05f, fallbackLifetime);
        }
    }

    [SerializeField] private Bishop owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private readonly List<Vector3> blastPoints = new();
    private AttackTelegraphStyle lineStyle;
    private Bishop.LineBlastContext currentContext;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<Bishop>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        lineStyle = MakeLineStyle();
    }

    private void OnDestroy()
    {
        if (lineStyle != null)
            Destroy(lineStyle);

    }

    private void OnDisable()
    {
        HideLine();
    }

    /// <summary>비숍의 경고선과 동시 폭발 공격을 실행합니다.</summary>
    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget, BlastEffectConfig blastEffectConfig)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildLineContext(initialTarget, out currentContext)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            float warningSeconds = CombatTimingService.ScaleSeconds(system, currentContext.WarningTime, CombatTimingSlot.AttackWarning);
            ShowLine(currentContext, warningSeconds);
            owner.PlayMagicPrepareAnimation();

            if (warningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, warningSeconds);

            if (cancelRequested || owner.IsDead) yield break;

            FireBlasts(system, spec, currentContext, blastEffectConfig);

            if (currentContext.BlastViewTime > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, currentContext.BlastViewTime);
        }
        finally
        {
            HideLine();
            blastPoints.Clear();
            currentContext = default;
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    /// <summary>비숍 공격 실행을 취소합니다.</summary>
    public void Cancel()
    {
        cancelRequested = true;
        HideLine();
    }

    /// <summary>남아 있는 비숍 공격 경고를 정리합니다.</summary>
    public void CleanupPresentation()
    {
        HideLine();
    }

    /// <summary>비숍의 긴 직사각형 경고선을 표시합니다.</summary>
    private void ShowLine(Bishop.LineBlastContext context, float duration)
    {
        if (telegraphService == null) return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateLine(
            context.LineStart,
            context.LineEnd,
            context.WarningWidth,
            duration,
            lineStyle);

        telegraphService.Show(spec);
    }

    /// <summary>현재 표시 중인 긴 경고선을 숨깁니다.</summary>
    private void HideLine()
    {
        if (telegraphService == null) return;

        telegraphService.HideCurrent();
    }

    /// <summary>경고선 위의 원형 폭발들을 동시에 발생시킵니다.</summary>
    private void FireBlasts(AbilitySystem system, AbilitySpec spec, Bishop.LineBlastContext context, BlastEffectConfig blastEffectConfig)
    {
        owner.PlayMagicCastAnimation();
        owner.FillBlastPoints(context, blastPoints);
        SpawnBlastEffects(context, blastEffectConfig);
        owner.TryHitBlasts(system, spec, context, blastPoints);
    }

    /// <summary>
    /// 책임:
    /// - Bishop 직선 마법의 실제 폭발 지점마다 독립 VFX를 생성한다.
    /// - VFX 프리팹 구현 방식이 Particle/Animator/단순 GameObject 중 무엇이어도 수명 뒤 자동 제거한다.
    /// </summary>
    private void SpawnBlastEffects(Bishop.LineBlastContext context, BlastEffectConfig config)
    {
        if (config.Prefab == null || blastPoints.Count == 0)
            return;

        Quaternion rotation = config.AlignToLine
            ? Quaternion.Euler(0f, 0f, Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg)
            : Quaternion.identity;

        float scale = Mathf.Max(0.01f, context.BlastDiameter * config.ScaleMultiplier);
        for (int i = 0; i < blastPoints.Count; i++)
        {
            GameObject effect = Instantiate(config.Prefab, blastPoints[i], rotation);
            if (effect == null)
                continue;

            effect.transform.localScale = Vector3.Scale(effect.transform.localScale, new Vector3(scale, scale, 1f));
            Destroy(effect, ResolveEffectLifetime(effect, config.FallbackLifetime));
        }
    }

    /// <summary>
    /// 책임:
    /// - 이펙트 프리팹의 재생 컴포넌트 정보를 읽어 자동 제거 시간을 산출한다.
    /// - 정보가 부족하면 authoring fallback 수명으로 제거해 누수를 막는다.
    /// </summary>
    private float ResolveEffectLifetime(GameObject effect, float fallbackLifetime)
    {
        float lifetime = Mathf.Max(0.05f, fallbackLifetime);

        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            ParticleSystem.MainModule main = particle.main;
            float startLifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                ? main.startLifetime.constant
                : main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, main.duration + startLifetime);
        }

        Animator[] animators = effect.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int j = 0; j < clips.Length; j++)
            {
                AnimationClip clip = clips[j];
                if (clip != null)
                    lifetime = Mathf.Max(lifetime, clip.length);
            }
        }

        return lifetime;
    }

    /// <summary>비숍의 긴 경고선 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeLineStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerLineColors(style);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.7f;
        style.blinkFrequency = 4f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

}
