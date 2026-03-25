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
}