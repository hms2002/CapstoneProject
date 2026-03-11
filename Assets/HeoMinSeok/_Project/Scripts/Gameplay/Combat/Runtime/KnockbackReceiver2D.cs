using UnityEngine;

namespace UnityGAS
{
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

        [Header("Velocity Injection (Fix small knockback being overwritten)")]
        [SerializeField] private bool useVelocityInjection = true;

        [Tooltip("How fast injected knockback velocity decays to zero (bigger = stops sooner).")]
        [SerializeField] private float damping = 18f;

        [Tooltip("Clamp injected knockback speed to avoid runaway.")]
        [SerializeField] private float maxInjectedSpeed = 20f;

        private AttributeSet _attributeSet;
        private TagSystem _tags;

        // accumulated external knockback velocity (what we WANT to add on top of AI velocity)
        private Vector2 _injectedVel;
        // what we actually applied last frame (so we can subtract and re-apply safely)
        private Vector2 _appliedLastFrame;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            _attributeSet = GetComponent<AttributeSet>();
            _tags = GetComponent<TagSystem>();
        }

        private void FixedUpdate()
        {
            if (!useVelocityInjection) return;

            // decay toward zero
            _injectedVel = Vector2.Lerp(_injectedVel, Vector2.zero, damping * Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (!useVelocityInjection) return;
            if (body == null) return;
            Debug.Log($"{gameObject.name} : {body.linearVelocity}");
            if (_injectedVel.magnitude <= 0.001f) return;

            // Remove last frame’s injected velocity, then add current injected velocity.
            // This prevents double-adding / runaway accumulation.
            var v = /*body.linearVelocity*/Vector2.zero;
            v = v - _appliedLastFrame + _injectedVel;
            body.linearVelocity = v;

            _appliedLastFrame = _injectedVel;
            Debug.Log($"{gameObject.name} : {body.linearVelocity}");
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

            if (useVelocityInjection)
            {
                _injectedVel += dir * finalImpulse;

                // clamp
                float mag = _injectedVel.magnitude;
                if (mag > maxInjectedSpeed)
                    _injectedVel = _injectedVel / mag * maxInjectedSpeed;

                return;
            }

            // fallback (old behavior)
            body.AddForce(dir * finalImpulse, ForceMode2D.Impulse);
        }
    }
}