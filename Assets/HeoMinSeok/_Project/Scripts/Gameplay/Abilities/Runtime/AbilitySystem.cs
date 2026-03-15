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
        public GameplayTag KillConfirmedTag => killConfirmedTag;

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