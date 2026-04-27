using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 바닥에 놓인 술 장판의 플레이어 버프, 점화 진입, 불 장판 교체를 관리한다.
    /// - 술 웅덩이 본체 연출은 shader visual에 맡기고 전투 판정만 소유한다.
    /// </summary>
    public sealed class AlcoholPuddleArea : PuddleAreaBase
    {
        private const string AlcoholBuffOwnerKey = "Puddle.Alcohol";

        [Header("Alcohol Buff")]
        [SerializeField] private CombatBuffDebuffApplicationDefinition alcoholBuffDefinition;
        [SerializeField, Min(0.05f)] private float buffRefreshIntervalSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float buffDurationSeconds = 0.35f;

        [Header("Projectile Damage")]
        [SerializeField] private GE_Damage_Spec projectileDamageEffect;
        [SerializeField, Min(0f)] private float projectilePlayerDamage = 1f;

        [Header("Ignition")]
        [SerializeField] private FirePuddleArea firePuddlePrefab;

        [Header("Presentation")]
        [SerializeField] private Color alcoholColor = new(0.72f, 0.34f, 0.12f, 0.72f);
        [SerializeField] private Color ignitingColor = new(1f, 0.45f, 0.12f, 0.8f);
        [SerializeField] private Color fireColor = new(1f, 0.18f, 0.04f, 0.8f);

        private readonly HashSet<GameObject> overlappingTargets = new();
        private float nextBuffRefreshTime;

        public override PuddleElementType ElementType => PuddleElementType.Alcohol;

        protected override void Awake()
        {
            base.Awake();
            // Blob visual + outline은 Noita풍 particle field 전환 테스트 동안 비활성화한다.
            // BlobVisual?.SetColor(alcoholColor);
        }

        private void Update()
        {
            if (!CanApplyGroundEffect)
                return;

            if (Time.time < nextBuffRefreshTime)
                return;

            nextBuffRefreshTime = Time.time + Mathf.Max(0.05f, buffRefreshIntervalSeconds);
            RefreshAlcoholBuffs();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (CanApplyProjectileEffect)
            {
                TryApplyProjectileDamage(other);
                return;
            }

            if (!CanApplyGroundEffect)
                return;

            GameObject target = ResolvePuddleTarget(other);
            if (target != null && CanApplyGroundEffectTo(target))
                overlappingTargets.Add(target);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            GameObject target = ResolvePuddleTarget(other);
            if (target != null)
                overlappingTargets.Remove(target);
        }

        public void RequestIgnite()
        {
            PuddleConversionService.ResolveForScene()?.RequestIgnite(this);
        }

        public void BeginIgnitionPresentation()
        {
            SetIgnitingMode();
            ShaderVisual?.SetIgnitionProgress(0f);
            // Blob visual + outline은 Noita풍 particle field 전환 테스트 동안 비활성화한다.
            // BlobVisual?.SetColor(ignitingColor);
        }

        public void SetIgnitionVisualProgress(float normalizedProgress)
        {
            float t = Mathf.Clamp01(normalizedProgress);
            ShaderVisual?.SetIgnitionProgress(t);
            Color color = Color.Lerp(alcoholColor, fireColor, t);
            // Blob visual + outline은 Noita풍 particle field 전환 테스트 동안 비활성화한다.
            // BlobVisual?.SetColor(color);
        }

        public void CompleteIgnitionToFire()
        {
            if (Mode == PuddleAreaMode.Consumed)
                return;

            SpawnFireReplacement();
            MarkConsumed();
            gameObject.SetActive(false);
        }

        protected override void HandleModeChanged(PuddleAreaMode previousMode, PuddleAreaMode newMode)
        {
            if (!CanApplyGroundEffect)
                overlappingTargets.Clear();
        }

        private void RefreshAlcoholBuffs()
        {
            if (alcoholBuffDefinition == null)
                return;

            foreach (GameObject target in overlappingTargets)
            {
                if (target == null || !IsPlayerTarget(target))
                    continue;

                if (!CanApplyGroundEffectTo(target))
                    continue;

                CombatBuffDebuffApplier applier = CombatBuffDebuffApplier.GetOrAdd(gameObject);
                applier?.ApplyFromSource(
                    this,
                    target,
                    alcoholBuffDefinition,
                    AlcoholBuffOwnerKey,
                    buffDurationSeconds);
            }
        }

        private void TryApplyProjectileDamage(Collider2D other)
        {
            if (projectileDamageEffect == null)
                return;

            GameObject target = ResolvePuddleTarget(other);
            if (target == null || !IsPlayerTarget(target))
                return;

            if (!TryRegisterProjectileHit(target))
                return;

            AbilitySystem targetSystem = ResolveAbilitySystem(target);
            if (targetSystem == null)
                return;

            HazardDamageAction.ApplyDamage(
                targetSystem,
                target,
                projectileDamageEffect,
                projectilePlayerDamage,
                gameObject,
                this);

            ConsumeAfterProjectileHit();
        }

        private void ConsumeAfterProjectileHit()
        {
            MarkConsumed();
            gameObject.SetActive(false);
        }

        private void SpawnFireReplacement()
        {
            if (firePuddlePrefab == null)
            {
                Debug.LogWarning("[AlcoholPuddleArea] firePuddlePrefab이 없어 불 장판으로 교체하지 못했습니다.", this);
                return;
            }

            FirePuddleArea fire = Instantiate(firePuddlePrefab, transform.position, transform.rotation);
            fire.name = $"{firePuddlePrefab.name}_FromAlcohol";
        }

    }
}
