using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// 타격에서 전달된 넉백 impulse를 면역/저항 규칙으로 보정하고 ExternalMovementController2D에 외압 이동으로 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackReceiver2D : MonoBehaviour
    {
        [Header("External Movement")]
        [SerializeField] private ExternalMovementController2D externalMovement;

        [Header("Immunity (Optional)")]
        [SerializeField] private GameplayTag knockbackImmuneTag;

        [Header("Resistance (Optional)")]
        [Tooltip("최종 넉백 = impulse * (1 - Clamp01(resistancePct))")]
        [SerializeField] private AttributeDefinition resistancePctAttribute;

        [Header("Dominance")]
        [SerializeField] private float knockbackDominanceTime = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool debugKnockback;

        private AttributeSet attributeSet;
        private TagSystem tags;
        private const string DefaultKnockbackImmuneTagResourcePath = "Tags/State.Status.KnockbackImmune";

        private void Awake()
        {
            if (externalMovement == null)
                externalMovement = GetComponent<ExternalMovementController2D>();

            attributeSet = GetComponent<AttributeSet>();
            tags = GetComponent<TagSystem>();

            if (knockbackImmuneTag == null)
                knockbackImmuneTag = Resources.Load<GameplayTag>(DefaultKnockbackImmuneTagResourcePath);
        }

        public void ApplyKnockback(GameObject causer, float impulse)
        {
            if (externalMovement == null)
            {
                Log($"Skipped: externalMovement is null. causer={(causer != null ? causer.name : "null")}, impulse={impulse}");
                return;
            }

            if (impulse <= 0f)
            {
                Log($"Skipped: impulse <= 0. causer={(causer != null ? causer.name : "null")}, impulse={impulse}");
                return;
            }

            if (knockbackImmuneTag != null && tags != null && tags.HasTag(knockbackImmuneTag))
            {
                Log($"Skipped: knockback immune tag active. causer={(causer != null ? causer.name : "null")}, impulse={impulse}, tag={knockbackImmuneTag.name}");
                return;
            }

            float resist = 0f;
            if (attributeSet != null && resistancePctAttribute != null)
                resist = Mathf.Clamp01(attributeSet.GetAttributeValue(resistancePctAttribute));

            float finalImpulse = impulse * (1f - resist);
            if (finalImpulse <= 0f)
            {
                Log($"Skipped: finalImpulse <= 0. causer={(causer != null ? causer.name : "null")}, impulse={impulse}, resist={resist}");
                return;
            }

            Vector2 dir = Vector2.zero;
            if (causer != null)
            {
                Vector2 from = causer.transform.position;
                Vector2 to = transform.position;
                dir = to - from;
            }

            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;

            dir.Normalize();

            Vector2 velocity = dir * finalImpulse;
            Log($"Apply. causer={(causer != null ? causer.name : "null")}, impulse={impulse}, resist={resist}, finalImpulse={finalImpulse}, direction={dir}, velocity={velocity}, dominance={knockbackDominanceTime}");
            externalMovement.ApplyKnockback(velocity, knockbackDominanceTime);
        }

        private void Log(string message)
        {
            if (!debugKnockback)
                return;

            Debug.Log($"[KnockbackReceiver2D] {name}: {message}", this);
        }
    }
}
