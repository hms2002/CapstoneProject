using UnityEngine;

/// <summary>
/// 책임:
/// - 보스 개체 하나가 사망했을 때 보스전이 클리어되는 가장 기본적인 종료 조건을 표현한다.
/// - 기존 단일보스 씬을 새 BossEncounterEndDirector 구조로 이주할 때 사용하는 호환 조건이다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Encounter Conditions/Single Boss Clear Condition")]
public sealed class SingleBossEncounterClearCondition : BossEncounterClearCondition
{
    [SerializeField] private BossControllerBase boss;

    private bool hasObservedBoss;

    public override bool IsCleared
    {
        get
        {
            if (boss != null)
                hasObservedBoss = true;

            return hasObservedBoss && IsBossDefeated(boss);
        }
    }

    public override BossControllerBase RewardBoss => boss;

    public void Bind(BossControllerBase targetBoss)
    {
        boss = targetBoss;
        hasObservedBoss = boss != null;
    }

    public override bool ControlsBoss(BossControllerBase targetBoss)
    {
        return targetBoss != null && boss != null && ReferenceEquals(targetBoss, boss);
    }

    private static bool IsBossDefeated(BossControllerBase targetBoss)
    {
        return targetBoss == null ||
               targetBoss.IsDead ||
               targetBoss.HasDeadTag() ||
               targetBoss.CurrentHealthValue <= 0f;
    }
}
