using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 슬라임 여왕 1페이즈 분열 이후 생성된 2페이즈 개체들이 모두 사망했는지 추적한다.
/// - 슬라임 여왕 보스전의 최종 보상 기준을 개별 보스 사망이 아니라 2페이즈 전원 사망으로 고정한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Encounter Conditions/Slime Queen Clear Condition")]
public sealed class SlimeQueenEncounterClearCondition : BossEncounterClearCondition
{
    [SerializeField] private SlimeQueen phaseOneBoss;

    private readonly HashSet<SlimeQueenPhaseTwoBase> observedPhaseTwoBosses = new();
    private SlimeQueenPhaseTwoBase latestObservedPhaseTwoBoss;
    private Vector3 latestObservedPhaseTwoPosition;
    private bool hasObservedPhaseTwo;

    public override bool IsCleared
    {
        get
        {
            ObserveActivePhaseTwoBosses();
            if (!hasObservedPhaseTwo)
                return false;

            foreach (SlimeQueenPhaseTwoBase boss in observedPhaseTwoBosses)
            {
                if (!IsPhaseTwoBossDefeated(boss))
                    return false;
            }

            return true;
        }
    }

    public override BossControllerBase RewardBoss
    {
        get
        {
            if (latestObservedPhaseTwoBoss != null)
                return latestObservedPhaseTwoBoss;

            foreach (SlimeQueenPhaseTwoBase boss in observedPhaseTwoBosses)
            {
                if (boss != null)
                    return boss;
            }

            return phaseOneBoss;
        }
    }

    public override Vector3 RewardOrigin
    {
        get
        {
            BossControllerBase rewardBoss = RewardBoss;
            if (rewardBoss != null)
                return rewardBoss.transform.position;

            if (phaseOneBoss != null)
                return phaseOneBoss.transform.position;

            if (hasObservedPhaseTwo)
                return latestObservedPhaseTwoPosition;

            return transform.position;
        }
    }

    public override bool ControlsBoss(BossControllerBase boss)
    {
        if (boss == null)
            return false;

        return boss is SlimeQueenPhaseTwoBase ||
               (phaseOneBoss != null && ReferenceEquals(boss, phaseOneBoss));
    }

    private void ObserveActivePhaseTwoBosses()
    {
        SlimeQueenPhaseTwoBase[] bosses = FindObjectsByType<SlimeQueenPhaseTwoBase>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bosses.Length; i++)
        {
            SlimeQueenPhaseTwoBase boss = bosses[i];
            if (boss == null)
                continue;

            hasObservedPhaseTwo = true;
            latestObservedPhaseTwoBoss = boss;
            latestObservedPhaseTwoPosition = boss.transform.position;
            observedPhaseTwoBosses.Add(boss);
        }
    }

    private static bool IsPhaseTwoBossDefeated(SlimeQueenPhaseTwoBase boss)
    {
        return boss == null ||
               !boss.gameObject.activeInHierarchy;
    }
}
