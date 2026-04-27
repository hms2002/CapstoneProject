using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// SetByCaller 기반 넉백 GameplayEffect.
    /// - GameplayEffectRunner.ApplyEffectSpec() 경로로 적용되는 것을 전제로 한다.
    /// - spec.SetSetByCallerMagnitude(knockbackKey, value) 로 넉백 세기를 전달할 수 있다.
    ///
    /// 책임:
    /// - 최종 넉백 수치를 해석하고
    /// - 타겟의 KnockbackReceiver2D에게 넉백 적용을 요청한다.
    ///
    /// 주의:
    /// - 실제 물리 적용은 KnockbackReceiver2D -> ExternalMovementController2D -> MovementMotor2D 순으로 처리된다.
    /// - 이 Effect는 Rigidbody2D를 직접 만지지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "GE_Knockback_Spec", menuName = "GAS/Effects/Knockback (Spec)")]
    public sealed class GE_Knockback_Spec : GameplayEffect, ISpecGameplayEffect
    {
        [Header("Knockback")]
        [Tooltip("SetByCaller 키 (예: Data.Knockback)")]
        public GameplayTag knockbackKey;

        [Tooltip("SetByCaller 키가 없을 때 사용할 기본 넉백 세기")]
        public float fallbackKnockback = 0f;

        [Header("Debug")]
        [SerializeField] private bool debugKnockback;

        private void OnValidate()
        {
            duration = 0f;
            if (fallbackKnockback < 0f)
                fallbackKnockback = 0f;
        }

        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            if (target == null)
            {
                Log("Spec apply skipped: target is null.", null);
                return;
            }


            // 1) 넉백 수치 해석
            float knockback = fallbackKnockback;
            bool usedSetByCaller = false;
            if (spec != null && knockbackKey != null &&
                spec.TryGetSetByCallerMagnitude(knockbackKey, out var value))
            {
                knockback = value;
                usedSetByCaller = true;
            }

            if (knockback <= 0f)
            {
                Log($"Spec apply skipped: knockback <= 0. target={target.name}, knockback={knockback}, usedSetByCaller={usedSetByCaller}", target);
                return;
            }

            // 2) 가해자 결정
            GameObject causer = null;
            if (spec != null && spec.Context != null)
            {
                causer = spec.Context.Causer;
                if (causer == null)
                    causer = spec.Context.Instigator;
            }

            // 3) 넉백 적용 요청
            var receiver = target.GetComponent<KnockbackReceiver2D>();
            if (receiver == null)
            {
                Log($"Spec apply skipped: receiver missing. target={target.name}, causer={(causer != null ? causer.name : "null")}, knockback={knockback}", target);
                return;
            }

            Log($"Spec apply. target={target.name}, causer={(causer != null ? causer.name : "null")}, knockback={knockback}, usedSetByCaller={usedSetByCaller}", target);
            receiver.ApplyKnockback(causer, knockback);
        }

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            // legacy/non-spec 경로
            if (target == null)
            {
                Log("Legacy apply skipped: target is null.", null);
                return;
            }


            if (fallbackKnockback <= 0f)
            {
                Log($"Legacy apply skipped: fallbackKnockback <= 0. target={target.name}, fallback={fallbackKnockback}", target);
                return;
            }

            var receiver = target.GetComponent<KnockbackReceiver2D>();
            if (receiver == null)
            {
                Log($"Legacy apply skipped: receiver missing. target={target.name}, instigator={(instigator != null ? instigator.name : "null")}, fallback={fallbackKnockback}", target);
                return;
            }

            Log($"Legacy apply. target={target.name}, instigator={(instigator != null ? instigator.name : "null")}, fallback={fallbackKnockback}", target);
            receiver.ApplyKnockback(instigator, fallbackKnockback);
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            // instant effect이므로 제거 동작 없음
        }

        private void Log(string message, Object context)
        {
            if (!debugKnockback)
                return;

            Debug.Log($"[GE_Knockback_Spec] {message}", context);
        }
    }
}
