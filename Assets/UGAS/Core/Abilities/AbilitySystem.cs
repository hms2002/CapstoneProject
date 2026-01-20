using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityGAS.AbilityDefinition;

namespace UnityGAS
{
    public class AbilitySystem : MonoBehaviour
    {
        [Header("Initial Abilities (Definitions)")]
        [SerializeField] private List<AbilityDefinition> initialAbilities = new List<AbilityDefinition>();

        // 런타임 "소유" 상태는 Spec으로 들고 간다
        private readonly List<AbilitySpec> runtimeSpecs = new List<AbilitySpec>();

        [Header("Components")]
        [SerializeField] private AttributeSet attributeSet;
        [SerializeField] private GameplayEffectRunner effectRunner;
        [SerializeField] private TagSystem tagSystem;

        [Header("Cue")]
        [SerializeField] private GameplayCueManager cueManager;
        [Tooltip("SendGameplayEvent(tag) 호출 시, 해당 tag가 Cue로 등록되어 있으면 자동으로 ExecuteCue로도 처리")]
        [SerializeField] private bool autoExecuteCueWhenGameplayEventTagExists = true;

        // 상태
        private bool isCasting;
        private bool isExecuting;
        private float castTimeRemaining;

        private AbilitySpec currentCastSpec;
        private GameObject currentTarget;

        private AbilitySpec currentExecSpec;
        private GameObject currentExecTarget;

        private Coroutine activeExecution;

        public System.Action<AbilityDefinition> OnAbilityCastStart;
        public System.Action<AbilityDefinition> OnAbilityCastCompleted;
        public System.Action<AbilityDefinition> OnAbilityCastCancelled;
        
        // 스택형 쿨타임 키
        private const string KEY_CHARGES = "__Charges";
        private const string KEY_RECHARGE = "__RechargeRemaining";

        public bool IsCasting => isCasting;
        public bool IsExecuting => isExecuting;
        public bool IsBusy => isCasting || isExecuting;

        // 실행/캐스팅 우선순위로 노출
        public AbilityDefinition CurrentCast =>
            isCasting ? (currentCastSpec != null ? currentCastSpec.Definition : null)
                     : (currentExecSpec != null ? currentExecSpec.Definition : null);

        public AbilitySpec CurrentCastSpec => isCasting ? currentCastSpec : currentExecSpec;
        public AbilitySpec CurrentExecSpec => currentExecSpec;
        public GameObject CurrentTargetGameObject => isCasting ? currentTarget : currentExecTarget;

        // 이벤트 버스
        public event System.Action<GameplayTag, AbilityEventData> OnGameplayEvent;

        // 애니메이팅
        [SerializeField] private Animator animator;
        [SerializeField] private Animator playerAnimator;
        private Animator weaponAnimator;

        public Animator PlayerAnimator => playerAnimator;
        public Animator WeaponAnimator => weaponAnimator;

        /// 무기 장착/교체 시 호출
        public void RegisterWeaponAnimator(Animator newWeaponAnimator)
        {
            weaponAnimator = newWeaponAnimator;
        }

        public void OnWeaponEquipped()
        {
            if (CurrentExecSpec?.Definition?.animationChannel == AnimationChannel.Weapon)
                CancelExecution(force: true);
        }

        public void SendGameplayEvent(GameplayTag tag, AbilityEventData data = new AbilityEventData())
        {
            if (tag == null) return;

            OnGameplayEvent?.Invoke(tag, data);

            // (선택) 태그가 Cue로 등록되어 있으면 연출도 자동 처리
            if (autoExecuteCueWhenGameplayEventTagExists && cueManager != null && cueManager.HasCue(tag))
            {
                var p = BuildCueParamsFromEvent(data);
                cueManager.ExecuteCue(tag, p);
            }
        }

        // -----------------------------
        // Waiter 관리
        // -----------------------------
        private readonly Dictionary<AbilitySpec, List<GameplayEventWaiter>> waitersBySpec
            = new Dictionary<AbilitySpec, List<GameplayEventWaiter>>();

        public GameplayEventWaiter WaitGameplayEvent(GameplayTag tag, AbilitySpec ownerSpec)
        {
            if (tag == null) return null;

            var waiter = new GameplayEventWaiter();

            if (ownerSpec != null)
            {
                if (!waitersBySpec.TryGetValue(ownerSpec, out var list))
                {
                    list = new List<GameplayEventWaiter>();
                    waitersBySpec.Add(ownerSpec, list);
                }
                list.Add(waiter);
            }

            void Handler(GameplayTag t, AbilityEventData d)
            {
                if (t != tag || waiter.Done) return;

                waiter.Data = d;
                waiter.Done = true;

                OnGameplayEvent -= Handler;
                waiter.Cleanup = null;

                if (ownerSpec != null && waitersBySpec.TryGetValue(ownerSpec, out var list))
                    list.Remove(waiter);
            }

            waiter.Cleanup = () =>
            {
                OnGameplayEvent -= Handler;
                if (ownerSpec != null && waitersBySpec.TryGetValue(ownerSpec, out var list))
                    list.Remove(waiter);
            };

            OnGameplayEvent += Handler;
            return waiter;
        }

        private void CancelAllWaiters()
        {
            foreach (var kv in waitersBySpec)
            {
                var list = kv.Value;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++) list[i]?.Cancel();
            }
            waitersBySpec.Clear();
        }

        private void CancelWaiters(AbilitySpec spec)
        {
            if (spec == null) return;
            if (!waitersBySpec.TryGetValue(spec, out var list) || list.Count == 0) return;

            var copy = list.ToArray();
            list.Clear();
            foreach (var w in copy) w?.Cancel();
        }

        private void Awake()
        {
            if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
            if (effectRunner == null) effectRunner = GetComponent<GameplayEffectRunner>();
            if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
#if UNITY_2023_1_OR_NEWER
            if (cueManager == null) cueManager = Object.FindAnyObjectByType<GameplayCueManager>();
#else
            if (cueManager == null) cueManager = FindObjectOfType<GameplayCueManager>();
#endif
            runtimeSpecs.Clear();
            foreach (var def in initialAbilities)
                if (def != null) GiveAbility(def);
        }

        private void Update()
        {
            HandleCooldowns();
            HandleCasting();
        }

        // -----------------------------
        // Ability Management
        // -----------------------------
        public AbilitySpec GiveAbility(AbilityDefinition def)
        {
            var spec = new AbilitySpec(def);
            runtimeSpecs.Add(spec);

            if (def != null && def.useCharges)
            {
                spec.SetInt(KEY_CHARGES, Mathf.Max(1, def.maxCharges));
                spec.SetFloat(KEY_RECHARGE, 0f);
            }

            return spec;
        }


        public AbilitySpec FindSpec(AbilityDefinition def)
        {
            if (def == null) return null;
            for (int i = 0; i < runtimeSpecs.Count; i++)
                if (runtimeSpecs[i].Definition == def) return runtimeSpecs[i];
            return null;
        }

        // -----------------------------
        // Activation
        // -----------------------------
        private AbilitySpec bufferedSpec;
        private GameObject bufferedTarget;

        private bool IsOnCooldown(AbilitySpec spec)
        {
            if (spec == null) return false;
            var def = spec.Definition;

            if (def != null && def.useCharges)
                return spec.GetInt(KEY_CHARGES, 0) <= 0;

            if (def != null && def.cooldownEffect != null && effectRunner != null)
                return effectRunner.HasActiveEffect(def.cooldownEffect, gameObject);

            return spec.CooldownRemaining > 0f;
        }

        private void Buffer(AbilitySpec spec, GameObject target)
        {
            bufferedSpec = spec;
            bufferedTarget = target;
        }

        private void TryConsumeBuffered()
        {
            if (bufferedSpec == null) return;

            var s = bufferedSpec;
            var t = bufferedTarget;

            bufferedSpec = null;
            bufferedTarget = null;

            TryActivateAbility(s, t);
        }

        public bool TryActivateAbility(AbilityDefinition ability, GameObject target = null)
        {
            var spec = FindSpec(ability);
            if (spec == null) return false;
            return TryActivateAbility(spec, target);
        }

        public bool TryActivateAbility(AbilitySpec spec, GameObject target = null)
        {
            var def = spec?.Definition;
            if (def == null) return false;
            if (!def.canCastWhileMoving)
            {
                var mover = GetComponent<IMovementStateProvider>();
                if (mover != null && mover.IsMoving)
                    return false;
            }
            // 쿨다운/조건 불만족은 즉시 false
            if (IsOnCooldown(spec) || !def.CanActivate(gameObject, target))
                return false;

            // Busy면 버퍼
            if (IsBusy)
            {
                Buffer(spec, target);
                return true; // 입력 접수 의미
            }

            // 캐스팅 시작
            StartCasting(spec, target);
            return true;
        }

        private void StartCooldown(AbilitySpec spec)
        {

            var def = spec?.Definition;
            if (def == null) return;
            if (def.useCharges) return; // 충전형은 일반 쿨다운(또는 cooldownEffect) 사용 안 함

            if (def.cooldown <= 0f) return;

            // GE 기반 쿨다운(권장)
            if (def.cooldownEffect != null && effectRunner != null)
            {
                var cdSpec = MakeSpec(def.cooldownEffect, causer: gameObject, sourceObject: def);
                cdSpec.SetDuration(def.cooldown);
                effectRunner.ApplyEffectSpec(cdSpec, gameObject);
                return;
            }

            // 레거시 타이머(간단용)
            spec.CooldownRemaining = def.cooldown;
        }


        // -----------------------------
        // Cooldowns
        // -----------------------------
        public float GetCooldownRemaining(AbilityDefinition ability)
        {
            var spec = FindSpec(ability);
            if (spec == null) return 0f;

            var def = spec.Definition;
            if (def != null && def.cooldownEffect != null && effectRunner != null)
                return effectRunner.GetRemainingTime(def.cooldownEffect, gameObject);

            return Mathf.Max(0f, spec.CooldownRemaining);
        }


        private void HandleCooldowns()
        {
            if (runtimeSpecs.Count == 0) return;

            for (int i = 0; i < runtimeSpecs.Count; i++)
            {
                var s = runtimeSpecs[i];
                var def = s.Definition;

                if (def != null && def.useCharges)
                {
                    int charges = s.GetInt(KEY_CHARGES, 0);
                    int max = Mathf.Max(1, def.maxCharges);

                    if (charges < max)
                    {
                        float r = s.GetFloat(KEY_RECHARGE, 0f);

                        if (r <= 0f)
                            r = Mathf.Max(0.01f, def.cooldown);

                        r -= Time.deltaTime;

                        if (r <= 0f)
                        {
                            charges++;
                            s.SetInt(KEY_CHARGES, charges);

                            // 아직 덜 찼으면 다음 충전 시작
                            r = (charges < max) ? Mathf.Max(0.01f, def.cooldown) : 0f;
                        }

                        s.SetFloat(KEY_RECHARGE, r);
                    }
                    else
                    {
                        s.SetFloat(KEY_RECHARGE, 0f);
                    }

                    continue;
                }

                // -------- 기존 쿨다운 로직 --------
                if (def != null && def.cooldownEffect != null) continue;
                if (s.CooldownRemaining > 0f) s.CooldownRemaining -= Time.deltaTime;
            }
        }

        // -----------------------------
        // Casting
        // -----------------------------
        private void StartCasting(AbilitySpec spec, GameObject target)
        {
            isCasting = true;
            currentCastSpec = spec;
            currentTarget = target;

            var def = spec.Definition;

            castTimeRemaining = def.castTime;

            // Cue: CastStart + WhileCasting(Add)
            if (cueManager != null)
            {
                var p = BuildCueParamsForAbility(def, spec, target);
                if (def.cueOnCastStart != null) cueManager.ExecuteCue(def.cueOnCastStart, p);
                if (def.cueWhileCasting != null) cueManager.AddCue(def.cueWhileCasting, p);
            }

            OnAbilityCastStart?.Invoke(def);

            if (def.IsInstant)
                CompleteCast();
        }

        private void HandleCasting()
        {
            if (!isCasting) return;

            castTimeRemaining -= Time.deltaTime;
            if (castTimeRemaining <= 0f)
                CompleteCast();
        }

        private void CompleteCast()
        {
            if (!isCasting) return;

            var spec = currentCastSpec;
            var def = spec != null ? spec.Definition : null;
            var target = currentTarget;

            if (def != null)
            {
                def.ApplyCost(gameObject);
                // Definition에 animationTrigger가 있으면 자동 실행(간단한 능력용)
                if (def.animationTriggerHash != 0)
                    TryPlayAnimationTriggerHash(def.animationTriggerHash, def);

                // Cue: Commit(Execute) + WhileCasting(Remove)
                if (cueManager != null)
                {
                    var p = BuildCueParamsForAbility(def, spec, target);
                    if (def.cueWhileCasting != null) cueManager.RemoveCue(def.cueWhileCasting, p);
                    if (def.cueOnCommit != null) cueManager.ExecuteCue(def.cueOnCommit, p);
                }

                if (activeExecution != null)
                    StopCoroutine(activeExecution);

                activeExecution = StartCoroutine(RunAbility(spec, target));

                // “charge 1회 소비”
                if (def.useCharges)
                {
                    int c = spec.GetInt(KEY_CHARGES, 0);
                    if (c > 0) spec.SetInt(KEY_CHARGES, c - 1);

                    // 충전 타이머가 돌고 있지 않다면 시작
                    if (spec.GetInt(KEY_CHARGES, 0) < def.maxCharges && spec.GetFloat(KEY_RECHARGE, 0f) <= 0f)
                        spec.SetFloat(KEY_RECHARGE, Mathf.Max(0.01f, def.cooldown));
                }

                // 쿨다운은 캐스팅 완료 즉시 시작
                StartCooldown(spec);
            }

            isCasting = false;
            currentCastSpec = null;
            currentTarget = null;

            OnAbilityCastCompleted?.Invoke(def);
        }

        public void CancelCasting(bool force = false)
        {
            if (!isCasting) return;

            var cancelledSpec = currentCastSpec;
            var cancelledDef = cancelledSpec != null ? cancelledSpec.Definition : null;
            var target = currentTarget;
            if (!force && cancelledDef != null && !cancelledDef.interruptible)
                return;
            // Cue: CastCancelled + WhileCasting(Remove)
            if (cueManager != null && cancelledDef != null)
            {
                var p = BuildCueParamsForAbility(cancelledDef, cancelledSpec, target);
                if (cancelledDef.cueWhileCasting != null) cueManager.RemoveCue(cancelledDef.cueWhileCasting, p);
                if (cancelledDef.cueOnCastCancelled != null) cueManager.ExecuteCue(cancelledDef.cueOnCastCancelled, p);
            }

            isCasting = false;
            currentCastSpec = null;
            currentTarget = null;

            CancelAllWaiters();
            OnAbilityCastCancelled?.Invoke(cancelledDef);
        }

        // -----------------------------
        // Execution
        // -----------------------------
        public void CancelExecution(bool force = false)
        {
            var def = currentExecSpec != null ? currentExecSpec.Definition : null;
            if (!force && def != null && !def.interruptible)
                return;

            if (currentExecSpec?.Token != null)
                currentExecSpec.Token.Cancel();
        }

        private IEnumerator RunAbility(AbilitySpec spec, GameObject target)
        {
            isExecuting = true;
            currentExecSpec = spec;
            currentExecTarget = target;

            var def = spec.Definition;

            // 실행 단위 토큰
            spec.Token = new AbilityCancellationToken();

            // 활성 태그 부여: logic + recovery 동안 유지 (여기서만 관리!)
            if (tagSystem != null && def.grantedTagsWhileActive != null)
                tagSystem.AddTags(def.grantedTagsWhileActive);

            // Cue: WhileActive(Add)
            if (cueManager != null && def.cueWhileActive != null)
            {
                var p = BuildCueParamsForAbility(def, spec, target);
                cueManager.AddCue(def.cueWhileActive, p);
            }

            bool cancelled = false;
            // containers: OnActivate
            ApplyEffectContainers(spec, target, AbilityEffectTiming.OnActivate, null);

            System.Action<GameplayTag, AbilityEventData> onEvent = null;

            onEvent = (t, d) =>
            {
                // 이 능력 실행 중에만
                if (!isExecuting || currentExecSpec != spec) return;

                // Spec이 실린 이벤트면 “내 spec”만 받는다 (브로드캐스트 허용하려면 d.Spec==null은 통과)
                if (d.Spec != null && d.Spec != spec) return;

                ApplyEffectContainers(spec, target, AbilityEffectTiming.OnEvent, t);
            };
            try
            {
                if (def.logic == null)
                {
                    Debug.LogError($"[GAS] Ability '{def.name}' has no Logic. (Legacy pipeline removed)");
                    yield break;
                }


                OnGameplayEvent += onEvent;


                // 로직 실행
                yield return def.logic.Activate(this, spec, target);

                // 후딜(Recovery)
                float recovery = def.recoveryTime;

                if (spec.TryGetFloat("RecoveryOverride", out var overrideRecovery))
                    recovery = overrideRecovery;

                if (recovery > 0f)
                {
                    float end = Time.time + recovery;
                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            break;
                        yield return null;
                    }
                }
            }
            finally
            {
                cancelled = (spec.Token != null && spec.Token.IsCancelled);

                // waiter 정리
                CancelWaiters(spec);

                if (onEvent != null) OnGameplayEvent -= onEvent;

                // Cue: WhileActive(Remove) + End/Cancelled(Execute)
                if (cueManager != null)
                {
                    var p = BuildCueParamsForAbility(def, spec, target);
                    if (def.cueWhileActive != null) cueManager.RemoveCue(def.cueWhileActive, p);

                    if (cancelled && def.cueOnExecutionCancelled != null)
                        cueManager.ExecuteCue(def.cueOnExecutionCancelled, p);
                    else if (!cancelled && def.cueOnEnd != null)
                        cueManager.ExecuteCue(def.cueOnEnd, p);
                }
                ApplyEffectContainers(spec, target, AbilityEffectTiming.OnEnd, null);

                // 태그 회수
                if (tagSystem != null && def.grantedTagsWhileActive != null)
                    tagSystem.RemoveTags(def.grantedTagsWhileActive);

                // 토큰 종료
                spec.Token?.Cancel();
                spec.Token = null;

                // 실행 상태 정리
                isExecuting = false;
                currentExecSpec = null;
                currentExecTarget = null;
                activeExecution = null;

                // 버퍼 소비
                TryConsumeBuffered();
            }
        }

        // -----------------------------
        // Animation helpers
        // -----------------------------
        public void TryPlayAnimationTriggerHash(int triggerHash, AbilityDefinition def)
        {
            if (triggerHash == 0) return;

            Animator target = null;

            if (def != null && def.animationChannel == AbilityDefinition.AnimationChannel.Weapon)
                target = weaponAnimator != null ? weaponAnimator : playerAnimator; // fallback
            else
                target = playerAnimator != null ? playerAnimator : weaponAnimator; // fallback

            if (target == null) return;
            target.SetTrigger(triggerHash);
        }


        // -----------------------------
        // Spec helpers
        // -----------------------------
        public GameplayEffectSpec MakeSpec(GameplayEffect effect, GameObject causer = null, Object sourceObject = null)
        {
            var ctx = new GameplayEffectContext(gameObject, causer != null ? causer : gameObject);
            ctx.SourceObject = sourceObject;
            return new GameplayEffectSpec(effect, ctx);
        }

        public void ApplyEffectContainers(AbilitySpec spec, GameObject target, AbilityEffectTiming timing, GameplayTag eventTag)
        {
            var def = spec.Definition;
            if (def.containers == null || def.containers.Count == 0) return;

            for (int i = 0; i < def.containers.Count; i++)
            {
                var c = def.containers[i];
                if (c == null) continue;
                if (c.timing != timing) continue;

                if (timing == AbilityEffectTiming.OnEvent && c.eventTag != eventTag)
                    continue;

                if (c.effects == null || c.effects.Count == 0) continue;

                GameObject receiver = null;
                switch (c.targetPolicy)
                {
                    case AbilityEffectTargetPolicy.Caster:
                        receiver = gameObject;
                        break;
                    case AbilityEffectTargetPolicy.ExplicitTarget:
                        receiver = target;
                        break;
                }

                if (receiver == null) continue;

                foreach (var e in c.effects)
                    effectRunner.ApplyEffect(e, receiver, gameObject);
            }
        }

        // -----------------------------
        // Cue params builders
        // -----------------------------
        private GameplayCueParams BuildCueParamsForAbility(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            return new GameplayCueParams
            {
                Instigator = gameObject,
                Causer = gameObject,
                Target = target,
                Position = target != null ? target.transform.position : transform.position,
                Normal = Vector3.up,
                SourceObject = def,
                Magnitude = 1f
            };
        }

        private GameplayCueParams BuildCueParamsFromEvent(AbilityEventData data)
        {
            var p = new GameplayCueParams
            {
                Instigator = data.Instigator != null ? data.Instigator : gameObject,
                Causer = data.Instigator != null ? data.Instigator : gameObject,
                Target = data.Target,
                Position = data.WorldPosition != Vector3.zero ? data.WorldPosition : (data.Target != null ? data.Target.transform.position : transform.position),
                Normal = Vector3.up,
                SourceObject = (data.Spec != null ? data.Spec.Definition : null),
                Magnitude = 1f
            };
            return p;
        }

        //UI용 getter 
        public int GetChargesRemaining(AbilityDefinition ability)
        {
            var spec = FindSpec(ability);
            if (spec == null || spec.Definition == null || !spec.Definition.useCharges) return 0;
            return spec.GetInt(KEY_CHARGES, 0);
        }

        public int GetMaxCharges(AbilityDefinition ability)
        {
            var spec = FindSpec(ability);
            if (spec == null || spec.Definition == null || !spec.Definition.useCharges) return 1;
            return Mathf.Max(1, spec.Definition.maxCharges);
        }

        public float GetRechargeRemaining(AbilityDefinition ability)
        {
            var spec = FindSpec(ability);
            if (spec == null || spec.Definition == null || !spec.Definition.useCharges) return 0f;
            return Mathf.Max(0f, spec.GetFloat(KEY_RECHARGE, 0f));
        }

    }
}
