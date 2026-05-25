using UnityEngine;

/// <summary>
/// 책임:
/// - 보스전 하나가 언제 클리어되었는지 판정하는 조건 컴포넌트의 공통 계약을 제공한다.
/// - 단일보스, 분열보스, 다중보스처럼 서로 다른 종료 규칙을 동일한 디렉터가 다룰 수 있게 한다.
/// </summary>
public abstract class BossEncounterClearCondition : MonoBehaviour
{
    public abstract bool IsCleared { get; }

    public abstract BossControllerBase RewardBoss { get; }

    public virtual Vector3 RewardOrigin =>
        RewardBoss != null ? RewardBoss.transform.position : transform.position;

    public virtual bool ControlsBoss(BossControllerBase boss)
    {
        return boss != null && RewardBoss != null && ReferenceEquals(boss, RewardBoss);
    }
}
