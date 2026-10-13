using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 술 노출 owner에 플레이어 접촉을 전달하고 점화 진입, 불 장판 교체를 관리한다.
    /// - 술 웅덩이 본체 연출은 shader visual에 맡기고 전투 판정만 소유한다.
    /// </summary>
    public sealed class AlcoholPuddleArea : PuddleAreaBase
    {
        // Existing serialized authoring is retained; the recipient now owns charge and effect timing.
        [SerializeField, HideInInspector] private CombatBuffDebuffApplicationDefinition alcoholBuffDefinition;
        [SerializeField, HideInInspector] private float buffRefreshIntervalSeconds = 0.2f;
        [SerializeField, HideInInspector] private float buffDurationSeconds = 0.35f;

        [Header("Projectile Damage")]
        [SerializeField] private GE_Damage_Spec projectileDamageEffect;
        [SerializeField, Min(0f)] private float projectilePlayerDamage = 1f;

        [Header("Ignition")]
        [SerializeField] private FirePuddleArea firePuddlePrefab;

        [Header("Presentation")]
        [SerializeField] private Color alcoholColor = new(0.72f, 0.34f, 0.12f, 0.72f);
        [SerializeField] private Color ignitingColor = new(1f, 0.45f, 0.12f, 0.8f);
        [SerializeField] private Color fireColor = new(1f, 0.18f, 0.04f, 0.8f);

        private readonly Dictionary<Collider2D, PlayerAlcoholExposure> overlappingColliders = new();
        private readonly HashSet<PlayerAlcoholExposure> exposedPlayers = new();
        private readonly HashSet<PlayerAlcoholExposure> validContactPlayers = new();
        private readonly List<Collider2D> staleColliders = new();

        public override PuddleElementType ElementType => PuddleElementType.Alcohol;

        protected override void Awake()
        {
            base.Awake();
            // Blob visual + outline은 Noita풍 particle field 전환 테스트 동안 비활성화한다.
            // BlobVisual?.SetColor(alcoholColor);
        }

        private void Update()
        {
            RefreshPlayerContacts();
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

            TrackGroundContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // A player can land inside an existing puddle without another enter callback.
            if (CanApplyGroundEffect)
                TrackGroundContact(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null && overlappingColliders.Remove(other))
                RefreshPlayerContacts();
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
                ReleasePlayerContacts();
        }

        protected override void OnDisable()
        {
            ReleasePlayerContacts();
            base.OnDisable();
        }

        private void TrackGroundContact(Collider2D other)
        {
            if (other == null)
                return;

            if (!overlappingColliders.ContainsKey(other))
            {
                GameObject target = ResolvePuddleTarget(other);
                if (target == null || !IsPlayerTarget(target) ||
                    !target.TryGetComponent(out PlayerAlcoholExposure exposure))
                    return;
                overlappingColliders.Add(other, exposure);
            }

            RefreshPlayerContacts();
        }

        private void RefreshPlayerContacts()
        {
            validContactPlayers.Clear();
            staleColliders.Clear();
            foreach (var contact in overlappingColliders)
            {
                Collider2D collider = contact.Key;
                PlayerAlcoholExposure exposure = contact.Value;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || exposure == null)
                {
                    staleColliders.Add(collider);
                    continue;
                }
                if (CanApplyGroundEffect && exposure.isActiveAndEnabled && CanApplyGroundEffectTo(exposure.gameObject))
                    validContactPlayers.Add(exposure);
            }

            foreach (Collider2D collider in staleColliders)
                overlappingColliders.Remove(collider);
            foreach (PlayerAlcoholExposure exposure in exposedPlayers)
                if (exposure != null && !validContactPlayers.Contains(exposure))
                    exposure.ExitPuddle(this);
            foreach (PlayerAlcoholExposure exposure in validContactPlayers)
                if (!exposedPlayers.Contains(exposure))
                    exposure.EnterPuddle(this);

            exposedPlayers.Clear();
            exposedPlayers.UnionWith(validContactPlayers);
        }

        private void ReleasePlayerContacts()
        {
            foreach (PlayerAlcoholExposure exposure in exposedPlayers)
                if (exposure != null)
                    exposure.ExitPuddle(this);
            exposedPlayers.Clear();
            validContactPlayers.Clear();
            overlappingColliders.Clear();
            staleColliders.Clear();
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
                CapstoneDiagnostics.EditorOnlyLog.LogWarning("[AlcoholPuddleArea] firePuddlePrefab이 없어 불 장판으로 교체하지 못했습니다.", this);
                return;
            }

            FirePuddleArea fire = Instantiate(firePuddlePrefab, transform.position, transform.rotation);
            fire.name = $"{firePuddlePrefab.name}_FromAlcohol";
        }

    }
}
