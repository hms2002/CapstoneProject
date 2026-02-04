using UnityEngine;
using System;

// 유물 레어도 정의
public enum ItemRarity
{
    Common,
    Rare,
    Epic
}

// 드롭 개수와 가중치를 묶는 구조체 (예: 2개 나올 확률 60)
[Serializable]
public struct DropCountOption
{
    public int count;  // 드롭할 개수
    public int weight; // 확률 가중치
}

// 보스 전용 드롭 정의 (아이템 + 확률)
[Serializable]
public struct BossSpecificLoot
{
    public ScriptableObject item;   // 보스 전용 무기 or 유물
    [Range(0, 100)] public int dropChance; // 드롭 확률 (0~100%)
}