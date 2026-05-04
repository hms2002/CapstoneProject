using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bishop))]
public class BishopLineBlastRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private Bishop owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private readonly List<Vector3> blastPoints = new();
    private readonly List<AttackTelegraphView> blastViews = new();
    private AttackTelegraphStyle lineStyle;
    private AttackTelegraphStyle blastStyle;
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
        blastStyle = MakeBlastStyle();
    }

    private void OnDestroy()
    {
        if (lineStyle != null)
            Destroy(lineStyle);

        if (blastStyle != null)
            Destroy(blastStyle);
    }

    private void OnDisable()
    {
        HideLine();
        ClearBlastViews();
    }

    /// <summary>비숍의 경고선과 동시 폭발 공격을 실행합니다.</summary>
    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildLineContext(initialTarget, out currentContext)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            ShowLine(currentContext);

            if (currentContext.WarningTime > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, currentContext.WarningTime);

            if (cancelRequested || owner.IsDead) yield break;

            FireBlasts(system, spec, currentContext);

            if (currentContext.BlastViewTime > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, currentContext.BlastViewTime);
        }
        finally
        {
            HideLine();
            ClearBlastViews();
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
        ClearBlastViews();
    }

    /// <summary>남아 있는 비숍 공격 경고를 정리합니다.</summary>
    public void CleanupPresentation()
    {
        HideLine();
        ClearBlastViews();
    }

    /// <summary>비숍의 긴 직사각형 경고선을 표시합니다.</summary>
    private void ShowLine(Bishop.LineBlastContext context)
    {
        if (telegraphService == null) return;

        float angleDeg = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            context.Center,
            new Vector2(context.HalfLength * 2f, context.WarningWidth),
            angleDeg,
            context.WarningTime,
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
    private void FireBlasts(AbilitySystem system, AbilitySpec spec, Bishop.LineBlastContext context)
    {
        owner.FillBlastPoints(context, blastPoints);
        ShowBlastViews(context);
        owner.TryHitBlasts(system, spec, context, blastPoints);
    }

    /// <summary>원형 폭발 표시들을 생성합니다.</summary>
    private void ShowBlastViews(Bishop.LineBlastContext context)
    {
        if (telegraphService == null) return;

        ClearBlastViews();

        for (int i = 0; i < blastPoints.Count; i++)
        {
            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
                blastPoints[i],
                context.BlastDiameter,
                context.BlastViewTime,
                blastStyle);

            AttackTelegraphView view = telegraphService.SpawnDetachedView(spec);
            if (view != null)
                blastViews.Add(view);
        }
    }

    /// <summary>생성된 원형 폭발 표시들을 제거합니다.</summary>
    private void ClearBlastViews()
    {
        for (int i = 0; i < blastViews.Count; i++)
        {
            AttackTelegraphView view = blastViews[i];
            if (view != null)
                Destroy(view.gameObject);
        }

        blastViews.Clear();
    }

    /// <summary>비숍의 긴 경고선 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeLineStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0.95f, 0.12f);
        style.fillColorEnd = new Color(1f, 0f, 0.95f, 0.24f);
        style.borderColorStart = new Color(1f, 0.15f, 0.95f, 0.95f);
        style.borderColorEnd = new Color(1f, 0.15f, 0.95f, 0.95f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.7f;
        style.blinkFrequency = 4f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>비숍의 원형 폭발 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeBlastStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0.95f, 0.4f);
        style.fillColorEnd = new Color(1f, 0f, 0.95f, 0.15f);
        style.borderColorStart = new Color(1f, 0.35f, 1f, 1f);
        style.borderColorEnd = new Color(1f, 0.35f, 1f, 0.5f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
