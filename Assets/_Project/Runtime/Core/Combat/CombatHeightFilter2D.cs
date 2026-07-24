using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 전투 판정 후보가 가짜 2D 높이 규칙상 유효한 대상인지 검사한다.
    /// - Unity Physics2D가 찾은 XY 후보에 대해 Z/height 후처리 필터를 제공한다.
    /// </summary>
    public static class CombatHeightFilter2D
    {
        public enum TargetHeightRule
        {
            Any,
            GroundOnly,
            AirOnly,
            ZOverlap
        }

        public static bool CanAffectGroundTarget(GameObject target)
        {
            return CanAffectTarget(null, target, TargetHeightRule.GroundOnly);
        }

        public static bool CanAffectTarget(GameObject source, GameObject target, TargetHeightRule rule)
        {
            if (target == null)
                return false;

            CombatHeightState2D targetHeight = target.GetComponent<CombatHeightState2D>();

            switch (rule)
            {
                case TargetHeightRule.GroundOnly:
                    return targetHeight == null || targetHeight.IsGrounded;
                case TargetHeightRule.AirOnly:
                    return targetHeight != null && targetHeight.IsAirborne;
                case TargetHeightRule.ZOverlap:
                    return OverlapsZ(source, target);
                case TargetHeightRule.Any:
                default:
                    return true;
            }
        }

        public static bool OverlapsZ(GameObject a, GameObject b)
        {
            if (a == null || b == null)
                return false;

            CombatHeightState2D heightA = a.GetComponent<CombatHeightState2D>();
            CombatHeightState2D heightB = b.GetComponent<CombatHeightState2D>();

            float minA = heightA != null ? heightA.ZMin : 0f;
            float maxA = heightA != null ? heightA.ZMax : 1f;
            float minB = heightB != null ? heightB.ZMin : 0f;
            float maxB = heightB != null ? heightB.ZMax : 1f;

            return minA <= maxB && minB <= maxA;
        }
    }
}
