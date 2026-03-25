using UnityEngine;

/// <summary>
/// 책임 : 바닥에 떨어진 무기 정의와 그 무기 인스턴스의 영속 상태를 함께 보관한다.
/// 플레이어가 주우면 WeaponInventory2D로 전달하고 자신은 제거된다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WeaponDrop2D : MonoBehaviour
{
    [SerializeField] private WeaponDefinition weapon;

    [Header("Runtime Payload")]
    [SerializeField] private WeaponPersistentStatePayload payload;

    public WeaponDefinition Weapon => weapon;
    public WeaponPersistentStatePayload Payload => payload;

    public void SetWeapon(WeaponDefinition def, WeaponPersistentStatePayload runtimePayload = null)
    {
        weapon = def;
        payload = runtimePayload;
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

        if (inv.TryPickupWeapon(weapon, payload))
            Destroy(gameObject);
    }
}