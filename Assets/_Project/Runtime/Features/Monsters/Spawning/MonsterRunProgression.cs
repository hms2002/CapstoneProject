using UnityEngine;

/// <summary>
/// 책임:
/// - 이번 런에서 처치한 보스 수를 공통 몬스터 교체와 능력치 보정에 사용할 0-based 진행 단계로 변환한다.
/// - 복도 테마와 이동 경로 인덱스가 전투 진행도를 덮어쓰지 않도록 런 세션 데이터를 단일 기준으로 사용한다.
/// </summary>
public static class MonsterRunProgression
{
    public static int CurrentStageIndex
    {
        get
        {
            if (!RunSessionStore.IsRunActive)
                return 0;

            GamePlayData data = RunSessionStore.Data;
            return Mathf.Max(0, data?.defeatedBossIds?.Count ?? 0);
        }
    }
}
