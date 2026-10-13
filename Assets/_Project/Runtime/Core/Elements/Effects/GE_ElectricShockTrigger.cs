using System;
using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    public readonly struct ElectricDischargeDamageModifierContext
    {
        public ElectricDischargeDamageModifierContext(GameObject instigator, GameObject target, float baseDamage)
        {
            Instigator = instigator;
            Target = target;
            BaseDamage = baseDamage;
        }

        public GameObject Instigator { get; }
        public GameObject Target { get; }
        public float BaseDamage { get; }
    }

    public static class ElectricDischargeDamageModifiers
    {
        private static readonly List<Func<ElectricDischargeDamageModifierContext, float>> Modifiers = new();

        public static IDisposable Register(Func<ElectricDischargeDamageModifierContext, float> modifier)
        {
            if (modifier == null)
                return EmptyHandle.Instance;

            Modifiers.Add(modifier);
            return new Registration(modifier);
        }

        internal static float Apply(ElectricDischargeDamageModifierContext context)
        {
            float result = Mathf.Max(0f, context.BaseDamage);
            for (int i = 0; i < Modifiers.Count; i++)
            {
                Func<ElectricDischargeDamageModifierContext, float> modifier = Modifiers[i];
                if (modifier == null)
                    continue;

                result = Mathf.Max(0f, modifier(new ElectricDischargeDamageModifierContext(
                    context.Instigator,
                    context.Target,
                    result)));
            }

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Modifiers.Clear();
        }

        private sealed class Registration : IDisposable
        {
            private Func<ElectricDischargeDamageModifierContext, float> modifier;

            public Registration(Func<ElectricDischargeDamageModifierContext, float> modifier)
            {
                this.modifier = modifier;
            }

            public void Dispose()
            {
                if (modifier == null)
                    return;

                Modifiers.Remove(modifier);
                modifier = null;
            }
        }

        private sealed class EmptyHandle : IDisposable
        {
            public static readonly EmptyHandle Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// 책임: GE_ElectricShockTrigger가 구체 VFX 타입을 알지 않고 연쇄 지점 목록만 presentation 구현에 전달하게 한다.
    /// </summary>
    public interface IChainPointPresentation
    {
        void Play(IReadOnlyList<Vector3> worldPoints);
    }

    // The first point is a saved line origin, not a repeated initial-target impact.
    public interface IChainReplayPresentation
    {
        void PlayReplay(IReadOnlyList<Vector3> worldPoints);
    }

    public static class ElectricChainDurationBonuses
    {
        private static readonly List<Func<GameObject, float>> Bonuses = new();

        public static IDisposable Register(Func<GameObject, float> bonus)
        {
            if (bonus == null) throw new ArgumentNullException(nameof(bonus));
            Bonuses.Add(bonus);
            return new Registration(bonus);
        }

        internal static float Read(GameObject instigator)
        {
            float seconds = 0f;
            for (int i = 0; i < Bonuses.Count; i++)
                seconds += Mathf.Max(0f, Bonuses[i](instigator));
            return seconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Bonuses.Clear();

        private sealed class Registration : IDisposable
        {
            private Func<GameObject, float> bonus;
            public Registration(Func<GameObject, float> bonus) => this.bonus = bonus;
            public void Dispose()
            {
                if (bonus == null) return;
                Bonuses.Remove(bonus);
                bonus = null;
            }
        }
    }

    public static class ElectricChainTargetBonuses
    {
        private static readonly List<Func<GameObject, int>> Bonuses = new();
        public static IDisposable Register(Func<GameObject, int> bonus)
        {
            if (bonus == null) throw new ArgumentNullException(nameof(bonus));
            Bonuses.Add(bonus);
            return new Registration(bonus);
        }
        internal static int Read(GameObject instigator)
        {
            int targets = 0;
            for (int i = 0; i < Bonuses.Count; i++) targets += Mathf.Max(0, Bonuses[i](instigator));
            return targets;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Bonuses.Clear();
        private sealed class Registration : IDisposable
        {
            private Func<GameObject, int> bonus;
            public Registration(Func<GameObject, int> bonus) => this.bonus = bonus;
            public void Dispose()
            {
                if (bonus == null) return;
                Bonuses.Remove(bonus);
                bonus = null;
            }
        }
    }

    public readonly struct ElectricDischargeRelicBonus
    {
        public ElectricDischargeRelicBonus(float chainStepBonus = 0f, float chainMaximumBonus = 0f, float echoDamageRatio = 0f)
        {
            ChainStepBonus = Mathf.Max(0f, chainStepBonus);
            ChainMaximumBonus = Mathf.Max(0f, chainMaximumBonus);
            EchoDamageRatio = Mathf.Max(0f, echoDamageRatio);
        }
        public float ChainStepBonus { get; }
        public float ChainMaximumBonus { get; }
        public float EchoDamageRatio { get; }
        public float ChainMultiplier(int step) => 1f + Mathf.Min(ChainMaximumBonus, ChainStepBonus * Mathf.Max(0, step));
    }

    public static class ElectricDischargeRelicBonuses
    {
        private static readonly List<Func<GameObject, ElectricDischargeRelicBonus>> Bonuses = new();
        public static IDisposable Register(Func<GameObject, ElectricDischargeRelicBonus> bonus)
        {
            if (bonus == null) throw new ArgumentNullException(nameof(bonus));
            Bonuses.Add(bonus);
            return new Registration(bonus);
        }
        internal static ElectricDischargeRelicBonus Read(GameObject instigator)
        {
            float step = 0f, maximum = 0f, echo = 0f;
            for (int i = 0; i < Bonuses.Count; i++)
            {
                var bonus = Bonuses[i](instigator);
                step += bonus.ChainStepBonus;
                maximum += bonus.ChainMaximumBonus;
                // One replay even if an equivalent source is registered more than once.
                echo = Mathf.Max(echo, bonus.EchoDamageRatio);
            }
            return new ElectricDischargeRelicBonus(step, maximum, echo);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Bonuses.Clear();
        private sealed class Registration : IDisposable
        {
            private Func<GameObject, ElectricDischargeRelicBonus> bonus;
            public Registration(Func<GameObject, ElectricDischargeRelicBonus> bonus) => this.bonus = bonus;
            public void Dispose()
            {
                if (bonus == null) return;
                Bonuses.Remove(bonus);
                bonus = null;
            }
        }
    }

    public readonly struct ElectricShockApplicationContext
    {
        public ElectricShockApplicationContext(GameObject instigator, GameObject target)
        {
            Instigator = instigator;
            Target = target;
        }
        public GameObject Instigator { get; }
        public GameObject Target { get; }
    }

    public static class ElectricShockApplications
    {
        private static readonly List<Action<ElectricShockApplicationContext>> Listeners = new();
        public static IDisposable Register(Action<ElectricShockApplicationContext> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            Listeners.Add(listener);
            return new Registration(listener);
        }
        internal static void Notify(ElectricShockApplicationContext context)
        {
            foreach (var listener in Listeners.ToArray()) listener(context);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Listeners.Clear();
        private sealed class Registration : IDisposable
        {
            private Action<ElectricShockApplicationContext> listener;
            public Registration(Action<ElectricShockApplicationContext> listener) => this.listener = listener;
            public void Dispose()
            {
                if (listener == null) return;
                Listeners.Remove(listener);
                listener = null;
            }
        }
    }

    /// <summary>
    /// 책임 : 전기 게이지가 발현됐을 때 감전 상태 부여, 연쇄 대상 탐색, 전기 발현 피해 적용을 수행한다.
    /// 전기 발현 피해가 일반 피해 팝업으로 섞이지 않도록 팝업용 속성 태그도 함께 전달한다.
    /// </summary>
    [CreateAssetMenu(fileName = "GE_ElectricShockTrigger", menuName = "GAS/Effects/Electric Shock Trigger")]
    public sealed class GE_ElectricShockTrigger : GameplayEffect, ISpecGameplayEffect
    {
        [Header("Electrocute")]
        [SerializeField] private GameplayEffect electrocutedStatusEffect;
        [SerializeField] private GameplayTag electrocutedTag;

        [Header("Damage")]
        [SerializeField] private GameplayEffect electricDamageEffect;
        [SerializeField] private GameplayTag electricDamageKey;
        [SerializeField] private GameplayTag electricPopupElementTag;
        [SerializeField, Min(0f)] private float electricDamage = 5f;
        [SerializeField, Min(0f)] private float initialElectricCoefficient = 1f;
        [SerializeField, Min(0f)] private float chainElectricCoefficient = 0.5f;

        [Header("Discharge")]
        [SerializeField, Min(0f)] private float chainRadius = 4f;
        [SerializeField, Min(1)] private int maxTargetsPerDischarge = 3;
        [SerializeField, Min(8)] private int maxScanColliders = 64;
        [SerializeField] private LayerMask chainTargetLayers = ~0;

        [Header("Visual")]
        [SerializeField] private MonoBehaviour chainVfxPrefab;

        private void OnValidate()
        {
            duration = 0f;
            if (electricDamage < 0f) electricDamage = 0f;
            if (initialElectricCoefficient < 0f) initialElectricCoefficient = 0f;
            if (chainElectricCoefficient < 0f) chainElectricCoefficient = 0f;
            if (chainRadius < 0f) chainRadius = 0f;
            if (maxTargetsPerDischarge < 1) maxTargetsPerDischarge = 1;
            if (maxScanColliders < 8) maxScanColliders = 8;
        }

        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            GameObject instigator = spec != null ? spec.Context?.Instigator : null;
            GameObject causer = spec != null ? spec.Context?.Causer : instigator;
            GameplayTag popupElementTag = ResolvePopupElementTag(spec != null ? spec.Context?.DamagePopupElementTag : null);
            Execute(target, instigator, causer, popupElementTag);
        }

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            Execute(target, instigator, instigator, ResolvePopupElementTag(null));
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
        }

        private void Execute(
            GameObject initialTarget,
            GameObject instigator,
            GameObject causer,
            GameplayTag popupElementTag)
        {
            if (initialTarget == null)
                return;

            if (causer == null)
                causer = instigator;

            // One attacker stat snapshot keeps a discharge consistent across tag and damage callbacks.
            float electricAttribute = ReadElectricAttribute(instigator);
            float chainDurationBonus = ElectricChainDurationBonuses.Read(instigator);
            int maximumTargets = Mathf.Max(1, maxTargetsPerDischarge + ElectricChainTargetBonuses.Read(instigator));
            ElectricDischargeRelicBonus relicBonus = ElectricDischargeRelicBonuses.Read(instigator);
            var visited = new HashSet<GameObject>();
            var chainTargets = new List<GameObject>(maximumTargets);
            var chainPoints = new List<Vector3>(maximumTargets);
            var echoHits = relicBonus.EchoDamageRatio > 0f ? new List<ElectricHit>(maximumTargets) : null;

            AddVisitedTarget(initialTarget, visited, chainTargets, chainPoints);
            Vector3 initialPoint = chainPoints[0];
            ApplyElectricHit(initialTarget, instigator, causer, popupElementTag,
                electricAttribute, relicBonus.ChainMultiplier(0), applyElectrocutedStatus: true);

            if (chainRadius <= 0f || electrocutedTag == null || maximumTargets <= 1)
            {
                PlayChainVfx(chainPoints);
                return;
            }

            var scanBuffer = new Collider2D[Mathf.Max(8, maxScanColliders)];
            var contactFilter = CreateContactFilter();

            GameObject current = initialTarget;
            while (chainTargets.Count < maximumTargets)
            {
                GameObject next = FindNearestElectrocutedCandidate(
                    current,
                    visited,
                    scanBuffer,
                    contactFilter);

                if (next == null)
                    break;

                AddVisitedTarget(next, visited, chainTargets, chainPoints);
                float chainDamage = ApplyElectricHit(next, instigator, causer, popupElementTag,
                    electricAttribute, relicBonus.ChainMultiplier(chainTargets.Count - 1), applyElectrocutedStatus: false);
                if (chainDamage > 0f) echoHits?.Add(new ElectricHit(next, chainDamage, chainPoints[^1]));
                ExtendChainDuration(next, chainDurationBonus);
                current = next;
            }

            PlayChainVfx(chainPoints);
            ReplayElectricDamage(echoHits, initialPoint, instigator, causer, popupElementTag, relicBonus.EchoDamageRatio);
        }

        private readonly struct ElectricHit
        {
            public ElectricHit(GameObject target, float damage, Vector3 point) { Target = target; Damage = damage; Point = point; }
            public GameObject Target { get; }
            public float Damage { get; }
            public Vector3 Point { get; }
        }

        private void ReplayElectricDamage(List<ElectricHit> hits, Vector3 initialPoint, GameObject instigator, GameObject causer,
            GameplayTag popupElementTag, float ratio)
        {
            if (hits == null || hits.Count == 0) return;
            var points = new List<Vector3>(hits.Count + 1) { initialPoint };
            foreach (var hit in hits)
                if (ApplySecondaryElectricDamage(hit.Target, instigator, causer, hit.Damage * ratio, popupElementTag))
                    points.Add(hit.Point);
            if (points.Count > 1) PlayChainVfx(points, replay: true);
        }

        private ContactFilter2D CreateContactFilter()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(chainTargetLayers);
            filter.useTriggers = true;
            return filter;
        }

        private void ExtendChainDuration(GameObject target, float seconds)
        {
            if (target == null || seconds <= 0f || electrocutedStatusEffect == null)
                return;
            var active = ResolveEffectRunner(target)?.FindActiveEffect(electrocutedStatusEffect, target);
            float maximum = Mathf.Max(0f, electrocutedStatusEffect.duration);
            if (active == null || active.TimeRemaining <= 0f || active.TimeRemaining >= maximum)
                return;
            active.TimeRemaining = Mathf.Min(maximum, active.TimeRemaining + seconds);
        }

        private GameObject FindNearestElectrocutedCandidate(
            GameObject current,
            HashSet<GameObject> visited,
            Collider2D[] scanBuffer,
            ContactFilter2D contactFilter)
        {
            if (current == null || scanBuffer == null || electrocutedTag == null)
                return null;

            Vector2 origin = ResolveTargetVisualPoint(current);
            int count = Physics2D.OverlapCircle(origin, chainRadius, contactFilter, scanBuffer);

            GameObject nearest = null;
            float nearestDistanceSq = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = scanBuffer[i];
                if (hit == null)
                    continue;

                if (hit.GetComponent<CombatHurtbox2D>() == null)
                    continue;

                GameObject candidate = CombatTargetResolver2D.ResolveDamageTarget(hit);
                if (!IsValidDischargeCandidate(candidate, visited))
                    continue;

                float distanceSq = ((Vector2)ResolveTargetVisualPoint(candidate) - origin).sqrMagnitude;
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearest = candidate;
                nearestDistanceSq = distanceSq;
            }

            return nearest;
        }

        private bool IsValidDischargeCandidate(GameObject candidate, HashSet<GameObject> visited)
        {
            if (candidate == null || !candidate.activeInHierarchy)
                return false;

            if (visited != null && visited.Contains(candidate))
                return false;

            var tags = candidate.GetComponent<TagSystem>();
            if (tags == null || electrocutedTag == null || !tags.HasTag(electrocutedTag))
                return false;

            return ResolveEffectRunner(candidate) != null;
        }

        private void AddVisitedTarget(
            GameObject target,
            HashSet<GameObject> visited,
            List<GameObject> chainTargets,
            List<Vector3> chainPoints)
        {
            if (target == null)
                return;

            visited?.Add(target);
            chainTargets?.Add(target);
            chainPoints?.Add(ResolveTargetVisualPoint(target));
        }

        private static Vector3 ResolveTargetVisualPoint(GameObject target)
        {
            if (target == null)
                return Vector3.zero;

            if (PresentationTargetBoundsUtility.TryResolveSpriteBounds(target, out Bounds bounds))
                return bounds.center;

            return target.transform.position;
        }

        private float ApplyElectricHit(
            GameObject target,
            GameObject instigator,
            GameObject causer,
            GameplayTag popupElementTag,
            float electricAttribute,
            float chainMultiplier,
            bool applyElectrocutedStatus)
        {
            if (target == null)
                return 0f;

            GameplayEffectRunner runner = ResolveEffectRunner(target);
            if (runner == null)
                return 0f;

            // Chained targets keep their existing status lifetime; only the initial discharge reapplies it.
            if (applyElectrocutedStatus && electrocutedStatusEffect != null)
            {
                bool wasActive = runner.HasActiveEffect(electrocutedStatusEffect, target);
                GameplayEffectSpec statusSpec = CreateSpec(
                    electrocutedStatusEffect,
                    instigator,
                    causer,
                    electrocutedStatusEffect,
                    popupElementTag: null);
                runner.ApplyEffectSpec(statusSpec, target);
                if (target != null && runner != null && runner.HasActiveEffect(electrocutedStatusEffect, target))
                {
                    target.GetComponent<ElementGaugeSystem>()?.RecordDirectElectrocution(
                        this, electrocutedStatusEffect, wasActive);
                    ElectricShockApplications.Notify(new ElectricShockApplicationContext(instigator, target));
                }
            }

            float coefficient = applyElectrocutedStatus ? initialElectricCoefficient : chainElectricCoefficient;
            float baseDamage = Mathf.Max(0f, electricDamage) + electricAttribute * Mathf.Max(0f, coefficient);
            if (electricDamageEffect != null && baseDamage > 0f)
            {
                float resolvedElectricDamage = ElectricDischargeDamageModifiers.Apply(
                    new ElectricDischargeDamageModifierContext(instigator, target, baseDamage)) * chainMultiplier;
                if (ApplySecondaryElectricDamage(target, instigator, causer, resolvedElectricDamage, popupElementTag))
                    return resolvedElectricDamage;
            }
            return 0f;
        }

        // Reports payload submission; the damage effect retains authority over shields, immunity and HP.
        // No status, chain search, gauge, duration bonus or relic activation.
        public bool ApplySecondaryElectricDamage(GameObject target, GameObject instigator, GameObject causer,
            float damage, GameplayTag popupTag = null)
        {
            if (CombatHitPause2D.IsActionStoppedOn(target) || electricDamageEffect == null ||
                !float.IsFinite(damage) || damage <= 0f)
                return false;
            var runner = ResolveEffectRunner(target);
            if (runner == null) return false;
            var damageSpec = CreateSpec(electricDamageEffect, instigator, causer, this, ResolvePopupElementTag(popupTag));
            if (electricDamageKey != null)
                damageSpec.SetSetByCallerMagnitude(electricDamageKey, damage);
            runner.ApplyEffectSpec(damageSpec, target);
            return true;
        }

        public static float ReadElectricAttribute(GameObject instigator)
        {
            if (instigator == null)
                return 0f;

            IStatProvider provider = instigator.GetComponent<AttributeStatSource>();
            provider ??= AbilityStatProviderFactory.Create(instigator.GetComponent<AbilitySystem>());
            return provider != null ? Mathf.Max(0f, provider.Get(StatId.ElectricFinal)) : 0f;
        }

        private static GameplayEffectSpec CreateSpec(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            UnityEngine.Object sourceObject,
            GameplayTag popupElementTag)
        {
            var context = new GameplayEffectContext(instigator, causer != null ? causer : instigator)
            {
                SourceObject = sourceObject != null ? sourceObject : effect,
                DamagePopupElementTag = popupElementTag
            };

            return new GameplayEffectSpec(effect, context);
        }

        private static GameplayEffectRunner ResolveEffectRunner(GameObject target)
        {
            if (target == null)
                return null;

            var abilitySystem = target.GetComponent<AbilitySystem>();
            if (abilitySystem != null && abilitySystem.EffectRunner != null)
                return abilitySystem.EffectRunner;

            return target.GetComponent<GameplayEffectRunner>();
        }

        private GameplayTag ResolvePopupElementTag(GameplayTag contextTag)
        {
            return contextTag != null ? contextTag : electricPopupElementTag;
        }

        private void PlayChainVfx(IReadOnlyList<Vector3> chainPoints, bool replay = false)
        {
            if (chainVfxPrefab == null || chainPoints == null || chainPoints.Count == 0)
                return;
            if (replay && !(chainVfxPrefab is IChainReplayPresentation))
                return;

            MonoBehaviour instance = Instantiate(chainVfxPrefab);
            if (replay && instance is IChainReplayPresentation replayPresentation)
            {
                replayPresentation.PlayReplay(chainPoints);
                return;
            }
            if (!replay && instance is IChainPointPresentation chainPresentation)
            {
                chainPresentation.Play(chainPoints);
                return;
            }

            if (instance != null)
                Destroy(instance.gameObject);
        }
    }
}
