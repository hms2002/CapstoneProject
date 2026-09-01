using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 책임:
/// - 공통 몬스터의 종류와 능력치 단계가 복도 route index가 아니라 이번 런의 보스 처치 수를 따르는지 회귀 검증한다.
/// </summary>
public sealed class MonsterRunProgressionPlayModeTests
{
    [Test]
    public void CurrentStageIndex_UsesDefeatedBossCountDuringActiveRun()
    {
        GamePlayDataManager manager = GamePlayDataManager.EnsureInstance();
        Assert.That(manager, Is.Not.Null);

        GamePlayData data = manager.Data;
        bool originalRunActive = data.isRunActive;
        List<string> originalDefeatedBossIds = data.defeatedBossIds;

        try
        {
            data.isRunActive = true;
            data.defeatedBossIds = new List<string>();
            Assert.That(MonsterRunProgression.CurrentStageIndex, Is.Zero);

            data.defeatedBossIds.Add("shadow");
            Assert.That(MonsterRunProgression.CurrentStageIndex, Is.EqualTo(1));

            data.defeatedBossIds.Add("dragon");
            Assert.That(MonsterRunProgression.CurrentStageIndex, Is.EqualTo(2));
        }
        finally
        {
            data.isRunActive = originalRunActive;
            data.defeatedBossIds = originalDefeatedBossIds;
        }
    }

    [Test]
    public void CurrentStageIndex_UsesFirstStageOutsideRun()
    {
        GamePlayDataManager manager = GamePlayDataManager.EnsureInstance();
        Assert.That(manager, Is.Not.Null);

        GamePlayData data = manager.Data;
        bool originalRunActive = data.isRunActive;
        List<string> originalDefeatedBossIds = data.defeatedBossIds;

        try
        {
            data.isRunActive = false;
            data.defeatedBossIds = new List<string> { "shadow", "dragon" };

            Assert.That(MonsterRunProgression.CurrentStageIndex, Is.Zero);
        }
        finally
        {
            data.isRunActive = originalRunActive;
            data.defeatedBossIds = originalDefeatedBossIds;
        }
    }
}
