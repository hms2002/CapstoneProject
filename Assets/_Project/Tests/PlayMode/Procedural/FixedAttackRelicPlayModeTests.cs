#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class FixedAttackRelicPlayModeTests
{
    private const string Definitions = "Assets/_Project/Data/Items/Relics/Definitions/";
    private const string AttributesRoot = "Assets/_Project/Data/Attributes/Definitions/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void RegisteredRelics_DoNotGrantAttackMultiplierBonuses_AfterTheSevenIdentityPreservingMigrations()
    {
        var database = Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        var attackMul = Load<AttributeDefinition>(AttributesRoot + "DamageCalcAttribute/AttackMulAttribute.asset");
        string[] names = { "WornWhetstone", "MasterWhetstone", "HeavyBracelet", "LastBreath", "RedContract", "RotatingIornBall", "FeatherOrbit" };
        string[] ids = { "RD_WornWhetstone", "RD_MasterWhetstone", "RD_HeavyBracelet", "RD_LastBreath", "RD_RedContract", "RD_RotatingIornBall", "RD_FeatherOrbit" };
        for (int i = 0; i < names.Length; i++)
        {
            var definition = Definition(names[i]);
            Assert.That(definition.relicId, Is.EqualTo(ids[i]));
            Assert.That(definition.icon, Is.Not.Null);
            CollectionAssert.Contains(database.allRelics, definition);
        }

        foreach (var definition in database.allRelics)
        {
            if (definition == null) continue;
            switch (definition.logic)
            {
                case RelicLogic_StatModifiers modifiers:
                    Assert.That(modifiers.entries.Any(entry => entry.attribute == attackMul), Is.False, definition.name);
                    break;
                case RelicLogic_StatWhileHealthRatio_Managed conditional:
                    Assert.That(conditional.attribute, Is.Not.EqualTo(attackMul), definition.name);
                    break;
                case RelicLogic_TimedStatOnGameplayEvent_Managed timed:
                    Assert.That(timed.attribute, Is.Not.EqualTo(attackMul), definition.name);
                    Assert.That(timed.passiveEntries.Any(entry => entry.attribute == attackMul), Is.False, definition.name);
                    break;
                case RelicLogic_MoveSpeedOnKill_Managed kill:
                    Assert.That(kill.attackAttribute, Is.Not.EqualTo(attackMul), definition.name);
                    break;
            }
        }
    }

    [TestCase("WornWhetstone", 1f, 2f, 3f)]
    [TestCase("MasterWhetstone", 1f, 2f, 3f)]
    [TestCase("HeavyBracelet", 2f, 3f, 4f)]
    public void PassiveAttackBonus_RemainsFlatAcrossAttackChanges_UpgradePreviewAndUnequip(
        string name, float first, float second, float third)
    {
        using var fixture = new Fixture();
        var definition = Definition(name);
        var logic = definition.logic as RelicLogic_StatModifiers;
        Assert.That(logic, Is.Not.Null);
        // Exercise the retained movement penalty above its existing multiplier minimum.
        float initialMoveMultiplier = name == "HeavyBracelet" ? 2f : 1f;
        fixture.Set(fixture.MoveMul, initialMoveMultiplier);
        var entry = logic.entries.Single(value => value.attribute == fixture.AttackAdd);
        Assert.That(entry.type, Is.EqualTo(ModifierType.Flat));
        CollectionAssert.AreEqual(new[] { first, second, third }, entry.valueByLevel);
        Assert.That(fixture.Inventory.TryAcquireOrUpgrade(definition), Is.True);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(first));
        Assert.That(fixture.Read(fixture.AttackMul), Is.EqualTo(1f));

        fixture.Set(fixture.AttackBase, 80f);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(first));
        Assert.That(fixture.AttackFinal, Is.EqualTo(80f + first));

        var snapshot = fixture.Attributes.CreatePreviewSnapshot();
        fixture.Inventory.ProjectAcquisitions(snapshot, new[] { (definition, 1) });
        Assert.That(snapshot[fixture.AttackAdd].CurrentValue, Is.EqualTo(second));
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(first));

        float[] curve = { first, second, third };
        for (int level = 1; level <= 3; level++)
        {
            if (level > 1) Assert.That(fixture.Inventory.TryAcquireOrUpgrade(definition), Is.True);
            Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(curve[level - 1]));
            string tooltip = logic.BuildTooltip(definition, level, default).effectText;
            Assert.That(tooltip, Does.Contain($"[+{curve[level - 1]:0}]"));
            string[] attackLines = tooltip.Split('\n').Where(line => line.Contains("[[공격력]]")).ToArray();
            Assert.That(attackLines, Is.Not.Empty);
            Assert.That(attackLines.All(line => !line.Contains("%")), Is.True);
            if (name == "MasterWhetstone")
                Assert.That(fixture.Read(fixture.NormalMul), Is.EqualTo(1f + new[] { 0.03f, 0.05f, 0.08f }[level - 1]).Within(0.0001f));
            if (name == "HeavyBracelet")
                Assert.That(fixture.Read(fixture.MoveMul), Is.EqualTo(initialMoveMultiplier *
                    (1f - new[] { 0.04f, 0.03f, 0.02f }[level - 1])).Within(0.0001f));
        }

        Assert.That(fixture.Inventory.RemoveAt(0), Is.True);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
        Assert.That(fixture.Read(fixture.MoveMul), Is.EqualTo(initialMoveMultiplier));
        Assert.That(fixture.Read(fixture.NormalMul), Is.EqualTo(1f));
    }

    [Test]
    public void LastBreath_GrantsFixedThreeAtHalfHealth_AndPreservesDetachedPreviewAndRestoreCleanup()
    {
        using var fixture = new Fixture();
        var definition = Definition("LastBreath");
        var logic = definition.logic as RelicLogic_StatWhileHealthRatio_Managed;
        Assert.That(logic, Is.Not.Null);
        Assert.That(logic.attribute, Is.EqualTo(fixture.AttackAdd));
        Assert.That(logic.modifierType, Is.EqualTo(ModifierType.Flat));
        Assert.That(logic.value, Is.EqualTo(3f));
        Assert.That(fixture.Inventory.TryAcquireOrUpgrade(definition), Is.True);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
        fixture.Set(fixture.Health, 5f);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(3f));
        fixture.Set(fixture.AttackBase, 80f);
        Assert.That(fixture.AttackFinal, Is.EqualTo(83f));
        fixture.Set(fixture.Health, 6f);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);

        var snapshot = fixture.Attributes.CreatePreviewSnapshot();
        Assert.That(snapshot[fixture.Health].TrySetCurrentValue(2f), Is.True);
        fixture.Inventory.ProjectAcquisitions(snapshot, Array.Empty<(RelicDefinition, int)>());
        Assert.That(snapshot[fixture.AttackAdd].CurrentValue, Is.EqualTo(3f));
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
        Assert.That(logic.BuildTooltip(definition, 1, default).effectText, Does.Contain("[+3]"));

        fixture.Set(fixture.Health, 4f);
        Assert.That(fixture.Inventory.TryGetRuntimeContextForSlot(0, out var context), Is.True);
        logic.OnUnequipped(context);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
        logic.OnRestoreAttached(context);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(3f));
        Assert.That(fixture.Inventory.RemoveAt(0), Is.True);
        fixture.Set(fixture.Health, 2f);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
    }

    [TestCase("RedContract", false)]
    [TestCase("RotatingIornBall", true)]
    public void KillAttackBuff_GrantsFixedTwo_RefreshesFourSeconds_AndKeepsMovementPercentageAndOtherSources(
        string name, bool hasMovementBonus)
    {
        using var fixture = new Fixture();
        var definition = Definition(name);
        Assert.That(fixture.Inventory.TryAcquireOrUpgrade(definition), Is.True);
        GameplayTag trigger;
        if (definition.logic is RelicLogic_TimedStatOnGameplayEvent_Managed timed)
        {
            Assert.That(timed.attribute, Is.EqualTo(fixture.AttackAdd));
            Assert.That(timed.modifierType, Is.EqualTo(ModifierType.Flat));
            Assert.That(timed.value, Is.EqualTo(2f));
            Assert.That(timed.durationSeconds, Is.EqualTo(4f));
            trigger = timed.triggerTag;
        }
        else
        {
            var kill = definition.logic as RelicLogic_MoveSpeedOnKill_Managed;
            Assert.That(kill, Is.Not.Null);
            Assert.That(kill.attackAttribute, Is.EqualTo(fixture.AttackAdd));
            Assert.That(kill.attackPercentBonus, Is.EqualTo(2f));
            Assert.That(kill.percentBonus, Is.EqualTo(0.1f));
            Assert.That(kill.durationSeconds, Is.EqualTo(4f));
            trigger = kill.triggerTag;
        }
        Assert.That(trigger, Is.Not.Null);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
        fixture.Kill(trigger);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(2f));
        Assert.That(fixture.Read(fixture.AttackMul), Is.EqualTo(1f));
        Assert.That(fixture.Read(fixture.MoveMul), Is.EqualTo(hasMovementBonus ? 1.1f : 1f).Within(0.0001f));
        fixture.Set(fixture.AttackBase, 80f);
        Assert.That(fixture.AttackFinal, Is.EqualTo(82f));

        var snapshot = fixture.Attributes.CreatePreviewSnapshot();
        fixture.Inventory.ProjectAcquisitions(snapshot, new[] { (Definition("WornWhetstone"), 1) });
        Assert.That(snapshot[fixture.AttackAdd].CurrentValue, Is.EqualTo(3f), "Detached acquisition keeps the active temporary attack bonus.");
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(2f));

        fixture.AdvanceBuffs(2f);
        fixture.Kill(trigger);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(2f), "A second kill refreshes rather than stacks attack.");
        fixture.AdvanceBuffs(3.9f);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(2f));
        fixture.AdvanceBuffs(0.11f);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero);
        Assert.That(fixture.Read(fixture.MoveMul), Is.EqualTo(1f));
        string tooltip = definition.logic.BuildTooltip(definition, 1, default).effectText;
        Assert.That(tooltip, Does.Contain("[+2]"));
        if (hasMovementBonus) Assert.That(tooltip, Does.Contain("+10%"));

        Assert.That(fixture.Inventory.TryGetRuntimeContextForSlot(0, out var context), Is.True);
        definition.logic.OnUnequipped(context);
        definition.logic.OnRestoreAttached(context);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.Zero, "Restore reconnects future kill hooks without granting the buff.");
        fixture.Kill(trigger);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(2f));
        fixture.AddIndependentAttackBonus(5f);
        Assert.That(fixture.Inventory.RemoveAt(0), Is.True);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(5f));
        Assert.That(fixture.Read(fixture.MoveMul), Is.EqualTo(1f));
        fixture.Kill(trigger);
        Assert.That(fixture.Read(fixture.AttackAdd), Is.EqualTo(5f), "Unequip disposes the old kill subscriptions only.");
    }

    [TestCase(1, 3f)]
    [TestCase(2, 4f)]
    [TestCase(3, 5f)]
    [TestCase(4, 6f)]
    [TestCase(5, 7f)]
    public void FeatherDamage_IsFixedPerLevel_IndependentOfAttack_WhileMovementCooldownAndPayloadStayIntact(int level, float expected)
    {
        using var fixture = new Fixture();
        var definition = Definition("FeatherOrbit");
        var logic = definition.logic as RelicLogic_FeatherOrbit_Managed;
        Assert.That(logic, Is.Not.Null);
        Assert.That(definition.maxLevel, Is.EqualTo(5));
        CollectionAssert.AreEqual(new[] { 3f, 4f, 5f, 6f, 7f }, logic.damageCoefByLevel);
        var controller = fixture.Owner.AddComponent<FeatherOrbitController>();
        controller.Setup(new FeatherOrbitController.Config
        {
            owner = fixture.Owner,
            damageEffect = logic.damageEffect,
            attackStatId = logic.attackStatId,
            damageCoef = logic.damageCoefByLevel[level - 1],
            moveSpeedFinalStatId = logic.moveSpeedFinalStatId,
            basePerTargetHitCooldown = logic.basePerTargetHitCooldown,
            knockbackImpulse = logic.knockbackImpulse,
            hitConfirmedTag = logic.hitConfirmedTag
        });

        foreach (float attack in new[] { 0f, 10f, 80f })
        {
            fixture.Set(fixture.AttackBase, attack);
            fixture.Set(fixture.AttackAdd, attack);
            Assert.That(controller.ComputeHpDamage(), Is.EqualTo(expected));
        }
        fixture.Set(fixture.MoveMul, 2f);
        Assert.That(controller.GetMoveSpeedMultX1(), Is.EqualTo(2f));
        Assert.That(controller.GetPerTargetHitCooldown(), Is.EqualTo(logic.basePerTargetHitCooldown / 2f));
        Assert.That(controller.ComputeHpDamage(), Is.EqualTo(expected));
        Assert.That(controller.TryBuildHitPayload(fixture.Owner, out var payload), Is.True);
        Assert.That(payload.finalHpDamage, Is.EqualTo(expected));
        Assert.That(payload.sourceSystem, Is.EqualTo(fixture.System));
        Assert.That(payload.damageEffect, Is.EqualTo(logic.damageEffect));
        Assert.That(payload.sourceSpec, Is.Null);

        string tooltip = logic.BuildTooltip(definition, level, default).effectText;
        Assert.That(tooltip, Does.Contain($"{{val:{expected:0}}}"));
        Assert.That(tooltip, Does.Not.Contain("%"));
        Assert.That(tooltip, Does.Not.Contain("[[공격력]]"));
    }

    private static RelicDefinition Definition(string name) => Load<RelicDefinition>(
        (name == "FeatherOrbit" ? "Assets/_Project/Unused/Items/Relics/" : Definitions) +
        "RD_" + name + ".asset");

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        public readonly GameObject Owner;
        public readonly AttributeSet Attributes;
        public readonly AbilitySystem System;
        public readonly RelicInventory Inventory;
        public readonly AttributeDefinition AttackBase, AttackAdd, AttackMul, MoveMul, NormalMul, Health, MaxHealth;
        private readonly AttributeStatProvider provider;

        public Fixture()
        {
            AttackBase = Load<AttributeDefinition>(AttributesRoot + "DamageCalcAttribute/AttackBaseAttribute.asset");
            AttackAdd = Load<AttributeDefinition>(AttributesRoot + "DamageCalcAttribute/AttackAddAttribute.asset");
            AttackMul = Load<AttributeDefinition>(AttributesRoot + "DamageCalcAttribute/AttackMulAttribute.asset");
            MoveMul = Load<AttributeDefinition>(AttributesRoot + "MoveSpeedMulAttribute.asset");
            NormalMul = Load<AttributeDefinition>(AttributesRoot + "DamageCalcAttribute/NormalMulAttribute.asset");
            Health = Load<AttributeDefinition>(AttributesRoot + "HealthAttribute.asset");
            MaxHealth = Load<AttributeDefinition>(AttributesRoot + "MaxHealthAttribute.asset");
            var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
            typeof(AttributeCatalogSO).GetField("attributes", Private).SetValue(catalog,
                new[] { AttackBase, AttackAdd, AttackMul, MoveMul, NormalMul, Health, MaxHealth });
            var bindings = Load<StatTypeBindings>("Assets/_Project/Data/Attributes/Formulas/StatTypeBindings.asset");
            var profile = Own(ScriptableObject.CreateInstance<DamageProfileDefinition>());
            profile.statBindings = bindings;
            Owner = Own(new GameObject("Fixed attack relic owner"));
            Owner.SetActive(false);
            Attributes = Owner.AddComponent<AttributeSet>();
            typeof(AttributeSet).GetField("attributeCatalog", Private).SetValue(Attributes, catalog);
            Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            System = Owner.AddComponent<AbilitySystem>();
            typeof(AbilitySystem).GetField("damageProfile", Private).SetValue(System, profile);
            Owner.SetActive(true);
            Set(AttackBase, 10f);
            Set(AttackAdd, 0f);
            Set(AttackMul, 1f);
            Set(MoveMul, 1f);
            Set(NormalMul, 1f);
            Set(MaxHealth, 10f);
            Set(Health, 10f);
            provider = new AttributeStatProvider(Attributes, bindings);
            Inventory = Owner.AddComponent<RelicInventory>();
        }

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public float Read(AttributeDefinition attribute) => Attributes.GetCurrentValue(attribute);
        public float AttackFinal => provider.Get(StatId.AttackFinal);
        public void Set(AttributeDefinition attribute, float value) => Assert.That(Attributes.TrySetBaseValue(attribute, value, Owner), Is.True);
        public void Kill(GameplayTag tag) => Owner.GetComponent<RelicProcManager>().HandleGameplayEvent(tag,
            new AbilityEventData { AbilitySystem = System, Instigator = Owner });
        public void AdvanceBuffs(float seconds)
        {
            Attributes.GetAttribute(AttackAdd).Update(seconds);
            Attributes.GetAttribute(MoveMul).Update(seconds);
        }
        public void AddIndependentAttackBonus(float value)
        {
            var source = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>());
            Assert.That(Attributes.TryAddModifier(AttackAdd, new AttributeModifier(ModifierType.Flat, value, source)), Is.True);
        }
        public void Dispose()
        {
            for (int i = Inventory.Capacity - 1; i >= 0; i--)
                if (Inventory.GetRelicInSlot(i) != null) Inventory.RemoveAt(i);
            Object.DestroyImmediate(Owner);
            foreach (var value in owned)
                if (value != null && value is not GameObject) Object.DestroyImmediate(value);
        }
    }
}
#endif
