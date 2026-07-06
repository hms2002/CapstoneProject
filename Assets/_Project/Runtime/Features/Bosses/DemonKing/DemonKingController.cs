using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 마왕 보스의 페이즈, 패턴 역할, 검 모드, 전용 연출 상태를 BossControllerBase 위에서 조율한다.
/// - 말풍선/텔레그래프/잔상 같은 표현 요청은 Core 계약을 통해 외부 구현과 분리한다.
/// </summary>
public sealed class DemonKingController : BossControllerBase
{
    private const int DefaultWallLayer = 30;
    private const float AutoFaceTargetDeadZone = 0.05f;
    private const string StaggerImmuneTagResourcePath = "Tags/State.Status.StaggerImmune";
    private const string KnockbackImmuneTagResourcePath = "Tags/State.Status.KnockbackImmune";

    public const string DarkLordSwordIdleState = "DarkLord_Sword_Idle";
    public const string DarkLordSwordSlashState = "DarkLord_Sword_Slash";
    public const string DarkLordHandIdleState = "DarkLord_Hand_Idle";
    public const string DarkLordHandBaltState = "DarkLord_Hand_Balt";
    public const string DarkLordSwordThrowingState = "DarkLord_Sword_Throwing";
    public const string DarkLordHandSwordRecoverState = "DarkLord_Hand_SwordRecover";
    public const string DarkLordHandGroggyState = "DarkLord_Hand_Groggy";
    public const string DarkLordSwordDashStabState = "DarkLord_Sword_DashStab";
    public const string DarkLordSwordDashStabReadyState = "DarkLord_Sword_DashStabReady";
    public const string DarkLordSitState = "DarkLord_Sit";
    public const string DarkLordHandJumpAttackState = "DarkLord_Hand_JumpAttack";
    public const string DarkLordHandChargeState = "DarkLord_Hand_Charge";
    public const string DarkLordSwordGroggyState = "DarkLord_Sword_Groggy";
    public const string DarkLordHandGroggyCounterState = "DarkLord_Hand_GroggyCounter";
    public const string DarkLordSwordGroggyCounterState = "DarkLord_Sword_GroggyCounter";
    public const string DarkLord10PercentState = "DarkLord_10Percent";

    [Header("Demon King Runtime")]
    [SerializeField] private bool configureRuntimePatternsOnStart = true;
    [SerializeField] private EgoSwordActor egoSword;
    [SerializeField] private AbilityDefinition egoSwordVerticalStrikeAbility;
    [SerializeField] private AbilityDefinition egoSwordCrossLaserAbility;
    [SerializeField] private Transform arenaCenterPoint;

    [Header("Combat Shared Data")]
    [SerializeField] private GE_Damage_Spec defaultDamageEffect;
    [SerializeField] private GE_Knockback_Spec defaultKnockbackEffect;
    [SerializeField] private AttackTelegraphStyle defaultWarningStyle;
    [SerializeField] private LayerMask wallMask = 1 << DefaultWallLayer;
    [SerializeField] private Collider2D wallRushCollisionProbe;

    [Header("Rule Thresholds")]
    [SerializeField, Min(1)] private int holdPatternsBeforeThrow = 3;
    [SerializeField, Min(1)] private int droppedSwordPatternsBeforeRecall = 5;
    [SerializeField, Range(0f, 1f)] private float hp50RushRatio = 0.5f;
    [SerializeField, Range(0f, 1f)] private float finalDesperationHpRatio = 0.1f;

    [Header("Fallback Tuning")]
    [SerializeField, Min(0.1f)] private float playerMoveSpeedReference = 4.5f;
    [SerializeField, Min(0.1f)] private float playerDashDistanceReference = 4f;
    [SerializeField] private bool faceTargetDuringCombat = true;

    [Header("Groggy Counter Presentation")]
    [SerializeField] private SoundRef groggyRecoverCounterWarningPingSound;

    [Header("VFX Sockets")]
    [SerializeField] private DemonKingVfxSocketMap vfxSocketMap;

    [Header("Afterimage")]
    [SerializeField] private bool enableBodyAfterimage = true;
    [SerializeField, Min(0.01f)] private float bodyAfterimageIntervalSeconds = 0.04f;
    [SerializeField, Min(0.01f)] private float bodyAfterimageLifetimeSeconds = 0.16f;
    [SerializeField] private Color bodyAfterimageColor = new(1f, 0.25f, 0.18f, 0.45f);

    private readonly Dictionary<AbilityDefinition, DemonKingPatternRole> roleByAbility = new();
    private readonly HashSet<string> patternAnimationWarnings = new();
    private readonly HashSet<string> playedPatternAnimationStartStates = new();
    private DemonKingRuntimeData runtimeData;
    private IAttackTelegraphPresenter telegraphPresenter;
    private IBossSpeechPlayback speechController;
    private GameplayTag staggerImmuneTag;
    private GameplayTag knockbackImmuneTag;
    private IAfterimageEmitter2D bodyAfterimageEmitter;
    private SpriteRenderer bodySpriteRenderer;
    private int faceTargetLockCount;
    private int thresholdStaggerGuardCount;
    private bool permanentKnockbackImmuneApplied;
    private bool runtimePatternsConfigured;
    private bool authoredPatternRolesBound;
    private bool finalDesperationHealthClampActive;
    private bool restoringFinalDesperationHealthClamp;
    private bool patternAnimationHoldActive;
    private float patternAnimationSpeedBeforeHold = 1f;

    private BossPatternEntry throwSwordEntry;
    private BossPatternEntry recallSwordEntry;
    private BossPatternEntry hp50RushEntry;
    private BossPatternEntry groggyRecoverCounterEntry;
    private BossPatternEntry finalDesperationEntry;
    private bool missingEgoSwordSubPatternAbilityLogged;

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
    public Collider2D WallRushCollisionProbe => wallRushCollisionProbe;
    public float PlayerMoveSpeedReference => playerMoveSpeedReference;
    public float PlayerDashDistanceReference => playerDashDistanceReference;
    public SoundRef GroggyRecoverCounterWarningPingSound => groggyRecoverCounterWarningPingSound;
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

    public Vector2 FacingDirection => IsFacingLeft ? Vector2.left : Vector2.right;
    public Vector3 ArenaCenterPosition => arenaCenterPoint != null ? arenaCenterPoint.position : transform.position;

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new DemonKingRuntimeData();
        telegraphPresenter = AttackTelegraphPresenterResolver.Resolve(this);
        bodySpriteRenderer = GetComponent<SpriteRenderer>();
        NormalizeBodySorting();
        staggerImmuneTag = Resources.Load<GameplayTag>(StaggerImmuneTagResourcePath);
        knockbackImmuneTag = Resources.Load<GameplayTag>(KnockbackImmuneTagResourcePath);
        ApplyPermanentKnockbackImmunity();
    }

    protected override void Start()
    {
        if (ShouldConfigureRuntimePatterns())
            ConfigureRuntimePatternsIfNeeded();
        else
            BindAuthoredPatternRolesIfNeeded();

        PrepareEgoSwordHeldHidden();
        base.Start();
        RegisterEgoSwordSubPatternAbilities();
    }

    protected override void Update()
    {
        base.Update();

        NormalizeBodySorting();

        if (faceTargetDuringCombat && faceTargetLockCount <= 0 && CanAutoFaceTarget())
            FaceCurrentTarget();
    }

    private void NormalizeBodySorting()
    {
        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponent<SpriteRenderer>();

        DemonKingPrimitiveVisual.ApplyEntitySorting(bodySpriteRenderer);
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (restoringFinalDesperationHealthClamp)
        {
            base.OnEnemyAttributeChanged(attribute, oldValue, newValue);
            return;
        }

        if (TryMaintainFinalDesperationHealthClamp(attribute, newValue))
            return;

        if (TryStartFinalDesperationFromHealthGate(attribute, oldValue, newValue))
            return;

        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (IsDead || RuntimeData.FinalDesperationStarted)
            return;

        if (IsCurrentHealthAttribute(attribute) && CurrentHealthRatio <= finalDesperationHpRatio)
            ForceFinalDesperationNow();
    }

    protected override void OnDeathStarted()
    {
        ReleasePatternAnimationHold();
        ClearPatternAnimationStartRecords();
        ReleaseFinalDesperationHealthClamp();
        ClearThresholdStaggerGuard();
        StopBodyAfterimage(clearGhosts: true);
        StartEgoSwordDeathPlant();
        base.OnDeathStarted();
        PlayDeathPoseAnimation();
    }

    protected override void PlayDeathAnimation()
    {
        if (PlayDeathPoseAnimation())
            return;

        base.PlayDeathAnimation();
    }

    protected override void OnDestroy()
    {
        ReleasePatternAnimationHold();
        ClearPatternAnimationStartRecords();
        ReleaseFinalDesperationHealthClamp();
        ClearThresholdStaggerGuard();
        ClearPermanentKnockbackImmunity();
        StopBodyAfterimage(clearGhosts: true);
        CleanupEgoSwordForBattleEnd();
        base.OnDestroy();
    }

    public override BossPatternEntry SelectNextPattern()
    {
        if (ShouldConfigureRuntimePatterns())
            ConfigureRuntimePatternsIfNeeded();
        else
            BindAuthoredPatternRolesIfNeeded();

        if (!RuntimeData.FinalDesperationStarted &&
            CurrentHealthRatio <= finalDesperationHpRatio &&
            finalDesperationEntry != null)
        {
            return finalDesperationEntry;
        }

        if (RuntimeData.GroggyRecoverCounterRequested && groggyRecoverCounterEntry != null)
            return groggyRecoverCounterEntry;

        if (CanUseHp50Rush())
        {
            if (RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold && throwSwordEntry != null)
                return throwSwordEntry;

            if (RuntimeData.SwordMode == DemonKingEgoSwordMode.Drop && hp50RushEntry != null)
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
            DemonKingPatternRole.ThrowSword => RuntimeData.ShouldThrowSword(holdPatternsBeforeThrow) ||
                                                ShouldThrowSwordForHp50Rush(),
            DemonKingPatternRole.RecallSword => RuntimeData.ShouldRecallSword(),
            DemonKingPatternRole.Hp50Rush => CanUseHp50Rush() &&
                                             RuntimeData.SwordMode == DemonKingEgoSwordMode.Drop,
            DemonKingPatternRole.GroggyRecoverCounter => RuntimeData.GroggyRecoverCounterRequested,
            DemonKingPatternRole.FinalDesperation => RuntimeData.FinalDesperationStarted || CurrentHealthRatio <= finalDesperationHpRatio,
            _ => true
        };

        return canUseRole ? result : BossPatternEvalResult.HardFail("Demon King pattern role condition was not met.");
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        ReleasePatternAnimationHold();
        ClearPatternAnimationStartRecords();

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

        RestoreCombatPose();
    }

    protected override void OnGroggyStateEntered()
    {
        PlayGroggyPose();
    }

    protected override void OnGroggyStateExited()
    {
        RuntimeData.RequestGroggyRecoverCounter();
        if (RuntimeData.GroggyRecoverCounterRequested)
            HoldGroggyPoseAnimation(allowDuringGroggy: true);
        else
            RestoreCombatPose();
    }

    public IAttackTelegraphPresenter GetTelegraphService()
    {
        if (telegraphPresenter == null)
            telegraphPresenter = AttackTelegraphPresenterResolver.Resolve(this);

        return telegraphPresenter;
    }

    public bool TrySpeakPattern(BossSpeechSituationEnum situation, float duration)
    {
        return SpeakSituation(situation, duration);
    }

    public bool TrySpeakPatternAt(
        BossSpeechSituationEnum situation,
        float duration,
        Transform anchor,
        Vector3 offsetDelta)
    {
        IBossSpeechPlayback controller = ResolveSpeechController();
        return controller != null &&
               controller.TrySpeakSituationParallelAt(situation, duration, anchor, offsetDelta);
    }

    public bool TrySpeakPatternAt(
        BossSpeechSituationEnum situation,
        float duration,
        Func<Vector3> anchorPositionResolver,
        Vector3 offsetDelta)
    {
        IBossSpeechPlayback controller = ResolveSpeechController();
        return controller != null &&
               controller.TrySpeakSituationParallelAt(situation, duration, anchorPositionResolver, offsetDelta);
    }

    public bool TrySpeakPatternAt(
        BossSpeechSituationEnum situation,
        float duration,
        Func<Vector3> anchorPositionResolver,
        Func<Quaternion> anchorRotationResolver,
        Vector3 offsetDelta)
    {
        IBossSpeechPlayback controller = ResolveSpeechController();
        return controller != null &&
               controller.TrySpeakSituationParallelAt(situation, duration, anchorPositionResolver, anchorRotationResolver, offsetDelta);
    }

    private IBossSpeechPlayback ResolveSpeechController()
    {
        if (speechController == null)
            speechController = GetComponent<IBossSpeechPlayback>();

        return speechController;
    }

    public Vector2 ResolveSwordHoldPosition(Vector3 localOffset)
    {
        Vector3 offset = localOffset;
        if (IsFacingLeft)
            offset.x = -offset.x;

        return transform.position + offset;
    }

    public Vector2 ResolveVfxSocketWorld(
        DemonKingVfxSocketId socketId,
        Vector2 fallbackLeftFacingLocalOffset)
    {
        DemonKingVfxSocketMap socketMap = ResolveVfxSocketMap();
        if (socketMap != null)
            return socketMap.ResolveWorldPosition(socketId, fallbackLeftFacingLocalOffset, IsFacingLeft);

        Vector3 localOffset = ResolveLeftFacingLocalOffset(fallbackLeftFacingLocalOffset);
        return transform.TransformPoint(localOffset);
    }

    public Vector2 ResolveVfxSocketWorldAtBasePosition(
        DemonKingVfxSocketId socketId,
        Vector2 baseWorldPosition,
        Vector2 fallbackLeftFacingLocalOffset)
    {
        DemonKingVfxSocketMap socketMap = ResolveVfxSocketMap();
        if (socketMap != null)
            return socketMap.ResolveWorldPositionAt(socketId, baseWorldPosition, fallbackLeftFacingLocalOffset, IsFacingLeft);

        Vector3 localOffset = ResolveLeftFacingLocalOffset(fallbackLeftFacingLocalOffset);
        return baseWorldPosition + (Vector2)transform.TransformVector(localOffset);
    }

    public Vector3 ResolveVfxSocketLocal(
        DemonKingVfxSocketId socketId,
        Vector2 fallbackLeftFacingLocalOffset)
    {
        DemonKingVfxSocketMap socketMap = ResolveVfxSocketMap();
        if (socketMap != null)
            return socketMap.ResolveLocalOffset(socketId, fallbackLeftFacingLocalOffset, IsFacingLeft);

        return ResolveLeftFacingLocalOffset(fallbackLeftFacingLocalOffset);
    }

    private Vector3 ResolveLeftFacingLocalOffset(Vector2 leftFacingLocalOffset)
    {
        Vector3 offset = leftFacingLocalOffset;
        if (!IsFacingLeft)
            offset.x = -offset.x;

        return offset;
    }

    private DemonKingVfxSocketMap ResolveVfxSocketMap()
    {
        if (vfxSocketMap == null)
            vfxSocketMap = GetComponentInChildren<DemonKingVfxSocketMap>(true);

        return vfxSocketMap;
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

        ApplyFacingLeft(direction.x < 0f);
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

    public bool PlayPatternAnimation(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        ReleasePatternAnimationHold();
        return TryPlayPatternAnimation(stateName, 0f, allowDuringGroggy, allowDuringFinalDesperation);
    }

    public bool PlayPatternAnimationOncePerPattern(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName) || playedPatternAnimationStartStates.Contains(stateName))
            return false;

        if (!PlayPatternAnimation(stateName, allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        playedPatternAnimationStartStates.Add(stateName);
        return true;
    }

    public bool PlayPatternAnimationIfChanged(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        ReleasePatternAnimationHold();
        if (!CanPlayPatternAnimation(allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        if (IsCurrentPatternAnimationState(stateName))
            return true;

        return TryPlayPatternAnimation(stateName, 0f, allowDuringGroggy, allowDuringFinalDesperation);
    }

    public bool HoldPatternAnimationFirstFrame(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        ReleasePatternAnimationHold();
        if (!TryPlayPatternAnimation(stateName, 0f, allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        HoldPatternAnimatorSpeed();
        return true;
    }

    public bool HoldPatternAnimationFirstFrameOncePerPattern(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName) || playedPatternAnimationStartStates.Contains(stateName))
            return false;

        if (!HoldPatternAnimationFirstFrame(stateName, allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        playedPatternAnimationStartStates.Add(stateName);
        return true;
    }

    public bool HoldPatternAnimationFrame(
        string stateName,
        int frameIndex,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!CanPlayPatternAnimation(allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        float normalizedTime = ResolvePatternAnimationFrameNormalizedTime(stateName, frameIndex);
        ReleasePatternAnimationHold();
        if (!TryPlayPatternAnimation(stateName, normalizedTime, allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        HoldPatternAnimatorSpeed();
        return true;
    }

    public bool PlayPatternAnimationLastFrame(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!CanPlayPatternAnimation(allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        float normalizedTime = ResolvePatternAnimationLastFrameNormalizedTime(stateName);
        if (IsCurrentPatternAnimationStateAtOrAfter(stateName, normalizedTime))
            return true;

        ReleasePatternAnimationHold();
        return TryPlayPatternAnimation(stateName, normalizedTime, allowDuringGroggy, allowDuringFinalDesperation);
    }

    public bool HoldPatternAnimationLastFrame(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!CanPlayPatternAnimation(allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        float normalizedTime = ResolvePatternAnimationLastFrameNormalizedTime(stateName);
        if (IsCurrentPatternAnimationStateAtOrAfter(stateName, normalizedTime))
        {
            HoldPatternAnimatorSpeed();
            return true;
        }

        ReleasePatternAnimationHold();
        if (!TryPlayPatternAnimation(stateName, normalizedTime, allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        HoldPatternAnimatorSpeed();
        return true;
    }

    public void ReleasePatternAnimationHold()
    {
        if (!patternAnimationHoldActive)
            return;

        patternAnimationHoldActive = false;
        if (animator != null)
            animator.speed = patternAnimationSpeedBeforeHold;
        patternAnimationSpeedBeforeHold = 1f;
    }

    public float ResolvePatternAnimationLastFrameStartDelay(string stateName, float fallbackSeconds = 0.08333334f)
    {
        if (!TryResolvePatternAnimationClip(stateName, out AnimationClip clip))
            return Mathf.Max(0f, fallbackSeconds);

        float frameSeconds = ResolveClipFrameSeconds(clip, fallbackSeconds);
        return Mathf.Max(0f, clip.length - frameSeconds);
    }

    public float ResolvePatternAnimationFrameSeconds(string stateName, float fallbackSeconds = 0.08333334f)
    {
        if (!TryResolvePatternAnimationClip(stateName, out AnimationClip clip))
            return Mathf.Max(0f, fallbackSeconds);

        return ResolveClipFrameSeconds(clip, fallbackSeconds);
    }

    public void RestoreCombatPose()
    {
        ReleasePatternAnimationHold();

        if (IsDead || HasDeadTag() || RuntimeData.FinalDesperationStarted)
            return;

        if (HasGroggyTag())
        {
            PlayGroggyPose();
            return;
        }

        string stateName = RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold
            ? DarkLordSwordIdleState
            : DarkLordHandIdleState;
        PlayPatternAnimationIfChanged(stateName);
    }

    public string ResolveGroggyCounterAnimationState()
    {
        return RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold
            ? DarkLordSwordGroggyCounterState
            : DarkLordHandGroggyCounterState;
    }

    public bool HoldGroggyPoseAnimation(bool allowDuringGroggy = true)
    {
        if (IsDead || HasDeadTag() || RuntimeData.FinalDesperationStarted)
            return false;

        return HoldPatternAnimationCurrentFrameIfState(
            ResolveGroggyPoseState(),
            allowDuringGroggy: allowDuringGroggy);
    }

    private bool HoldPatternAnimationCurrentFrameIfState(
        string stateName,
        bool allowDuringGroggy = false,
        bool allowDuringFinalDesperation = false)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!CanPlayPatternAnimation(allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        if (IsCurrentPatternAnimationState(stateName))
        {
            HoldPatternAnimatorSpeed();
            return true;
        }

        return HoldPatternAnimationFirstFrame(stateName, allowDuringGroggy, allowDuringFinalDesperation);
    }

    private bool TryPlayPatternAnimation(
        string stateName,
        float normalizedTime,
        bool allowDuringGroggy,
        bool allowDuringFinalDesperation)
    {
        if (!CanPlayPatternAnimation(allowDuringGroggy, allowDuringFinalDesperation))
            return false;

        if (!TryResolvePatternAnimationStateHash(stateName, out int stateHash))
            return false;

        animator.Play(stateHash, 0, Mathf.Clamp01(normalizedTime));
        animator.Update(0f);
        return true;
    }

    public void PushFaceTargetLock()
    {
        faceTargetLockCount++;
    }

    public void PopFaceTargetLock()
    {
        faceTargetLockCount = Mathf.Max(0, faceTargetLockCount - 1);
    }

    public void PushThresholdStaggerGuard()
    {
        if (staggerImmuneTag == null || TagSystem == null)
            return;

        if (thresholdStaggerGuardCount <= 0 && !TryAddStateTag(staggerImmuneTag, 1))
            return;

        thresholdStaggerGuardCount++;
    }

    public void PopThresholdStaggerGuard()
    {
        if (thresholdStaggerGuardCount <= 0)
            return;

        thresholdStaggerGuardCount--;
        if (thresholdStaggerGuardCount > 0 || staggerImmuneTag == null || TagSystem == null)
            return;

        TryRemoveStateTag(staggerImmuneTag, 1);
    }

    public void BeginBodyAfterimage()
    {
        if (!enableBodyAfterimage || !isActiveAndEnabled)
            return;

        IAfterimageEmitter2D emitter = ResolveBodyAfterimageEmitter();
        if (emitter == null)
            return;

        emitter.Begin(
            transform,
            bodyAfterimageIntervalSeconds,
            bodyAfterimageLifetimeSeconds,
            bodyAfterimageColor);
    }

    public void StopBodyAfterimage(bool clearGhosts = false)
    {
        if (bodyAfterimageEmitter == null)
            return;

        bodyAfterimageEmitter.StopEmission();
        if (clearGhosts)
            bodyAfterimageEmitter.ClearSpawnedGhosts();
    }

    public void ReleaseFinalDesperationHealthClamp()
    {
        finalDesperationHealthClampActive = false;
    }

    public void SetSwordDropped()
    {
        RuntimeData.SetSwordDropped();
    }

    public void NotifyEgoSwordPatternCompleted()
    {
        RuntimeData.RecordEgoSwordPatternUse(droppedSwordPatternsBeforeRecall);
    }

    public bool TryStartEgoSwordVerticalStrikeSubPattern()
    {
        return TryStartEgoSwordSubPatternAbility(egoSwordVerticalStrikeAbility, "EgoSwordVerticalStrike");
    }

    public bool TryStartEgoSwordCrossLaserSubPattern()
    {
        return TryStartEgoSwordSubPatternAbility(egoSwordCrossLaserAbility, "EgoSwordCrossLaser");
    }

    public void CompleteEgoSwordRecall()
    {
        RuntimeData.SetSwordHeld();
        ResolveEgoSword()?.HideWhileHeld();
    }

    public void MarkHp50PatternUsed()
    {
        RuntimeData.MarkHp50PatternUsed();
    }

    public void MarkFinalDesperationStarted()
    {
        RuntimeData.MarkFinalDesperationStarted();
    }

    public void ShowEgoSwordFinalDesperationPlant(Vector2 center)
    {
        ResolveEgoSword()?.ShowFinalDesperationPlanted(center, FacingDirection);
    }

#if UNITY_EDITOR
    public void RefreshWorkbenchRuntimeState()
    {
        if (AbilitySystem != null)
            AbilitySystem.ResetTransientRuntimeState();

        ReleasePatternAnimationHold();
        ClearPatternAnimationStartRecords();
        ReleaseFinalDesperationHealthClamp();
        ClearThresholdStaggerGuard();
        TryEndGroggyStateImmediately();
        StopBodyAfterimage(clearGhosts: true);
        GetComponent<AbilityMotionController2D>()?.CancelMotion();

        RuntimeData.ResetForWorkbenchRuntimeRefresh();
        CompleteEgoSwordRecall();
        RestoreCombatPose();
    }
#endif

    public EgoSwordActor ResolveEgoSword()
    {
        if (egoSword != null)
        {
            egoSword.Bind(this);
            return egoSword;
        }

        egoSword = FindAnyObjectByType<EgoSwordActor>(FindObjectsInactive.Include);
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

    private IAfterimageEmitter2D ResolveBodyAfterimageEmitter()
    {
        if (bodyAfterimageEmitter != null)
            return bodyAfterimageEmitter;

        bodyAfterimageEmitter = AfterimageEmitterPlayback.GetOrAdd(gameObject);
        return bodyAfterimageEmitter;
    }

    private void ClearThresholdStaggerGuard()
    {
        if (thresholdStaggerGuardCount <= 0)
            return;

        thresholdStaggerGuardCount = 0;
        if (staggerImmuneTag != null && TagSystem != null)
            TryRemoveStateTag(staggerImmuneTag, 1);
    }

    private void ApplyPermanentKnockbackImmunity()
    {
        if (permanentKnockbackImmuneApplied || knockbackImmuneTag == null || TagSystem == null)
            return;

        permanentKnockbackImmuneApplied = TryAddStateTag(knockbackImmuneTag, 1);
    }

    private void ClearPermanentKnockbackImmunity()
    {
        if (!permanentKnockbackImmuneApplied)
            return;

        permanentKnockbackImmuneApplied = false;
        if (knockbackImmuneTag != null && TagSystem != null)
            TryRemoveStateTag(knockbackImmuneTag, 1);
    }

    private bool TryStartFinalDesperationFromHealthGate(
        AttributeDefinition attribute,
        float oldValue,
        float newValue)
    {
        if (IsDead || RuntimeData.FinalDesperationStarted)
            return false;

        if (!IsCurrentHealthAttribute(attribute))
            return false;

        float thresholdHealth = ResolveFinalDesperationThresholdHealth();
        if (thresholdHealth <= 0f || oldValue <= thresholdHealth || newValue > thresholdHealth)
            return false;

        EnsurePatternRolesReady();
        if (finalDesperationEntry == null)
            return false;

        finalDesperationHealthClampActive = true;
        RestoreFinalDesperationThresholdHealth(thresholdHealth);
        ForceFinalDesperationNow();
        return true;
    }

    private bool TryMaintainFinalDesperationHealthClamp(AttributeDefinition attribute, float newValue)
    {
        if (!finalDesperationHealthClampActive || IsDead || !IsCurrentHealthAttribute(attribute))
            return false;

        float thresholdHealth = ResolveFinalDesperationThresholdHealth();
        if (thresholdHealth <= 0f || newValue >= thresholdHealth)
            return false;

        RestoreFinalDesperationThresholdHealth(thresholdHealth);
        return true;
    }

    private float ResolveFinalDesperationThresholdHealth()
    {
        float maxHealth = MaxHealthValue;
        if (maxHealth <= 0f)
            return 0f;

        return Mathf.Max(0.01f, maxHealth * finalDesperationHpRatio);
    }

    private void RestoreFinalDesperationThresholdHealth(float thresholdHealth)
    {
        try
        {
            restoringFinalDesperationHealthClamp = true;
            TrySetCurrentHealthValue(thresholdHealth, this);
        }
        finally
        {
            restoringFinalDesperationHealthClamp = false;
        }
    }

    private void EnsurePatternRolesReady()
    {
        if (ShouldConfigureRuntimePatterns())
            ConfigureRuntimePatternsIfNeeded();
        else
            BindAuthoredPatternRolesIfNeeded();
    }

    private bool CanUseHp50Rush()
    {
        return !RuntimeData.FinalDesperationStarted &&
               !RuntimeData.Hp50PatternUsed &&
               CurrentHealthRatio > finalDesperationHpRatio &&
               CurrentHealthRatio <= hp50RushRatio;
    }

    private bool ShouldThrowSwordForHp50Rush()
    {
        return CanUseHp50Rush() && RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold;
    }

    private bool IsFacingLeft => sprite == null || !sprite.flipX;

    private void ApplyFacingLeft(bool faceLeft)
    {
        if (sprite != null)
            sprite.flipX = !faceLeft;
    }

    private bool TryFaceTargetX(float targetX, float deadZone)
    {
        if (sprite == null)
            return false;

        float deltaX = targetX - transform.position.x;
        if (Mathf.Abs(deltaX) <= deadZone)
            return false;

        ApplyFacingLeft(deltaX < 0f);
        return true;
    }

    private bool PlayDeathPoseAnimation()
    {
        ReleasePatternAnimationHold();
        ClearPatternAnimationStartRecords();
        if (!TryResolvePatternAnimationStateHash(DarkLordHandGroggyState, out int stateHash))
            return false;

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
        return true;
    }

    private void PlayGroggyPose()
    {
        if (IsDead || HasDeadTag() || RuntimeData.FinalDesperationStarted)
            return;

        PlayPatternAnimationIfChanged(ResolveGroggyPoseState(), allowDuringGroggy: true);
    }

    private string ResolveGroggyPoseState()
    {
        return RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold
            ? DarkLordSwordGroggyState
            : DarkLordHandGroggyState;
    }

    private bool TryResolvePatternAnimationStateHash(string stateName, out int stateHash)
    {
        stateHash = 0;
        if (animator == null)
        {
            WarnPatternAnimationInvalid(stateName, "missing Animator");
            return false;
        }

        if (!animator.isActiveAndEnabled)
        {
            WarnPatternAnimationInvalid(stateName, "Animator is inactive or disabled");
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            WarnPatternAnimationInvalid(
                stateName,
                "Animator has no RuntimeAnimatorController; assign DarkLordBoss.controller to the DemonKing Animator");
            return false;
        }

        stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
            return true;

        if (animator.layerCount > 0)
        {
            string layerName = animator.GetLayerName(0);
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                stateHash = Animator.StringToHash($"{layerName}.{stateName}");
                if (animator.HasState(0, stateHash))
                    return true;
            }
        }

        WarnPatternAnimationInvalid(stateName, $"AnimatorController has no state '{stateName}'");
        return false;
    }

    private bool CanPlayPatternAnimation(bool allowDuringGroggy, bool allowDuringFinalDesperation)
    {
        if (IsDead || HasDeadTag())
            return false;

        if (!allowDuringGroggy && HasGroggyTag())
            return false;

        if (!allowDuringFinalDesperation && RuntimeData.FinalDesperationStarted)
            return false;

        return true;
    }

    private bool IsCurrentPatternAnimationState(string stateName)
    {
        return TryGetCurrentPatternAnimationStateInfo(stateName, out _);
    }

    private bool IsCurrentPatternAnimationStateAtOrAfter(string stateName, float normalizedTime)
    {
        if (!TryGetCurrentPatternAnimationStateInfo(stateName, out AnimatorStateInfo stateInfo))
            return false;

        float currentNormalizedTime = stateInfo.loop
            ? Mathf.Repeat(stateInfo.normalizedTime, 1f)
            : stateInfo.normalizedTime;
        return currentNormalizedTime >= Mathf.Clamp01(normalizedTime);
    }

    private bool TryGetCurrentPatternAnimationStateInfo(string stateName, out AnimatorStateInfo stateInfo)
    {
        stateInfo = default;
        if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrWhiteSpace(stateName))
            return false;

        stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int shortNameHash = Animator.StringToHash(stateName);
        if (stateInfo.shortNameHash == shortNameHash)
            return true;

        if (animator.layerCount <= 0)
            return false;

        string layerName = animator.GetLayerName(0);
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
        return stateInfo.fullPathHash == fullPathHash;
    }

    private void ClearPatternAnimationStartRecords()
    {
        playedPatternAnimationStartStates.Clear();
    }

    private bool TryResolvePatternAnimationClip(string stateName, out AnimationClip clip)
    {
        clip = null;
        if (animator == null)
        {
            WarnPatternAnimationInvalid(stateName, "missing Animator");
            return false;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            WarnPatternAnimationInvalid(
                stateName,
                "Animator has no RuntimeAnimatorController; assign DarkLordBoss.controller to the DemonKing Animator");
            return false;
        }

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip candidate = clips[i];
            if (candidate != null && candidate.name == stateName)
            {
                clip = candidate;
                return true;
            }
        }

        WarnPatternAnimationInvalid(stateName, $"AnimatorController has no clip '{stateName}'");
        return false;
    }

    private float ResolvePatternAnimationLastFrameNormalizedTime(string stateName)
    {
        if (!TryResolvePatternAnimationClip(stateName, out AnimationClip clip) || clip.length <= 0f)
            return 0f;

        float frameSeconds = ResolveClipFrameSeconds(clip, 0.08333334f);
        return Mathf.Clamp01(Mathf.Max(0f, clip.length - frameSeconds) / clip.length);
    }

    private float ResolvePatternAnimationFrameNormalizedTime(string stateName, int frameIndex)
    {
        if (!TryResolvePatternAnimationClip(stateName, out AnimationClip clip) || clip.length <= 0f)
            return 0f;

        float frameSeconds = ResolveClipFrameSeconds(clip, 0.08333334f);
        float sampleTime = Mathf.Clamp(Mathf.Max(0, frameIndex) * frameSeconds, 0f, Mathf.Max(0f, clip.length - frameSeconds));
        return Mathf.Clamp01(sampleTime / clip.length);
    }

    private static float ResolveClipFrameSeconds(AnimationClip clip, float fallbackSeconds)
    {
        if (clip == null || clip.frameRate <= 0f)
            return Mathf.Max(0.001f, fallbackSeconds);

        return Mathf.Max(0.001f, 1f / clip.frameRate);
    }

    private void HoldPatternAnimatorSpeed()
    {
        if (animator == null)
            return;

        if (!patternAnimationHoldActive)
        {
            patternAnimationSpeedBeforeHold = animator.speed;
            patternAnimationHoldActive = true;
        }

        animator.speed = 0f;
    }

    private void WarnPatternAnimationInvalid(string stateName, string reason)
    {
        string key = $"{stateName}:{reason}";
        if (patternAnimationWarnings.Add(key))
            Debug.LogWarning($"DemonKing pattern animation '{stateName}' skipped: {reason}.", this);
    }

    private void CleanupEgoSwordForBattleEnd()
    {
        if (!Application.isPlaying)
            return;

        RuntimeData.SetSwordHeld();

        if (egoSword == null)
            return;

        egoSword.CleanupForBossBattleEnd();
    }

    private void StartEgoSwordDeathPlant()
    {
        if (!Application.isPlaying)
            return;

        RuntimeData.SetSwordHeld();
        ResolveEgoSword()?.StartDeathPlant(transform.position, FacingDirection);
    }

    private void PrepareEgoSwordHeldHidden()
    {
        RuntimeData.SetSwordHeld();
        ResolveEgoSword()?.HideWhileHeld();
    }

    private void ConfigureRuntimePatternsIfNeeded()
    {
        if (runtimePatternsConfigured)
            return;

        runtimePatternsConfigured = true;
        authoredPatternRolesBound = false;
        roleByAbility.Clear();
        ClearSpecialPatternEntries();

        BossPatternEntry pierce = CreatePattern<AbilityLogic_DemonKingPierceCombo>(
            "DemonKing_PierceCombo",
            DemonKingPatternRole.HoldNormal,
            weight: 120,
            maxConsecutive: 1,
            lockTime: 0.25f,
            postDelay: 0.25f,
            minDistance: 0f,
            maxDistance: 999f);
        BossPatternEntry heavySlash = CreatePattern<AbilityLogic_DemonKingHeavySlash>(
            "DemonKing_HeavySlash",
            DemonKingPatternRole.HoldNormal,
            weight: 90,
            maxConsecutive: 1,
            lockTime: 0.5f,
            postDelay: 0.35f,
            minDistance: 0f,
            maxDistance: 999f);

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

        RegisterPatternRole(ability, role, null);

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

    private bool ShouldConfigureRuntimePatterns()
    {
        return configureRuntimePatternsOnStart || ConfiguredPhaseCount == 0;
    }

    private void RegisterEgoSwordSubPatternAbilities()
    {
        RegisterEgoSwordSubPatternAbility(egoSwordVerticalStrikeAbility, "EgoSwordVerticalStrike");
        RegisterEgoSwordSubPatternAbility(egoSwordCrossLaserAbility, "EgoSwordCrossLaser");
    }

    private bool RegisterEgoSwordSubPatternAbility(AbilityDefinition ability, string label)
    {
        if (ability == null)
            return false;

        if (ability.executionPolicy != AbilityDefinition.ExecutionPolicy.ParallelIndependent)
        {
            Debug.LogWarning(
                $"DemonKing {label} should use ParallelIndependent so it can run outside the main boss pattern timer.",
                this);
        }

        return TryRegisterAbility(ability);
    }

    private bool TryStartEgoSwordSubPatternAbility(AbilityDefinition ability, string label)
    {
        if (ability == null)
        {
            if (!missingEgoSwordSubPatternAbilityLogged)
            {
                missingEgoSwordSubPatternAbilityLogged = true;
                Debug.LogWarning(
                    $"DemonKing is missing an authored GAS AbilityDefinition for {label}. EgoSword dropped subpatterns will not run.",
                    this);
            }

            return false;
        }

        RegisterEgoSwordSubPatternAbility(ability, label);
        return TryStartAbility(ability);
    }

    private void BindAuthoredPatternRolesIfNeeded()
    {
        if (authoredPatternRolesBound || runtimePatternsConfigured)
            return;

        authoredPatternRolesBound = true;
        roleByAbility.Clear();
        ClearSpecialPatternEntries();

        IReadOnlyList<BossPhaseConfig> phases = ConfiguredPhases;
        if (phases == null)
            return;

        for (int phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
        {
            IReadOnlyList<BossPatternEntry> patterns = phases[phaseIndex]?.Patterns;
            if (patterns == null)
                continue;

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                BossPatternEntry pattern = patterns[patternIndex];
                AbilityDefinition ability = pattern != null ? pattern.Ability : null;
                if (ability == null)
                    continue;

                if (TryResolvePatternRole(ability, out DemonKingPatternRole role))
                    RegisterPatternRole(ability, role, pattern);
            }
        }
    }

    private void ClearSpecialPatternEntries()
    {
        throwSwordEntry = null;
        recallSwordEntry = null;
        hp50RushEntry = null;
        groggyRecoverCounterEntry = null;
        finalDesperationEntry = null;
    }

    private void RegisterPatternRole(AbilityDefinition ability, DemonKingPatternRole role, BossPatternEntry patternEntry)
    {
        if (ability == null)
            return;

        roleByAbility[ability] = role;

        switch (role)
        {
            case DemonKingPatternRole.ThrowSword:
                throwSwordEntry = patternEntry;
                break;
            case DemonKingPatternRole.RecallSword:
                recallSwordEntry = patternEntry;
                break;
            case DemonKingPatternRole.Hp50Rush:
                hp50RushEntry = patternEntry;
                break;
            case DemonKingPatternRole.GroggyRecoverCounter:
                groggyRecoverCounterEntry = patternEntry;
                break;
            case DemonKingPatternRole.FinalDesperation:
                finalDesperationEntry = patternEntry;
                break;
        }
    }

    private static bool TryResolvePatternRole(AbilityDefinition ability, out DemonKingPatternRole role)
    {
        role = default;
        AbilityLogic logic = ability != null ? ability.logic : null;
        if (logic == null)
            return false;

        switch (logic)
        {
            case AbilityLogic_DemonKingPierceCombo:
            case AbilityLogic_DemonKingHeavySlash:
                role = DemonKingPatternRole.HoldNormal;
                return true;
            case AbilityLogic_DemonKingThrowEgoSword:
                role = DemonKingPatternRole.ThrowSword;
                return true;
            case AbilityLogic_DemonKingHomingMagic:
            case AbilityLogic_DemonKingBombardment:
            case AbilityLogic_DemonKingExplosionJump:
                role = DemonKingPatternRole.DropNormal;
                return true;
            case AbilityLogic_DemonKingRecallEgoSword:
                role = DemonKingPatternRole.RecallSword;
                return true;
            case AbilityLogic_DemonKingWallBounceRush:
                role = DemonKingPatternRole.Hp50Rush;
                return true;
            case AbilityLogic_DemonKingGroggyRecoverCounter:
                role = DemonKingPatternRole.GroggyRecoverCounter;
                return true;
            case AbilityLogic_DemonKingFinalDesperation:
                role = DemonKingPatternRole.FinalDesperation;
                return true;
            default:
                return false;
        }
    }

    private void ForceFinalDesperationNow()
    {
        EnsurePatternRolesReady();
        if (finalDesperationEntry == null)
            return;

        RuntimeData.MarkFinalDesperationStarted();
        TryEndGroggyStateImmediately();
        AbortCurrentPattern();
        PatternRuntime.ReserveForcedPattern(finalDesperationEntry);
        ChangeState(GetPatternState(finalDesperationEntry));
    }

    private bool CanAutoFaceTarget()
    {
        return IsCombatActive && !HasGroggyTag() && !HasDeadTag();
    }

    private void FaceCurrentTarget()
    {
        if (CurrentTarget == null)
            return;

        TryFaceTargetX(CurrentTarget.position.x, AutoFaceTargetDeadZone);
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
