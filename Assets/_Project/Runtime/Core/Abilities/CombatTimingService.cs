using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 전투 의도를 가진 대기 시간을 caster의 공격속도에 맞춰 런타임 보정한다.
    /// - 경고/후딜/연사 간격 같은 gameplay 시간과 VFX/대사 같은 presentation 시간을 분리한다.
    /// </summary>
    public static class CombatTimingService
    {
        private const string SettingsResourcePath = "MonsterStageHpScalingSettings";
        private const float FallbackMinimumScaledSeconds = 0.08f;

        private static MonsterStageHpScalingSettings cachedSettings;

        public static float ScaleSeconds(AbilitySystem system, float baseSeconds, CombatTimingSlot slot)
        {
            if (baseSeconds <= 0f)
                return 0f;

            MonsterStageHpScalingSettings settings = ResolveSettings();
            if (!ShouldScale(settings, system, slot))
            {
                LogTiming(settings, system, slot, baseSeconds, 1f, baseSeconds, "skipped by settings");
                return baseSeconds;
            }

            float attackSpeed = AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system);
            if (attackSpeed <= 1.0001f)
            {
                LogTiming(settings, system, slot, baseSeconds, attackSpeed, baseSeconds, "unchanged");
                return baseSeconds;
            }

            float minimum = settings != null ? settings.MinimumScaledSeconds : FallbackMinimumScaledSeconds;
            float scaledSeconds = Mathf.Max(minimum, baseSeconds / attackSpeed);
            LogTiming(settings, system, slot, baseSeconds, attackSpeed, scaledSeconds, "scaled");
            return scaledSeconds;
        }

        public static float ScaleSeconds(Component context, float baseSeconds, CombatTimingSlot slot)
        {
            AbilitySystem system = context != null ? context.GetComponentInParent<AbilitySystem>() : null;
            return ScaleSeconds(system, baseSeconds, slot);
        }

        private static bool ShouldScale(MonsterStageHpScalingSettings settings, AbilitySystem system, CombatTimingSlot slot)
        {
            if (slot == CombatTimingSlot.PresentationOnly)
                return false;

            if (settings != null && !settings.Enabled)
                return false;

            bool globalValue = settings != null
                ? settings.ShouldScaleTimingSlot(slot)
                : slot is CombatTimingSlot.AttackRecovery or
                CombatTimingSlot.AttackInterval or
                CombatTimingSlot.AbilityRecovery or
                CombatTimingSlot.AbilityCooldown;

            ICombatTimingProfile profile = ResolveTimingProfile(system);

            if (profile != null && profile.TryResolveTimingSlotScale(slot, globalValue, out bool resolvedValue))
                return resolvedValue;

            return globalValue;
        }

        private static ICombatTimingProfile ResolveTimingProfile(AbilitySystem system)
        {
            if (system == null)
                return null;

            MonoBehaviour[] behaviours = system.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatTimingProfile profile)
                    return profile;
            }

            return null;
        }

        private static MonsterStageHpScalingSettings ResolveSettings()
        {
            if (cachedSettings != null)
                return cachedSettings;

            cachedSettings = Resources.Load<MonsterStageHpScalingSettings>(SettingsResourcePath);
            return cachedSettings;
        }

        /// <summary>
        /// 책임:
        /// - CombatTimingService 테스트 중 슬롯별 보정 결과를 토글 기반으로 짧게 출력한다.
        /// - 기본 플레이에서는 로그를 완전히 끄고, 설정 SO가 명시적으로 요청한 경우에만 콘솔을 사용한다.
        /// </summary>
        private static void LogTiming(
            MonsterStageHpScalingSettings settings,
            AbilitySystem system,
            CombatTimingSlot slot,
            float baseSeconds,
            float attackSpeed,
            float scaledSeconds,
            string reason)
        {
            if (settings == null || !settings.LogCombatTimingDebug)
                return;

            string casterName = system != null ? system.name : "null";
            Debug.Log(
                $"[CombatTimingService] caster={casterName}, slot={slot}, base={baseSeconds:0.###}, speed={attackSpeed:0.###}, scaled={scaledSeconds:0.###}, reason={reason}",
                system);
        }
    }
}
