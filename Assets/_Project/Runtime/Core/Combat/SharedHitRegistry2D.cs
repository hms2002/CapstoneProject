using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 같은 전투 실행 흐름 안에서 여러 hit source가 동일 대상을 중복 타격하지 않도록 공유 적중 목록을 관리한다.
    /// - Core 전투 코드가 특정 VFX 컴포넌트 타입 없이 중복 적중 방지 상태를 전달할 수 있게 한다.
    /// </summary>
    public class SharedHitRegistry2D
    {
        private readonly HashSet<GameObject> hitTargets = new();

        public bool Contains(GameObject target)
        {
            return target != null && hitTargets.Contains(target);
        }

        public void Register(GameObject target)
        {
            if (target != null)
                hitTargets.Add(target);
        }

        public bool TryRegister(GameObject target)
        {
            return target != null && hitTargets.Add(target);
        }
    }
}
