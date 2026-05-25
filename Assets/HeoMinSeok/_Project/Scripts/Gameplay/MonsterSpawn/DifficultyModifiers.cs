using System;
using UnityEngine;

[Serializable]
public class DifficultyModifiers
{
    [Header("Spawn")]
    [Range(0f, 3f)] public float extraSpawnRatio = 0f;
    [Range(0f, 5f)] public float eliteChanceBonus = 0f;

    [Header("Stats")]
    [Min(0f)] public float hpMultiplier = 1f;
    [Min(0f)] public float attackMultiplier = 1f;

    public DifficultyModifiers()
    {
    }

    /// <summary>
    /// 책임:
    /// - 런타임 스테이지 보정이 인스펙터 원본 DifficultyModifiers를 직접 변경하지 않도록 안전한 복사본을 만든다.
    /// </summary>
    public DifficultyModifiers(DifficultyModifiers source)
    {
        if (source == null)
            return;

        extraSpawnRatio = source.extraSpawnRatio;
        eliteChanceBonus = source.eliteChanceBonus;
        hpMultiplier = source.hpMultiplier;
        attackMultiplier = source.attackMultiplier;
    }

    /// <summary>
    /// 책임:
    /// - 스폰/재적용 경로에서 같은 난이도 값을 독립적으로 수정할 수 있는 복사본을 제공한다.
    /// </summary>
    public DifficultyModifiers Clone()
    {
        return new DifficultyModifiers(this);
    }
}
