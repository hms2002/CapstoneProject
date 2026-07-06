using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 : Core 전투 피해 확정 지점이 상위 오디오 라우터 없이 피격 사운드 재생 문맥을 전달하는 값 타입이다.
    /// </summary>
    public readonly struct CombatHitAudioRequest
    {
        public readonly AbilitySystem System;
        public readonly AbilitySpec Spec;
        public readonly GameplayEffect DamageEffect;
        public readonly GameObject Target;
        public readonly GameObject Causer;

        public CombatHitAudioRequest(
            AbilitySystem system,
            AbilitySpec spec,
            GameplayEffect damageEffect,
            GameObject target,
            GameObject causer)
        {
            System = system;
            Spec = spec;
            DamageEffect = damageEffect;
            Target = target;
            Causer = causer;
        }
    }

    /// <summary>
    /// 책임 : Core의 피격 사운드 요청을 실제 오디오/피드백 구현으로 넘기는 backend 계약이다.
    /// </summary>
    public interface ICombatHitAudioBackend
    {
        void PlayImpact(in CombatHitAudioRequest request);
    }

    /// <summary>
    /// 책임 : Core 전투 코드가 구체 CombatHitAudioRouter 없이 피격 사운드 재생을 요청하게 한다.
    /// </summary>
    public static class CombatHitAudioPlayback
    {
        private static ICombatHitAudioBackend backend;

        public static void RegisterBackend(ICombatHitAudioBackend combatHitAudioBackend)
        {
            backend = combatHitAudioBackend;
        }

        public static void PlayImpact(
            AbilitySystem system,
            AbilitySpec spec,
            GameplayEffect damageEffect,
            GameObject target,
            GameObject causer)
        {
            backend?.PlayImpact(new CombatHitAudioRequest(
                system,
                spec,
                damageEffect,
                target,
                causer));
        }
    }
}
