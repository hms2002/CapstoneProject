using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Table_Stage1", menuName = "Game/Loot/Stage Loot Table")]
public class StageLootTable : ScriptableObject
{
    [Header("1. [상자] 드롭 개수 확률")]
    public List<DropCountOption> chestWeaponCounts;
    public List<DropCountOption> chestRelicCounts;

    [Header("2. [유물] 등급 가중치")]
    public int commonWeight = 60;
    public int rareWeight = 30;
    public int epicWeight = 10;
    public int legendaryWeight = 0;

    [Header("3. [일반 몬스터] 통합 드롭 가중치")]
    public int mobNothingWeight = 65;
    public int mobWeaponWeight = 2;
    public int mobRelicWeight = 3;
    public int mobConsumableWeight = 15;
    public int mobFieldItemWeight = 15;

    [Header("4. [보스] 마정석 드롭 개수")]
    [Tooltip("1스테이지: 5, 2스테이지: 10, 3스테이지: 15...")]
    public int bossStoneCount = 5;
}