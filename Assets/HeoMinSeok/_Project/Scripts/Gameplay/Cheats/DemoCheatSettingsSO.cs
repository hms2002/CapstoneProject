using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 빌드에서도 사용할 시연용 치트의 활성화 여부, 단축키, 적용 수치를 보관한다.
/// Resources에서 읽히는 단일 설정 에셋을 통해 릴리즈 전 치트 비활성화와 키 변경을 가능하게 한다.
/// </summary>
[CreateAssetMenu(fileName = "DemoCheatSettings", menuName = "Game/Demo Cheat Settings")]
public sealed class DemoCheatSettingsSO : ScriptableObject
{
    [Header("Enable")]
    [SerializeField] private bool enableDemoCheats = true;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode cheatGuideKey = KeyCode.F1;
    [SerializeField] private KeyCode warpToRunSpecialNpcKey = KeyCode.F7;
    [SerializeField] private KeyCode addMagicStoneKey = KeyCode.F8;
    [SerializeField] private KeyCode maxHealthKey = KeyCode.F9;
    [SerializeField] private KeyCode warpToPortalKey = KeyCode.F10;
    [SerializeField] private KeyCode resetWeaponCooldownKey = KeyCode.F11;
    [SerializeField] private KeyCode increaseAttackKey = KeyCode.F12;

    [Header("Attributes")]
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private AttributeDefinition attackAddAttribute;

    [Header("Attack Cheat")]
    [SerializeField] private float attackIncreaseAmount = 10f;

    [Header("Currency Cheat")]
    [SerializeField, Min(1)] private int magicStoneAddAmount = 100;

    [Header("Notification")]
    [SerializeField, Min(0.1f)] private float notificationDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float cheatGuideDuration = 4f;

    public bool EnableDemoCheats => enableDemoCheats;
    public KeyCode CheatGuideKey => cheatGuideKey;
    public KeyCode WarpToRunSpecialNpcKey => warpToRunSpecialNpcKey;
    public KeyCode AddMagicStoneKey => addMagicStoneKey;
    public KeyCode MaxHealthKey => maxHealthKey;
    public KeyCode WarpToPortalKey => warpToPortalKey;
    public KeyCode ResetWeaponCooldownKey => resetWeaponCooldownKey;
    public KeyCode IncreaseAttackKey => increaseAttackKey;
    public AttributeDefinition HealthAttribute => healthAttribute;
    public AttributeDefinition MaxHealthAttribute => maxHealthAttribute;
    public AttributeDefinition AttackAddAttribute => attackAddAttribute;
    public float AttackIncreaseAmount => attackIncreaseAmount;
    public int MagicStoneAddAmount => magicStoneAddAmount;
    public float NotificationDuration => notificationDuration;
    public float CheatGuideDuration => cheatGuideDuration;
}
