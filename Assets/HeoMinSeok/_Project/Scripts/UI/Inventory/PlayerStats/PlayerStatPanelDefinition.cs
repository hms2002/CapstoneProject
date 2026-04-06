using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널 전체가 어떤 섹션 순서로 구성되는지 정의한다.
/// - 인벤토리 UI와 상자 UI가 같은 패널 정의를 재사용할 수 있게 한다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerStatPanel_", menuName = "UI/Player Stats/Player Stat Panel Definition")]
public sealed class PlayerStatPanelDefinition : ScriptableObject
{
    [SerializeField] private StatSectionDefinition[] sections;

    public StatSectionDefinition[] Sections => sections;
}
