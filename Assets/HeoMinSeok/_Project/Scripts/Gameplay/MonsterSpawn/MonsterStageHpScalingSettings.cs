using UnityEngine;

/// <summary>
/// 책임:
/// - 일반 몬스터의 스테이지 진행도 기반 HP 보정 정책을 중앙 설정 에셋으로 제공한다.
/// - 여러 씬의 MonsterSpawner가 같은 보정 수치를 공유하게 해 밸런스 조정을 한 곳에서 끝내게 한다.
/// </summary>
[CreateAssetMenu(fileName = "MonsterStageHpScalingSettings", menuName = "GAS/Monster Spawn/Stage HP Scaling Settings")]
public sealed class MonsterStageHpScalingSettings : ScriptableObject
{
    [SerializeField] private bool enabled = true;
    [SerializeField, Min(0f)] private float hpMultiplierPerClearedStage = 0.5f;

    public bool Enabled => enabled;
    public float HpMultiplierPerClearedStage => Mathf.Max(0f, hpMultiplierPerClearedStage);

    /// <summary>
    /// 책임:
    /// - 현재 stage index를 최종 HP 배율로 변환한다.
    /// - stage 0은 1배, 이후 스테이지는 설정된 증가량만큼 선형 누적된다.
    /// </summary>
    public float CalculateStageHpMultiplier(int stageIndex)
    {
        if (!enabled)
            return 1f;

        return 1f + HpMultiplierPerClearedStage * Mathf.Max(0, stageIndex);
    }
}
