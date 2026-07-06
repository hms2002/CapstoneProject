using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임: Gameplay 코드가 구체 타이밍 기반 피격 이펙트 구현 없이 피해 타이밍 연출을 시작하고 충돌 설정을 전달하게 하는 계약이다.
    /// </summary>
    public interface ITimedHitEffect2D
    {
        void Play(float lifetimeSeconds, CombatHitPayload hitPayload, SharedHitRegistry2D registry = null);
        void Play(
            float lifetimeSeconds,
            CombatHitPayload hitPayload,
            SharedHitRegistry2D registry,
            Action onHitWindowOpened);
        void ConfigureHitCollision(Collider2D[] colliders, LayerMask layers, bool applyOnlyOnce = true);
    }
}
