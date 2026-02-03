using UnityEngine;

[CreateAssetMenu(menuName = "Game/Monster/Monster Definition", fileName = "MD_NewMonster")]
public class MonsterDefinition : ScriptableObject
{
    [Header("Vitals")]
    [Min(1f)] public float maxHealth = 100f;

    [Header("Scaling (Optional)")]
    [Min(0f)] public float eliteMultiplier = 1f;   // 엘리트면 1.5 같은 값
    [Min(0f)] public float difficultyMultiplier = 1f; // 난이도 배수

    public float GetMaxHealth(bool isElite)
    {
        float v = maxHealth * difficultyMultiplier;
        if (isElite) v *= eliteMultiplier;
        return Mathf.Max(1f, v);
    }
}
