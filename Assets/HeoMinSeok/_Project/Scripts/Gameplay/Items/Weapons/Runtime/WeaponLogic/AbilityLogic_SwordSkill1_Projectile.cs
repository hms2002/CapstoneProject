using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - SwordSkill1 투사체 스킬의 발사 시점 계산을 담당한다.
    /// - 방향, 피해 스냅샷, 투사체 생성 문맥을 준비한 뒤 projectile에게 넘긴다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_SwordSkill1_Projectile", menuName = "GAS/Samples/AbilityLogic/Sword Skill1 Projectile")]
    public class AbilityLogic_SwordSkill1_Projectile : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            var def = spec?.Definition;
            if (system == null || def == null) yield break;
            if (system.AttributeSet == null) yield break;

            var data = def.sourceObject as SwordSkill1ProjectileData;
            if (data == null || data.projectilePrefab == null) yield break;

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);

            Vector3 spawnPos = system.transform.position + data.spawnOffset;
            var go = Object.Instantiate(data.projectilePrefab, spawnPos, Quaternion.identity);

            var cfg = data.DamageConfig;
            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            float legacyBaseHp = data.damage;
            float baseHp = data.damageFormula != null
                ? data.damageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: legacyBaseHp)
                : legacyBaseHp;

            float baseKnockback = (data.knockbackFormula != null)
                ? data.knockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : 0f;

            float baseStagger = (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                ? cfg.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : Mathf.Max(0f, data.baseStaggerDamage);

            var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: cfg,
                baseHp: baseHp,
                baseStagger: baseStagger,
                baseKnockback: baseKnockback,
                elementSource: system.gameObject
            );

            var proj = go.GetComponent<SwordSkill1Projectile2D>();
            if (proj == null)
            {
                Debug.LogError("[AbilityLogic_SwordSkill1_Projectile] Projectile prefab is missing SwordSkill1Projectile2D.", go);
                Object.Destroy(go);
                yield break;
            }

            // 책임 :
            // - 발사 시점에 확정된 피해 정보를 공용 CombatHitPayload로 고정한다.
            // - 이후 투사체/근접/유물 공격이 같은 payload 규약으로 피해를 적용하게 한다.
            var payload = CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: spec,
                damageEffect: data.damageEffect,
                knockbackEffect: data.knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: null,
                causer: system.gameObject);

            var context = new ProjectileAttackSpawnContext
            {
                ownerSystem = system,
                sourceSpec = spec,
                causer = system.gameObject,
                ignoreTarget = system.gameObject,
                lifetime = data.lifetime,
                wallLayers = data.wallLayers,
                damageLayers = data.damageLayers,
                hitPayload = payload,
                direction = dir,
                speed = data.projectileSpeed
            };

            proj.Setup(context);
            yield break;
        }
    }
}
