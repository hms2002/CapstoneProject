using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널의 섹션 제목과 섹션에 속한 행 목록을 정의한다.
/// - 행의 순서와 섹션 단위 묶음을 데이터로 관리하게 한다.
/// </summary>
[CreateAssetMenu(fileName = "StatSection_", menuName = "UI/Player Stats/Stat Section Definition")]
public sealed class StatSectionDefinition : ScriptableObject
{
    [SerializeField] private string title = "Section";
    [SerializeField] private StatInfoUIDefinition[] entries;

    public string Title => title;
    public StatInfoUIDefinition[] Entries => entries;
}
