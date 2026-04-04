using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널의 한 줄이 어떤 아이콘, 라벨, 값 읽기 규칙을 사용할지 정의한다.
/// - 하나의 행이 단일 Attribute, 현재/최대 Attribute 쌍, 최종 StatId 중 무엇을 읽을지 데이터로 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "StatInfoUI_", menuName = "UI/Player Stats/Stat Info UI Definition")]
public sealed class StatInfoUIDefinition : ScriptableObject
{
    [Header("Display")]
    [SerializeField] private Sprite icon;
    [SerializeField] private string label = "HP";

    [Header("Value Source")]
    [SerializeField] private PlayerStatValueMode valueMode = PlayerStatValueMode.AttributeCurrent;
    [SerializeField] private AttributeDefinition valueAttribute;
    [SerializeField] private AttributeDefinition maxAttribute;
    [SerializeField] private StatId statId = StatId.None;

    [Header("Formatting")]
    [SerializeField] private PlayerStatDisplayFormat displayFormat = PlayerStatDisplayFormat.WholeNumber;
    [SerializeField] private float valueMultiplier = 1f;
    [Min(0)]
    [SerializeField] private int decimalPlaces = 0;

    public Sprite Icon => icon;
    public string Label => label;
    public PlayerStatValueMode ValueMode => valueMode;
    public AttributeDefinition ValueAttribute => valueAttribute;
    public AttributeDefinition MaxAttribute => maxAttribute;
    public StatId StatId => statId;
    public PlayerStatDisplayFormat DisplayFormat => displayFormat;
    public float ValueMultiplier => valueMultiplier;
    public int DecimalPlaces => decimalPlaces;
}
