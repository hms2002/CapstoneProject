using UnityEngine;
using UnityGAS;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 이 콜라이더가 실제 피해 판정용 허트박스임을 명시한다.
    /// - 피해를 적용할 최종 대상 루트(GameObject)를 안정적으로 해석해 CombatTargetResolver2D에 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHurtbox2D : MonoBehaviour
    {
        [Tooltip("비워두면 상위 AttributeSet, AbilitySystem, Rigidbody 순으로 자동 탐색합니다.")]
        [SerializeField] private GameObject targetRoot;

        [Tooltip("비워두면 같은 GameObject의 Collider2D만 허트박스로 취급합니다.")]
        [SerializeField] private Collider2D[] ownedColliders;

        /// <summary>이 허트박스가 전달된 콜라이더를 실제 허트박스로 소유하는지 확인합니다.</summary>
        public bool OwnsCollider(Collider2D collider)
        {
            if (collider == null)
                return false;

            if (ownedColliders != null && ownedColliders.Length > 0)
            {
                for (int i = 0; i < ownedColliders.Length; i++)
                {
                    if (ownedColliders[i] == collider)
                        return true;
                }

                return false;
            }

            return collider.gameObject == gameObject;
        }

        /// <summary>허트박스가 대표하는 실제 피해 대상 루트를 반환합니다.</summary>
        public GameObject ResolveTargetRoot()
        {
            if (targetRoot != null)
                return targetRoot;

            AttributeSet attrs = GetComponentInParent<AttributeSet>();
            if (attrs != null)
                return attrs.gameObject;

            AbilitySystem abilitySystem = GetComponentInParent<AbilitySystem>();
            if (abilitySystem != null)
                return abilitySystem.gameObject;

            Rigidbody2D rb = GetComponentInParent<Rigidbody2D>();
            if (rb != null)
                return rb.gameObject;

            return transform.root != null ? transform.root.gameObject : gameObject;
        }
    }
}
