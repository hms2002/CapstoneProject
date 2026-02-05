using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Simple 2D knockback applicator.
    /// - Intended to be called from GE_Damage_Spec via SetByCaller knockbackKey.
    /// - Reads optional resistance from AttributeSet (if provided).
    /// - Optional: ignore knockback when a specific tag is present (e.g. SuperArmor / KnockbackImmune).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackReceiver2D : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] private Rigidbody2D body;

        [Header("Immunity (Optional)")]
        [Tooltip("If the target has this tag, knockback is ignored.")]
        [SerializeField] private GameplayTag knockbackImmuneTag;

        [Header("Resistance (Optional)")]
        [Tooltip("If set, finalKnockback *= (1 - Clamp01(resistancePct)).")]
        [SerializeField] private AttributeDefinition resistancePctAttribute;

        private AttributeSet _attributeSet;
        private TagSystem _tags;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            _attributeSet = GetComponent<AttributeSet>();
            _tags = GetComponent<TagSystem>();
        }

        public void ApplyKnockback(GameObject causer, float impulse)
        {
            if (body == null) return;
            if (impulse <= 0f) return;

            if (knockbackImmuneTag != null && _tags != null && _tags.HasTag(knockbackImmuneTag))
                return;

            float resist = 0f;
            if (_attributeSet != null && resistancePctAttribute != null)
                resist = Mathf.Clamp01(_attributeSet.GetAttributeValue(resistancePctAttribute));

            float finalImpulse = impulse * (1f - resist);
            if (finalImpulse <= 0f) return;

            Vector2 dir = Vector2.zero;
            if (causer != null)
            {
                Vector2 a = causer.transform.position;
                Vector2 b = transform.position;
                dir = (b - a);
            }

            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;

            dir.Normalize();
            body.AddForce(dir * finalImpulse, ForceMode2D.Impulse);
        }
    }
}
