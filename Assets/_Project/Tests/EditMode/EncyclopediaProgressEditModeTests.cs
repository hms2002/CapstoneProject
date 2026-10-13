using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>실제 프로필 파일을 읽거나 쓰지 않고 구형 JSON, 런 대기분과 프로필 커밋 경계를 검증한다.</summary>
public sealed class EncyclopediaProgressEditModeTests
{
    [Test]
    public void OldProfileWithoutEncyclopediaFieldStartsAtZeroAndPreservesOtherProgress()
    {
        GameData profile = JsonUtility.FromJson<GameData>(
            "{\"magicStone\":17,\"affectionData\":{\"affectionRecords\":[{\"npcId\":3001,\"amount\":3}]}}");
        profile.monsterEncyclopediaData ??= new MonsterEncyclopediaSaveData();
        profile.monsterEncyclopediaData.Normalize();

        Assert.That(profile.monsterEncyclopediaData.GetKillCount("monster.slime_pawn"), Is.Zero);
        Assert.That(profile.magicStone, Is.EqualTo(17));
        Assert.That(profile.affectionData.affectionRecords[0].amount, Is.EqualTo(3));
    }

    [Test]
    public void ProfileRoundTripPreservesSpeciesCountsAndOtherFields()
    {
        var profile = new GameData { magicStone = 42 };
        profile.itemData.unlockedWeaponIDs.Add("weapon.existing");
        profile.monsterEncyclopediaData.AddKills("monster.frog", 12);
        profile.monsterEncyclopediaData.AddKills("monster.frog_elite", 2);

        GameData restored = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(profile));
        Assert.That(restored.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.EqualTo(12));
        Assert.That(restored.monsterEncyclopediaData.GetKillCount("monster.frog_elite"), Is.EqualTo(2));
        Assert.That(restored.magicStone, Is.EqualTo(42));
        Assert.That(restored.itemData.unlockedWeaponIDs, Does.Contain("weapon.existing"));
    }

    [Test]
    public void NormalizationMergesDuplicateIdsAndDiscardsInvalidRecords()
    {
        var data = new MonsterEncyclopediaSaveData
        {
            killRecords = new List<MonsterEncyclopediaKillRecord>
            {
                null,
                new MonsterEncyclopediaKillRecord("monster.frog", 4),
                new MonsterEncyclopediaKillRecord(" monster.frog ", 3),
                new MonsterEncyclopediaKillRecord("monster.frog_elite", 2),
                new MonsterEncyclopediaKillRecord("", 100),
                new MonsterEncyclopediaKillRecord("monster.bad", -1)
            }
        };
        data.Normalize();
        data.Normalize();

        Assert.That(data.killRecords.Count, Is.EqualTo(2));
        Assert.That(data.GetKillCount("monster.frog"), Is.EqualTo(7));
        Assert.That(data.GetKillCount("monster.frog_elite"), Is.EqualTo(2));
        Assert.That(data.GetKillCount("monster.bad"), Is.Zero);
    }

    [Test]
    public void ActiveRunKillsStayPendingUntilCommitAndCommitOnlyOnce()
    {
        var run = new GamePlayData { isRunActive = true };
        var profile = new GameData();
        profile.monsterEncyclopediaData.AddKills("monster.frog", 8);
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog"), Is.True);
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog"), Is.True);
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog_elite"), Is.True);

        Assert.That(profile.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.EqualTo(8));
        Assert.That(MonsterEncyclopediaRunProgress.GetPendingKillCount(run, "monster.frog"), Is.EqualTo(2));
        MonsterEncyclopediaRunProgress.Commit(run, profile);
        MonsterEncyclopediaRunProgress.Commit(run, profile);

        Assert.That(profile.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.EqualTo(10));
        Assert.That(profile.monsterEncyclopediaData.GetKillCount("monster.frog_elite"), Is.EqualTo(1));
        Assert.That(MonsterEncyclopediaRunProgress.GetPendingKillCount(run, "monster.frog"), Is.Zero);
    }

    [Test]
    public void InactiveRunAndMissingIdsDoNotAccumulate()
    {
        var run = new GamePlayData();
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog"), Is.False);
        run.isRunActive = true;
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(run, " "), Is.False);
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(null, "monster.frog"), Is.False);
        Assert.That(run.pendingMonsterEncyclopediaData.killRecords, Is.Empty);
    }

    [Test]
    public void ProfileSwitchOrDevelopmentResetDiscardsPendingKills()
    {
        var run = new GamePlayData { isRunActive = true };
        var previousProfile = new GameData();
        previousProfile.monsterEncyclopediaData.AddKills("monster.frog", 9);
        var nextProfile = new GameData();
        MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog");

        MonsterEncyclopediaRunProgress.Clear(run);
        MonsterEncyclopediaRunProgress.Commit(run, nextProfile);

        Assert.That(nextProfile.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.Zero);
        Assert.That(previousProfile.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.EqualTo(9));
    }

    [Test]
    public void MissingNestedSaveRecordsAndPendingDataAreSafe()
    {
        var profile = new GameData { monsterEncyclopediaData = null };
        var run = new GamePlayData { isRunActive = true, pendingMonsterEncyclopediaData = null };
        Assert.That(MonsterEncyclopediaRunProgress.GetPendingKillCount(run, "monster.frog"), Is.Zero);
        Assert.That(MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog"), Is.True);
        MonsterEncyclopediaRunProgress.Commit(run, profile);
        Assert.That(profile.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.EqualTo(1));

        var nullRecords = new MonsterEncyclopediaSaveData { killRecords = null };
        nullRecords.Normalize();
        Assert.That(nullRecords.GetKillCount("monster.frog"), Is.Zero);
    }

    [Test]
    public void CountOverflowSaturatesInsteadOfBecomingNegative()
    {
        var profile = new GameData();
        profile.monsterEncyclopediaData.AddKills("monster.frog", int.MaxValue);
        var run = new GamePlayData { isRunActive = true };
        MonsterEncyclopediaRunProgress.RecordKill(run, "monster.frog");
        MonsterEncyclopediaRunProgress.Commit(run, profile);
        Assert.That(profile.monsterEncyclopediaData.GetKillCount("monster.frog"), Is.EqualTo(int.MaxValue));
    }
}
