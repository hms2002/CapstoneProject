using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임: 레벨업 선택 카드 하나의 안정적인 ID, 표시 정보, 조합 효과 목록과 중복 선택 정책을 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "LevelReward", menuName = "Gameplay/Progression/Level Reward Definition")]
public sealed class LevelRewardDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string rewardId;
    [SerializeField] private bool allowMultipleSelections;

    [Header("Display")]
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Effects")]
    [SerializeField] private List<LevelRewardEffectSO> effects = new List<LevelRewardEffectSO>();

    public string RewardId => rewardId;
    public bool AllowMultipleSelections => allowMultipleSelections;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<LevelRewardEffectSO> Effects => effects;

    public bool CanSelect(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            failureReason = "보상 ID가 비어 있습니다.";
            return false;
        }

        if (effects == null || effects.Count == 0)
        {
            failureReason = "적용할 효과가 없습니다.";
            return false;
        }

        var effectIds = new HashSet<string>();
        for (int i = 0; i < effects.Count; i++)
        {
            LevelRewardEffectSO effect = effects[i];
            if (effect == null || string.IsNullOrWhiteSpace(effect.EffectId))
            {
                failureReason = "효과 참조 또는 효과 ID가 비어 있습니다.";
                return false;
            }

            if (!effectIds.Add(effect.EffectId))
            {
                failureReason = $"같은 카드에 중복된 효과 ID가 있습니다: {effect.EffectId}";
                return false;
            }

            if (!effect.CanApply(context, out failureReason))
                return false;
        }

        failureReason = null;
        return true;
    }
}
