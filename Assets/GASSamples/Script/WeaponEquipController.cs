using UnityEngine;
using UnityGAS;
using static UnityGAS.AbilityDefinition;

public class WeaponEquipController : MonoBehaviour
{
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private Transform weaponSocket;

    private GameObject currentWeaponGO;

    #region 테스트 코드
    [Header("임시 변수지롱")]
    [SerializeField]GameObject Weapon;
    private void Start()
    {
        Equip(Weapon);
    }
    #endregion
    public void Equip(GameObject weaponPrefab)
    {
        // 1) 실행 중 무기 액션 정리(권장)
        abilitySystem.OnWeaponEquipped();

        // 2) 기존 무기 제거
        if (currentWeaponGO != null) Destroy(currentWeaponGO);

        // 3) 새 무기 생성
        currentWeaponGO = Instantiate(weaponPrefab, weaponSocket);

        // 4) 무기 Animator 등록
        var weaponAnim = currentWeaponGO.GetComponentInChildren<Animator>();
        abilitySystem.RegisterWeaponAnimator(weaponAnim);

        // 5) 무기 Relay 바인딩(핵심)
        var relays = currentWeaponGO.GetComponentsInChildren<AbilityAnimationEventRelay>();
        foreach (var r in relays) r.Bind(abilitySystem);
    }
}
