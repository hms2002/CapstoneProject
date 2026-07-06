using UnityEngine;

/// <summary>
/// 책임:
/// - 오래된 몬스터 밸런스 ScriptableObject 직렬화 참조를 보존한다.
/// - 현재 스폰/난이도 시스템으로 마이그레이션되지 않은 레거시 데이터의 최대 체력 계산만 제공한다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Monster/Monster Definition", fileName = "MD_NewMonster")]
public class MonsterDefinition : ScriptableObject
{
    [Header("Vitals")]
    [Min(1f)] public float maxHealth = 100f;

    [Header("Scaling (Optional)")]
    [Min(0f)] public float eliteMultiplier = 1f;
    [Min(0f)] public float difficultyMultiplier = 1f;

    public float GetMaxHealth(bool isElite)
    {
        float value = maxHealth * difficultyMultiplier;
        if (isElite)
            value *= eliteMultiplier;

        return Mathf.Max(1f, value);
    }
}
