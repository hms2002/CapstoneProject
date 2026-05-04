using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class DemonKingController : BossControllerBase
{
    private const int DefaultWallLayer = 30;

    [Header("Demon King Runtime")]
    [SerializeField] private bool configureRuntimePatternsOnStart = true;
    [SerializeField] private EgoSwordActor egoSword;
    [SerializeField] private Transform arenaCenterPoint;

    [Header("Combat Shared Data")]
    [SerializeField] private GE_Damage_Spec defaultDamageEffect;
    [SerializeField] private GE_Knockback_Spec defaultKnockbackEffect;
    [SerializeField] private AttackTelegraphStyle defaultWarningStyle;
    [SerializeField] private LayerMask wallMask = 1 << DefaultWallLayer;

    [Header("Rule Thresholds")]
    [SerializeField, Min(1)] private int holdPatternsBeforeThrow = 3;
    [SerializeField, Min(1)] private int droppedSwordPatternsBeforeRecall = 5;
    [SerializeField, Range(0f, 1f)] private float hp50RushRatio = 0.5f;
    [SerializeField, Range(0f, 1f)] private float finalDesperationHpRatio = 0.1f;

    [Header("Fallback Tuning")]
    [SerializeField, Min(0.1f)] private float playerMoveSpeedReference = 4.5f;
    [SerializeField, Min(0.1f)] private float playerDashDistanceReference = 4f;
    [SerializeField] private bool faceTargetDuringCombat = true;

    private readonly Dictionary<AbilityDefinition, DemonKingPatternRole> roleByAbility = new();
    private DemonKingRuntimeData runtimeData;
    private AttackTelegraphService telegraphService;
    private int faceTargetLockCount;
    private bool runtimePatternsConfigured;

    private BossPatternEntry throwSwordEntry;
    private BossPatternEntry recallSwordEntry;
    private BossPatternEntry hp50RushEntry;
    private BossPatternEntry groggyRecoverCounterEntry;
    private BossPatternEntry finalDesperationEntry;

    public DemonKingRuntimeData RuntimeData
    {
        get
        {
            runtimeData ??= new DemonKingRuntimeData();
            return runtimeData;
        }
    }

    public GE_Damage_Spec DefaultDamageEffect => defaultDamageEffect;
    public GE_Knockback_Spec DefaultKnockbackEffect => defaultKnockbackEffect;
    public AttackTelegraphStyle DefaultWarningStyle => defaultWarningStyle;
    public LayerMask WallMask => wallMask;
    public float PlayerMoveSpeedReference => playerMoveSpeedReference;
    public float PlayerDashDistanceReference => playerDashDistanceReference;
    public EgoSwordActor EgoSword => ResolveEgoSword();

    public LayerMask TargetMask
    {
        get
        {
            if (CurrentTarget != null)
                return 1 << CurrentTarget.gameObject.layer;

            return Physics2D.DefaultRaycastLayers;
        }
    }

    public Vector2 FacingDirection => sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    public Vector3 ArenaCenterPosition => arenaCenterPoint != null ? arenaCenterPoint.position : transform.position;

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new DemonKingRuntimeData();
        telegraphService = GetComponent<AttackTelegraphService>();
    }

    protected override void Start()
    {
        if (configureRuntimePatternsOnStart || ConfiguredPhaseCount == 0)
            ConfigureRuntimePatternsIfNeeded();

        ResolveEgoSword()?.AttachToOwner();
        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        if (faceTargetDuringCombat && faceTargetLockCount <= 0 && CanAutoFaceTarget())
            FaceCurrentTarget();
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (IsDead || RuntimeData.FinalDesperationStarted)
            return;

        if (CurrentHealthRatio <= finalDesperationHpRatio)
            ForceFinalDesperationNow();
    }

    public override BossPatternEntry SelectNextPattern()
    {
        ConfigureRuntimePatternsIfNeeded();

        if (!RuntimeData.FinalDesperationStarted &&
            CurrentHealthRatio <= finalDesperationHpRatio &&
            finalDesperationEntry != null)
        {
            return finalDesperationEntry;
        }

        if (RuntimeData.GroggyRecoverCounterRequested && groggyRecoverCounterEntry != null)
            return groggyRecoverCounterEntry;

        if (!RuntimeData.Hp50PatternUsed &&
            CurrentHealthRatio <= hp50RushRatio &&
            hp50RushEntry != null)
        {
            return hp50RushEntry;
        }

        if (RuntimeData.ShouldThrowSword(holdPatternsBeforeThrow) && throwSwordEntry != null)
            return throwSwordEntry;

        if (RuntimeData.ShouldRecallSword() && recallSwordEntry != null)
            return recallSwordEntry;

        return base.SelectNextPattern();
    }

    protected override BossPatternEvalResult AdjustPatternEval(BossPatternEntry patternEntry, BossPatternEvalResult result)
    {
        if (patternEntry == null || patternEntry.Ability == null)
            return result;

        if (!roleByAbility.TryGetValue(patternEntry.Ability, out DemonKingPatternRole role))
            return result;

        bool canUseRole = role switch
        {
            DemonKingPatternRole.HoldNormal => RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold,
            DemonKingPatternRole.DropNormal => RuntimeData.SwordMode == DemonKingEgoSwordMode.Drop,
            DemonKingPatternRole.ThrowSword => RuntimeData.ShouldThrowSword(holdPatternsBeforeThrow),
            DemonKingPatternRole.RecallSword => RuntimeData.ShouldRecallSword(),
            DemonKingPatternRole.Hp50Rush => !RuntimeData.Hp50PatternUsed && CurrentHealthRatio <= hp50RushRatio,
            DemonKingPatternRole.GroggyRecoverCounter => RuntimeData.GroggyRecoverCounterRequested,
            DemonKingPatternRole.FinalDesperation => RuntimeData.FinalDesperationStarted || CurrentHealthRatio <= finalDesperationHpRatio,
            _ => true
        };

        return canUseRole ? result : BossPatternEvalResult.HardFail("Demon King pattern role condition was not met.");
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        if (patternEntry == null || patternEntry.Ability == null)
            return;

        if (!roleByAbility.TryGetValue(patternEntry.Ability, out DemonKingPatternRole role))
            return;

        switch (role)
        {
            case DemonKingPatternRole.HoldNormal:
                if (!forced)
                    RuntimeData.RecordHoldNormalPattern();
                break;
            case DemonKingPatternRole.ThrowSword:
                if (!forced)
                    SetSwordDropped();
                break;
            case DemonKingPatternRole.Hp50Rush:
                if (!forced)
                    RuntimeData.MarkHp50PatternUsed();
                break;
            case DemonKingPatternRole.GroggyRecoverCounter:
                RuntimeData.ConsumeGroggyRecoverCounter();
                break;
            case DemonKingPatternRole.FinalDesperation:
                RuntimeData.MarkFinalDesperationStarted();
                break;
        }
    }

    protected override void OnGroggyStateExited()
    {
        RuntimeData.RequestGroggyRecoverCounter();
    }

    public AttackTelegraphService GetTelegraphService()
    {
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        return telegraphService;
    }

    public Vector2 ResolveSwordHoldPosition(Vector3 localOffset)
    {
        Vector3 offset = localOffset;
        if (sprite != null && sprite.flipX)
            offset.x = -offset.x;

        return transform.position + offset;
    }

    public Vector2 GetDirectionToTargetOrFacing(Vector2? fromPosition = null)
    {
        Vector2 origin = fromPosition ?? (Vector2)transform.position;
        return DemonKingCombatUtil.DirectionToTargetOrFacing(this, origin);
    }

    public void FacePatternDirection(Vector2 direction)
    {
        if (sprite == null || Mathf.Abs(direction.x) <= 0.0001f)
            return;

        sprite.flipX = direction.x < 0f;
    }

    public void PlayPatternTrigger(string triggerName = "attack")
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                animator.SetTrigger(triggerName);
                return;
            }
        }
    }

    public void PushFaceTargetLock()
    {
        faceTargetLockCount++;
    }

    public void PopFaceTargetLock()
    {
        faceTargetLockCount = Mathf.Max(0, faceTargetLockCount - 1);
    }

    public void SetSwordDropped()
    {
        RuntimeData.SetSwordDropped();
    }

    public void NotifyEgoSwordPatternCompleted()
    {
        RuntimeData.RecordEgoSwordPatternUse(droppedSwordPatternsBeforeRecall);
    }

    public void CompleteEgoSwordRecall()
    {
        RuntimeData.SetSwordHeld();
        ResolveEgoSword()?.AttachToOwner();
    }

    public void MarkHp50PatternUsed()
    {
        RuntimeData.MarkHp50PatternUsed();
    }

    public void MarkFinalDesperationStarted()
    {
        RuntimeData.MarkFinalDesperationStarted();
    }

    public EgoSwordActor ResolveEgoSword()
    {
        if (egoSword != null)
        {
            egoSword.Bind(this);
            return egoSword;
        }

        egoSword = FindAnyObjectByType<EgoSwordActor>();
        if (egoSword != null)
        {
            egoSword.Bind(this);
            return egoSword;
        }

        GameObject swordObject = GameObject.Find("EgoSword");
        if (swordObject == null)
        {
            Debug.LogError("DemonKingController requires an editable scene object named EgoSword with EgoSwordActor.", this);
            return null;
        }

        StripCopiedBossBehaviours(swordObject);
        egoSword = swordObject.GetComponent<EgoSwordActor>();
        if (egoSword == null)
        {
            Debug.LogError("EgoSword scene object is missing EgoSwordActor.", swordObject);
            return null;
        }

        egoSword.Bind(this);
        return egoSword;
    }

    private void ConfigureRuntimePatternsIfNeeded()
    {
        if (runtimePatternsConfigured)
            return;

        runtimePatternsConfigured = true;
        roleByAbility.Clear();

        BossPatternEntry pierce = CreatePattern<AbilityLogic_DemonKingPierceCombo>(
            "DemonKing_PierceCombo",
            DemonKingPatternRole.HoldNormal,
            weight: 120,
            maxConsecutive: 1,
            lockTime: 0.25f,
            postDelay: 0.25f,
            minDistance: 0f,
            maxDistance: 6.5f);
        BossPatternEntry heavySlash = CreatePattern<AbilityLogic_DemonKingHeavySlash>(
            "DemonKing_HeavySlash",
            DemonKingPatternRole.HoldNormal,
            weight: 90,
            maxConsecutive: 1,
            lockTime: 0.5f,
            postDelay: 0.35f,
            minDistance: 0f,
            maxDistance: 5.8f);

        throwSwordEntry = CreatePattern<AbilityLogic_DemonKingThrowEgoSword>(
            "DemonKing_ThrowEgoSword",
            DemonKingPatternRole.ThrowSword,
            weight: 300,
            maxConsecutive: 1,
            lockTime: 0.2f,
            postDelay: 0.4f,
            minDistance: 0f,
            maxDistance: 999f);

        BossPatternEntry homingMagic = CreatePattern<AbilityLogic_DemonKingHomingMagic>(
            "DemonKing_HomingMagic",
            DemonKingPatternRole.DropNormal,
            weight: 105,
            maxConsecutive: 1,
            lockTime: 0.4f,
            postDelay: 0.25f,
            minDistance: 0f,
            maxDistance: 999f);
        BossPatternEntry bombardment = CreatePattern<AbilityLogic_DemonKingBombardment>(
            "DemonKing_Bombardment",
            DemonKingPatternRole.DropNormal,
            weight: 100,
            maxConsecutive: 1,
            lockTime: 0.45f,
            postDelay: 0.25f,
            minDistance: 0f,
            maxDistance: 999f);
        BossPatternEntry explosionJump = CreatePattern<AbilityLogic_DemonKingExplosionJump>(
            "DemonKing_ExplosionJump",
            DemonKingPatternRole.DropNormal,
            weight: 80,
            maxConsecutive: 1,
            lockTime: 0.6f,
            postDelay: 0.35f,
            minDistance: 0f,
            maxDistance: 999f);

        recallSwordEntry = CreatePattern<AbilityLogic_DemonKingRecallEgoSword>(
            "DemonKing_RecallEgoSword",
            DemonKingPatternRole.RecallSword,
            weight: 350,
            maxConsecutive: 1,
            lockTime: 0.2f,
            postDelay: 0.3f,
            minDistance: 0f,
            maxDistance: 999f);

        hp50RushEntry = CreatePattern<AbilityLogic_DemonKingWallBounceRush>(
            "DemonKing_Hp50WallBounceRush",
            DemonKingPatternRole.Hp50Rush,
            weight: 500,
            maxConsecutive: 1,
            maxUse: 1,
            lockTime: 0.2f,
            postDelay: 2.3f,
            minDistance: 0f,
            maxDistance: 999f,
            minHp: 0f,
            maxHp: hp50RushRatio);

        groggyRecoverCounterEntry = CreatePattern<AbilityLogic_DemonKingGroggyRecoverCounter>(
            "DemonKing_GroggyRecoverCounter",
            DemonKingPatternRole.GroggyRecoverCounter,
            weight: 400,
            maxConsecutive: 1,
            lockTime: 0.2f,
            postDelay: 0.45f,
            minDistance: 0f,
            maxDistance: 999f);

        finalDesperationEntry = CreatePattern<AbilityLogic_DemonKingFinalDesperation>(
            "DemonKing_FinalDesperation",
            DemonKingPatternRole.FinalDesperation,
            weight: 1000,
            maxConsecutive: 1,
            maxUse: 1,
            lockTime: 0.1f,
            postDelay: 0f,
            minDistance: 0f,
            maxDistance: 999f,
            minHp: 0f,
            maxHp: finalDesperationHpRatio);

        SetRuntimePhases(new[]
        {
            BossPhaseConfig.CreateRuntime(
                "Demon King",
                1f,
                0.2f,
                0.55f,
                pierce,
                heavySlash,
                throwSwordEntry,
                homingMagic,
                bombardment,
                explosionJump,
                recallSwordEntry,
                hp50RushEntry,
                groggyRecoverCounterEntry,
                finalDesperationEntry)
        });
    }

    private BossPatternEntry CreatePattern<TLogic>(
        string abilityName,
        DemonKingPatternRole role,
        int weight,
        int maxConsecutive,
        float lockTime,
        float postDelay,
        float minDistance,
        float maxDistance,
        int maxUse = 0,
        float minHp = 0f,
        float maxHp = 1f)
        where TLogic : AbilityLogic
    {
        TLogic logic = ScriptableObject.CreateInstance<TLogic>();
        logic.name = $"AL_{abilityName}";

        AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.name = $"AD_{abilityName}";
        ability.abilityName = abilityName;
        ability.castTime = 0f;
        ability.recoveryTime = 0f;
        ability.canCastWhileMoving = true;
        ability.interruptible = role != DemonKingPatternRole.FinalDesperation;
        ability.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;
        ability.logic = logic;

        roleByAbility[ability] = role;

        return BossPatternEntry.CreateRuntime(
            ability,
            weight,
            maxConsecutive,
            maxUse,
            lockTime,
            postDelay,
            minDistance,
            maxDistance,
            minHp,
            maxHp);
    }

    private void ForceFinalDesperationNow()
    {
        if (finalDesperationEntry == null)
            return;

        RuntimeData.MarkFinalDesperationStarted();
        AbortCurrentPattern();
        PatternRuntime.ReservePattern(finalDesperationEntry);
        ChangeState(GetPatternState(finalDesperationEntry));
    }

    private bool CanAutoFaceTarget()
    {
        return !HasGroggyTag() && !HasDeadTag();
    }

    private void FaceCurrentTarget()
    {
        if (CurrentTarget == null || sprite == null)
            return;

        if (transform.position.x > CurrentTarget.position.x)
            sprite.flipX = true;
        else if (transform.position.x < CurrentTarget.position.x)
            sprite.flipX = false;
    }

    private static void StripCopiedBossBehaviours(GameObject swordObject)
    {
        if (swordObject == null)
            return;

        MonoBehaviour[] behaviours = swordObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is EgoSwordActor)
                continue;

            string typeName = behaviour.GetType().Name;
            bool isCopiedBossBehaviour =
                behaviour is BossControllerBase ||
                typeName.StartsWith("Witch") ||
                typeName.Contains("Boss");

            if (isCopiedBossBehaviour)
                UnityEngine.Object.Destroy(behaviour);
        }

        AbilitySystem copiedAbilitySystem = swordObject.GetComponent<AbilitySystem>();
        if (copiedAbilitySystem != null)
            UnityEngine.Object.Destroy(copiedAbilitySystem);

        EntityCollisionProfile2D collisionProfile = swordObject.GetComponent<EntityCollisionProfile2D>();
        if (collisionProfile != null)
            collisionProfile.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.Disabled);
    }
}
