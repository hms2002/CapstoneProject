using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EncyclopediaDiscoveryEditModeTests
{
    [Test]
    public void OldUnlockListsDoNotBecomeAcquisitionHistory()
    {
        var profile=JsonUtility.FromJson<GameData>("{\"magicStone\":42,\"itemData\":{\"unlockedWeaponIDs\":[\"Weapon.A\"],\"unlockedRelicIDs\":[\"Relic.A\"]}}");
        profile.encyclopediaDiscoveryData ??= new();
        profile.encyclopediaDiscoveryData.Normalize();
        Assert.That(profile.encyclopediaDiscoveryData.Contains(EncyclopediaDiscoveryKind.Weapon,"Weapon.A"),Is.False);
        Assert.That(profile.encyclopediaDiscoveryData.Contains(EncyclopediaDiscoveryKind.Relic,"Relic.A"),Is.False);
        Assert.That(profile.magicStone,Is.EqualTo(42));
        Assert.That(profile.itemData.unlockedWeaponIDs,Does.Contain("Weapon.A"));
    }

    [Test]
    public void NormalizationKeepsUnknownValidIdsAndIsIdempotent()
    {
        var data=new EncyclopediaDiscoverySaveData
        {version=0,weaponIds=new List<string>{null," "," Weapon.A ","Weapon.A","future.weapon"},relicIds=null,monsterIds=null};
        data.Normalize();data.Normalize();
        Assert.That(data.weaponIds,Is.EqualTo(new[]{"Weapon.A","future.weapon"}));
        Assert.That(data.relicIds,Is.Empty);Assert.That(data.monsterIds,Is.Empty);Assert.That(data.version,Is.EqualTo(1));
    }

    [TestCase(EncyclopediaDiscoveryKind.Weapon)]
    [TestCase(EncyclopediaDiscoveryKind.Relic)]
    [TestCase(EncyclopediaDiscoveryKind.Monster)]
    public void PendingDiscoveryCommitsOnceAndSurvivesJsonRoundTrip(EncyclopediaDiscoveryKind kind)
    {
        var profile=new GameData{magicStone=31};var run=new GamePlayData{isRunActive=true};
        Assert.That(run.pendingEncyclopediaDiscoveryData.Add(kind,"entry.one"),Is.True);
        Assert.That(run.pendingEncyclopediaDiscoveryData.Add(kind,"entry.one"),Is.False);
        Assert.That(profile.encyclopediaDiscoveryData.Contains(kind,"entry.one"),Is.False);
        EncyclopediaDiscoveryProgress.Commit(run,profile);EncyclopediaDiscoveryProgress.Commit(run,profile);
        var restored=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(profile));
        Assert.That(restored.encyclopediaDiscoveryData.Contains(kind,"entry.one"),Is.True);
        Assert.That(run.pendingEncyclopediaDiscoveryData.Contains(kind,"entry.one"),Is.False);
        Assert.That(restored.magicStone,Is.EqualTo(31));
    }

    [Test]
    public void ResetAndProfileSwitchDoNotCarryPendingDiscovery()
    {
        var run=new GamePlayData();var nextProfile=new GameData();
        run.pendingEncyclopediaDiscoveryData.Add(EncyclopediaDiscoveryKind.Monster,"monster.previous");
        run.pendingEncyclopediaDiscoveryData.Add(EncyclopediaDiscoveryKind.Weapon,"Weapon.previous");
        EncyclopediaDiscoveryProgress.Clear(run);EncyclopediaDiscoveryProgress.Commit(run,nextProfile);
        Assert.That(nextProfile.encyclopediaDiscoveryData.monsterIds,Is.Empty);
        Assert.That(nextProfile.encyclopediaDiscoveryData.weaponIds,Is.Empty);
    }

    [Test]
    public void MissingNestedDataAndInvalidIdsAreSafe()
    {
        var run=new GamePlayData{pendingEncyclopediaDiscoveryData=null};
        var profile=new GameData{encyclopediaDiscoveryData=null};
        EncyclopediaDiscoveryProgress.Commit(run,profile);EncyclopediaDiscoveryProgress.Clear(null);
        Assert.That(profile.encyclopediaDiscoveryData.Add(EncyclopediaDiscoveryKind.Relic," "),Is.False);
        Assert.That(profile.encyclopediaDiscoveryData.Contains(EncyclopediaDiscoveryKind.Monster,null),Is.False);
    }
}
