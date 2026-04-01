using UnityEngine;

/// <summary>
/// 책임 : 바닥에 떨어진 무기 정의와 그 무기 인스턴스의 영속 상태를 함께 보관하고,
/// 플레이어 상호작용을 통해 획득을 시도하게 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WeaponDrop2D : InteractableBase
{
    [SerializeField] private WeaponDefinition weapon;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "획득하기";

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

    public override bool CanInteract(IPlayerInteractor player)
    {
        return weapon != null;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (weapon == null)
            return;

        var inventory = ResolveWeaponInventory(player);
        if (inventory != null && inventory.TryPickupWeapon(weapon, payload))
        {
            Destroy(gameObject);
            return;
        }

        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return weapon != null ? interactPromptText : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private static WeaponInventory2D ResolveWeaponInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return component.GetComponent<WeaponInventory2D>();

        return null;
    }
}
