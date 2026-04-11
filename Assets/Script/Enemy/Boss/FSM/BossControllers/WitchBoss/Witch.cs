using System.Collections;
using UnityEngine;
using UnityGAS;

public class Witch : BossControllerBase
{
    // 이 클래스의 책임:
    // 마녀 보스 전용 상태, 연출, 패턴 보조 동작을 조율하고 전용 런타임 데이터를 관리한다.

    private static readonly int AttackHash = Animator.StringToHash("attack");
    private static readonly Vector3 RetreatLeftOffset = new Vector3(-0.5f, 0.2f, 0f);
    private static readonly Vector3 RetreatRightOffset = new Vector3(0.5f, 0.2f, 0f);
    private const int Normal1Count = 3;
    private const float Normal1Interval = 0.3f;
    private const float Normal1Size = 1.7f;
    private const float Normal1HitTime = 0.12f;
    private const float RetreatExplosionDiameter = 6f;
    private const float RetreatSpeedScale = 1f;

    [Header("Pattern")]
    [Tooltip("촛대를 끄는 패턴에 사용할 Fog 프리팹입니다.")]
    [SerializeField] private GameObject fogPrefab;
    [SerializeField] private GE_Damage_Spec extinguishDamageEffect;
    [SerializeField] private float extinguishDamage = 1f;
    [SerializeField] private WitchNormalAttack1Tile normalAttack1TilePrefab;
    [SerializeField] private GE_Damage_Spec normalAttack1DamageEffect;
    [SerializeField] private float normalAttack1Damage = 1f;
    [SerializeField] private DeadsSkeleton retreatSkeletonPrefab;

    private BossDialogueRunner dialogueRunner;
    private Coroutine dialogueRoutine;
    private WitchExtinguishPatternState extinguishState;
    private WitchNormalAttack1State normalAttack1State;
    private WitchRetreatToCandleState retreatState;
    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle extinguishWarningStyle;
    private bool hasAttackTrigger;
    private WitchRuntimeData runtimeData;

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new WitchRuntimeData();
        telegraphService = GetComponent<AttackTelegraphService>();
        extinguishWarningStyle = MakeWarningStyle();
        hasAttackTrigger = CheckAttackTrigger();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        extinguishState = new WitchExtinguishPatternState(this);
        normalAttack1State = new WitchNormalAttack1State(this);
        retreatState = new WitchRetreatToCandleState(this);
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        HideExtinguishWarning();
        ClearNormal1();
    }

    public WitchRuntimeData RuntimeData => runtimeData;

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

    public override BossState GetPatternState(BossPatternEntry patternEntry)
    {
        if (IsExtinguishPattern(patternEntry)) return extinguishState;
        if (IsNormal1Pattern(patternEntry)) return normalAttack1State;
        if (IsRetreatPattern(patternEntry)) return retreatState;

        return base.GetPatternState(patternEntry);
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

    /// <summary>촛불 끄기 패턴인지 확인합니다.</summary>
    private bool IsExtinguishPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null) return false;

        return patternEntry.Ability.logic is UnityGAS.Sample.AbilityLogic_ExtinguishCandle;
    }

    /// <summary>평타1 패턴인지 확인합니다.</summary>
    private bool IsNormal1Pattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null) return false;

        return patternEntry.Ability.logic is UnityGAS.Sample.AbilityLogic_NormalAttack1;
    }

    /// <summary>촛대로의 피난 패턴인지 확인합니다.</summary>
    private bool IsRetreatPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null) return false;

        return patternEntry.Ability.logic is UnityGAS.Sample.AbilityLogic_RetreatToCandle;
    }

    /// <summary>촛불 끄기 패턴을 시작합니다.</summary>
    public bool StartExtinguish(float warningTime)
    {
        if (telegraphService == null || fogPrefab == null) return false;

        Candlestick candle = GetNearestCandle();
        if (candle == null) return false;

        if (GetFogRadius() <= 0f) return false;

        Vector3 extinguishCenter = GetCandleCenter(candle);
        runtimeData.SetExtinguishSelection(candle, extinguishCenter);
        PlayAttackMotion();
        ShowWarning(extinguishCenter, warningTime);
        return true;
    }

    /// <summary>평타1 장판 공격을 시작합니다.</summary>
    public bool StartNormal1()
    {
        if (abilitySystem == null || Target == null) return false;
        if (normalAttack1TilePrefab == null || normalAttack1DamageEffect == null) return false;

        Vector2 aimDir = GetAimDir();
        if (aimDir == Vector2.zero) return false;

        runtimeData.ClearNormal1Tiles();
        PlayAttackMotion();

        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        Vector2 tileSize = Vector2.one * Normal1Size;
        CombatHitPayload payload = MakeNormal1Payload();

        for (int i = 0; i < Normal1Count; i++)
        {
            WitchNormalAttack1Tile tile = Instantiate(
                normalAttack1TilePrefab,
                GetTilePoint(aimDir, i),
                Quaternion.Euler(0f, 0f, angle));

            runtimeData.AddNormal1Tile(tile);
            tile.Play(
                Target.gameObject,
                payload,
                tileSize,
                angle,
                i * Normal1Interval,
                GetNormal1StartTime() + (i * Normal1Interval));
        }

        return true;
    }

    /// <summary>촛대로의 피난 패턴을 시작합니다.</summary>
    public bool StartRetreat()
    {
        if (retreatSkeletonPrefab == null) return false;

        PlayAttackMotion();
        bool spawnedLeft = SpawnRetreatSkeleton(RetreatLeftOffset);
        bool spawnedRight = SpawnRetreatSkeleton(RetreatRightOffset);
        return spawnedLeft || spawnedRight;
    }

    /// <summary>촛불 끄기 패턴을 끝냅니다.</summary>
    public void FinishExtinguish()
    {
        Candlestick extinguishCandle = runtimeData.SelectedCandle;
        if (extinguishCandle == null) return;

        Vector3 extinguishCenter = runtimeData.SelectedCenter;

        TryHitPlayer(extinguishCenter);
        SpawnFog(extinguishCenter);
        extinguishCandle.Seal();
        HideExtinguishWarning();
    }

    public void HideExtinguishWarning()
    {
        HideWarning();
        runtimeData.ClearExtinguishSelection();
    }

    /// <summary>평타1 장판을 모두 지웁니다.</summary>
    public void ClearNormal1()
    {
        runtimeData.ClearNormal1Tiles();
    }

    /// <summary>평타1 전체 시간을 돌려줍니다.</summary>
    public float GetNormal1Time()
    {
        return GetNormal1StartTime() + ((Normal1Count - 1) * Normal1Interval) + Normal1HitTime;
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
    public Candlestick GetNearestCandle()
    {
        float bestDistance = float.MaxValue;
        Candlestick bestCandle = null;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null || candle.IsSealed) continue;

            float sqrDistance = (GetCandleCenter(candle) - transform.position).sqrMagnitude;
            if (sqrDistance >= bestDistance)
                continue;

            bestDistance = sqrDistance;
            bestCandle = candle;
        }

        return bestCandle;
    }

    /// <summary>플레이어를 향한 방향을 구합니다.</summary>
    private Vector2 GetAimDir()
    {
        Vector2 toTarget = Target.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f) return Vector2.right;

        return toTarget.normalized;
    }

    /// <summary>평타1 장판 위치를 구합니다.</summary>
    private Vector3 GetTilePoint(Vector2 aimDir, int index)
    {
        float distance = (Normal1Size * 0.5f) + (Normal1Size * index);
        Vector3 offset = new Vector3(aimDir.x, aimDir.y, 0f) * distance;
        return transform.position + offset;
    }

    /// <summary>강화된 망자의 해골 하나를 소환합니다.</summary>
    private bool SpawnRetreatSkeleton(Vector3 localOffset)
    {
        DeadsSkeleton skeleton = Instantiate(
            retreatSkeletonPrefab,
            transform.TransformPoint(localOffset),
            Quaternion.identity);

        if (skeleton == null) return false;

        skeleton.SetBoost(Target, RetreatExplosionDiameter, RetreatSpeedScale, true);
        return true;
    }

    /// <summary>촛대 중심 위치를 구합니다.</summary>
    public Vector3 GetCandleCenter(Candlestick candle)
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

    /// <summary>평타1 공격 payload를 만듭니다.</summary>
    private CombatHitPayload MakeNormal1Payload()
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: normalAttack1Damage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: normalAttack1DamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
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

    /// <summary>평타1 타격 시작 시점을 구합니다.</summary>
    private float GetNormal1StartTime()
    {
        return Normal1Count * Normal1Interval;
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
