using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class Witch : BossControllerBase
{
    // 이 클래스의 책임:
    // 마녀 보스 전용 상태, 연출, 패턴 보조 동작을 조율하고 전용 런타임 데이터를 관리한다.

    private static readonly int AttackHash = Animator.StringToHash("attack");
    private const int WallLayer = 30;

    [Header("Pattern")]
    [Tooltip("촛대를 끄는 패턴에 사용할 Fog 프리팹입니다.")]
    [SerializeField] private GameObject fogPrefab;
    [SerializeField] private GameObject candlestickPrefab;
    [SerializeField] private GameObject lightBeadPrefab;
    [SerializeField] private GE_Damage_Spec extinguishDamageEffect;
    [SerializeField] private GameplayEffect groggyStatusEffect;
    [SerializeField] private AttackTelegraphStyle mapWideWarningStyleAsset;
    [SerializeField] private Transform phaseTransitionCenterPoint;
    [SerializeField] private float extinguishDamage = 1f;
    [SerializeField] private float projectileSpeed = 4f;
    [SerializeField] private bool useRuntimeDefaultPatternsWhenPhasesEmpty = true;
    [SerializeField] private float fallbackCandleSpawnRadius = 6f;

    private BossDialogueRunner dialogueRunner;
    private Coroutine dialogueRoutine;
    private WitchExtinguishState extinguishState;
    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle extinguishWarningStyle;
    private AttackTelegraphStyle mapWideWarningStyle;
    private bool hasAttackTrigger;
    private WitchRuntimeData runtimeData;
    private bool hasLoggedRuntimeDataReady;
    private AbilityDefinition basicAttack2Ability;
    private AbilityDefinition sealedCandleRampageAbility;
    private AbilityDefinition lightAllCandlesAbility;
    private readonly List<Candlestick> runtimeSpawnedCandles = new();
    private WitchShieldController shieldController;
    private WitchShieldVisualController shieldVisualController;

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new WitchRuntimeData();
        telegraphService = GetComponent<AttackTelegraphService>();
        extinguishWarningStyle = MakeWarningStyle();
        mapWideWarningStyle = mapWideWarningStyleAsset != null
            ? mapWideWarningStyleAsset
            : MakeMapWideWarningStyle();
        hasAttackTrigger = CheckAttackTrigger();
        shieldController = GetComponent<WitchShieldController>();
        if (shieldController == null)
            shieldController = gameObject.AddComponent<WitchShieldController>();
        shieldVisualController = GetComponent<WitchShieldVisualController>();
        if (shieldVisualController == null)
            shieldVisualController = gameObject.AddComponent<WitchShieldVisualController>();
    }

    protected override void Start()
    {
        EnsureRuntimeCandlesIfNeeded();
        ConfigureRuntimePatternsIfNeeded();
        base.Start();
        LogRuntimeDataReadyIfNeeded();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        extinguishState = new WitchExtinguishState(this);
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        HideExtinguishWarning();

        if (patternEntry != null && patternEntry.Ability != null && patternEntry.Ability.logic is AbilityLogic_WitchLightAllCandles)
            ClearShield();
    }

    public WitchRuntimeData RuntimeData
    {
        get
        {
            if (runtimeData == null)
                runtimeData = new WitchRuntimeData();

            return runtimeData;
        }
    }
    public GameObject LightBeadPrefab => lightBeadPrefab;
    public GE_Damage_Spec ProjectileDamageEffect => extinguishDamageEffect;
    public float ProjectileDamage => extinguishDamage;
    public float ProjectileSpeed => projectileSpeed;
    public bool HasProjectilePatternConfig => lightBeadPrefab != null && extinguishDamageEffect != null;
    public WitchShieldController ShieldController => shieldController;

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
        if (mapWideWarningStyle != null && mapWideWarningStyle != mapWideWarningStyleAsset)
            Destroy(mapWideWarningStyle);
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

    /// <summary>패턴 공용 공격 모션을 재생합니다.</summary>
    public void PlayPatternAttackMotion()
    {
        PlayAttackMotion();
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

    /// <summary>마녀 보호막을 지정 단계수로 활성화합니다.</summary>
    public void ActivateShield(int stageCount = 4)
    {
        shieldController?.ActivateShield(stageCount);
    }

    /// <summary>마녀 보호막 전용 타격을 적용합니다.</summary>
    public bool TryApplyShieldHit(int amount = 1)
    {
        return shieldController != null && shieldController.TryApplyShieldHit(amount);
    }

    /// <summary>마녀 보호막을 깨뜨립니다.</summary>
    public void BreakShield()
    {
        shieldController?.BreakShield();
    }

    /// <summary>마녀 보호막을 조용히 정리합니다.</summary>
    public void ClearShield()
    {
        shieldController?.ClearShield();
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

    /// <summary>현재 봉인된 촛대 수를 구합니다.</summary>
    public int GetSealedCandleCount()
    {
        int sealedCount = 0;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle != null && candle.IsSealed)
                sealedCount++;
        }

        return sealedCount;
    }

    /// <summary>봉인된 촛대가 하나라도 있는지 확인합니다.</summary>
    public bool HasAnySealedCandles()
    {
        return GetSealedCandleCount() > 0;
    }

    /// <summary>모든 촛대를 봉인 상태로 만듭니다.</summary>
    public void SealAllCandles()
    {
        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null || candle.IsSealed)
                continue;

            candle.Seal();
        }
    }

    /// <summary>현재 봉인된 촛대를 버퍼에 수집합니다.</summary>
    public void CollectSealedCandles(List<Candlestick> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle != null && candle.IsSealed)
                buffer.Add(candle);
        }
    }

    /// <summary>테스트나 예외 상황에서 가장 가까운 미봉인 촛대 하나를 즉시 봉인합니다.</summary>
    public Candlestick SealNearestAvailableCandle()
    {
        Candlestick candle = GetNearestCandle();
        if (candle == null)
            return null;

        candle.Seal();
        return candle;
    }

    /// <summary>대상 방향이나 현재 바라보는 방향을 기반으로 투사체 방향을 구합니다.</summary>
    public Vector2 GetDirectionToTargetOrFacing(Transform targetTransform, Vector3? fromPosition = null)
    {
        Vector3 origin = fromPosition ?? transform.position;
        if (targetTransform != null)
        {
            Vector2 toTarget = (Vector2)(targetTransform.position - origin);
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    /// <summary>그로기 상태 효과를 보스 자신에게 적용합니다.</summary>
    public void ApplyGroggyStatus()
    {
        if (abilitySystem == null || groggyStatusEffect == null)
            return;

        abilitySystem.EffectRunner.ApplyEffect(groggyStatusEffect, gameObject, gameObject);
    }

    /// <summary>페이즈 전환 패턴의 중앙 위치를 구합니다.</summary>
    public Vector3 GetPhaseTransitionCenter()
    {
        if (phaseTransitionCenterPoint != null)
            return phaseTransitionCenterPoint.position;

        int candleCount = 0;
        Vector3 accumulatedCenter = Vector3.zero;
        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null)
                continue;

            accumulatedCenter += GetCandleCenter(candle);
            candleCount++;
        }

        if (candleCount > 0)
            return accumulatedCenter / candleCount;

        return transform.position;
    }

    /// <summary>지정한 중심 기준으로 촛대들을 모두 덮는 반경을 구합니다.</summary>
    public float GetArenaRadiusFromCandles(Vector3 center, float fallbackRadius = 6f)
    {
        float radius = 0f;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null)
                continue;

            Vector3 candleCenter = GetCandleCenter(candle);
            float candleDistance = Vector2.Distance(center, candleCenter);
            float candleExtent = GetObjectExtentRadius(candle.gameObject);
            radius = Mathf.Max(radius, candleDistance + candleExtent);
        }

        return Mathf.Max(fallbackRadius, radius);
    }

    /// <summary>중앙 위치로 보스를 이동시킵니다.</summary>
    public void MoveToPhaseTransitionCenter(float duration)
    {
        if (duration <= 0f)
            return;

        AbilityMotionController2D motion = GetComponent<AbilityMotionController2D>();
        if (motion == null)
            return;

        Vector2 start = transform.position;
        Vector2 destination = GetPhaseTransitionCenter();
        Vector2 delta = destination - start;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return;

        motion.StartLunge(start, delta.normalized, distance, duration);
    }

    /// <summary>벽 레이어를 기준으로 맵 전체 피해 경고를 표시합니다.</summary>
    public void ShowMapWideWarning(Vector3 center, float warningTime)
    {
        if (telegraphService == null)
            return;

        ResolveArenaRectangle(center, out Vector3 rectCenter, out Vector2 rectSize);
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            rectCenter,
            rectSize,
            0f,
            Mathf.Max(0f, warningTime),
            mapWideWarningStyle);

        telegraphService.Show(spec);
    }

    /// <summary>현재 타깃에게 맵 전체 피해를 적용합니다.</summary>
    public bool ApplyMapWideDamage(GameObject explicitTarget = null)
    {
        if (abilitySystem == null || extinguishDamageEffect == null)
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : Target != null ? Target.gameObject : null;
        if (targetObject == null)
            return false;

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

        return CombatHitPayloadApplier.Apply(targetObject, payload, targetObject.transform.position);
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

    /// <summary>맵 전체 직사각 경고에 사용할 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeMapWideWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0.1f, 0.1f, 0.08f);
        style.fillColorEnd = new Color(1f, 0.1f, 0.1f, 0.26f);
        style.borderColorStart = new Color(1f, 0.4f, 0.4f, 0.75f);
        style.borderColorEnd = new Color(1f, 0.15f, 0.15f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.82f;
        style.blinkFrequency = 9f;
        style.blinkAlphaMin = 0.4f;
        style.scaleFillWithProgress = true;
        style.fillScaleStart = 0f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>중앙 기준으로 벽까지 raycast해 맵 직사각 범위를 구합니다.</summary>
    private void ResolveArenaRectangle(Vector3 probeCenter, out Vector3 rectCenter, out Vector2 rectSize)
    {
        const float fallbackHalfExtent = 6f;
        LayerMask wallMask = 1 << WallLayer;

        float leftDistance = GetWallDistance(probeCenter, Vector2.left, wallMask, fallbackHalfExtent);
        float rightDistance = GetWallDistance(probeCenter, Vector2.right, wallMask, fallbackHalfExtent);
        float downDistance = GetWallDistance(probeCenter, Vector2.down, wallMask, fallbackHalfExtent);
        float upDistance = GetWallDistance(probeCenter, Vector2.up, wallMask, fallbackHalfExtent);

        rectCenter = new Vector3(
            probeCenter.x + ((rightDistance - leftDistance) * 0.5f),
            probeCenter.y + ((upDistance - downDistance) * 0.5f),
            probeCenter.z);
        rectSize = new Vector2(
            Mathf.Max(0.5f, leftDistance + rightDistance),
            Mathf.Max(0.5f, downDistance + upDistance));
    }

    /// <summary>지정 방향으로 가장 가까운 벽까지의 거리를 구합니다.</summary>
    private float GetWallDistance(Vector3 origin, Vector2 direction, LayerMask wallMask, float fallbackDistance)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, fallbackDistance * 4f, wallMask);
        return hit.collider != null
            ? Mathf.Max(0.25f, hit.distance)
            : fallbackDistance;
    }

    /// <summary>오브젝트의 시각 또는 충돌 반경을 구합니다.</summary>
    private float GetObjectExtentRadius(GameObject gameObject)
    {
        if (gameObject == null)
            return 0f;

        Collider2D collider = gameObject.GetComponent<Collider2D>();
        if (collider != null)
            return Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);

        SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            return Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);

        return 0f;
    }

    /// <summary>씬에 촛대가 하나도 없을 때 테스트용 기본 촛대를 생성합니다.</summary>
    private void EnsureRuntimeCandlesIfNeeded()
    {
        if (candlestickPrefab == null || Candlestick.Instances.Count > 0)
            return;

        Vector2[] offsets =
        {
            new Vector2(-fallbackCandleSpawnRadius, 0f),
            new Vector2(fallbackCandleSpawnRadius, 0f),
            new Vector2(0f, -fallbackCandleSpawnRadius),
            new Vector2(0f, fallbackCandleSpawnRadius)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 spawnPosition = transform.position + (Vector3)offsets[i];
            GameObject candleObject = Instantiate(candlestickPrefab, spawnPosition, Quaternion.identity);
            Candlestick candle = candleObject.GetComponent<Candlestick>();
            if (candle != null)
                runtimeSpawnedCandles.Add(candle);
        }
    }

    /// <summary>기본 phase 자산이 비어 있으면 마녀 기본 패턴 구성을 런타임에 만든다.</summary>
    private void ConfigureRuntimePatternsIfNeeded()
    {
        if (!useRuntimeDefaultPatternsWhenPhasesEmpty || ConfiguredPhaseCount > 0 || abilitySystem == null)
            return;

        basicAttack2Ability = CreateRuntimeAbility(
            "AD_Witch_BasicAttack2_Runtime",
            ScriptableObject.CreateInstance<AbilityLogic_WitchBasicAttack2>(),
            castTime: 0f,
            recoveryTime: 0.55f);

        sealedCandleRampageAbility = CreateRuntimeAbility(
            "AD_Witch_SealedCandleRampage_Runtime",
            ScriptableObject.CreateInstance<AbilityLogic_WitchSealedCandleRampage>(),
            castTime: 0f,
            recoveryTime: 0.85f);

        lightAllCandlesAbility = CreateRuntimeAbility(
            "AD_Witch_LightAllCandles_Runtime",
            ScriptableObject.CreateInstance<AbilityLogic_WitchLightAllCandles>(),
            castTime: 0f,
            recoveryTime: 0.2f);

        BossPatternEntry phase1BasicAttack = BossPatternEntry.CreateRuntime(
            basicAttack2Ability,
            runtimeSelectionWeight: 100,
            runtimeMaxConsecutiveUseCount: 2,
            runtimeMaxUseCount: 0,
            runtimeSelectionLockTime: 0.2f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0.5f,
            runtimeMaxHpRatio: 1f);

        BossPatternEntry phase2BasicAttack = BossPatternEntry.CreateRuntime(
            basicAttack2Ability,
            runtimeSelectionWeight: 100,
            runtimeMaxConsecutiveUseCount: 2,
            runtimeMaxUseCount: 0,
            runtimeSelectionLockTime: 0.2f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0f,
            runtimeMaxHpRatio: 0.5f);

        BossPatternEntry rampagePattern = BossPatternEntry.CreateRuntime(
            sealedCandleRampageAbility,
            runtimeSelectionWeight: 140,
            runtimeMaxConsecutiveUseCount: 1,
            runtimeMaxUseCount: 0,
            runtimeSelectionLockTime: 0.6f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0f,
            runtimeMaxHpRatio: 1f,
            WitchSealedCandlesCondition.CreateRuntime());

        BossPatternEntry lightAllCandlesPattern = BossPatternEntry.CreateRuntime(
            lightAllCandlesAbility,
            runtimeSelectionWeight: 3000,
            runtimeMaxConsecutiveUseCount: 1,
            runtimeMaxUseCount: 1,
            runtimeSelectionLockTime: 0f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0f,
            runtimeMaxHpRatio: 0.5f);

        SetRuntimePhases(new[]
        {
            BossPhaseConfig.CreateRuntime("Phase 1", 1f, 0.3f, 0.55f, phase1BasicAttack),
            BossPhaseConfig.CreateRuntime("Phase 2", 0.5f, 0.25f, 0.5f, lightAllCandlesPattern, rampagePattern, phase2BasicAttack)
        });
    }

    private AbilityDefinition CreateRuntimeAbility(
        string runtimeAbilityName,
        AbilityLogic runtimeLogic,
        float castTime,
        float recoveryTime,
        List<GameplayTag> runtimeGrantedTags = null)
    {
        AbilityDefinition definition = ScriptableObject.CreateInstance<AbilityDefinition>();
        definition.name = runtimeAbilityName;
        definition.abilityName = runtimeAbilityName;
        definition.castTime = castTime;
        definition.recoveryTime = recoveryTime;
        definition.animationChannel = AbilityDefinition.AnimationChannel.Player;
        definition.animationTrigger = "attack";
        definition.animationTriggerHash = AttackHash;
        definition.logic = runtimeLogic;
        definition.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;

        if (runtimeGrantedTags != null && runtimeGrantedTags.Count > 0)
            definition.grantedTagsWhileActive = new List<GameplayTag>(runtimeGrantedTags);

        abilitySystem.GiveAbility(definition);
        return definition;
    }

    /// <summary>마녀 전용 런타임 데이터가 준비되었음을 한 번만 기록합니다.</summary>
    private void LogRuntimeDataReadyIfNeeded()
    {
        if (hasLoggedRuntimeDataReady)
            return;

        hasLoggedRuntimeDataReady = true;
        Debug.Log(
            $"[BossFSM] {name}: WitchRuntimeData 준비 완료. sealedCandles={GetSealedCandleCount()}, hasSelection={RuntimeData.HasActiveExtinguishSelection}",
            this);
    }
}
