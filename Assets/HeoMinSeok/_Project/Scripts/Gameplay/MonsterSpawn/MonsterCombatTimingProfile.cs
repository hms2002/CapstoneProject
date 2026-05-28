using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 몬스터 프리팹이 전역 전투 타이밍 설정을 그대로 따를지, 특정 슬롯만 개별 override할지 표현한다.
    /// </summary>
    public enum MonsterTimingOverrideMode
    {
        UseGlobal = 0,
        ForceEnabled = 1,
        ForceDisabled = 2
    }

    /// <summary>
    /// 책임:
    /// - 몬스터 루트에 붙어 CombatTimingService가 경고/후딜 같은 전투 시간 보정 정책을 몬스터별로 해석하게 한다.
    /// - v1에서는 공격 경고 telegraph 시간이 공격속도 영향을 받을지만 개별 override한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterCombatTimingProfile : MonoBehaviour
    {
        [SerializeField] private MonsterTimingOverrideMode attackWarningTiming = MonsterTimingOverrideMode.UseGlobal;

        public MonsterTimingOverrideMode AttackWarningTiming => attackWarningTiming;

        public bool TryResolveTimingSlotScale(CombatTimingSlot slot, bool globalValue, out bool resolvedValue)
        {
            resolvedValue = globalValue;
            if (slot != CombatTimingSlot.AttackWarning || attackWarningTiming == MonsterTimingOverrideMode.UseGlobal)
                return false;

            resolvedValue = attackWarningTiming == MonsterTimingOverrideMode.ForceEnabled;
            return true;
        }
    }
}
