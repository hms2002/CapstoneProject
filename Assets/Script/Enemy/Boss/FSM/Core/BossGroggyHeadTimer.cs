using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 대상 보스에 걸린 그로기 GameplayEffect의 남은 시간을 읽어 머리 위 원형 타이머를 갱신한다.
/// - 링 fill, 색 변화, 종료 직전 pulse를 한 곳에서 제어해 플레이어가 스턴 종료 시점을 직관적으로 읽게 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroggyHeadTimer : MonoBehaviour
{
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static int TempEnemyLayer;

    [Header("Binding")]
    [SerializeField] private BossControllerBase targetBoss;
    [SerializeField] private GameplayEffect groggyEffect;
    [SerializeField] private GameplayEffectRunner effectRunner;
    [SerializeField] private StaggerGaugeSystem staggerGaugeSystem;
    [SerializeField] private Transform followAnchor;

    [Header("Visual")]
    [SerializeField] private GameObject timerRoot;
    [SerializeField] private SpriteRenderer fillRingRenderer;
    [SerializeField] private SpriteRenderer backgroundRingRenderer;
    [SerializeField] private Vector3 fallbackWorldOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float spriteTopPadding = 0.2f;
    [SerializeField] private Color fullTimeColor = new Color(0.95f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color lowTimeColor = new Color(1f, 0.35f, 0.25f, 1f);

    [Header("Low Time Pulse")]
    [SerializeField] private bool pulseNearEnd = true;
    [SerializeField] private float nearEndThresholdNormalized = 0.33f;
    [SerializeField] private float pulseSpeed = 12f;
    [SerializeField] private float pulseAmplitude = 0.14f;

    private MaterialPropertyBlock propertyBlock;
    private Vector3 initialTimerScale = Vector3.one;

    /// <summary>
    /// 책임 :
    /// - 스태거 시스템이 생성한 타이머 인스턴스에 기본 바인딩을 일괄 주입한다.
    /// - 프리팹 쪽 시각 세팅은 유지한 채 대상 보스/이펙트 참조만 런타임에서 안전하게 연결한다.
    /// </summary>
    public void ConfigureForBoss(
        BossControllerBase boss,
        StaggerGaugeSystem gaugeSystem,
        GameplayEffect effect,
        GameplayEffectRunner runner = null)
    {
        targetBoss = boss;
        staggerGaugeSystem = gaugeSystem;
        groggyEffect = effect;
        effectRunner = runner != null ? runner : boss != null ? boss.GetComponent<GameplayEffectRunner>() : null;
        followAnchor = boss != null ? boss.transform : followAnchor;
    }

    private void Awake()
    {
        if (targetBoss == null)
            targetBoss = GetComponent<BossControllerBase>();

        if (effectRunner == null && targetBoss != null)
            effectRunner = targetBoss.GetComponent<GameplayEffectRunner>();

        if (staggerGaugeSystem == null && targetBoss != null)
            staggerGaugeSystem = targetBoss.GetComponent<StaggerGaugeSystem>();

        if (followAnchor == null && targetBoss != null)
            followAnchor = targetBoss.transform;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        TempEnemyLayer = LayerMask.NameToLayer("TEMP_Enemy_LAYER");

        initialTimerScale = GetPulseTarget().localScale;

        SetVisible(false);
    }

    private void Update()
    {
        if (targetBoss == null || groggyEffect == null)
        {
            SetVisible(false);
            return;
        }

        if (effectRunner == null)
            effectRunner = targetBoss.GetComponent<GameplayEffectRunner>();

        if (effectRunner == null || groggyEffect.duration <= 0f)
        {
            SetVisible(false);
            return;
        }

        float remaining = effectRunner.GetRemainingTime(groggyEffect, targetBoss.gameObject);
        if (remaining <= 0.001f)
        {
            SetVisible(false);
            return;
        }

        float normalized = Mathf.Clamp01(remaining / groggyEffect.duration);
        SetVisible(true);
        UpdateTransform();
        UpdateFill(normalized);
        UpdateColor(normalized);
        UpdatePulse(normalized);
    }

    /// <summary>
    /// 책임 :
    /// - 스태거 시스템이 허용한 경우 그 오프셋을 우선 적용한다.
    /// - 오프셋이 비활성일 때는 대표 스프라이트의 상단 위에 타이머를 자동 배치한다.
    /// - 링 자체는 월드 공간 오브젝트로 유지해 씬 내 읽힘을 확보한다.
    /// </summary>
    private void UpdateTransform()
    {
        if (timerRoot == null)
            return;

        timerRoot.transform.position = ResolveWorldPosition();
    }

    /// <summary>
    /// 책임 :
    /// - 머티리얼 프로퍼티 블록으로 남은 시간 비율을 링 셰이더에 전달한다.
    /// - 공유 머티리얼을 바꾸지 않고도 각 보스 인스턴스 타이머를 독립적으로 표현한다.
    /// </summary>
    private void UpdateFill(float normalized)
    {
        if (fillRingRenderer == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        fillRingRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FillAmountId, normalized);
        fillRingRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateColor(float normalized)
    {
        if (fillRingRenderer != null)
            fillRingRenderer.color = Color.Lerp(lowTimeColor, fullTimeColor, normalized);

        if (backgroundRingRenderer != null)
            backgroundRingRenderer.color = new Color(fullTimeColor.r, fullTimeColor.g, fullTimeColor.b, 0.2f);
    }

    private void UpdatePulse(float normalized)
    {
        if (!pulseNearEnd)
            return;

        Transform pulseTarget = GetPulseTarget();
        if (pulseTarget == null)
            return;

        if (normalized > nearEndThresholdNormalized)
        {
            pulseTarget.localScale = initialTimerScale;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        pulseTarget.localScale = initialTimerScale * pulse;
    }

    private void SetVisible(bool isVisible)
    {
        if (timerRoot != null)
            timerRoot.SetActive(isVisible);

        if (fillRingRenderer != null)
            fillRingRenderer.enabled = isVisible;

        if (backgroundRingRenderer != null)
            backgroundRingRenderer.enabled = isVisible;

        Transform pulseTarget = GetPulseTarget();
        if (!isVisible && pulseTarget != null)
            pulseTarget.localScale = initialTimerScale;
    }

    /// <summary>
    /// 책임 :
    /// - 타이머의 월드 배치 기준을 한 곳에서 계산한다.
    /// - 스태거 게이지 표현 오프셋과 자동 스프라이트 상단 배치를 통합해 호출부를 단순화한다.
    /// </summary>
    private Vector3 ResolveWorldPosition()
    {
        if (staggerGaugeSystem != null && staggerGaugeSystem.AllowPresentationOffset)
        {
            Transform baseAnchor = staggerGaugeSystem.PresentationAnchor != null
                ? staggerGaugeSystem.PresentationAnchor
                : followAnchor != null ? followAnchor : targetBoss != null ? targetBoss.transform : transform;
            return baseAnchor.position + staggerGaugeSystem.PresentationWorldOffset;
        }

        if (TryGetSpriteTopPosition(out Vector3 spriteTopPosition))
            return spriteTopPosition;

        Transform fallbackAnchor = followAnchor != null ? followAnchor : targetBoss != null ? targetBoss.transform : transform;
        return fallbackAnchor.position + fallbackWorldOffset;
    }

    /// <summary>
    /// 책임 :
    /// - 대상 보스의 대표 스프라이트 범위를 읽어 머리 위 기본 타이머 위치를 계산한다.
    /// - 명시적 오프셋을 쓰지 않는 보스도 별도 authoring 없이 자연스러운 기본 위치를 갖게 만든다.
    /// </summary>
    private bool TryGetSpriteTopPosition(out Vector3 position)
    {
        position = default;
        if (staggerGaugeSystem != null && staggerGaugeSystem.PresentationBoundsSource != null)
        {
            Bounds explicitBounds = staggerGaugeSystem.PresentationBoundsSource.bounds;
            position = new Vector3(
                explicitBounds.center.x,
                explicitBounds.max.y + spriteTopPadding,
                explicitBounds.center.z);
            return true;
        }

        if (targetBoss == null)
            return TryGetAnchorTopPosition(out position);

        SpriteRenderer[] renderers = targetBoss.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return TryGetAnchorTopPosition(out position);

        SpriteRenderer representative = FindRepresentativeRenderer(renderers, requireTempEnemyLayer: true);
        representative ??= FindRepresentativeRenderer(renderers, requireTempEnemyLayer: false);

        if (representative == null)
            return false;

        Bounds representativeBounds = representative.bounds;
        position = new Vector3(
            representativeBounds.center.x,
            representativeBounds.max.y + spriteTopPadding,
            representativeBounds.center.z);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 명시적 bounds source가 없는 경우 anchor 위치를 기준으로 최소한의 머리 위 위치를 제공한다.
    /// - 자동 탐색 결과가 없는 예외 상황에서도 타이머가 엉뚱한 대상에 붙지 않게 한다.
    /// </summary>
    private bool TryGetAnchorTopPosition(out Vector3 position)
    {
        position = default;
        Transform anchor = staggerGaugeSystem != null && staggerGaugeSystem.PresentationAnchor != null
            ? staggerGaugeSystem.PresentationAnchor
            : followAnchor != null ? followAnchor : targetBoss != null ? targetBoss.transform : null;

        if (anchor == null)
            return false;

        position = anchor.position + new Vector3(0f, spriteTopPadding, 0f);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 자동 배치에 쓸 대표 렌더러를 안정적으로 고른다.
    /// - TEMP_Enemy_LAYER를 우선 사용하고, 타이머 자신의 렌더러와 무관한 보조 오브젝트가 기준으로 뽑히는 일을 줄인다.
    /// </summary>
    private SpriteRenderer FindRepresentativeRenderer(SpriteRenderer[] renderers, bool requireTempEnemyLayer)
    {
        SpriteRenderer representative = null;
        float largestArea = -1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (!IsEligibleRepresentativeRenderer(renderer, requireTempEnemyLayer))
                continue;

            Bounds bounds = renderer.bounds;
            float area = bounds.size.sqrMagnitude;
            if (area <= largestArea)
                continue;

            largestArea = area;
            representative = renderer;
        }

        return representative;
    }

    /// <summary>
    /// 책임 :
    /// - 대표 렌더러 후보를 필터링해 자동 위치 계산이 의도치 않은 자식 오브젝트에 흔들리지 않게 한다.
    /// </summary>
    private bool IsEligibleRepresentativeRenderer(SpriteRenderer renderer, bool requireTempEnemyLayer)
    {
        if (renderer == null || renderer.sprite == null)
            return false;

        if (timerRoot != null && renderer.transform.IsChildOf(timerRoot.transform))
            return false;

        if (requireTempEnemyLayer && renderer.gameObject.layer != TempEnemyLayer)
            return false;

        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 타이머 pulse와 표시 루트에 쓸 안전한 transform 대상을 반환한다.
    /// - visual root가 비어 있어도 타이머 인스턴스 자체를 비활성화하지 않도록 보호한다.
    /// </summary>
    private Transform GetPulseTarget()
    {
        if (timerRoot != null)
            return timerRoot.transform;

        return transform;
    }
}
