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

        private struct ChargeState
        {
            public int charges;
            public float rechargeRemaining;
        }

        private readonly Dictionary<AbilityDefinition, ChargeState> savedChargeStates = new();

        private const string KEY_CHARGES = "__Charges";
        private const string KEY_RECHARGE = "__RechargeRemaining";

        private AbilityGameplayEventChannel gameplayEventChannel;
        private AbilityCooldownController cooldownController;
        private AbilityPresentationRouter presentationRouter;
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

        private void Update()
        {
            cooldownController?.TickCooldowns(runtimeSpecs);
            TickCasting();
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
            presentationRouter = new AbilityPresentationRouter(
                gameObject,
                cueManager,
                playerAnimator,
                initialWeaponAnimator);

            gameplayEventChannel = new AbilityGameplayEventChannel(
                this,
                cueManager,
                autoExecuteCueWhenGameplayEventTagExists);

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

            if (!CanActivateWhileCurrentMovementStateAllows(def))
                return false;

            if (cooldownController != null && cooldownController.IsOnCooldown(spec))
                return false;

            if (!def.CanActivate(gameObject, target))
                return false;

            if (IsBusy)
            {
                BufferActivation(spec, target);
                return true;
            }

            StartCasting(spec, target);
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
            bufferedSpec = spec;
            bufferedTarget = target;
        }

        internal void TryConsumeBufferedActivation_Internal()
        {
            if (bufferedSpec == null)
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

            activeExecution = StartCoroutine(executionCoordinator.Run(this, spec, target));
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
            CancelCasting(force: true);
            CancelExecution(force: true);

            ClearBufferedActivation();
            gameplayEventChannel?.CancelAllWaiters();

            if (activeExecution != null)
            {
                StopCoroutine(activeExecution);
                activeExecution = null;
            }

            isCasting = false;
            isExecuting = false;
            castTimeRemaining = 0f;

            currentCastSpec = null;
            currentTarget = null;

            currentExecSpec = null;
            currentExecTarget = null;
        }

        /// <summary>
        /// 책임 : 현재 ability들의 복원 가능한 런타임 상태를 스냅샷으로 캡처한다.
        /// 현재 버전은 ability 식별자, 남은 cooldown, 현재 충전 수를 저장한다.
        /// </summary>
        public IReadOnlyList<AbilityRuntimeSnapshot> CaptureAbilitySnapshots()
        {
            var result = new List<AbilityRuntimeSnapshot>();

            for (int i = 0; i < runtimeSpecs.Count; i++)
            {
                var spec = runtimeSpecs[i];
                if (spec == null || spec.Definition == null)
                    continue;

                var def = spec.Definition;

                result.Add(new AbilityRuntimeSnapshot
                {
                    abilityId = def.name,
                    cooldownRemaining = Mathf.Max(0f, GetCooldownRemaining(def)),
                    chargesRemaining = Mathf.Max(0, GetChargesRemaining(def))
                });
            }

            return result;
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

        /// <summary>
        /// 책임 : 저장된 ability 스냅샷 전체를 현재 AbilitySystem에 복원한다.
        /// 진행 중 상태는 먼저 초기화하고, 식별된 ability의 cooldown과 charges를 순차 복원한다.
        /// </summary>
        public void RestoreAbilitySnapshots(
                   IEnumerable<AbilityRuntimeSnapshot> snapshots,
                   Func<string, AbilityDefinition> resolver)
        {
            ResetTransientRuntimeState();

            if (snapshots == null || resolver == null || cooldownController == null)
                return;

            foreach (var entry in snapshots)
            {
                if (entry == null || string.IsNullOrEmpty(entry.abilityId))
                    continue;

                var def = resolver(entry.abilityId);
                if (def == null)
                {
                    Debug.LogWarning($"[AbilitySystem] ability 복원 실패: '{entry.abilityId}' 을(를) 찾지 못했습니다.", this);
                    continue;
                }

                // 책임 : 복원 대상 ability의 런타임 spec이 없으면 먼저 생성한다.
                var spec = FindSpec(def);
                if (spec == null)
                {
                    spec = GiveAbility(def);
                    if (spec == null)
                    {
                        Debug.LogWarning($"[AbilitySystem] ability spec 생성 실패: '{entry.abilityId}'", this);
                        continue;
                    }

                    Debug.Log($"[AbilitySystem] ability spec 자동 생성: {def.name}", this);
                }

                bool restored = cooldownController.TryRestoreCooldownState(
                    def,
                    entry.cooldownRemaining,
                    entry.chargesRemaining);

                Debug.Log(
                    $"[AbilitySystem] ability 복원 id={entry.abilityId}, specExists={spec != null}, restored={restored}, cd={entry.cooldownRemaining}, charges={entry.chargesRemaining}",
                    this);
            }
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
    }
}