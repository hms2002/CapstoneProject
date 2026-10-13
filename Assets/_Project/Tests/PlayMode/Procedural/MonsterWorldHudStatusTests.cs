#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

    [TestCase(false, false, 0f, 1f, 0.5f, 0f)]
    [TestCase(true, false, 0f, 1f, 0.5f, 0f)]
    [TestCase(false, true, 0f, 1f, 0.5f, 0f)]
    [TestCase(true, true, 0f, 1f, 0.5f, 0f)]
    [TestCase(false, true, 7f, 1f, 0.5f, 0f)]
    [TestCase(true, true, 7f, 1f, 0.5f, 0f)]
    [TestCase(true, true, 7f, 0.5f, 2f, 0f)]
    [TestCase(true, true, 7f, 1f, 0.5f, 2f)]
    public void ElectricDischarge_RefreshesInitialTargetOnly_AndPreservesChainDamageAndLimit(
        bool useSpec, bool withStatProvider, float electricStat,
        float initialCoefficient, float chainCoefficient, float modifierBonus)
    {
        var authoredStatus = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
        var authoredDischarge = AssetDatabase.LoadAssetAtPath<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset");
        Assert.That(authoredStatus, Is.Not.Null);
        Assert.That(authoredDischarge, Is.Not.Null);

        var discharge = Object.Instantiate(authoredDischarge);
        var status = ScriptableObject.CreateInstance<GE_StatusOnlyDuration>();
        var damage = ScriptableObject.CreateInstance<ElectricDischargeTestDamageEffect>();
        var targets = new List<GameObject>();
        var source = new GameObject("Electric discharge test source");
        AttributeDefinition electricAttribute = null;
        AttributeCatalogSO statCatalog = null;
        StatTypeBindings statBindings = null;
        AttributeSet sourceAttributes = null;
        IDisposable damageModifier = null;
        try
        {
            if (withStatProvider)
            {
                electricAttribute = ScriptableObject.CreateInstance<AttributeDefinition>();
                electricAttribute.defaultBaseValue = electricStat;
                electricAttribute.maxValue = 1000f;
                statCatalog = ScriptableObject.CreateInstance<AttributeCatalogSO>();
                typeof(AttributeCatalogSO).GetField("attributes", Private).SetValue(statCatalog, new[] { electricAttribute });
                statBindings = ScriptableObject.CreateInstance<StatTypeBindings>();
                typeof(StatTypeBindings).GetField("bindings", Private).SetValue(statBindings,
                    new List<StatTypeBindings.Binding> { new() { id = StatId.ElectricFinal, attribute = electricAttribute } });
                source.SetActive(false);
                sourceAttributes = source.AddComponent<AttributeSet>();
                typeof(AttributeSet).GetField("attributeCatalog", Private).SetValue(sourceAttributes, statCatalog);
                var provider = source.AddComponent<AttributeStatSource>();
                typeof(AttributeStatSource).GetField("statBindingsOverride", Private).SetValue(provider, statBindings);
                source.SetActive(true);
                Assert.That(provider.Get(StatId.ElectricFinal), Is.EqualTo(electricStat));
            }
            if (modifierBonus != 0f)
                damageModifier = ElectricDischargeDamageModifiers.Register(context =>
                    context.Instigator == source ? context.BaseDamage + modifierBonus : context.BaseDamage);

            var tag = authoredStatus.grantedTags[0];
            status.duration = 4f;
            status.grantedTags.Add(tag);
            damage.MagnitudeKey = tag;
            void Set(string field, object value) => typeof(GE_ElectricShockTrigger).GetField(field, Private).SetValue(discharge, value);
            Set("electrocutedStatusEffect", status);
            Set("electrocutedTag", tag);
            Set("electricDamageEffect", damage);
            Set("electricDamageKey", tag);
            Set("chainRadius", 1.1f);
            Set("chainVfxPrefab", null);
            Assert.That((int)typeof(GE_ElectricShockTrigger).GetField("maxTargetsPerDischarge", Private).GetValue(discharge), Is.EqualTo(3));
            Assert.That((float)typeof(GE_ElectricShockTrigger).GetField("initialElectricCoefficient", Private).GetValue(discharge), Is.EqualTo(1f));
            Assert.That((float)typeof(GE_ElectricShockTrigger).GetField("chainElectricCoefficient", Private).GetValue(discharge), Is.EqualTo(0.5f));
            Set("initialElectricCoefficient", initialCoefficient);
            Set("chainElectricCoefficient", chainCoefficient);

            GameObject CreateTarget(float offset)
            {
                var target = new GameObject("Electric discharge test target");
                targets.Add(target);
                target.transform.position = new Vector3(1000f + offset, 1000f, 0f);
                target.AddComponent<TagSystem>();
                target.AddComponent<GameplayEffectRunner>();
                var collider = target.AddComponent<CircleCollider2D>();
                collider.radius = 0.05f;
                collider.isTrigger = true;
                target.AddComponent<CombatHurtbox2D>();
                return target;
            }

            for (int i = 0; i < 4; i++)
            {
                var target = CreateTarget(i);
                if (i == 0) continue;
                var runner = target.GetComponent<GameplayEffectRunner>();
                runner.ApplyEffectSpec(new GameplayEffectSpec(status, source), target);
                runner.FindActiveEffect(status, target).TimeRemaining = 1.25f + i * 0.01f;
            }
            var untaggedTarget = CreateTarget(0.25f);
            if (withStatProvider)
                targets[0].GetComponent<TagSystem>().OnTagAdded += addedTag =>
                {
                    if (addedTag == tag)
                        sourceAttributes.TrySetBaseValue(electricAttribute, electricStat + 10f, source);
                };
            Physics2D.SyncTransforms();

            for (int dischargeNumber = 1; dischargeNumber <= 2; dischargeNumber++)
            {
                if (sourceAttributes != null)
                    Assert.That(sourceAttributes.TrySetBaseValue(electricAttribute, electricStat, source), Is.True);
                var initialRunner = targets[0].GetComponent<GameplayEffectRunner>();
                if (dischargeNumber == 2)
                    initialRunner.FindActiveEffect(status, targets[0]).TimeRemaining = 0.75f;
                if (useSpec)
                    discharge.Apply(new GameplayEffectSpec(discharge, source), targets[0]);
                else
                    discharge.Apply(targets[0], source);

                if (withStatProvider && dischargeNumber == 1)
                    Assert.That(sourceAttributes.GetAttributeValue(electricAttribute), Is.EqualTo(electricStat + 10f),
                        "The initial status must change live stats after the discharge snapshot is captured.");
                Assert.That(initialRunner.GetRemainingTime(status, targets[0]), Is.EqualTo(4f));
                Assert.That(damage.TotalHits, Is.EqualTo(3 * dischargeNumber));
                for (int i = 0; i < 4; i++)
                {
                    var target = targets[i];
                    if (i > 0)
                        Assert.That(target.GetComponent<GameplayEffectRunner>().GetRemainingTime(status, target), Is.EqualTo(1.25f + i * 0.01f), $"Chain recipient {i} must retain its remaining duration.");
                    if (i < 3)
                    {
                        Assert.That(damage.HitCounts[target], Is.EqualTo(dischargeNumber));
                        float coefficient = i == 0 ? initialCoefficient : chainCoefficient;
                        Assert.That(damage.LastDamage[target], Is.EqualTo(5f + electricStat * coefficient + modifierBonus),
                            "All targets must use attacker stats captured before the initial status, then apply discharge modifiers.");
                    }
                    else
                        Assert.That(damage.HitCounts.ContainsKey(target), Is.False, "The third chain recipient exceeds the limit including the initial target.");
                }
                Assert.That(damage.HitCounts.ContainsKey(untaggedTarget), Is.False);
                Assert.That(untaggedTarget.GetComponent<GameplayEffectRunner>().HasActiveEffect(status, untaggedTarget), Is.False);
            }
        }
        finally
        {
            damageModifier?.Dispose();
            foreach (var target in targets)
            {
                target.GetComponent<GameplayEffectRunner>().ClearAllActiveEffects();
                Object.DestroyImmediate(target);
            }
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(discharge);
            Object.DestroyImmediate(status);
            Object.DestroyImmediate(damage);
            Object.DestroyImmediate(electricAttribute);
            Object.DestroyImmediate(statCatalog);
            Object.DestroyImmediate(statBindings);
        }
    }

    public sealed class ElectricDischargeTestDamageEffect : GameplayEffect, ISpecGameplayEffect
    {
        public GameplayTag MagnitudeKey;
        public readonly Dictionary<GameObject, int> HitCounts = new();
        public readonly Dictionary<GameObject, float> LastDamage = new();
        public int TotalHits;

        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            Assert.That(spec.TryGetSetByCallerMagnitude(MagnitudeKey, out float magnitude), Is.True);
            HitCounts.TryGetValue(target, out int count);
            HitCounts[target] = count + 1;
            LastDamage[target] = magnitude;
            TotalHits++;
        }

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Assert.Fail("Electric damage must retain its spec application path.");
        public override void Remove(GameObject target, GameObject instigator) { }
    }
}
#endif
