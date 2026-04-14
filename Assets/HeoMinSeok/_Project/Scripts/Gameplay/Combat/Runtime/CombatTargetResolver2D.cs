using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Collider2D로부터 실제 피해 적용 대상으로 사용할 GameObject를 정규화한다.
    /// - 공격용 히트박스/투사체 콜라이더처럼 "피해를 받는 대상"이 아닌 오브젝트는 초기에 제외한다.
    /// - AttributeSet이 있는 부모를 우선하고, 없으면 AbilitySystem 부모, 없으면 Rigidbody 루트, 마지막으로 자기 자신을 사용한다.
    /// </summary>
    public static class CombatTargetResolver2D
    {
        public static GameObject ResolveDamageTarget(Collider2D other)
        {
            if (other == null)
                return null;

            // 공격용 히트박스가 플레이어/몬스터 루트로 역참조되어 오탐 피해를 내지 않도록 공통 차단한다.
            if (other.GetComponentInParent<AttackBase>() != null)
                return null;

            var attrs = other.GetComponentInParent<AttributeSet>();
            if (attrs != null)
                return attrs.gameObject;

            var asys = other.GetComponentInParent<AbilitySystem>();
            if (asys != null)
                return asys.gameObject;

            if (other.attachedRigidbody != null)
                return other.attachedRigidbody.gameObject;

            return other.gameObject;
        }
    }
}
