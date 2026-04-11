using System.Collections;
using UnityEngine;
using UnityGAS;

public class Witch : BossControllerBase
{
    private static readonly int AttackHash = Animator.StringToHash("attack");

    [Header("Pattern")]
    [Tooltip("촛대를 끄는 패턴에 사용할 Fog 프리팹입니다.")]
    [SerializeField] private GameObject fogPrefab;
    [SerializeField] private GE_Damage_Spec extinguishDamageEffect;
    [SerializeField] private float extinguishDamage = 1f;

    private BossDialogueRunner dialogueRunner;
    private Coroutine dialogueRoutine;
    private WitchExtinguishState extinguishState;
    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle extinguishWarningStyle;
    private bool hasAttackTrigger;
    private StrangeCandlestick extinguishCandle;
    private Vector3 extinguishCenter;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        extinguishWarningStyle = MakeWarningStyle();
        hasAttackTrigger = CheckAttackTrigger();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        extinguishState = new WitchExtinguishState(this);
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        HideExtinguishWarning();
    }

    protected override void Update()
    {
        base.Update();

        // 스프라이트 반전
        if (Target == null) return;

        if      (transform.position.x > Target.position.x) sprite.flipX = true;
        else if (transform.position.x < Target.position.x) sprite.flipX = false;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (extinguishWarningStyle != null) Destroy(extinguishWarningStyle);
    }

    public BossState GetExtinguishState()
    {
        return extinguishState;
    }

    public override bool TryStartDialogue()
    {
        BossDialogueRunner runner = GetDialogueRunner();
        if (runner == null || dialogueRoutine != null) return false;

        dialogueRoutine = StartCoroutine(PlayDialogue(runner));
        return true;
    }

    public override bool IsDialogueActive()
    {
        return dialogueRoutine != null ||
               (DialogueService.Instance != null && DialogueService.Instance.IsPlaying);
    }

    /// <summary>촛불 끄기 패턴을 시작합니다.</summary>
    public bool StartExtinguish(float warningTime)
    {
        if (telegraphService == null || fogPrefab == null) return false;

        StrangeCandlestick candle = GetNearestCandle();
        if (candle == null) return false;

        if (GetFogRadius() <= 0f) return false;

        extinguishCandle = candle;
        extinguishCenter = GetCandleCenter(candle);
        PlayAttackMotion();
        ShowWarning(extinguishCenter, warningTime);
        return true;
    }

    /// <summary>촛불 끄기 패턴을 끝냅니다.</summary>
    public void FinishExtinguish()
    {
        if (extinguishCandle == null) return;

        TryHitPlayer(extinguishCenter);
        SpawnFog(extinguishCenter);
        extinguishCandle.Seal();
        HideExtinguishWarning();
    }

    public void HideExtinguishWarning()
    {
        HideWarning();
        extinguishCandle = null;
        extinguishCenter = Vector3.zero;
    }

    /// <summary>대화 State 사용 여부를 정합니다.</summary>
    protected override bool CanUseDialogue()
    {
        return GetDialogueRunner() != null && FindAnyObjectByType<BossEncounterDirector>() == null;
    }

    /// <summary>대화를 실행합니다.</summary>
    private IEnumerator PlayDialogue(BossDialogueRunner runner)
    {
        yield return runner.PlayDialogueRoutine();
        dialogueRoutine = null;
    }

    /// <summary>대화 러너를 구합니다.</summary>
    private BossDialogueRunner GetDialogueRunner()
    {
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<BossDialogueRunner>();

        return dialogueRunner;
    }

    /// <summary>가장 가까운 촛대를 구합니다.</summary>
    public StrangeCandlestick GetNearestCandle()
    {
        float bestDistance = float.MaxValue;
        StrangeCandlestick bestCandle = null;

        for (int i = 0; i < StrangeCandlestick.Instances.Count; i++)
        {
            StrangeCandlestick candle = StrangeCandlestick.Instances[i];
            if (candle == null || candle.IsDead || candle.IsSealed) continue;

            float sqrDistance = (GetCandleCenter(candle) - transform.position).sqrMagnitude;
            if (sqrDistance >= bestDistance)
                continue;

            bestDistance = sqrDistance;
            bestCandle = candle;
        }

        return bestCandle;
    }

    /// <summary>촛대 중심 위치를 구합니다.</summary>
    public Vector3 GetCandleCenter(StrangeCandlestick candle)
    {
        if (candle == null) return transform.position;

        Collider2D candleCollider = candle.GetComponent<Collider2D>();
        if (candleCollider != null) return candleCollider.bounds.center;

        SpriteRenderer candleSprite = candle.GetComponent<SpriteRenderer>();
        if (candleSprite != null) return candleSprite.bounds.center;

        return candle.transform.position;
    }

    /// <summary>폭발 경고를 표시합니다.</summary>
    private void ShowWarning(Vector3 center, float warningTime)
    {
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            center,
            GetFogRadius() * 2f,
            Mathf.Max(0f, warningTime),
            extinguishWarningStyle);

        telegraphService.Show(spec);
    }

    /// <summary>폭발 경고를 숨깁니다.</summary>
    private void HideWarning()
    {
        if (telegraphService != null) telegraphService.HideCurrent();
    }

    /// <summary>플레이어에게 폭발 피해를 적용합니다.</summary>
    private bool TryHitPlayer(Vector3 center)
    {
        if (Target == null || abilitySystem == null || extinguishDamageEffect == null) return false;

        float fogRadius = GetFogRadius();
        Vector2 toTarget = (Vector2)(Target.position - center);

        if (toTarget.sqrMagnitude > fogRadius * fogRadius) return false;

        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: extinguishDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        CombatHitPayload payload = CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: extinguishDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);

        return CombatHitPayloadApplier.Apply(Target.gameObject, payload, center);
    }

    /// <summary>촛대 위치에 Fog를 생성합니다.</summary>
    private bool SpawnFog(Vector3 center)
    {
        if (fogPrefab == null) return false;

        Instantiate(fogPrefab, GetFogSpawnPos(center), Quaternion.identity);
        return true;
    }

    /// <summary>Fog 생성 위치를 구합니다.</summary>
    private Vector3 GetFogSpawnPos(Vector3 center)
    {
        Vector2 fogOffset = GetFogOffset();
        return center - new Vector3(fogOffset.x, fogOffset.y, 0f);
    }

    /// <summary>Fog 반지름을 구합니다.</summary>
    private float GetFogRadius()
    {
        if (fogPrefab == null) return 0f;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null) return 0f;

        Vector3 scale = fogPrefab.transform.localScale;
        float xRadius = fogCollider.radius * Mathf.Abs(scale.x);
        float yRadius = fogCollider.radius * Mathf.Abs(scale.y);
        return Mathf.Max(xRadius, yRadius);
    }

    /// <summary>Fog 오프셋을 구합니다.</summary>
    private Vector2 GetFogOffset()
    {
        if (fogPrefab == null) return Vector2.zero;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null) return Vector2.zero;

        Vector3 scale = fogPrefab.transform.localScale;
        return new Vector2(
            fogCollider.offset.x * scale.x,
            fogCollider.offset.y * scale.y);
    }

    /// <summary>패턴 모션을 재생합니다.</summary>
    private void PlayAttackMotion()
    {
        if (animator != null && hasAttackTrigger) animator.SetTrigger(AttackHash);
    }

    /// <summary>attack 트리거 유무를 확인합니다.</summary>
    private bool CheckAttackTrigger()
    {
        if (animator == null) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                parameter.nameHash == AttackHash)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.35f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.35f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
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
