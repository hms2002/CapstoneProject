using UnityEngine;
using System.Collections.Generic;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Collider2D로부터 실제 피해 적용 대상으로 사용할 GameObject를 정규화한다.
    /// - 공격용 히트박스/투사체 콜라이더처럼 "피해를 받는 대상"이 아닌 오브젝트는 초기에 제외한다.
    /// - 충돌한 콜라이더 자신에게 CombatHurtbox2D가 붙어 있을 때만 부모를 타고 실제 피해 대상을 찾는다.
    /// - 허트박스가 아닌 자식 이펙트/장식 콜라이더는 절대로 부모를 타고 본체로 역참조하지 않는다.
    /// </summary>
    public static class CombatTargetResolver2D
    {
        private static readonly HashSet<int> warnedMissingHurtboxColliders = new();

        public static GameObject ResolveDamageTarget(Collider2D other)
        {
            if (other == null)
                return null;

            // 공격용 히트박스가 플레이어/몬스터 루트로 역참조되어 오탐 피해를 내지 않도록 공통 차단한다.
            if (other.GetComponentInParent<AttackBase>() != null)
                return null;

            CombatHurtbox2D hurtbox = other.GetComponent<CombatHurtbox2D>();
            if (hurtbox != null)
            {
                if (hurtbox.OwnsCollider(other))
                    return hurtbox.ResolveTargetRoot();

                return null;
            }

            WarnMissingHurtbox(other);
            return null;
        }

        /// <summary>
        /// 책임 :
        /// - 명시적 허트박스 구조에서 세팅 누락을 개발 중에 빨리 발견할 수 있게 경고를 남긴다.
        /// - 동일 콜라이더에 대해서는 한 번만 경고해 로그 노이즈를 줄인다.
        /// </summary>
        private static void WarnMissingHurtbox(Collider2D other)
        {
            if (other == null)
                return;

            int instanceId = other.GetInstanceID();
            if (!warnedMissingHurtboxColliders.Add(instanceId))
                return;

            CombatHurtbox2D ancestorHurtbox = other.GetComponentInParent<CombatHurtbox2D>();
            string hierarchyPath = GetHierarchyPath(other.transform);
            string ownerHint = ancestorHurtbox != null
                ? $"상위 허트박스는 있지만 이 콜라이더 자신엔 없습니다. ancestor={ancestorHurtbox.name}"
                : "상위에도 CombatHurtbox2D가 없습니다.";

            Debug.LogWarning(
                $"[{nameof(CombatTargetResolver2D)}] 충돌한 콜라이더에 CombatHurtbox2D가 없습니다. " +
                $"name={other.name}, path={hierarchyPath}, layer={LayerMask.LayerToName(other.gameObject.layer)}({other.gameObject.layer}), " +
                $"isTrigger={other.isTrigger}, attachedRigidbody={(other.attachedRigidbody != null ? other.attachedRigidbody.name : "null")}, " +
                ownerHint,
                other);
        }

        /// <summary>
        /// 책임 :
        /// - 경고 로그에서 문제 콜라이더를 빠르게 찾을 수 있도록 Transform 전체 경로를 문자열로 만든다.
        /// </summary>
        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
                return "(null)";

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
