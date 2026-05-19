using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 AL들이 공유하는 잔탄 조회, 고정 피해 payload 생성, 사격체 생성 보조 로직을 제공한다.
    /// - 개별 AL이 inventory/runtime data/CombatHitPayload 세부를 반복해서 알지 않게 한다.
    /// </summary>
    internal static class OddIronAbilityUtility
    {
        public static OddIronRuntimeData ResolveRuntimeData(AbilitySystem system)
        {
            WeaponInventory2D inventory = system != null ? system.GetComponent<WeaponInventory2D>() : null;
            return inventory != null ? inventory.ActiveRuntimeData as OddIronRuntimeData : null;
        }

        public static WeaponInventory2D ResolveInventory(AbilitySystem system)
        {
            return system != null ? system.GetComponent<WeaponInventory2D>() : null;
        }

        public static OddIronRuntimeState ResolveRuntimeState(AbilitySystem system)
        {
            WeaponEquipController equipController = system != null
                ? system.GetComponent<WeaponEquipController>()
                : null;

            return equipController != null
                ? equipController.GetCurrentWeaponRuntimeState() as OddIronRuntimeState
                : null;
        }

        public static CombatHitPayload BuildFixedPayload(
            AbilitySystem system,
            AbilitySpec spec,
            DamagePayloadConfig config,
            GameplayEffect damageEffect,
            GE_Knockback_Spec knockbackEffect,
            float fixedDamage,
            float fixedStaggerDamage,
            float fixedKnockbackImpulse)
        {
            if (system == null || damageEffect == null)
                return null;

            CombatDamageSnapshot snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: null,
                config: config,
                baseHp: fixedDamage,
                baseStagger: fixedStaggerDamage,
                baseKnockback: fixedKnockbackImpulse,
                elementSource: system.gameObject);

            return CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: spec,
                damageEffect: damageEffect,
                knockbackEffect: knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: null,
                causer: system.gameObject);
        }

        public static Vector2 ApplySpread(Vector2 direction, float spreadAngle)
        {
            Vector2 normalized = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;

            float halfSpread = Mathf.Max(0f, spreadAngle) * 0.5f;
            if (halfSpread <= 0f)
                return normalized;

            float angle = Random.Range(-halfSpread, halfSpread);
            return Quaternion.Euler(0f, 0f, angle) * normalized;
        }

        public static Vector3 ResolveSpawnPosition(AbilitySystem system, Vector2 direction, Vector3 localOffset)
        {
            if (system == null)
                return localOffset;

            Vector2 normalized = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
            Vector2 perpendicular = new(-normalized.y, normalized.x);
            Vector2 offset = normalized * localOffset.x + perpendicular * localOffset.y;
            return system.transform.position + (Vector3)offset + Vector3.forward * localOffset.z;
        }

        public static Vector3 ResolveMuzzlePosition(AbilitySystem system, Vector2 direction, Vector3 localOffset)
        {
            OddIronRuntimeState runtimeState = ResolveRuntimeState(system);
            return runtimeState != null
                ? runtimeState.ResolveMuzzlePosition(system, direction, localOffset)
                : ResolveSpawnPosition(system, direction, localOffset);
        }

        public static Quaternion ResolveMuzzleRotation(AbilitySystem system, Vector2 direction)
        {
            OddIronRuntimeState runtimeState = ResolveRuntimeState(system);
            if (runtimeState != null)
                return runtimeState.ResolveMuzzleRotation(direction);

            float angle = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;
            return Quaternion.Euler(0f, 0f, angle);
        }

        public static void PlayFireRecoil(AbilitySystem system, Vector2 direction)
        {
            ResolveRuntimeState(system)?.PlayFireRecoil(direction);
        }

        public static void ApplyProjectileScale(GameObject projectileObject, Vector3 projectileScale)
        {
            if (projectileObject == null)
                return;

            if (projectileScale == Vector3.zero)
                return;

            projectileObject.transform.localScale = projectileScale;
        }

        public static void SpawnMuzzleFlash(GameObject prefab, Vector3 position, Vector2 direction)
        {
            if (prefab == null)
                return;

            float angle = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;
            SpawnMuzzleFlash(prefab, position, Quaternion.Euler(0f, 0f, angle));
        }

        public static void SpawnMuzzleFlash(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return;

            Object.Instantiate(prefab, position, rotation);
        }
    }
}
