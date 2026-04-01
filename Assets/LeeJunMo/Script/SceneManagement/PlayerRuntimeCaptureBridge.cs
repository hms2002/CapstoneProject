using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 플레이어에 붙어 있는 여러 런타임 시스템 참조를 모아
/// PlayerRuntimeCaptureCoordinator로 전달하고,
/// 씬 이동 직전 캡처 진입점을 단일화하는 브리지 역할을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRuntimeCaptureBridge : MonoBehaviour
{
    [Header("Core Runtime Refs")]
    [SerializeField] private PlayerConsumableInventory consumableInventory;
    [SerializeField] private WeaponInventory2D weaponInventory;
    [SerializeField] private RelicInventory relicInventory;
    [SerializeField] private AttributeSet attributeSet;
    [SerializeField] private GameplayEffectRunner effectRunner;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private AbilitySystem abilitySystem;

    [Header("Optional Runtime Capturers")]
    [SerializeField] private MonoBehaviour weaponRuntimeCapturerSource;
    [SerializeField] private MonoBehaviour relicRuntimeCapturerSource;

    private IWeaponRuntimeStateCapturer weaponRuntimeCapturer;
    private IRelicRuntimeStateCapturer relicRuntimeCapturer;

    private void Awake()
    {
        CacheComponents();
        CacheOptionalCapturers();
        ValidateOptionalCapturers();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComponents();
        CacheOptionalCapturers();
    }
#endif

    /// <summary>
    /// 책임 : 현재 플레이어에 붙어 있는 핵심 런타임 컴포넌트를 자동 캐싱한다.
    /// 인스펙터에서 수동 연결해도 되고, 비어 있으면 같은 오브젝트에서 찾아 채운다.
    /// </summary>
    private void CacheComponents()
    {
        if (consumableInventory == null) consumableInventory = GetComponent<PlayerConsumableInventory>();
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
        if (relicInventory == null) relicInventory = GetComponent<RelicInventory>();
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
        if (effectRunner == null) effectRunner = GetComponent<GameplayEffectRunner>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
    }

    /// <summary>
    /// 책임 : 선택적으로 연결된 장비 개별 런타임 캡처 구현체를 인터페이스로 캐싱한다.
    /// 인스펙터 연결이 비어 있으면 같은 오브젝트의 기본 브리지를 자동 탐색한다.
    /// </summary>
    private void CacheOptionalCapturers()
    {
        if (weaponRuntimeCapturerSource == null)
            weaponRuntimeCapturerSource = GetComponent<WeaponAbilityRuntimeStateBridge>();

        if (relicRuntimeCapturerSource == null)
            relicRuntimeCapturerSource = GetComponent<RelicRuntimeStateBridge>();

        weaponRuntimeCapturer = weaponRuntimeCapturerSource as IWeaponRuntimeStateCapturer;
        relicRuntimeCapturer = relicRuntimeCapturerSource as IRelicRuntimeStateCapturer;
    }

    /// <summary>
    /// 책임 : 선택 소스가 잘못 연결된 경우 조기에 오류를 알려준다.
    /// </summary>
    private void ValidateOptionalCapturers()
    {
        if (weaponRuntimeCapturerSource != null && weaponRuntimeCapturer == null)
        {
            Debug.LogError("[PlayerRuntimeCaptureBridge] weaponRuntimeCapturerSource가 IWeaponRuntimeStateCapturer를 구현하지 않았습니다.", this);
        }

        if (relicRuntimeCapturerSource != null && relicRuntimeCapturer == null)
        {
            Debug.LogError("[PlayerRuntimeCaptureBridge] relicRuntimeCapturerSource가 IRelicRuntimeStateCapturer를 구현하지 않았습니다.", this);
        }
    }

    /// <summary>
    /// 책임 : 현재 플레이어의 전체 런타임 상태를 최신 캡처 파이프라인으로 수집한다.
    /// </summary>
    public PlayerRuntimeState CaptureRuntimeState()
    {
        CacheComponents();
        WarnIfMissingCoreComponents();

        return PlayerRuntimeCaptureCoordinator.CaptureAll(
            consumableInventory,
            weaponInventory,
            relicInventory,
            attributeSet,
            effectRunner,
            tagSystem,
            abilitySystem,
            weaponRuntimeCapturer,
            relicRuntimeCapturer);
    }

    /// <summary>
    /// 책임 : 핵심 시스템이 빠져 있으면 어떤 종류의 상태가 저장되지 않을지 로그로 알려준다.
    /// null 허용 설계이므로 캡처 자체는 계속 진행한다.
    /// </summary>
    private void WarnIfMissingCoreComponents()
    {
        if (consumableInventory == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] PlayerConsumableInventory가 없어 1회용 아이템 상태를 저장하지 못합니다.", this);

        if (weaponInventory == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] WeaponInventory2D가 없어 무기 배치/런타임 상태를 저장하지 못합니다.", this);

        if (relicInventory == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] RelicInventory가 없어 유물 배치/런타임 상태를 저장하지 못합니다.", this);

        if (attributeSet == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] AttributeSet이 없어 Attribute 상태를 저장하지 못합니다.", this);

        if (effectRunner == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] GameplayEffectRunner가 없어 활성 Effect 상태를 저장하지 못합니다.", this);

        if (tagSystem == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] TagSystem이 없어 explicit tag 상태를 저장하지 못합니다.", this);

        if (abilitySystem == null)
            Debug.LogWarning("[PlayerRuntimeCaptureBridge] AbilitySystem이 없어 ability runtime 상태를 저장하지 못합니다.", this);
    }
}
