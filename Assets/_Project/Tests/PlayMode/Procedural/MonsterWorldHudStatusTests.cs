#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class MonsterWorldHudStatusTests
{
    private const string Root = "Assets/_Project/Prefabs/Monsters/";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [TestCase("SlimeCorridor/Pawn", MonsterSizeCategory.Small, false)]
    [TestCase("SlimeCorridor/Knight", MonsterSizeCategory.Small, true)]
    [TestCase("SlimeCorridor/Wizard", MonsterSizeCategory.Small, true)]
    [TestCase("ShadowCorridor/ShadowMonster", MonsterSizeCategory.Small, true)]
    [TestCase("BeerMonster", MonsterSizeCategory.Small, true)]
    [TestCase("CommonCorridor/LizardWarrior", MonsterSizeCategory.Medium, true)]
    [TestCase("CommonCorridor/ArcaneTankGolem", MonsterSizeCategory.Large, true)]
    public void AuthoredHud_UsesApprovedSizeAndVisibility(string path, MonsterSizeCategory size, bool bar)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + path + ".prefab");
        var profile = prefab.GetComponent<MonsterSizeProfile>();
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.Size, Is.EqualTo(size));
        Assert.That(profile.ShowHealthBar, Is.EqualTo(bar));
        Assert.That(profile.HudAnchor, Is.Not.Null);
        var runtime = prefab.GetComponent<MonsterStatusRuntime>();
        Assert.That(runtime, Is.Not.Null);
        Assert.That(new SerializedObject(runtime).FindProperty("electrocutedEffect").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.transform.Find("MonsterWorldHud"), Is.Not.Null);
    }

    [Test]
    public void Burn_ReapplicationKeepsTickProgress_AndCleanseDisableDeathClearStacks()
    {
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "CommonCorridor/GoblinWarrior.prefab"), Vector3.one * 10000f, Quaternion.identity);
        var source = new GameObject("Burn test source");
        source.AddComponent<AttributeSet>();
        var asc = source.AddComponent<AbilitySystem>();
        var damage = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_MobAttackDamage_Spec.asset");
        try
        {
            var status = BurnStatus2D.Apply(target, asc, damage, source, 12);
            var runtime = target.GetComponent<MonsterStatusRuntime>();
            Assert.That(runtime.TryGetActive("Burn", out var entry), Is.True);
            Assert.That(entry.DisplayValue, Is.EqualTo(12f));
            var elapsed = typeof(BurnStatus2D).GetField("tickElapsed", Private);
            elapsed.SetValue(status, 0.6f);
            BurnStatus2D.Apply(target, asc, damage, source, 99);
            Assert.That(status.CurrentStacks, Is.EqualTo(99));
            Assert.That((float)elapsed.GetValue(status), Is.EqualTo(0.6f));
            Assert.That(status.ConsumeUpTo(5), Is.EqualTo(5));
            runtime.ClearAll();
            Assert.That(status.CurrentStacks, Is.Zero);
            Assert.That(runtime.TryGetActive("Burn", out _), Is.False);
            BurnStatus2D.Apply(target, asc, damage, source, 3);
            target.SetActive(false);
            Assert.That(status.CurrentStacks, Is.Zero);
            target.SetActive(true);
            Assert.That(runtime.TryGetActive("Burn", out _), Is.False);
            BurnStatus2D.Apply(target, asc, damage, source, 3);
            target.GetComponent<Enemy>().RequestDeath();
            Assert.That(status.CurrentStacks, Is.Zero);
            Assert.That(BurnStatus2D.Apply(target, asc, damage, source, 3), Is.Null);
        }
        finally { Object.DestroyImmediate(target); Object.DestroyImmediate(source); }
    }

    [Test]
    public void Electrocuted_ProjectsActualEffectLifetime_RefreshAndCleanse()
    {
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SlimeCorridor/Pawn.prefab"), Vector3.one * 10000f, Quaternion.identity);
        try
        {
            var runtime = target.GetComponent<MonsterStatusRuntime>();
            var runner = target.GetComponent<GameplayEffectRunner>();
            var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
            runner.ApplyEffectSpec(new GameplayEffectSpec(effect, new GameplayEffectContext(target, target)), target);
            Assert.That(runtime.TryGetActive("Electrocuted", out var entry), Is.True);
            Assert.That(entry.ValueKind, Is.EqualTo(MonsterStatusValueKind.Seconds));
            Assert.That(entry.DisplayValue, Is.EqualTo(4f).Within(0.01f));
            runner.FindActiveEffect(effect, target).TimeRemaining = 1.25f;
            Assert.That(entry.DisplayValue, Is.EqualTo(1.25f));
            runner.ApplyEffectSpec(new GameplayEffectSpec(effect, new GameplayEffectContext(target, target)), target);
            Assert.That(entry.DisplayValue, Is.EqualTo(4f).Within(0.01f));
            runtime.ClearAll();
            Assert.That(runtime.TryGetActive("Electrocuted", out _), Is.False);
            Assert.That(runner.HasActiveEffect(effect, target), Is.False);
            Assert.That(target.GetComponent<TagSystem>().HasTag(effect.grantedTags[0]), Is.False);
        }
        finally { Object.DestroyImmediate(target); }
    }
}
#endif
