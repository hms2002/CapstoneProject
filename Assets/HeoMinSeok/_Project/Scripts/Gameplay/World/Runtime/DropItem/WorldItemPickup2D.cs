using UnityEngine;

/// <summary>
/// Generic world pickup representing any inventory ScriptableObject (weapon or relic).
/// - Registers itself into <see cref="WorldItemRegistry"/> so InventoryScreen can list nearby loot.
/// - Does NOT auto-pickup (pickup happens via UI drag from the "loot" list).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup2D : MonoBehaviour
{
    [SerializeField] private ScriptableObject item;

    [Header("Visual (optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    public ScriptableObject Item => item;

    public void SetItem(ScriptableObject so)
    {
        item = so;
        RefreshVisual();
    }

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        RefreshVisual();
    }

    private void OnEnable() => WorldItemRegistry.Register(this);
    private void OnDisable() => WorldItemRegistry.Unregister(this);

    private void RefreshVisual()
    {
        if (spriteRenderer == null) return;
        var def = item != null ? item.AsDef() : null;

        // Uses UI icon as a simple world sprite (good enough for prototyping).
        spriteRenderer.sprite = def != null ? def.Icon : null;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }
}
