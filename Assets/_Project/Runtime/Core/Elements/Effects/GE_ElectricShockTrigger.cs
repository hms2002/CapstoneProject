using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
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

        [Header("Discharge")]
        [SerializeField, Min(0f)] private float chainRadius = 4f;
        [SerializeField, Min(1)] private int maxTargetsPerDischarge = 8;
        [SerializeField, Min(8)] private int maxScanColliders = 64;
        [SerializeField] private LayerMask chainTargetLayers = ~0;

        [Header("Visual")]
        [SerializeField] private ElectricChainRibbonVfx chainVfxPrefab;

        private void OnValidate()
        {
            duration = 0f;
            if (electricDamage < 0f) electricDamage = 0f;
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

            var visited = new HashSet<GameObject>();
            var chainTargets = new List<GameObject>(Mathf.Max(1, maxTargetsPerDischarge));
            var chainPoints = new List<Vector3>(Mathf.Max(1, maxTargetsPerDischarge));

            AddVisitedTarget(initialTarget, visited, chainTargets, chainPoints);
            ApplyElectricHit(initialTarget, instigator, causer, popupElementTag);

            if (chainRadius <= 0f || electrocutedTag == null || maxTargetsPerDischarge <= 1)
            {
                PlayChainVfx(chainPoints);
                return;
            }

            var scanBuffer = new Collider2D[Mathf.Max(8, maxScanColliders)];
            var contactFilter = CreateContactFilter();

            GameObject current = initialTarget;
            while (chainTargets.Count < maxTargetsPerDischarge)
            {
                GameObject next = FindNearestElectrocutedCandidate(
                    current,
                    visited,
                    scanBuffer,
                    contactFilter);

                if (next == null)
                    break;

                AddVisitedTarget(next, visited, chainTargets, chainPoints);
                ApplyElectricHit(next, instigator, causer, popupElementTag);
                current = next;
            }

            PlayChainVfx(chainPoints);
        }

        private ContactFilter2D CreateContactFilter()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(chainTargetLayers);
            filter.useTriggers = true;
            return filter;
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

        private void ApplyElectricHit(
            GameObject target,
            GameObject instigator,
            GameObject causer,
            GameplayTag popupElementTag)
        {
            if (target == null)
                return;

            GameplayEffectRunner runner = ResolveEffectRunner(target);
            if (runner == null)
                return;

            if (electrocutedStatusEffect != null)
            {
                GameplayEffectSpec statusSpec = CreateSpec(
                    electrocutedStatusEffect,
                    instigator,
                    causer,
                    electrocutedStatusEffect,
                    popupElementTag: null);
                runner.ApplyEffectSpec(statusSpec, target);
            }

            if (electricDamageEffect != null && electricDamage > 0f)
            {
                GameplayEffectSpec damageSpec = CreateSpec(
                    electricDamageEffect,
                    instigator,
                    causer,
                    this,
                    popupElementTag);
                if (electricDamageKey != null)
                    damageSpec.SetSetByCallerMagnitude(electricDamageKey, electricDamage);

                runner.ApplyEffectSpec(damageSpec, target);
            }
        }

        private static GameplayEffectSpec CreateSpec(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            Object sourceObject,
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

        private void PlayChainVfx(IReadOnlyList<Vector3> chainPoints)
        {
            if (chainVfxPrefab == null || chainPoints == null || chainPoints.Count == 0)
                return;

            ElectricChainRibbonVfx instance = Instantiate(chainVfxPrefab);
            instance.Play(chainPoints);
        }
    }
}
