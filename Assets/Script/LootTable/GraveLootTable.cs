using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GraveLootTable", menuName = "Game/Loot/Grave Loot Table")]
public class GraveLootTable : ScriptableObject
{
    [Header("무기 유해 설정 - 드롭 개수")]
    [Tooltip("예: 1개 확률 90, 2개 확률 10")]
    public List<DropCountOption> weaponDropCounts;

    [Header("유물 유해 설정 - 드롭 개수")]
    [Tooltip("예: 1개 확률 70, 2개 확률 30")]
    public List<DropCountOption> relicDropCounts;

    [Header("유물 유해 설정 - 등급 가중치")]
    [Tooltip("노말(Common) 등급 등장 가중치 (예: 90)")]
    public float normalRelicWeight = 90f;

    [Tooltip("레어(Rare) 등급 등장 가중치 (예: 10)")]
    public float rareRelicWeight = 10f;

    [Tooltip("에픽(Epic) 등급 등장 가중치 (기본 0, 향후 업그레이드 대비용)")]
    public float epicRelicWeight = 0f;
}