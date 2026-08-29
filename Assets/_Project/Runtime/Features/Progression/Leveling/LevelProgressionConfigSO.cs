using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임: 현재 레벨에서 다음 레벨로 올라갈 때 필요한 경험치 표를 데이터로 제공한다.
/// 배열의 첫 항목은 Lv.1에서 Lv.2로 올라갈 때 필요한 경험치다.
/// </summary>
[CreateAssetMenu(fileName = "LevelProgressionConfig", menuName = "Gameplay/Progression/Level Progression Config")]
public sealed class LevelProgressionConfigSO : ScriptableObject
{
    [SerializeField] private int[] nextLevelRequirements =
    {
        100,
        130,
        160,
        200,
        250,
        310,
        380,
        460,
        550,
    };

    public IReadOnlyList<int> NextLevelRequirements => nextLevelRequirements;
    public int MaxLevel => (nextLevelRequirements?.Length ?? 0) + 1;

    public int GetRequiredExperience(int currentLevel)
    {
        int index = currentLevel - 1;
        if (nextLevelRequirements == null || index < 0 || index >= nextLevelRequirements.Length)
            return 0;

        return Mathf.Max(1, nextLevelRequirements[index]);
    }

    private void OnValidate()
    {
        if (nextLevelRequirements == null)
            nextLevelRequirements = new int[0];

        for (int i = 0; i < nextLevelRequirements.Length; i++)
            nextLevelRequirements[i] = Mathf.Max(1, nextLevelRequirements[i]);
    }
}
