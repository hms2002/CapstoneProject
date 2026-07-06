using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Ability가 이동 입력 방향을 우선 사용할지, 기존 조준 방향을 fallback으로 사용할지 공통 규칙으로 결정한다.
    /// - 공격 런지처럼 "전진 방향"만 바꾸고 싶을 때 입력 해석 로직이 각 Ability에 흩어지지 않게 한다.
    /// </summary>
    public static class AbilityMoveDirectionResolver2D
    {
        public static Vector2 ResolveMoveThenAim(GameObject owner, Vector2 fallbackDirection)
        {
            if (owner == null)
                return fallbackDirection;

            var intent = owner.GetComponent<IAbilityMoveInputSource2D>();
            if (intent != null && intent.AbilityMoveInput.sqrMagnitude > 0.0001f)
                return intent.AbilityMoveInput.normalized;

            return AbilityAimResolver2D.Resolve(owner, fallbackDirection);
        }
    }
}
