using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WeaponDrop2D : MonoBehaviour
{
    [SerializeField] private WeaponDefinition weapon;

    public void SetWeapon(WeaponDefinition def)
    {
        weapon = def;
        // 필요하면 여기서 아이콘/스프라이트 갱신
    }

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var inv = other.GetComponentInParent<WeaponInventory2D>();
        if (inv == null) return;

        if (inv.TryPickupWeapon(weapon))
            Destroy(gameObject);
    }
}
