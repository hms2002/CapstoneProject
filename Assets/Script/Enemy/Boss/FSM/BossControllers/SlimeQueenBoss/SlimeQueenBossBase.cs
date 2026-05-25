using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 계열 보스가 공유하는 타겟 방향, 이동 차단, 경고 표시 기반 기능입니다.
/// </summary>
public abstract class SlimeQueenBossBase : BossControllerBase, IIntentMovementSource2D
{
    private const string GroggyTagResourcePath = "Tags/State.Status.Groggy";
    private const float DefaultGroggyDurationSeconds = 3f;

    private AttackTelegraphService telegraphService;
    private GameplayTag patternMoveInvulnerableTag;
    private GameplayEffect runtimeGroggyStatusEffect;
    private bool isPatternMoveDamageBlocked;
    private bool hasAppliedPatternMoveInvulnerableTag;
    private bool isPitFallRuntimeLocked;
    private int pitFallTriggerBlockCount;

    public bool IsPatternMoveDamageBlocked => isPatternMoveDamageBlocked;

    public bool CanTriggerPitFall => !isPitFallRuntimeLocked && pitFallTriggerBlockCount <= 0;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        patternMoveInvulnerableTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        EnsureGroggyGauge();
    }

    protected override void Update()
    {
        if (isPitFallRuntimeLocked)
        {
            if (movementMotor != null)
                movementMotor.StopAllMotion();

            return;
        }

        base.Update();
        FaceCurrentTarget();
    }

    protected override void OnDestroy()
    {
        if (runtimeGroggyStatusEffect != null)
        {
            Destroy(runtimeGroggyStatusEffect);
            runtimeGroggyStatusEffect = null;
        }

        base.OnDestroy();
    }

    /// <summary>보스가 기본 의도 이동을 하지 않도록 빈 이동값을 제공합니다.</summary>
    public IntentMovementData GetIntent()
    {
        return IntentMovementData.None;
    }

    /// <summary>이동형 패턴 중 보스 피격과 접촉 피해를 임시로 막습니다.</summary>
    public void SetPatternMoveDamageBlocked(bool isBlocked)
    {
        if (isPatternMoveDamageBlocked == isBlocked)
            return;

        isPatternMoveDamageBlocked = isBlocked;

        if (isBlocked)
        {
            if (!hasAppliedPatternMoveInvulnerableTag && TryAddStateTag(patternMoveInvulnerableTag))
                hasAppliedPatternMoveInvulnerableTag = true;

            return;
        }

        if (hasAppliedPatternMoveInvulnerableTag && TryRemoveStateTag(patternMoveInvulnerableTag))
            hasAppliedPatternMoveInvulnerableTag = false;
    }

    /// <summary>공중 이동처럼 구덩이 판정을 받으면 안 되는 구간을 시작합니다.</summary>
    public void PushPitFallTriggerBlock()
    {
        pitFallTriggerBlockCount++;
    }

    /// <summary>구덩이 판정 차단 구간을 종료합니다.</summary>
    public void PopPitFallTriggerBlock()
    {
        if (pitFallTriggerBlockCount <= 0)
        {
            pitFallTriggerBlockCount = 0;
            return;
        }

        pitFallTriggerBlockCount--;
    }

    /// <summary>구덩이 낙하 연출 중 기존 패턴과 기본 추적 갱신을 멈춥니다.</summary>
    public void SetPitFallRuntimeLock(bool isLocked)
    {
        if (isPitFallRuntimeLocked == isLocked)
            return;

        isPitFallRuntimeLocked = isLocked;

        if (!isLocked)
            return;

        AbortCurrentPattern();

        if (movementMotor != null)
            movementMotor.StopAllMotion();
    }

    /// <summary>현재 타겟 방향에 맞춰 보스 스프라이트 방향을 갱신합니다.</summary>
    public void FaceCurrentTarget()
    {
        if (sprite == null || CurrentTarget == null)
            return;

        if (transform.position.x > CurrentTarget.position.x)
            sprite.flipX = true;
        else if (transform.position.x < CurrentTarget.position.x)
            sprite.flipX = false;
    }

    /// <summary>패턴 종료 시 이동형 패턴 피해 차단 상태를 정리합니다.</summary>
    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        SetPatternMoveDamageBlocked(false);
        pitFallTriggerBlockCount = 0;
    }

    /// <summary>AttackTelegraphService 참조를 반환합니다.</summary>
    protected AttackTelegraphService GetTelegraphService()
    {
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        return telegraphService;
    }

    /// <summary>슬라임 여왕 계열 보스가 다른 보스처럼 공용 스태거/그로기 게이지를 사용하도록 보장합니다.</summary>
    private void EnsureGroggyGauge()
    {
        StaggerGaugeSystem staggerGauge = GetComponent<StaggerGaugeSystem>();
        if (staggerGauge == null)
            staggerGauge = gameObject.AddComponent<StaggerGaugeSystem>();

        if (staggerGauge.currentGaugeAttribute == null)
            staggerGauge.currentGaugeAttribute = FindAttributeDefinition("Stagger", "StaggerBaseAttribute");

        if (staggerGauge.maxGaugeAttribute == null)
            staggerGauge.maxGaugeAttribute = FindAttributeDefinition("MaxStaggerAttribute");

        if (staggerGauge.resistancePercentAttribute == null)
            staggerGauge.resistancePercentAttribute = FindAttributeDefinition("StaggerResistanceAttribute");

        if (staggerGauge.staggeredEffect == null)
            staggerGauge.staggeredEffect = ResolveRuntimeGroggyStatusEffect();

        staggerGauge.allowOverflow = false;
    }

    private GameplayEffect ResolveRuntimeGroggyStatusEffect()
    {
        if (runtimeGroggyStatusEffect != null)
            return runtimeGroggyStatusEffect;

        GameplayTag groggyTag = Resources.Load<GameplayTag>(GroggyTagResourcePath);
        if (groggyTag == null)
            return null;

        GE_StatusOnlyDuration groggyEffect = ScriptableObject.CreateInstance<GE_StatusOnlyDuration>();
        groggyEffect.name = "GE_SlimeQueen_RuntimeGroggyStatus";
        groggyEffect.effectName = "Groggy";
        groggyEffect.duration = DefaultGroggyDurationSeconds;
        groggyEffect.canStack = false;
        groggyEffect.maxStacks = 1;
        groggyEffect.grantedTags.Add(groggyTag);

        runtimeGroggyStatusEffect = groggyEffect;
        return runtimeGroggyStatusEffect;
    }

    private AttributeDefinition FindAttributeDefinition(params string[] attributeNames)
    {
        if (AttributeSet == null || attributeNames == null || attributeNames.Length == 0)
            return null;

        foreach (AttributeDefinition definition in AttributeSet.EnumerateDefinitions())
        {
            if (definition == null)
                continue;

            for (int i = 0; i < attributeNames.Length; i++)
            {
                string attributeName = attributeNames[i];
                if (string.IsNullOrWhiteSpace(attributeName))
                    continue;

                if (string.Equals(definition.attributeName, attributeName, StringComparison.Ordinal) ||
                    string.Equals(definition.name, attributeName, StringComparison.Ordinal))
                    return definition;
            }
        }

        return null;
    }

    /// <summary>충돌한 콜라이더의 계층에 Player 태그가 있는지 확인합니다.</summary>
    protected bool HasPlayerTagInHierarchy(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return true;

            current = current.parent;
        }

        return false;
    }
}
