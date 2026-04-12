using UnityEngine;

namespace UnityGAS
{
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
            if (externalMovement == null) return;
            if (impulse <= 0f) return;

            if (knockbackImmuneTag != null && tags != null && tags.HasTag(knockbackImmuneTag))
                return;

            float resist = 0f;
            if (attributeSet != null && resistancePctAttribute != null)
                resist = Mathf.Clamp01(attributeSet.GetAttributeValue(resistancePctAttribute));

            float finalImpulse = impulse * (1f - resist);
            if (finalImpulse <= 0f) return;

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

            externalMovement.ApplyKnockback(dir * finalImpulse, knockbackDominanceTime);
        }
    }
}
