using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class BossPhaseConfig
{
    [Header("Phase Info")]
    [Tooltip("디버그용 페이즈 이름입니다.")]
    [SerializeField] private string phaseName = "Phase 1";

    [Tooltip("현재 HP 비율이 이 값 이하가 되면 이 페이즈로 진입합니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float enterHpRatioBelowOrEqual = 1f;

    [Space(8)]
    [Header("Thinking")]
    [Tooltip("패턴 선택 전 최소 대기 시간입니다.")]
    [SerializeField] private float thinkDelayMin = 0.15f;

    [Tooltip("패턴 선택 전 최대 대기 시간입니다.")]
    [SerializeField] private float thinkDelayMax = 0.35f;

    [Space(8)]
    [Header("Patterns")]
    [Tooltip("이 페이즈에서 사용할 패턴 목록입니다.")]
    [SerializeField] private List<BossPatternEntry> patterns = new();

    public string PhaseName => phaseName;
    public float EnterHpRatioBelowOrEqual => enterHpRatioBelowOrEqual;
    public float ThinkDelayMin => thinkDelayMin;
    public float ThinkDelayMax => thinkDelayMax;
    public IReadOnlyList<BossPatternEntry> Patterns => patterns;
}