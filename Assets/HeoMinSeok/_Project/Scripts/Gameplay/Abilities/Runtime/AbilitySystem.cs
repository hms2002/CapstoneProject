using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityGAS.AbilityDefinition;

namespace UnityGAS
{
    public class AbilitySystem : MonoBehaviour
    {
        [Header("Initial Abilities (Definitions)")]
        [SerializeField] private List<AbilityDefinition> initialAbilities = new();

        private readonly List<AbilitySpec> runtimeSpecs = new();

        [Header("Components")]
        [SerializeField] private AttributeSet attributeSet;
        [SerializeField] private GameplayEffectRunner effectRunner;
        [SerializeField] private TagSystem tagSystem;

        [Header("Execution")]
        [SerializeField] private bool enableExclusiveActivationBuffer = false;

        [Header("Cancellation Tags (Global)")]
        [SerializeField] private List<GameplayTag> globalCancelCastingOnTags = new();
        [SerializeField] private List<GameplayTag> globalCancelExecutionOnTags = new();

        [SerializeField] private DamageProfileDefinition damageProfile;
        [SerializeField] private GameplayEffect defaultCooldownEffect;

        [Header("Cooldown Attributes (GAS-style)")]
        [SerializeField] private AttributeDefinition cooldownDurationMultiplierAttribute;
        [SerializeField] private AttributeDefinition cooldownFlatReduceSecondsOnHitAttribute;
        [SerializeField] private float minCooldownSeconds = 0.05f;

        [Header("Cue")]
        [SerializeField] private GameplayCueManager cueManager;
        [SerializeField] private bool autoExecuteCueWhenGameplayEventTagExists = true;

        [Header("Combat Events")]
        [SerializeField] private GameplayTag killConfirmedTag;
        [SerializeField] private GameplayTag damagedTag;

        public GameplayTag KillConfirmedTag => killConfirmedTag;
        public GameplayTag DamagedTag => damagedTag;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Animator initialWeaponAnimator;

        private bool isCasting;
        private bool isExecuting;
        private float castTimeRemaining;

        private AbilitySpec currentCastSpec;
        private GameObject currentTarget;

        private AbilitySpec currentExecSpec;
        private GameObject currentExecTarget;

        private Coroutine activeExecution;

        private AbilitySpec bufferedSpec;
        private GameObject bufferedTarget;
        private float bufferedActivationNotBeforeTime;

        /// <summary>
        /// 책임 :
        /// - Exclusive 실행과 별도로 살아 있는 병행 실행 목록을 관리한다.
        /// - 1차 구현에서는 동일 spec의 중복 병행 실행은 허용하지 않는다.
        /// </summary>
        private readonly List<ParallelAbilityExecution> parallelExecutions = new();
        /// <summary>
        /// 책임 : 특정 spec이 현재 병행 실행 중인지 판별한다.
        /// </summary>
        internal bool IsParallelExecuting(AbilitySpec spec)
        {
            if (spec == null)
                return false;

            for (int i = 0; i < parallelExecutions.Count; i++)
            {
                if (parallelExecutions[i] != null && parallelExecutions[i].Spec == spec)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 책임 : 병행 실행 시작 시 spec/target을 등록한다.
        /// </summary>
        internal void BeginParallelExecution(AbilitySpec spec, GameObject target)
        {
            if (spec == null || IsParallelExecuting(spec))
                return;

            parallelExecutions.Add(new ParallelAbilityExecution
            {
                Spec = spec,
                Target = target,
                Coroutine = null
            });
        }

        /// <summary>
        /// 책임 : StartCoroutine 직후 생성된 Coroutine 핸들을 병행 실행 기록에 연결한다.
        /// </summary>
        internal void AttachParallelExecutionCoroutine(AbilitySpec spec, Coroutine coroutine)
        {
            if (spec == null)
                return;

            for (int i = 0; i < parallelExecutions.Count; i++)
            {
                var exec = parallelExecutions[i];
                if (exec != null && exec.Spec == spec)
                {
                    exec.Coroutine = coroutine;
                    return;
                }
            }
        }

        /// <summary>
        /// 책임 : 병행 실행 종료 시 등록 정보를 제거한다.
        /// </summary>
        internal void EndParallelExecution(AbilitySpec spec)
        {
            if (spec == null)
                return;

            for (int i = parallelExecutions.Count - 1; i >= 0; i--)
            {
                var exec = parallelExecutions[i];
                if (exec != null && exec.Spec == spec)
                {
                    parallelExecutions.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 책임 : 특정 병행 실행을 강제로 정리한다.
        /// coroutine이 Stop될 때 finally가 보장되지 않는 경로를 대비한 수동 정리 창구다.
        /// </summary>
        private void ForceCleanupParallelExecution(ParallelAbilityExecution exec, bool cancelled)
        {
            if (exec == null || exec.Spec == null || exec.Spec.Definition == null)
                return;

            var spec = exec.Spec;
            var def = spec.Definition;
            var target = exec.Target;

            CancelGameplayEventWaiters(spec);
            presentationRouter?.PlayExecutionEnd(def, spec, target, cancelled);

            if (tagSystem != null && def.grantedTagsWhileActive != null)
                tagSystem.RemoveTags(def.grantedTagsWhileActive);

            spec.Token?.Cancel();
            spec.Token = null;

            if (exec.Coroutine != null)
                StopCoroutine(exec.Coroutine);
        }

        /// <summary>
        /// 책임 : 현재 살아 있는 모든 병행 실행을 강제로 정리한다.
        /// 씬 이동/리셋/강제 종료 경로에서 사용한다.
        /// </summary>
        private void ForceCleanupAllParallelExecutions(bool cancelled)
        {
            for (int i = parallelExecutions.Count - 1; i >= 0; i--)
            {
                ForceCleanupParallelExecution(parallelExecutions[i], cancelled);
            }

            parallelExecutions.Clear();
        }

        /// <summary>
        /// 책임 : 특정 태그 추가 시 취소 조건에 걸리는 병행 실행만 골라 취소한다.
        /// </summary>
        private void CancelMatchingParallelExecutions(GameplayTag tag, bool force)
        {
            if (tag == null)
                return;

            for (int i = 0; i < parallelExecutions.Count; i++)
            {
                var exec = parallelExecutions[i];
                var spec = exec != null ? exec.Spec : null;
                var def = spec != null ? spec.Definition : null;
                if (def == null)
                    continue;

                bool globalHit = ContainsTag(globalCancelExecutionOnTags, tag);
                bool localHit = ContainsTag(def.cancelExecutionOnTags, tag);

                if (!globalHit && !localHit)
                    continue;

                if (!force && !def.interruptible)
                    continue;

                spec.Token?.Cancel();
            }
        }
        private struct ChargeState
        {
            public int charges;
            public float rechargeRemaining;
        }

        private readonly Dictionary<AbilityDefinition, ChargeState> savedChargeStates = new();

        private const string KEY_CHARGES = "__Charges";
        private const string KEY_RECHARGE = "__RechargeRemaining";
        private const string KEY_NEXT_ACTIVATE_ALLOWED_TIME = "__NextActivateAllowedTime";

        private AbilityGameplayEventChannel gameplayEventChannel;
        private AbilityCooldownController cooldownController;
        private AbilityPresentationRouter presentationRouter;
        private AbilityVisualRouter visualRouter;
        private AbilityHitCueRouter hitCueRouter;
        private AbilityExecutionCoordinator executionCoordinator;

        public DamageProfileDefinition DamageProfile => damageProfile;
        public AttributeSet AttributeSet => attributeSet;
        public GameplayEffectRunner EffectRunner => effectRunner;
        public TagSystem TagSystem => tagSystem;

        public Animator PlayerAnimator => presentationRouter != null ? presentationRouter.PlayerAnimator : playerAnimator;
        public Animator WeaponAnimator => presentationRouter != null ? presentationRouter.WeaponAnimator : initialWeaponAnimator;

        public bool IsCasting => isCasting;
        public bool IsExecuting => isExecuting;
        public bool IsBusy => isCasting || isExecuting;

        public AbilityDefinition CurrentCast =>
            isCasting ? (currentCastSpec != null ? currentCastSpec.Definition : null)
                      : (currentExecSpec != null ? currentExecSpec.Definition : null);

        public AbilitySpec CurrentCastSpec => isCasting ? currentCastSpec : currentExecSpec;
        public AbilitySpec CurrentExecSpec => currentExecSpec;
        public GameObject CurrentTargetGameObject => isCasting ? currentTarget : currentExecTarget;

        internal AbilityPresentationRouter PresentationRouter => presentationRouter;
        internal AbilityVisualRouter VisualRouter => visualRouter;
        internal AbilityCooldownController CooldownController => cooldownController;

        public Action<AbilityDefinition> OnAbilityCastStart;
        public Action<AbilityDefinition> OnAbilityCastCompleted;
        public Action<AbilityDefinition> OnAbilityCastCancelled;

        public event Action<GameplayTag, AbilityEventData> GameplayEventRaised
        {
            add
            {
                if (gameplayEventChannel != null)
                    gameplayEventChannel.GameplayEventRaised += value;
            }
            remove
            {
                if (gameplayEventChannel != null)
                    gameplayEventChannel.GameplayEventRaised -= value;
            }
        }

        private void Awake()
        {
            CacheRequiredComponents();
            CreateControllers();
            InitializeInitialAbilities();
        }

        private void OnEnable()
        {
            if (tagSystem == null)
                tagSystem = GetComponent<TagSystem>();

            if (tagSystem != null)
                tagSystem.OnTagAdded += HandleOwnerTagAdded;
        }

        private void OnDisable()
        {
            if (tagSystem != null)
                tagSystem.OnTagAdded -= HandleOwnerTagAdded;
        }

        private void OnDestroy()
        {
            hitCueRouter?.Dispose();
            hitCueRouter = null;
        }

        private void Update()
        {
            cooldownController?.TickCooldowns(runtimeSpecs);
            TickCasting();
            TryConsumeBufferedActivationWhenReady();
        }

        private void CacheRequiredComponents()
        {
            if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
            if (effectRunner == null) effectRunner = GetComponent<GameplayEffectRunner>();
            if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
            if (damageProfile == null) damageProfile = GetComponent<DamageProfileDefinition>();

#if UNITY_2023_1_OR_NEWER
            if (cueManager == null) cueManager = UnityEngine.Object.FindAnyObjectByType<GameplayCueManager>();
#else
            if (cueManager == null) cueManager = FindObjectOfType<GameplayCueManager>();
#endif
        }

        private void CreateControllers()
        {
            if (playerAnimator == null)
                playerAnimator = animator != null ? animator : GetComponentInChildren<Animator>();

            presentationRouter = new AbilityPresentationRouter(
                gameObject,
                cueManager,
                playerAnimator,
                initialWeaponAnimator);
            visualRouter = new AbilityVisualRouter(gameObject);

            gameplayEventChannel = new AbilityGameplayEventChannel(
                this,
                cueManager,
                autoExecuteCueWhenGameplayEventTagExists);

            hitCueRouter = new AbilityHitCueRouter(
                this,
                cueManager,
                gameplayEventChannel);

            cooldownController = new AbilityCooldownController(
                this,
                effectRunner,
                attributeSet,
                defaultCooldownEffect,
                cooldownDurationMultiplierAttribute,
                cooldownFlatReduceSecondsOnHitAttribute,
                minCooldownSeconds);

            executionCoordinator = new AbilityExecutionCoordinator();
        }

        private void InitializeInitialAbilities()
        {
            runtimeSpecs.Clear();

            foreach (var def in initialAbilities)
            {
                if (def != null)
                    GiveAbility(def);
            }
        }


        public AbilitySpec GiveAbility(AbilityDefinition def)
        {
            if (def == null)
                return null;

            var existing = FindSpec(def);
            if (existing != null)
                return existing;

            var spec = new AbilitySpec(def);
            runtimeSpecs.Add(spec);

            RestoreChargeStateIfNeeded(def, spec);
            return spec;
        }
        
    

    public bool TakeAbility(AbilityDefinition def)
        {
            if (def == null)
                return false;

            AbilitySpec spec = FindSpec(def);
            if (spec == null)
                return false;

            SaveChargeStateIfNeeded(def, spec);

            if (CurrentExecSpec == spec)
                CancelExecution(force: true);

            if (CurrentCastSpec == spec)
                CancelCasting(force: true);

            if (IsParallelExecuting(spec))
            {
                for (int i = parallelExecutions.Count - 1; i >= 0; i--)
                {
                    var exec = parallelExecutions[i];
                    if (exec != null && exec.Spec == spec)
                    {
                        ForceCleanupParallelExecution(exec, cancelled: true);
                        parallelExecutions.RemoveAt(i);
                    }
                }
            }

            if (bufferedSpec == spec)
                ClearBufferedActivation();

            gameplayEventChannel?.CancelWaiters(spec);
            runtimeSpecs.Remove(spec);
            return true;
        }

        public AbilitySpec FindSpec(AbilityDefinition def)
        {
            if (def == null)
                return null;

            for (int i = 0; i < runtimeSpecs.Count; i++)
            {
                if (runtimeSpecs[i].Definition == def)
                    return runtimeSpecs[i];
            }

            return null;
        }

        /// <summary>
        /// 책임 : 씬 이동 직전에 현재 캐스팅/실행 중인 능력의 자체 cleanup을 먼저 호출하고,
        /// 그 다음 공통 일시 상태를 강제로 정리하여 저장 데이터에 남지 않도록 만든다.
        /// </summary>
        public void CancelAllForSceneTransition(IReadOnlyList<GameplayTagSet> extraCleanupTagSets = null)
        {
            var castingSpec = currentCastSpec;
            var castingTarget = currentTarget;
            var executingSpec = currentExecSpec;
            var executingTarget = currentExecTarget;

            var castingDef = castingSpec != null ? castingSpec.Definition : null;
            var executingDef = executingSpec != null ? executingSpec.Definition : null;
            // 책임 : 병행 실행 중인 ability들도 씬 이동 전에 자체 cleanup 기회를 받는다.
            for (int i = 0; i < parallelExecutions.Count; i++)
            {
                var exec = parallelExecutions[i];
                if (exec == null || exec.Spec == null)
                    continue;

                InvokeSceneTransitionCleanup(exec.Spec, exec.Target);
            }
            // 책임 : AbilityLogic이 직접 만든 modifier / motion / 직접 AddTag 상태를 먼저 정리할 기회를 준다.
            InvokeSceneTransitionCleanup(castingSpec, castingTarget);
            InvokeSceneTransitionCleanup(executingSpec, executingTarget);

            CancelCasting(force: true);
            CancelExecution(force: true);

            // 책임 : AbilityDefinition.grantedTagsWhileActive 로 부여한 태그를
            // 코루틴 종료 타이밍과 무관하게 즉시 회수한다.
            RemoveGrantedTagsImmediately(castingDef);
            RemoveGrantedTagsImmediately(executingDef);

            // 책임 : 씬 이동 시점에는 더 이상 기다리는 GameplayEvent가 의미 없으므로 전부 취소한다.
            gameplayEventChannel?.CancelAllWaiters();

            // 책임 : 실행 코루틴이 다음 프레임 finally 로 정리되기를 기다리지 않고 즉시 중단한다.
            if (activeExecution != null)
            {
                StopCoroutine(activeExecution);
                activeExecution = null;
            }

            ForceCleanupAllParallelExecutions(cancelled: true);
            visualRouter?.ReleaseAll();
            // 책임 : AbilityLogic 외부에서 직접 AddTag 한 전투/행동 상태 태그를 TagSet 기반으로 추가 정리한다.
            RemoveTagsFromSets(extraCleanupTagSets);

            ClearBufferedActivation();

            isCasting = false;
            isExecuting = false;
            castTimeRemaining = 0f;

            currentCastSpec = null;
            currentTarget = null;

            currentExecSpec = null;
            currentExecTarget = null;
        }
        /// <summary>
        /// 책임 : 현재 spec의 AbilityLogic이 씬 이동 cleanup 훅을 override 했다면 호출한다.
        /// AbilitySystem은 세부 정리 내용을 모르고, 각 로직이 자기 상태를 정리하도록 위임한다.
        /// </summary>
        private void InvokeSceneTransitionCleanup(AbilitySpec spec, GameObject target)
        {
            if (spec == null || spec.Definition == null)
                return;

            var logic = spec.Definition.logic;
            if (logic == null)
                return;

            try
            {
                logic.CleanupForSceneTransition(this, spec, target);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
        /// <summary>
        /// 책임 : 능력 실행 중 Definition이 active 동안 부여한 태그를 즉시 회수한다.
        /// </summary>
        private void RemoveGrantedTagsImmediately(AbilityDefinition def)
        {
            if (def == null || tagSystem == null || def.grantedTagsWhileActive == null)
                return;

            tagSystem.RemoveTags(def.grantedTagsWhileActive);
        }

        /// <summary>
        /// 책임 : 씬 이동 전 추가 정리 대상으로 지정한 GameplayTagSet들을 펼쳐 explicit tag를 제거한다.
        /// AbilityLogic이 직접 AddTag 한 상태를 캡처 전에 없애기 위한 보조 안전장치다.
        /// </summary>
        private void RemoveTagsFromSets(IReadOnlyList<GameplayTagSet> tagSets)
        {
            if (tagSystem == null || tagSets == null || tagSets.Count == 0)
                return;

            var collected = new HashSet<GameplayTag>();

            for (int i = 0; i < tagSets.Count; i++)
            {
                var set = tagSets[i];
                if (set == null)
                    continue;

                set.CollectTags(collected);
            }

            foreach (var tag in collected)
            {
                if (tag != null)
                    tagSystem.RemoveTag(tag, 1);
            }
        }
        private void RestoreChargeStateIfNeeded(AbilityDefinition def, AbilitySpec spec)
        {
            if (def == null || spec == null || !def.useCharges)
                return;

            int max = Mathf.Max(1, def.maxCharges);

            if (savedChargeStates.TryGetValue(def, out var st))
            {
                spec.SetInt(KEY_CHARGES, Mathf.Clamp(st.charges, 0, max));
                spec.SetFloat(KEY_RECHARGE, Mathf.Max(0f, st.rechargeRemaining));
            }
            else
            {
                spec.SetInt(KEY_CHARGES, max);
                spec.SetFloat(KEY_RECHARGE, 0f);
            }
        }

        private void SaveChargeStateIfNeeded(AbilityDefinition def, AbilitySpec spec)
        {
            if (def == null || spec == null || !def.useCharges)
                return;

            savedChargeStates[def] = new ChargeState
            {
                charges = spec.GetInt(KEY_CHARGES, 0),
                rechargeRemaining = spec.GetFloat(KEY_RECHARGE, 0f),
            };
        }

        public bool TryActivateAbility(AbilityDefinition ability, GameObject target = null)
        {
            var spec = FindSpec(ability);
            if (spec == null)
                return false;

            return TryActivateAbility(spec, target);
        }

        public bool TryActivateAbility(AbilitySpec spec, GameObject target = null)
        {
            var def = spec?.Definition;
            if (def == null)
                return false;

            if (!IsActivationDelaySatisfied(spec))
            {
                return false;
            }

            if (!CanActivateWhileCurrentMovementStateAllows(def))
                return false;

            if (cooldownController != null && cooldownController.IsOnCooldown(spec))
                return false;

            if (!def.CanActivate(gameObject, target))
                return false;

            if (def.executionPolicy == AbilityDefinition.ExecutionPolicy.ParallelIndependent)
                return TryActivateParallelAbility(spec, target);

            return TryActivateExclusiveAbility(spec, target);
        }

        /// <summary>
        /// 책임 : 기존 큐/버퍼 기반 단독 실행 Ability 활성화를 처리한다.
        /// </summary>
        private bool TryActivateExclusiveAbility(AbilitySpec spec, GameObject target)
        {
            if (IsBusy)
            {
                if (!enableExclusiveActivationBuffer)
                    return false;

                BufferActivation(spec, target);
                return true;
            }

            StartCasting(spec, target);
            return true;
        }

        /// <summary>
        /// 책임 : 큐에 묶이지 않는 병행 실행 Ability를 즉시 시작한다.
        /// 1차 구현에서는 Instant Ability만 지원하며, 동일 spec의 중복 병행 실행은 허용하지 않는다.
        /// </summary>
        private bool TryActivateParallelAbility(AbilitySpec spec, GameObject target)
        {
            var def = spec.Definition;

            if (!def.IsInstant)
            {
                Debug.LogWarning(
                    $"[AbilitySystem] ParallelIndependent 1차 구현은 Instant Ability만 지원합니다: {def.name}",
                    this);
                return false;
            }

            if (currentCastSpec == spec || currentExecSpec == spec || IsParallelExecuting(spec))
                return false;

            CommitAbilityCast(spec, def, target);
            cooldownController?.ConsumeChargeOnCommit(spec, def);

            if (!def.startCooldownOnEnd)
                cooldownController?.StartCooldown(spec);

            StartParallelAbilityExecution(spec, target);
            return true;
        }
        private bool CanActivateWhileCurrentMovementStateAllows(AbilityDefinition def)
        {
            if (def == null)
                return false;

            if (def.canCastWhileMoving)
                return true;

            var mover = GetComponent<IMovementStateProvider>();
            return mover == null || !mover.IsMoving;
        }

        private void BufferActivation(AbilitySpec spec, GameObject target)
        {
            BufferActivation(spec, target, 0f);
        }

        /// <summary>
        /// 책임 : ExclusiveQueued ability의 재시도 요청을 보관하고 지정 시각 이후에만 다시 소비되게 만든다.
        /// </summary>
        private void BufferActivation(AbilitySpec spec, GameObject target, float notBeforeTime)
        {
            bufferedSpec = spec;
            bufferedTarget = target;
            bufferedActivationNotBeforeTime = Mathf.Max(0f, notBeforeTime);
        }

        internal void TryConsumeBufferedActivation_Internal()
        {
            if (bufferedSpec == null)
                return;

            if (Time.time < bufferedActivationNotBeforeTime)
                return;

            var s = bufferedSpec;
            var t = bufferedTarget;

            ClearBufferedActivation();
            TryActivateAbility(s, t);
        }

        private void ClearBufferedActivation()
        {
            bufferedSpec = null;
            bufferedTarget = null;
            bufferedActivationNotBeforeTime = 0f;
        }

        /// <summary>
        /// 책임 : 예약된 ExclusiveQueued activation이 소비 가능한 시점이 오면 자동으로 재시도한다.
        /// </summary>
        private void TryConsumeBufferedActivationWhenReady()
        {
            if (bufferedSpec == null || IsBusy)
                return;

            if (Time.time < bufferedActivationNotBeforeTime)
                return;

            TryConsumeBufferedActivation_Internal();
        }

        private void StartCasting(AbilitySpec spec, GameObject target)
        {
            isCasting = true;
            currentCastSpec = spec;
            currentTarget = target;

            var def = spec.Definition;
            castTimeRemaining = def.castTime;

            presentationRouter?.PlayCastStart(def, spec, target);
            OnAbilityCastStart?.Invoke(def);

            if (def.IsInstant)
                CompleteCast();
        }

        private void TickCasting()
        {
            if (!isCasting)
                return;

            castTimeRemaining -= Time.deltaTime;
            if (castTimeRemaining <= 0f)
                CompleteCast();
        }

        private void CompleteCast()
        {
            if (!isCasting)
                return;

            var spec = currentCastSpec;
            var def = spec != null ? spec.Definition : null;
            var target = currentTarget;

            if (def != null)
            {
                CommitAbilityCast(spec, def, target);
                StartAbilityExecution(spec, target);
                cooldownController?.ConsumeChargeOnCommit(spec, def);

                if (!def.startCooldownOnEnd)
                    cooldownController?.StartCooldown(spec);
            }

            ClearCastingState();
            OnAbilityCastCompleted?.Invoke(def);
        }

        private void CommitAbilityCast(AbilitySpec spec, AbilityDefinition def, GameObject target)
        {
            def.ApplyCost(gameObject);

            if (def.animationTriggerHash != 0)
                presentationRouter?.TryPlayAnimationTriggerHash(def.animationTriggerHash, def);

            presentationRouter?.PlayCastCommit(def, spec, target);
        }

        private void StartAbilityExecution(AbilitySpec spec, GameObject target)
        {
            if (activeExecution != null)
                StopCoroutine(activeExecution);

            activeExecution = StartCoroutine(executionCoordinator.RunExclusive(this, spec, target));
        }

        /// <summary>
        /// 책임 : 병행 실행 Ability의 coroutine을 시작하고 추적 목록에 연결한다.
        /// </summary>
        private void StartParallelAbilityExecution(AbilitySpec spec, GameObject target)
        {
            var coroutine = StartCoroutine(executionCoordinator.RunParallel(this, spec, target));
            AttachParallelExecutionCoroutine(spec, coroutine);
        }

        private void ClearCastingState()
        {
            isCasting = false;
            currentCastSpec = null;
            currentTarget = null;
        }

        public void CancelCasting(bool force = false)
        {
            if (!isCasting)
                return;

            var cancelledSpec = currentCastSpec;
            var cancelledDef = cancelledSpec != null ? cancelledSpec.Definition : null;
            var target = currentTarget;

            if (!force && cancelledDef != null && !cancelledDef.interruptible)
                return;

            presentationRouter?.PlayCastCancelled(cancelledDef, cancelledSpec, target);

            ClearCastingState();
            gameplayEventChannel?.CancelAllWaiters();
            OnAbilityCastCancelled?.Invoke(cancelledDef);
        }

        public void CancelExecution(bool force = false)
        {
            var def = currentExecSpec != null ? currentExecSpec.Definition : null;
            if (!force && def != null && !def.interruptible)
                return;

            currentExecSpec?.Token?.Cancel();
        }

        public float GetCooldownRemaining(AbilityDefinition ability)
        {
            return cooldownController != null
                ? cooldownController.GetCooldownRemaining(ability)
                : 0f;
        }

        public bool ReduceCooldownRemaining(AbilityDefinition def, float reduceSeconds)
        {
            return cooldownController != null &&
                   cooldownController.ReduceCooldownRemaining(def, reduceSeconds);
        }

        public bool ReduceCooldownRemaining_OnHit(AbilityDefinition def)
        {
            return cooldownController != null &&
                   cooldownController.ReduceCooldownRemainingOnHit(def);
        }

        public int GetChargesRemaining(AbilityDefinition def)
        {
            var s = FindSpec(def);
            if (s != null)
                return s.GetInt(KEY_CHARGES, 0);

            if (def != null && def.useCharges && savedChargeStates.TryGetValue(def, out var st))
                return st.charges;

            return 0;
        }

        public int GetMaxCharges(AbilityDefinition ability)
        {
            var spec = FindSpec(ability);
            if (spec == null || spec.Definition == null || !spec.Definition.useCharges)
                return 1;

            return Mathf.Max(1, spec.Definition.maxCharges);
        }

        public float GetRechargeRemaining(AbilityDefinition def)
        {
            var s = FindSpec(def);
            if (s != null)
                return Mathf.Max(0f, s.GetFloat(KEY_RECHARGE, 0f));

            if (def != null && def.useCharges && savedChargeStates.TryGetValue(def, out var st))
                return Mathf.Max(0f, st.rechargeRemaining);

            return 0f;
        }

        public void SendGameplayEvent(GameplayTag tag, AbilityEventData data = default)
        {
            gameplayEventChannel?.Send(tag, data);
        }

        public GameplayEventWaiter WaitGameplayEvent(GameplayTag tag, AbilitySpec ownerSpec)
        {
            return gameplayEventChannel?.Wait(tag, ownerSpec);
        }

        internal void SubscribeGameplayEvent(Action<GameplayTag, AbilityEventData> handler)
        {
            if (handler == null || gameplayEventChannel == null)
                return;

            gameplayEventChannel.GameplayEventRaised += handler;
        }

        internal void UnsubscribeGameplayEvent(Action<GameplayTag, AbilityEventData> handler)
        {
            if (handler == null || gameplayEventChannel == null)
                return;

            gameplayEventChannel.GameplayEventRaised -= handler;
        }

        internal void CancelGameplayEventWaiters(AbilitySpec spec)
        {
            gameplayEventChannel?.CancelWaiters(spec);
        }

        internal void SetExecutionState(bool executing, AbilitySpec spec, GameObject target)
        {
            isExecuting = executing;
            currentExecSpec = spec;
            currentExecTarget = target;
        }

        internal void ClearActiveExecutionCoroutine()
        {
            activeExecution = null;
        }
        /// <summary>
        /// 책임 : 캐스팅/실행/버퍼링 중인 일시 런타임 상태를 초기화한다.
        /// 씬 이동 후 이전 씬의 진행 중 상태가 이어지지 않도록 정리하는 공식 창구다.
        /// </summary>
        public void ResetTransientRuntimeState()
        {
            var castingSpec = currentCastSpec;
            var executingSpec = currentExecSpec;
            var castingDef = castingSpec != null ? castingSpec.Definition : null;
            var executingDef = executingSpec != null ? executingSpec.Definition : null;

            // 책임 : 무기 교체/강제 리셋 전에 현재 AbilityLogic이 직접 만든
            // 이동, modifier, 구독 같은 일시 상태를 먼저 정리할 기회를 준다.
            InvokeTransientCleanupHooks();

            CancelCasting(force: true);
            CancelExecution(force: true);

            // 책임 : 강제 리셋으로 코루틴 finally를 기다리지 못하는 경우에도
            // active 동안 부여된 상태 태그를 즉시 회수해 입력/이동 모드가 남지 않게 한다.
            RemoveGrantedTagsImmediately(castingDef);
            RemoveGrantedTagsImmediately(executingDef);
            RemoveGrantedTagsFromParallelExecutions();

            ClearBufferedActivation();
            gameplayEventChannel?.CancelAllWaiters();

            if (activeExecution != null)
            {
                StopCoroutine(activeExecution);
                activeExecution = null;
            }
            ForceCleanupAllParallelExecutions(cancelled: true);
            visualRouter?.ReleaseAll();
            isCasting = false;
            isExecuting = false;
            castTimeRemaining = 0f;

            currentCastSpec = null;
            currentTarget = null;

            currentExecSpec = null;
            currentExecTarget = null;
        }

        /// <summary>
        /// 책임 : 병행 실행 중인 ability들이 active 동안 부여한 태그를 강제 리셋 시 즉시 회수한다.
        /// </summary>
        private void RemoveGrantedTagsFromParallelExecutions()
        {
            for (int i = 0; i < parallelExecutions.Count; i++)
            {
                var exec = parallelExecutions[i];
                var def = exec != null && exec.Spec != null ? exec.Spec.Definition : null;
                RemoveGrantedTagsImmediately(def);
            }
        }

        /// <summary>
        /// 책임 : 현재 캐스팅/실행/병행 실행 중인 AbilityLogic의 일시 상태 정리 훅을 호출한다.
        /// 씬 이동뿐 아니라 무기 교체처럼 실행 코루틴이 강제로 끊기는 경로에서도
        /// Rush/Dash가 남긴 motion과 임시 modifier가 누수되지 않도록 보장한다.
        /// </summary>
        private void InvokeTransientCleanupHooks()
        {
            var castingSpec = currentCastSpec;
            var castingTarget = currentTarget;
            var executingSpec = currentExecSpec;
            var executingTarget = currentExecTarget;

            for (int i = 0; i < parallelExecutions.Count; i++)
            {
                var exec = parallelExecutions[i];
                if (exec == null || exec.Spec == null)
                    continue;

                InvokeSceneTransitionCleanup(exec.Spec, exec.Target);
            }

            InvokeSceneTransitionCleanup(castingSpec, castingTarget);
            InvokeSceneTransitionCleanup(executingSpec, executingTarget);
        }


        /// <summary>
        /// 책임 : 특정 ability의 남은 cooldown을 공식 절차로 복원한다.
        /// 차지형 스킬은 현재 충전 수를 유지한 채 cooldown만 복원한다.
        /// </summary>
        public bool TrySetCooldownRemaining(AbilityDefinition def, float seconds)
        {
            if (def == null || cooldownController == null)
                return false;

            int currentCharges = GetChargesRemaining(def);
            return cooldownController.TryRestoreCooldownState(
                def,
                Mathf.Max(0f, seconds),
                currentCharges);
        }

        private static bool ContainsTag(List<GameplayTag> list, GameplayTag tag)
        {
            if (list == null || tag == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == tag)
                    return true;
            }

            return false;
        }

        private void HandleOwnerTagAdded(GameplayTag tag)
        {
            if (tag == null)
                return;

            if (isCasting && currentCastSpec != null)
            {
                var def = currentCastSpec.Definition;
                bool globalHit = ContainsTag(globalCancelCastingOnTags, tag);
                bool localHit = def != null && ContainsTag(def.cancelCastingOnTags, tag);

                if (globalHit || localHit)
                    CancelCasting(force: true);
            }

            if (currentExecSpec != null)
            {
                var def = currentExecSpec.Definition;
                bool globalHit = ContainsTag(globalCancelExecutionOnTags, tag);
                bool localHit = def != null && ContainsTag(def.cancelExecutionOnTags, tag);

                if (globalHit || localHit)
                    CancelExecution(force: true);
            }

            CancelMatchingParallelExecutions(tag, force: true);
        }

        public GameplayEffectSpec MakeSpec(GameplayEffect effect, GameObject causer = null, UnityEngine.Object sourceObject = null)
        {
            var ctx = new GameplayEffectContext(gameObject, causer != null ? causer : gameObject);
            ctx.SourceObject = sourceObject;
            return new GameplayEffectSpec(effect, ctx);
        }

        public void ApplyEffectContainers(AbilitySpec spec, GameObject target, AbilityEffectTiming timing, GameplayTag eventTag)
        {
            var def = spec.Definition;
            if (def.containers == null || def.containers.Count == 0)
                return;

            for (int i = 0; i < def.containers.Count; i++)
            {
                var c = def.containers[i];
                if (c == null || c.timing != timing)
                    continue;

                if (timing == AbilityEffectTiming.OnEvent && c.eventTag != eventTag)
                    continue;

                if (c.effects == null || c.effects.Count == 0)
                    continue;

                GameObject receiver = ResolveEffectContainerReceiver(c.targetPolicy, target);
                if (receiver == null)
                    continue;

                foreach (var e in c.effects)
                {
                    var geSpec = MakeSpec(e, causer: gameObject, sourceObject: def);
                    effectRunner.ApplyEffectSpec(geSpec, receiver);
                }
            }
        }

        private GameObject ResolveEffectContainerReceiver(AbilityEffectTargetPolicy policy, GameObject explicitTarget)
        {
            switch (policy)
            {
                case AbilityEffectTargetPolicy.Caster:
                    return gameObject;
                case AbilityEffectTargetPolicy.ExplicitTarget:
                    return explicitTarget;
                default:
                    return null;
            }
        }

        public void RegisterWeaponAnimator(Animator newWeaponAnimator)
        {
            initialWeaponAnimator = newWeaponAnimator;
            presentationRouter?.RegisterWeaponAnimator(newWeaponAnimator);
        }

        public void OnWeaponEquipped()
        {
            if (presentationRouter != null && presentationRouter.ShouldCancelOnWeaponEquipped(this))
                CancelExecution(force: true);
        }

        public void TryPlayAnimationTriggerHash(int triggerHash, AbilityDefinition def)
        {
            presentationRouter?.TryPlayAnimationTriggerHash(triggerHash, def);
        }

        /// <summary>
        /// 책임 : ability별 다음 재활성 가능 시각을 기록해 recovery와 별도로 공격 템포를 제어한다.
        /// </summary>
        public void SetNextActivationDelay(AbilitySpec spec, float delaySeconds)
        {
            if (spec == null)
                return;

            if (delaySeconds <= 0f)
            {
                spec.SetFloat(KEY_NEXT_ACTIVATE_ALLOWED_TIME, 0f);
                return;
            }

            spec.SetFloat(KEY_NEXT_ACTIVATE_ALLOWED_TIME, Time.time + delaySeconds);
        }

        /// <summary>
        /// 책임 : definition 기준 다음 재활성 가능 시각까지 남은 시간을 반환한다.
        /// 입력 계층이 공격 반복 간격을 조절할 때 공통으로 사용한다.
        /// </summary>
        public float GetNextActivationRemaining(AbilityDefinition def)
        {
            return GetNextActivationRemaining(FindSpec(def));
        }

        /// <summary>
        /// 책임 : spec 기준 다음 재활성 가능 시각까지 남은 시간을 반환한다.
        /// </summary>
        public float GetNextActivationRemaining(AbilitySpec spec)
        {
            if (spec == null)
                return 0f;

            return Mathf.Max(0f, GetNextActivationAllowedTime(spec) - Time.time);
        }

        /// <summary>
        /// 책임 : Ability 내부 특수 키인지 판별한다.
        /// charges/recharge 같은 시스템 내부 키는 일반 영속 변수 목록에서 제외한다.
        /// </summary>
        private static bool IsReservedRuntimeKey(string key)
        {
            return key == KEY_CHARGES || key == KEY_RECHARGE || key == KEY_NEXT_ACTIVATE_ALLOWED_TIME;
        }

        /// <summary>
        /// 책임 : spec의 다음 재활성 가능 시각을 조회한다.
        /// </summary>
        private float GetNextActivationAllowedTime(AbilitySpec spec)
        {
            return spec != null ? spec.GetFloat(KEY_NEXT_ACTIVATE_ALLOWED_TIME, 0f) : 0f;
        }

        /// <summary>
        /// 책임 : 현재 시각이 spec의 다음 재활성 가능 시각을 넘었는지 판정한다.
        /// </summary>
        private bool IsActivationDelaySatisfied(AbilitySpec spec)
        {
            return Time.time >= GetNextActivationAllowedTime(spec);
        }

        /// <summary>
        /// 책임 : null/empty 키를 방지하고, 내부 예약 키는 일반 상태 목록에서 제외한다.
        /// </summary>
        private static bool IsUserStateKey(string key)
        {
            return !string.IsNullOrEmpty(key) && !IsReservedRuntimeKey(key);
        }
        /// <summary>
        /// 책임 :
        /// - 현재 AbilityDefinition의 지속 상태를 영속 DTO로 내보낸다.
        /// - 씬 이동, 무기 드롭, 인벤토리 이동 등 소유권 이동 경로의 공식 export 창구다.
        /// </summary>
        public AbilityPersistentState ExportPersistentState(AbilityDefinition def)
        {
            var spec = FindSpec(def);
            return ExportPersistentState(spec);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 AbilitySpec의 지속 상태를 영속 DTO로 내보낸다.
        /// - 진행 중 실행 상태는 제외하고, 레벨/쿨다운/차지/커스텀 변수만 저장한다.
        /// </summary>
        public AbilityPersistentState ExportPersistentState(AbilitySpec spec)
        {
            if (spec == null || spec.Definition == null)
                return null;

            var def = spec.Definition;

            var state = new AbilityPersistentState
            {
                abilityId = def.name,
                level = Mathf.Max(1, spec.Level),
                cooldownRemaining = Mathf.Max(0f, GetCooldownRemaining(def)),
                chargesRemaining = def.useCharges
                    ? Mathf.Max(0, GetChargesRemaining(def))
                    : 0
            };

            foreach (var pair in spec.IntVars)
            {
                if (!IsUserStateKey(pair.Key))
                    continue;

                state.intVars.Add(new AbilityIntStateEntry
                {
                    key = pair.Key,
                    value = pair.Value
                });
            }

            foreach (var pair in spec.FloatVars)
            {
                if (!IsUserStateKey(pair.Key))
                    continue;

                state.floatVars.Add(new AbilityFloatStateEntry
                {
                    key = pair.Key,
                    value = pair.Value
                });
            }

            foreach (var pair in spec.BoolVars)
            {
                if (!IsUserStateKey(pair.Key))
                    continue;

                state.boolVars.Add(new AbilityBoolStateEntry
                {
                    key = pair.Key,
                    value = pair.Value
                });
            }

            return state;
        }

        /// <summary>
        /// 책임 :
        /// - 영속 DTO를 현재 AbilitySystem에 복원한다.
        /// - spec이 없으면 생성하고, 지속 상태만 복원하며 진행 중 실행 상태는 복원하지 않는다.
        /// </summary>
        public AbilitySpec ImportPersistentState(
            AbilityPersistentState state,
            Func<string, AbilityDefinition> resolver)
        {
            if (state == null || string.IsNullOrEmpty(state.abilityId) || resolver == null)
                return null;

            var def = resolver(state.abilityId);
            if (def == null)
            {
                Debug.LogWarning($"[AbilitySystem] ability 상태 복원 실패: '{state.abilityId}' 을(를) 찾지 못했습니다.", this);
                return null;
            }

            var spec = FindSpec(def);
            if (spec == null)
            {
                spec = GiveAbility(def);
                if (spec == null)
                {
                    Debug.LogWarning($"[AbilitySystem] ability spec 생성 실패: '{state.abilityId}'", this);
                    return null;
                }
            }

            spec.Level = Mathf.Max(1, state.level);

            // 책임 : 복원 시점의 커스텀 변수는 저장본을 진실 원천으로 보고 통째로 다시 채운다.
            spec.ClearRuntimeVars();

            if (state.intVars != null)
            {
                for (int i = 0; i < state.intVars.Count; i++)
                {
                    var entry = state.intVars[i];
                    if (entry == null || !IsUserStateKey(entry.key))
                        continue;

                    spec.SetInt(entry.key, entry.value);
                }
            }

            if (state.floatVars != null)
            {
                for (int i = 0; i < state.floatVars.Count; i++)
                {
                    var entry = state.floatVars[i];
                    if (entry == null || !IsUserStateKey(entry.key))
                        continue;

                    spec.SetFloat(entry.key, entry.value);
                }
            }

            if (state.boolVars != null)
            {
                for (int i = 0; i < state.boolVars.Count; i++)
                {
                    var entry = state.boolVars[i];
                    if (entry == null || !IsUserStateKey(entry.key))
                        continue;

                    spec.SetBool(entry.key, entry.value);
                }
            }

            cooldownController?.TryRestoreCooldownState(
                def,
                Mathf.Max(0f, state.cooldownRemaining),
                Mathf.Max(0, state.chargesRemaining));

            return spec;
        }
        /// <summary>
        /// 책임 :
        /// - 현재 AbilitySystem이 소유한 모든 AbilitySpec의 지속 상태를 영속 DTO 목록으로 캡처한다.
        /// - 씬 이동, 저장/로드, 소유권 이동 경로가 공통으로 사용할 공식 캡처 API다.
        /// </summary>
        public IReadOnlyList<AbilityPersistentState> CapturePersistentStates()
        {
            var result = new List<AbilityPersistentState>(runtimeSpecs.Count);

            for (int i = 0; i < runtimeSpecs.Count; i++)
            {
                var spec = runtimeSpecs[i];
                var state = ExportPersistentState(spec);
                if (state != null)
                    result.Add(state);
            }

            return result;
        }

        /// <summary>
        /// 책임 :
        /// - 영속 DTO 목록을 현재 AbilitySystem에 복원한다.
        /// - 진행 중 실행 상태는 먼저 끊고, 각 DTO를 순차 import 한다.
        /// </summary>
        public void RestorePersistentStates(
            IEnumerable<AbilityPersistentState> states,
            Func<string, AbilityDefinition> resolver)
        {
            ResetTransientRuntimeState();

            if (states == null || resolver == null)
                return;

            foreach (var state in states)
            {
                ImportPersistentState(state, resolver);
            }
        }
    }
}
