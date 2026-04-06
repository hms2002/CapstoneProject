using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldHealPickup2D : MonoBehaviour
{
    [Header("Heal")]
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField, Min(1)] private int healAmount = 1;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite icon;

    private bool collected;

    public void Configure(AttributeDefinition healthAttributeOverride, int healAmountOverride, Sprite iconOverride)
    {
        healthAttribute = healthAttributeOverride;
        healAmount = Mathf.Max(1, healAmountOverride);
        icon = iconOverride;
        RefreshVisual();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;

        RefreshVisual();
    }

    private void OnValidate()
    {
        if (healAmount < 1)
            healAmount = 1;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        RefreshVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (collected || other == null)
            return;

        if (!TryResolvePlayerAttributeSet(other, out AttributeSet attributeSet))
            return;

        collected = true;

        if (healthAttribute != null)
            attributeSet.TryModifyAttributeValue(healthAttribute, healAmount, this);

        Destroy(gameObject);
    }

    private bool TryResolvePlayerAttributeSet(Collider2D other, out AttributeSet attributeSet)
    {
        attributeSet = null;

        PlayerInteractor2D playerInteractor = null;
        if (other.attachedRigidbody != null)
            playerInteractor = other.attachedRigidbody.GetComponent<PlayerInteractor2D>();

        if (playerInteractor == null)
            playerInteractor = other.GetComponentInParent<PlayerInteractor2D>();

        if (playerInteractor == null)
            return false;

        attributeSet = playerInteractor.GetComponent<AttributeSet>();
        return attributeSet != null;
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = icon;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }
}
