using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임: 후보 생성과 씬 전환 재적용에서 사용할 레벨업 보상 정의 집합을 명시적으로 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "LevelRewardCatalog", menuName = "Gameplay/Progression/Level Reward Catalog")]
public sealed class LevelRewardCatalogSO : ScriptableObject
{
    [SerializeField] private List<LevelRewardDefinitionSO> rewards = new List<LevelRewardDefinitionSO>();

    public IReadOnlyList<LevelRewardDefinitionSO> Rewards => rewards;
}
