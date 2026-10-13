#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityGAS;
using UnityGAS.Sample;
using Object = UnityEngine.Object;
using Mode = RelicLogic_ElectricAttributeBonus_Managed.BonusMode;

public sealed class ElectricRelicPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string DefinitionRoot = "Assets/_Project/Data/Items/Relics/Definitions/";

    [TestCase("ThunderHeart", Mode.NearbyElectrocutedCount)]
    [TestCase("StormStep", Mode.MaxHealth)]
    [TestCase("DashCoil", Mode.AfterDash)]
    public void AuthoredRelics_AreUnlockedRareEntries_WithCurrentLevelTooltips(string name, Mode mode)
    {
        var definition = Load<RelicDefinition>(DefinitionRoot + "RD_" + name + ".asset");
        var database = Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        Assert.That(definition.relicId, Is.EqualTo("Relic.Electric." + name));
        Assert.That(definition.rarity, Is.EqualTo(ItemRarity.Rare));
        Assert.That(definition.maxLevel, Is.EqualTo(mode == Mode.AfterDash ? 3 : 5));
        Assert.That(definition.dropLevel, Is.EqualTo(1));
        Assert.That(definition.icon, Is.Not.Null);
        CollectionAssert.Contains(database.allRelics, definition);
        CollectionAssert.Contains(database.defaultUnlockedRelics, definition);
        var logic = definition.logic as RelicLogic_ElectricAttributeBonus_Managed;
        Assert.That(logic, Is.Not.Null);
        Assert.That(logic.mode, Is.EqualTo(mode));
        Assert.That(logic.electricAddAttribute, Is.EqualTo(Load<AttributeDefinition>(
            "Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementAddAttribute.asset")));
        if (mode == Mode.MaxHealth)
        {
            Assert.That(definition.displayName, Is.EqualTo("생명의 전류"));
            Assert.That(logic.healthStep, Is.EqualTo(1f));
            Assert.That(logic.maxHealthAttribute, Is.EqualTo(Load<AttributeDefinition>(
                "Assets/_Project/Data/Attributes/Definitions/MaxHealthAttribute.asset")));
        }
        for (int level = 1; level <= definition.maxLevel; level++)
        {
            int value = mode == Mode.AfterDash ? (level == 3 ? 2 : 1) : (level + 1) / 2;
            string tooltip = logic.BuildTooltip(definition, level, null).effectText;
            Assert.That(tooltip, Does.Contain("+" + value));
            Assert.That(tooltip, Does.Not.Contain("{value}"));
            Assert.That(tooltip, Does.Not.Contain("1/2/3"));
        }
    }

    [UnityTest]
    public IEnumerator SceneBonus_IncludesDistantEnemies_ExcludesOtherScenes_AndUpdatesAfterExpiryDisableDeathAndUnequip()
    {
        using var fixture = new Fixture(Mode.NearbyElectrocutedCount);
        Scene otherScene = SceneManager.CreateScene("ElectricRelicOtherScene_" + Guid.NewGuid().ToString("N"));
        try
        {
            var status = fixture.Own(ScriptableObject.CreateInstance<GE_StatusOnlyDuration>());
            status.duration = 4f;
            status.grantedTags.Add(fixture.Logic.electrocutedTag);
            var enemies = new List<Enemy>();
            for (int i = 1; i <= 4; i++) enemies.Add(fixture.CreateEnemy(i == 4 ? 20f : i, status));
            var otherSceneEnemy = fixture.CreateEnemy(0.1f, status);
            SceneManager.MoveGameObjectToScene(otherSceneEnemy.gameObject, otherScene);
            fixture.CreateEnemy(0.2f, null);
            fixture.TickNearby();
            Assert.That(fixture.ElectricBonus, Is.EqualTo(4f), "Every living same-scene shocked enemy contributes, without a three-enemy cap.");
            var countPreview = new List<AttributeModifier>();
            fixture.Logic.AppendPreviewModifiers(fixture.Context, fixture.ElectricAdd, countPreview);
            Assert.That(countPreview, Has.Count.EqualTo(1));
            Assert.That(countPreview[0].Value, Is.EqualTo(4f));
            var countContext = fixture.Context;
            float[] perEnemy = { 1f, 1f, 2f, 2f, 3f };
            for (int level = 1; level <= 5; level++)
            {
                countContext.level = level;
                fixture.Logic.OnEquipped(countContext);
                Assert.That(fixture.ElectricBonus, Is.EqualTo(4f * perEnemy[level - 1]));
            }
            fixture.Logic.OnEquipped(fixture.Context);

            enemies[0].RequestDeath();
            enemies[0].GetComponent<TagSystem>().AddTag(fixture.Logic.electrocutedTag);
            enemies[1].gameObject.SetActive(false);
            fixture.TickNearby();
            Assert.That(fixture.ElectricBonus, Is.EqualTo(2f), "Dead enemies remain excluded even if their tag is present.");
            enemies[2].enabled = false;
            fixture.TickNearby();
            Assert.That(fixture.ElectricBonus, Is.EqualTo(1f), "The distant same-scene enemy remains included.");
            var runner = enemies[3].GetComponent<GameplayEffectRunner>();
            runner.FindActiveEffect(status, enemies[3].gameObject).TimeRemaining = 0f;
            typeof(GameplayEffectRunner).GetMethod("Update", Private).Invoke(runner, null);
            Assert.That(enemies[3].GetComponent<TagSystem>().HasTag(fixture.Logic.electrocutedTag), Is.False);
            fixture.TickNearby();
            Assert.That(fixture.ElectricBonus, Is.Zero);

            runner.ApplyEffectSpec(new GameplayEffectSpec(status, fixture.Owner), enemies[3].gameObject);
            fixture.TickNearby();
            Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
            fixture.Logic.OnUnequipped(fixture.Context);
            Assert.That(fixture.ElectricBonus, Is.Zero);
            fixture.TickNearby();
            Assert.That(fixture.ElectricBonus, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(otherScene);
        }
        finally
        {
            if (otherScene.IsValid() && otherScene.isLoaded)
                SceneManager.UnloadSceneAsync(otherScene);
        }
    }

    [Test]
    public void MaxHealthBonus_CountsFullCellsWithoutCap_AndPreservesRuntimePreviewRestoreAndUnequip()
    {
        using var fixture = new Fixture(Mode.MaxHealth);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        // Keep boundary changes above AttributeValue's existing 0.001 notification threshold.
        fixture.Set(fixture.MaxHealth, 0.99f);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        fixture.Set(fixture.MaxHealth, 1f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
        fixture.Set(fixture.MaxHealth, 1.99f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
        fixture.Set(fixture.MaxHealth, 2f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(2f));
        fixture.Set(fixture.MaxHealth, 3f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(3f));
        fixture.Set(fixture.MaxHealth, 100f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(100f), "Maximum-health cells have no three-cell ceiling.");
        fixture.Set(fixture.Health, 20f);
        fixture.Set(fixture.Health, 0f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(100f), "Current health cannot change the maximum-health bonus.");
        fixture.Set(fixture.MaxHealth, 0f);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        fixture.Set(fixture.MaxHealth, 1f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
        fixture.Set(fixture.ElectricAdd, 10f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(11f), "Electric-stat changes must not feed back into maximum-health conditions.");
        fixture.Set(fixture.ElectricAdd, 0f);

        var context = fixture.Context;
        fixture.Set(fixture.MaxHealth, 4f);
        float[] expectedByLevel = { 4f, 4f, 8f, 8f, 12f };
        for (int level = 1; level <= 5; level++)
        {
            context.level = level;
            fixture.Logic.OnEquipped(context);
            fixture.Logic.OnEquipped(context);
            Assert.That(fixture.ElectricBonus, Is.EqualTo(expectedByLevel[level - 1]), "All four cells contribute at every level.");
            Assert.That(fixture.ProcCount, Is.EqualTo(1));
        }
        var projected = new List<AttributeModifier>();
        context.previewAttributeReader = attribute => attribute == fixture.MaxHealth ? 10.75f : fixture.Attributes.GetCurrentValue(attribute);
        fixture.Logic.AppendPreviewModifiers(context, fixture.ElectricAdd, projected);
        Assert.That(projected, Has.Count.EqualTo(1));
        Assert.That(projected[0].Value, Is.EqualTo(30f));
        Assert.That(fixture.ElectricBonus, Is.EqualTo(12f), "Detached previews cannot change the live bonus.");
        fixture.Set(fixture.MaxHealth, 5.99f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(15f));
        fixture.Set(fixture.MaxHealth, 6f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(18f));
        fixture.Logic.OnRestoreDetached(context);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        fixture.Logic.OnRestoreAttached(context);
        fixture.Logic.OnRestoreAttached(context);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(18f));
        Assert.That(fixture.ProcCount, Is.EqualTo(1));
        fixture.Logic.OnUnequipped(context);
        fixture.Set(fixture.MaxHealth, 10f);
        Assert.That(fixture.ElectricBonus, Is.Zero, "Unequipping must unsubscribe stat dependencies.");

        var inventory = fixture.Owner.AddComponent<RelicInventory>();
        var definition = fixture.Own(ScriptableObject.CreateInstance<RelicDefinition>());
        definition.relicId = "Test.LifeCurrent";
        definition.maxLevel = 5;
        definition.logic = fixture.Logic;
        fixture.Set(fixture.MaxHealth, 5.75f);
        var acquisition = fixture.Attributes.CreatePreviewSnapshot();
        inventory.ProjectAcquisitions(acquisition, new[] { (definition, 1) });
        Assert.That(acquisition[fixture.ElectricAdd].CurrentValue, Is.EqualTo(5f));
        Assert.That(fixture.ElectricBonus, Is.Zero);
        Assert.That(inventory.TryAcquireOrUpgrade(definition), Is.True);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(5f));

        var healthPreview = fixture.Attributes.CreatePreviewSnapshot();
        Assert.That(healthPreview[fixture.MaxHealth].TrySetCurrentValue(8.75f), Is.True);
        inventory.ProjectAcquisitions(healthPreview, Array.Empty<(RelicDefinition, int)>());
        Assert.That(healthPreview[fixture.ElectricAdd].CurrentValue, Is.EqualTo(8f));
        Assert.That(fixture.ElectricBonus, Is.EqualTo(5f));
        var upgrade = fixture.Attributes.CreatePreviewSnapshot();
        inventory.ProjectAcquisitions(upgrade, new[] { (definition, 2) });
        Assert.That(upgrade[fixture.ElectricAdd].CurrentValue, Is.EqualTo(10f));
        Assert.That(fixture.ElectricBonus, Is.EqualTo(5f));
        Assert.That(inventory.TryAcquireOrUpgrade(definition, 2), Is.True);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(10f));
        fixture.Set(fixture.MaxHealth, 8.75f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(16f));
        Assert.That(inventory.RemoveAt(0), Is.True);
        Assert.That(fixture.ElectricBonus, Is.Zero);
    }

    [TestCase(1, 1f)]
    [TestCase(2, 1f)]
    [TestCase(3, 2f)]
    public void MovementBonus_StacksUpToFour_RefreshesAllStacksAtCap_AndClearsOnRestore(int level, float bonus)
    {
        using var fixture = new Fixture(Mode.AfterDash, level);
        var movement = new AbilitySpec(fixture.Logic.triggerDashAbility);
        fixture.End(movement, false);
        fixture.Start(fixture.NewAbility(false));
        fixture.Start(null);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        for (int count = 1; count <= 4; count++)
        {
            fixture.Start(movement);
            Assert.That(fixture.ElectricBonus, Is.EqualTo(count * bonus));
        }
        fixture.TickElectric(2f);
        fixture.Start(movement);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(4f * bonus), "A capped use keeps four stacks and refreshes their shared timer.");
        fixture.TickElectric(2f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(4f * bonus), "All refreshed stacks survive the old expiry.");
        fixture.TickElectric(0f);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(4f * bonus), "A paused gameplay clock must preserve every stack.");
        fixture.Owner.GetComponent<RelicProcManager>().enabled = false;
        fixture.TickElectric(1.1f);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        fixture.Start(movement);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(bonus), "Attribute expiry resets the next use to one stack even without manager ticking.");
        fixture.Owner.GetComponent<RelicProcManager>().enabled = true;
        fixture.Logic.OnRestoreDetached(fixture.Context);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        fixture.Logic.OnRestoreAttached(fixture.Context);
        Assert.That(fixture.ElectricBonus, Is.Zero, "Scene restoration must not restore a transient dash bonus.");
        fixture.Start(movement);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(bonus));
        fixture.Logic.OnUnequipped(fixture.Context);
        fixture.Start(movement);
        Assert.That(fixture.ElectricBonus, Is.Zero);
    }

    [Test]
    public void MovementClassification_UsesTagIdentityAndDescendants_AndRejectsIncidentalMovementAndOtherOwners()
    {
        using var fixture = new Fixture(Mode.AfterDash);
        using var other = new Fixture(Mode.AfterDash);
        var movementTag = Load<GameplayTag>("Assets/_Project/Resources/Tags/Ability.Movement.asset");
        const string abilities = "Assets/_Project/Data/Abilities/Definitions/";
        foreach (string name in new[] { "AD_Dash", "AD_ApprenticeHeroSwordSkill2_DashStab", "AD_LightningSpearSkill1", "AD_RW_Skill1_Rush" })
            Assert.That(Load<AbilityDefinition>(abilities + name + ".asset").HasAbilityTag(movementTag), Is.True, name);
        foreach (string name in new[] { "AD_RW_Attack1", "AD_ApprenticeHeroSwordAttack", "AD_FloweringBloom" })
        {
            var definition = Load<AbilityDefinition>(abilities + name + ".asset");
            Assert.That(definition.HasAbilityTag(movementTag), Is.False, name);
            fixture.Start(new AbilitySpec(definition));
        }
        var untagged = fixture.NewAbility(false);
        untagged.Definition.name = "AD_Dash";
        var sameNameTag = fixture.Own(ScriptableObject.CreateInstance<GameplayTag>());
        sameNameTag.name = movementTag.name;
        untagged.Definition.abilityTags.Add(sameNameTag);
        Assert.That(untagged.Definition.HasAbilityTag(movementTag), Is.False);
        Assert.That(untagged.Definition.HasAbilityTag(null), Is.False);
        fixture.Start(untagged);
        other.Start(other.NewAbility(true));
        Assert.That(fixture.ElectricBonus, Is.Zero);
        Assert.That(other.ElectricBonus, Is.EqualTo(1f), "Movement events belong to the executing owner's AbilitySystem.");

        var child = fixture.Own(ScriptableObject.CreateInstance<GameplayTag>());
        child.Editor_SetName("TestReposition");
        child.Editor_SetParent(movementTag);
        var descendant = fixture.NewAbility(false);
        descendant.Definition.abilityTags.Add(child);
        Assert.That(descendant.Definition.HasAbilityTag(movementTag), Is.True);
        fixture.Start(descendant);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator MovementBonus_TriggersOnceOnAcceptedExecution_NotBlockedCooldownOrCancelledWindup()
    {
        using var fixture = new Fixture(Mode.AfterDash);
        var blockedTag = Load<GameplayTag>("Assets/_Project/Resources/Tags/State.Move.Blocked.asset");
        var definition = fixture.NewAbility(true).Definition;
        definition.blockedByTags.Add(blockedTag);
        definition.cooldown = 10f;
        var spec = fixture.System.GiveAbility(definition);
        fixture.Tags.AddTag(blockedTag);
        Assert.That(fixture.System.TryActivateAbility(spec), Is.False);
        Assert.That(fixture.ElectricBonus, Is.Zero);
        fixture.Tags.RemoveTag(blockedTag);
        Assert.That(fixture.System.TryActivateAbility(spec), Is.True);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
        for (int frame = 0; frame < 4; frame++) yield return null;
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f), "Finishing the execution must not grant another stack.");
        Assert.That(fixture.System.TryActivateAbility(spec), Is.False);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));

        var windup = fixture.NewAbility(true).Definition;
        windup.castTime = 10f;
        var windupSpec = fixture.System.GiveAbility(windup);
        Assert.That(fixture.System.TryActivateAbility(windupSpec), Is.True);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f), "Accepting a cast request alone is not an execution start.");
        fixture.System.CancelCasting(force: true);
        yield return null;
        Assert.That(fixture.ElectricBonus, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator DestroyingProcManager_ClearsDashBonusSubscriptionAndOwnedTemporaryToken()
    {
        using var fixture = new Fixture(Mode.AfterDash);
        var movement = new AbilitySpec(fixture.Logic.triggerDashAbility);
        fixture.Start(movement);
        fixture.Start(movement);
        Assert.That(fixture.ElectricBonus, Is.EqualTo(2f));
        var modifiers = (List<AttributeModifier>)typeof(AttributeValue).GetField("modifiers", Private)
            .GetValue(fixture.Attributes.GetAttribute(fixture.ElectricAdd));
        Object temporaryToken = modifiers[0].Source;
        Assert.That(temporaryToken, Is.Not.EqualTo(fixture.Context.token));
        Object.Destroy(fixture.Owner.GetComponent<RelicProcManager>());
        yield return null;
        yield return null;
        Assert.That(fixture.ElectricBonus, Is.Zero);
        Assert.That(temporaryToken == null, Is.True, "Destroying the proc owner must release its native temporary token.");
        fixture.Start(movement);
        Assert.That(fixture.ElectricBonus, Is.Zero, "The destroyed manager must unsubscribe the movement execution handler.");
    }

    [Test]
    public void ActiveDashBonus_SurvivesDetachedAcquisitionPreview_AndInventoryUpgradeReplacesItsLevel()
    {
        using var fixture = new Fixture(Mode.AfterDash);
        fixture.Logic.OnUnequipped(fixture.Context);
        var inventory = fixture.Owner.AddComponent<RelicInventory>();
        var definition = fixture.Own(ScriptableObject.CreateInstance<RelicDefinition>());
        definition.relicId = "ElectricRelicTest.Dash";
        definition.maxLevel = 3;
        definition.logic = fixture.Logic;
        try
        {
            Assert.That(inventory.TryAcquireOrUpgrade(definition), Is.True);
            Assert.That(inventory.TryGetRuntimeContextForSlot(0, out var context), Is.True);
            var movement = new AbilitySpec(fixture.Logic.triggerDashAbility);
            fixture.Start(movement);
            fixture.Start(movement);
            Assert.That(fixture.ElectricBonus, Is.EqualTo(2f));
            var modifiers = (List<AttributeModifier>)typeof(AttributeValue).GetField("modifiers", Private)
                .GetValue(fixture.Attributes.GetAttribute(fixture.ElectricAdd));
            Assert.That(modifiers, Has.Count.EqualTo(1));
            Assert.That(modifiers[0].Source, Is.Not.EqualTo(context.token));
            var snapshot = fixture.Attributes.CreatePreviewSnapshot();
            inventory.ProjectAcquisitions(snapshot, Array.Empty<(RelicDefinition, int)>());
            Assert.That(snapshot[fixture.ElectricAdd].CurrentValue, Is.EqualTo(2f));
            Assert.That(fixture.ElectricBonus, Is.EqualTo(2f));
            var upgradedPreview = fixture.Attributes.CreatePreviewSnapshot();
            inventory.ProjectAcquisitions(upgradedPreview, new[] { (definition, 2) });
            Assert.That(upgradedPreview[fixture.ElectricAdd].CurrentValue, Is.Zero,
                "Upgrading the dash relic must project the same transient-buff cleanup as actual level reapplication.");
            Assert.That(fixture.ElectricBonus, Is.EqualTo(2f), "Upgrade preview must leave the live timed bonus intact.");
            Assert.That(inventory.TryAcquireOrUpgrade(definition, 2), Is.True);
            Assert.That(inventory.GetRelicLevelInSlot(0), Is.EqualTo(3));
            Assert.That(fixture.ElectricBonus, Is.Zero);
            fixture.Start(movement);
            Assert.That(fixture.ElectricBonus, Is.EqualTo(2f));
            Assert.That(fixture.ProcCount, Is.EqualTo(1));
        }
        finally { inventory.RemoveAt(0); }
        Assert.That(fixture.ElectricBonus, Is.Zero);
    }

    [UnityTest]
    public IEnumerator DashCompletionMarker_SurvivesNormalCleanup() => ExerciseDashCompletion(false);

    [UnityTest]
    public IEnumerator DashCompletionMarker_RejectsHardStop() => ExerciseDashCompletion(true);

    private static IEnumerator ExerciseDashCompletion(bool hardStop)
    {
        using var fixture = new Fixture(Mode.AfterDash);
        var data = fixture.Own(ScriptableObject.CreateInstance<Dash2DData>());
        // Keep the interrupted dash inside its loop when the first frame has already elapsed.
        data.duration = hardStop ? Time.deltaTime + 1f : 0.01f;
        data.invulnerableTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        var definition = fixture.Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        definition.sourceObject = data;
        var logic = fixture.Own(ScriptableObject.CreateInstance<AbilityLogic_Dash2D>());
        var spec = new AbilitySpec(definition);
        spec.SetInt(AbilityLogic_Dash2D.TutorialForwardKey, 1);
        spec.SetInt(AbilityLogic_Dash2D.CompletedKey, 1);
        var routine = logic.Activate(fixture.System, spec, null);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(spec.GetInt(AbilityLogic_Dash2D.CompletedKey, -1), Is.Zero, "A new activation must erase any previous completion result.");
            Assert.That(fixture.Owner.GetComponent<AbilityMotionController2D>().HasActiveMotion, Is.True);
            if (hardStop)
            {
                var hardStopTag = Resources.Load<GameplayTag>("Tags/State.Move.Blocked");
                Assert.That(hardStopTag, Is.Not.Null);
                fixture.Tags.AddTag(hardStopTag);
            }
            bool running = true;
            for (int frame = 0; frame < 30 && running; frame++)
            {
                yield return null;
                running = routine.MoveNext();
            }
            Assert.That(running, Is.False);
            Assert.That(spec.GetInt(AbilityLogic_Dash2D.CompletedKey, -1), Is.EqualTo(hardStop ? 0 : 1));
            logic.CleanupForSceneTransition(fixture.System, spec, null);
            Assert.That(spec.GetInt(AbilityLogic_Dash2D.CompletedKey, -1), Is.EqualTo(hardStop ? 0 : 1));
            Assert.That(fixture.Owner.GetComponent<AbilityMotionController2D>().HasActiveMotion, Is.False);
            Assert.That(fixture.Tags.HasTag(data.invulnerableTag), Is.False);
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    private static T Load<T>(string path) where T : Object
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(value, Is.Not.Null, path);
        return value;
    }

    public sealed class TestAbilityLogic : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        public readonly GameObject Owner;
        public readonly AttributeSet Attributes;
        public readonly AbilitySystem System;
        public readonly TagSystem Tags;
        public readonly AttributeDefinition ElectricAdd, MoveBase, MoveAdd, MoveMul, MaxHealth, Health;
        public readonly RelicLogic_ElectricAttributeBonus_Managed Logic;
        public readonly RelicContext Context;
        public float ElectricBonus => Attributes.GetCurrentValue(ElectricAdd);
        public int ProcCount => Procs.Count;
        private List<IRelicProc> Procs => (List<IRelicProc>)typeof(RelicProcManager).GetField("procs", Private)
            .GetValue(Owner.GetComponent<RelicProcManager>());

        public Fixture(Mode mode, int level = 1)
        {
            ElectricAdd = Attribute(0f);
            MoveBase = Attribute(5f);
            MoveAdd = Attribute(0f);
            MoveMul = Attribute(1f);
            MaxHealth = Attribute(0f);
            Health = Attribute(0f);
            var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
            typeof(AttributeCatalogSO).GetField("attributes", Private).SetValue(catalog, new[] { ElectricAdd, MoveBase, MoveAdd, MoveMul, MaxHealth, Health });
            var bindings = Own(ScriptableObject.CreateInstance<StatTypeBindings>());
            typeof(StatTypeBindings).GetField("bindings", Private).SetValue(bindings, new List<StatTypeBindings.Binding>
            {
                new() { id = StatId.MoveSpeedBase, attribute = MoveBase },
                new() { id = StatId.MoveSpeedAdd, attribute = MoveAdd },
                new() { id = StatId.MoveSpeedMul, attribute = MoveMul },
                new() { id = StatId.ElectricAdd, attribute = ElectricAdd },
                new() { id = StatId.MaxHealth, attribute = MaxHealth },
                new() { id = StatId.Health, attribute = Health }
            });
            typeof(StatTypeBindings).GetField("composites", Private).SetValue(bindings, new List<StatTypeBindings.Composite>
            {
                new() { finalId = StatId.MoveSpeedFinal, baseId = StatId.MoveSpeedBase, addId = StatId.MoveSpeedAdd, mulId = StatId.MoveSpeedMul },
                new() { finalId = StatId.ElectricFinal, addId = StatId.ElectricAdd }
            });
            var profile = Own(ScriptableObject.CreateInstance<DamageProfileDefinition>());
            profile.statBindings = bindings;
            Owner = Own(new GameObject("Electric relic test owner"));
            Owner.transform.position = new Vector3(1000f, 1000f, 0f);
            Owner.SetActive(false);
            Attributes = Owner.AddComponent<AttributeSet>();
            typeof(AttributeSet).GetField("attributeCatalog", Private).SetValue(Attributes, catalog);
            Tags = Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            System = Owner.AddComponent<AbilitySystem>();
            typeof(AbilitySystem).GetField("damageProfile", Private).SetValue(System, profile);
            Owner.AddComponent<AbilityMotionController2D>();
            Owner.SetActive(true);
            Logic = Own(ScriptableObject.CreateInstance<RelicLogic_ElectricAttributeBonus_Managed>());
            Logic.mode = mode;
            Logic.electricAddAttribute = ElectricAdd;
            Logic.maxHealthAttribute = MaxHealth;
            if (mode == Mode.MaxHealth || mode == Mode.NearbyElectrocutedCount)
                Logic.bonusPerUnitByLevel = new List<float> { 1f, 1f, 2f, 2f, 3f };
            Logic.electrocutedTag = Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset").grantedTags[0];
            Logic.triggerDashAbility = Load<AbilityDefinition>("Assets/_Project/Data/Abilities/Definitions/AD_Dash.asset");
            Context = new RelicContext
            {
                owner = Owner, abilitySystem = System, tagSystem = Tags, attributeSet = Attributes,
                effectRunner = Owner.GetComponent<GameplayEffectRunner>(), level = level,
                token = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>())
            };
            Logic.OnEquipped(Context);
        }

        private AttributeDefinition Attribute(float value)
        {
            var attribute = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
            attribute.defaultBaseValue = value;
            attribute.maxValue = 1000f;
            return attribute;
        }

        public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public void Set(AttributeDefinition attribute, float value) => Assert.That(Attributes.TrySetBaseValue(attribute, value, Owner), Is.True);
        public void TickElectric(float seconds) => Attributes.GetAttribute(ElectricAdd).Update(seconds);
        public void TickNearby() { foreach (var proc in new List<IRelicProc>(Procs)) proc.Tick(0.11f); }
        public void End(AbilitySpec spec, bool cancelled) => typeof(AbilitySystem).GetMethod("NotifyExecutionEnded", Private).Invoke(System, new object[] { spec, cancelled });
        public void Start(AbilitySpec spec) => typeof(AbilitySystem).GetMethod("NotifyExecutionStarted", Private).Invoke(System, new object[] { spec });
        public AbilitySpec NewAbility(bool movement)
        {
            var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
            definition.logic = Own(ScriptableObject.CreateInstance<TestAbilityLogic>());
            if (movement)
                definition.abilityTags.Add(Load<GameplayTag>("Assets/_Project/Resources/Tags/Ability.Movement.asset"));
            return new AbilitySpec(definition);
        }

        public Enemy CreateEnemy(float offset, GameplayEffect status)
        {
            var target = Own(Object.Instantiate(Load<GameObject>("Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab"),
                Owner.transform.position + Vector3.right * offset, Quaternion.identity));
            if (status != null)
                target.GetComponent<GameplayEffectRunner>().ApplyEffectSpec(new GameplayEffectSpec(status, Owner), target);
            return target.GetComponent<Enemy>();
        }

        public void Dispose()
        {
            Logic.OnUnequipped(Context);
            foreach (var value in owned)
                if (value is GameObject && value != null) Object.DestroyImmediate(value);
            foreach (var value in owned)
                if (value != null && value is not GameObject) Object.DestroyImmediate(value);
        }
    }
}

public sealed class ElectricChainExtensionPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void ThunderKnot_IsUnlockedRareThreeLevelRelic_WithLeveledExtensionTooltip()
    {
        var definition = AssetDatabase.LoadAssetAtPath<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_ThunderKnot.asset");
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(database, Is.Not.Null);
        Assert.That(definition.relicId, Is.EqualTo("Relic.Electric.ThunderKnot"));
        Assert.That(definition.rarity, Is.EqualTo(ItemRarity.Rare));
        Assert.That(definition.maxLevel, Is.EqualTo(3));
        Assert.That(definition.dropLevel, Is.EqualTo(1));
        Assert.That(definition.icon, Is.Not.Null);
        CollectionAssert.Contains(database.allRelics, definition);
        CollectionAssert.Contains(database.defaultUnlockedRelics, definition);
        var logic = definition.logic as RelicLogic_ChainShockExtension_Managed;
        Assert.That(logic, Is.Not.Null);
        Assert.That(logic.electrocutedStatusEffect, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameplayEffect>(
            "Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset")));
        Assert.That(logic.electrocutedStatusEffect.duration, Is.EqualTo(4f));
        CollectionAssert.AreEqual(new[] { 0.5f, 1f, 1.5f }, logic.extensionSecondsByLevel);
        for (int level = 1; level <= 3; level++)
        {
            string tooltip = logic.BuildTooltip(definition, level, null).effectText;
            Assert.That(tooltip, Does.Contain((0.5f * level).ToString("0.##")));
            Assert.That(tooltip, Does.Contain("4"));
            Assert.That(tooltip, Does.Not.Contain("{value}"));
        }
    }

    [TestCase(false, 1, 0.5f)]
    [TestCase(true, 1, 0.5f)]
    [TestCase(false, 2, 1f)]
    [TestCase(true, 2, 1f)]
    [TestCase(false, 3, 1.5f)]
    [TestCase(true, 3, 1.5f)]
    public void Extension_AddsOnlyToFollowers_RespectsCapAndInstigator_AndUnsubscribesOnCleanup(bool useSpec, int level, float extension)
    {
        using var fixture = new Fixture(level);
        fixture.Apply(useSpec, fixture.Owner);
        Assert.That(fixture.Remaining(0), Is.EqualTo(4f), "The fresh initial discharge keeps its ordinary four-second status.");
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.25f + extension), "Followers receive additional remaining time rather than a reset.");
        Assert.That(fixture.Remaining(2), Is.EqualTo(4f), "Extension cannot exceed the authored status duration.");
        Assert.That(fixture.Remaining(3), Is.EqualTo(2.5f), "The fourth target exceeds the three-target discharge limit.");
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(3));
        foreach (float damage in fixture.Damage.Hits.Values) Assert.That(damage, Is.EqualTo(5f));

        fixture.SetRemaining(0, 0.25f);
        fixture.Apply(useSpec, fixture.OtherSource);
        Assert.That(fixture.Remaining(0), Is.EqualTo(4f));
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.25f + extension), "Another attacker cannot borrow the owner's duration relic.");
        fixture.SetRemaining(2, 5f);
        fixture.Apply(useSpec, fixture.Owner);
        Assert.That(fixture.Remaining(1), Is.EqualTo(Mathf.Min(4f, 1.25f + 2f * extension)));
        Assert.That(fixture.Remaining(2), Is.EqualTo(5f), "Existing duration above the ordinary cap must not be reduced.");

        fixture.Logic.OnUnequipped(fixture.Context);
        fixture.SetRemaining(1, 1.25f);
        fixture.Apply(useSpec, fixture.Owner);
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.25f), "The baseline chain never refreshes duration without the relic.");
        fixture.Logic.OnRestoreAttached(fixture.Context);
        fixture.Logic.OnRestoreAttached(fixture.Context);
        fixture.Apply(useSpec, fixture.Owner);
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.25f + extension), "Restoring the same token twice must not duplicate the bonus.");
        Object.DestroyImmediate(fixture.Owner.GetComponent<RelicProcManager>());
        fixture.SetRemaining(1, 0.75f);
        fixture.Apply(useSpec, fixture.Owner);
        Assert.That(fixture.Remaining(1), Is.EqualTo(0.75f), "Destroying the manager must dispose the global duration registration.");
    }

    [Test]
    public void Extension_DoesNotRecreateExpiredOrSynchronouslyClearedDeadFollowerStatus()
    {
        using var fixture = new Fixture(3, enemyFollower: true);
        var follower = fixture.Targets[1];
        var runner = follower.GetComponent<GameplayEffectRunner>();
        fixture.SetRemaining(1, 0f);
        typeof(GameplayEffectRunner).GetMethod("Update", Private).Invoke(runner, null);
        fixture.Apply(true, fixture.Owner);
        Assert.That(runner.HasActiveEffect(fixture.Status, follower), Is.False);
        runner.ApplyEffectSpec(new GameplayEffectSpec(fixture.Status, fixture.Owner), follower);
        fixture.SetRemaining(1, 0.75f);
        fixture.Damage.OnHit = target =>
        {
            if (target == follower) target.GetComponent<Enemy>().RequestDeath();
        };
        fixture.Apply(true, fixture.Owner);
        Assert.That(follower.GetComponent<Enemy>().IsDead, Is.True);
        Assert.That(runner.HasActiveEffect(fixture.Status, follower), Is.False,
            "Death clears the status during damage; the later extension must not recreate it.");
        Assert.That(follower.GetComponent<TagSystem>().HasTag(fixture.Status.grantedTags[0]), Is.False);
    }

    public sealed class RecordingDamage : GameplayEffect, ISpecGameplayEffect
    {
        public GameplayTag MagnitudeKey;
        public readonly Dictionary<GameObject, float> Hits = new();
        public Action<GameObject> OnHit;
        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            Assert.That(spec.TryGetSetByCallerMagnitude(MagnitudeKey, out float magnitude), Is.True);
            Hits[target] = magnitude;
            OnHit?.Invoke(target);
        }
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Assert.Fail("Discharge damage must use its spec payload.");
        public override void Remove(GameObject target, GameObject instigator) { }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        private readonly GE_ElectricShockTrigger discharge;
        public readonly GameObject Owner, OtherSource;
        public readonly GameplayEffect Status;
        public readonly RecordingDamage Damage;
        public readonly RelicLogic_ChainShockExtension_Managed Logic;
        public readonly RelicContext Context;
        public readonly List<GameObject> Targets = new();

        public Fixture(int level, bool enemyFollower = false)
        {
            Status = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
            Assert.That(Status, Is.Not.Null);
            Assert.That(Status.duration, Is.EqualTo(4f));
            Owner = Own(new GameObject("Chain extension owner"));
            Owner.AddComponent<AttributeSet>();
            Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            var system = Owner.AddComponent<AbilitySystem>();
            OtherSource = Own(new GameObject("Unrelated chain attacker"));
            Logic = Own(ScriptableObject.CreateInstance<RelicLogic_ChainShockExtension_Managed>());
            Logic.electrocutedStatusEffect = Status;
            Context = new RelicContext
            {
                owner = Owner, abilitySystem = system, level = level,
                token = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>())
            };
            Logic.OnEquipped(Context);
            discharge = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GE_ElectricShockTrigger>(
                "Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")));
            Damage = Own(ScriptableObject.CreateInstance<RecordingDamage>());
            Damage.MagnitudeKey = Status.grantedTags[0];
            void Set(string field, object value) => typeof(GE_ElectricShockTrigger).GetField(field, Private).SetValue(discharge, value);
            Set("electrocutedStatusEffect", Status);
            Set("electrocutedTag", Status.grantedTags[0]);
            Set("electricDamageEffect", Damage);
            Set("electricDamageKey", Damage.MagnitudeKey);
            Set("chainRadius", 1.4f);
            Set("chainVfxPrefab", null);
            for (int i = 0; i < 4; i++)
            {
                GameObject target;
                Vector3 position = new(1000f + i, 1000f, 0f);
                if (i == 1 && enemyFollower)
                    target = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab"), position, Quaternion.identity));
                else
                {
                    target = Own(new GameObject("Chain extension target"));
                    target.transform.position = position;
                    target.AddComponent<TagSystem>();
                    target.AddComponent<GameplayEffectRunner>();
                    var collider = target.AddComponent<CircleCollider2D>();
                    collider.radius = 0.05f;
                    collider.isTrigger = true;
                    target.AddComponent<CombatHurtbox2D>();
                }
                Targets.Add(target);
                if (i > 0)
                {
                    target.GetComponent<GameplayEffectRunner>().ApplyEffectSpec(new GameplayEffectSpec(Status, Owner), target);
                    SetRemaining(i, i == 1 ? 1.25f : i == 2 ? 3.75f : 2.5f);
                }
            }
            Physics2D.SyncTransforms();
        }

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public float Remaining(int index) => Targets[index].GetComponent<GameplayEffectRunner>().GetRemainingTime(Status, Targets[index]);
        public void SetRemaining(int index, float seconds) => Targets[index].GetComponent<GameplayEffectRunner>().FindActiveEffect(Status, Targets[index]).TimeRemaining = seconds;
        public void Apply(bool useSpec, GameObject source)
        {
            if (useSpec) discharge.Apply(new GameplayEffectSpec(discharge, source), Targets[0]);
            else discharge.Apply(Targets[0], source);
        }
        public void Dispose()
        {
            Logic.OnUnequipped(Context);
            foreach (var target in Targets)
                if (target != null) target.GetComponent<GameplayEffectRunner>().ClearAllActiveEffects();
            foreach (var value in owned)
                if (value is GameObject && value != null) Object.DestroyImmediate(value);
            foreach (var value in owned)
                if (value != null && value is not GameObject) Object.DestroyImmediate(value);
        }
    }
}

public sealed class ElectricTargetsAndParalysisPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(1, 4)]
    [TestCase(2, 5)]
    [TestCase(3, 6)]
    public void TargetBonus_IsOwnerScoped_UniqueAndSnapshotted_AndRestoresWithoutDuplicates(int level, int expected)
    {
        using var fixture = new Fixture();
        var logic = fixture.Own(ScriptableObject.CreateInstance<RelicLogic_ElectricChainTargets_Managed>());
        var context = fixture.Equip(logic, level);
        var hurtbox = fixture.Targets[1].GetComponent<CombatHurtbox2D>();
        var firstCollider = fixture.Targets[1].GetComponent<CircleCollider2D>();
        var duplicateCollider = fixture.Targets[1].AddComponent<CircleCollider2D>();
        duplicateCollider.radius = 0.05f;
        duplicateCollider.isTrigger = true;
        typeof(CombatHurtbox2D).GetField("ownedColliders", Private).SetValue(hurtbox,
            new Collider2D[] { firstCollider, duplicateCollider });
        Physics2D.SyncTransforms();
        fixture.Apply(fixture.Owner);
        Assert.That(fixture.Hits, Has.Count.EqualTo(expected));
        Assert.That(new HashSet<GameObject>(fixture.Hits), Has.Count.EqualTo(expected));
        for (int i = 1; i < expected; i++)
            Assert.That(fixture.Remaining(i), Is.EqualTo(1.25f), "Extra chain targets retain the baseline no-renewal status policy.");
        fixture.Apply(fixture.OtherSource);
        Assert.That(fixture.Hits, Has.Count.EqualTo(3));
        Assert.That(fixture.BaseLimit, Is.EqualTo(3));
        using (ElectricShockApplications.Register(application =>
        {
            if (application.Instigator == fixture.Owner) logic.OnUnequipped(context);
        }))
        {
            fixture.Apply(fixture.Owner);
            Assert.That(fixture.Hits, Has.Count.EqualTo(expected), "Removing the relic during the initial status must not change this discharge's captured count.");
        }
        fixture.Apply(fixture.Owner);
        Assert.That(fixture.Hits, Has.Count.EqualTo(3));
        logic.OnRestoreAttached(context);
        logic.OnRestoreAttached(context);
        fixture.Apply(fixture.Owner);
        Assert.That(fixture.Hits, Has.Count.EqualTo(expected));
        Object.DestroyImmediate(fixture.Owner.GetComponent<RelicProcManager>());
        fixture.Apply(fixture.Owner);
        Assert.That(fixture.Hits, Has.Count.EqualTo(3));
        Assert.That((int)typeof(GE_ElectricShockTrigger).GetField("maxTargetsPerDischarge", Private).GetValue(
            AssetDatabase.LoadAssetAtPath<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")), Is.EqualTo(3));
    }

    [Test]
    public void TargetBonus_InventoryUpgradeAndRemoval_ChangeOnlyTheOwnersResolvedLimit()
    {
        using var fixture = new Fixture();
        var inventory = fixture.Owner.AddComponent<RelicInventory>();
        var definition = fixture.Own(ScriptableObject.CreateInstance<RelicDefinition>());
        definition.relicId = "ElectricRelicTest.Targets";
        definition.maxLevel = 3;
        definition.logic = fixture.Own(ScriptableObject.CreateInstance<RelicLogic_ElectricChainTargets_Managed>());
        try
        {
            for (int level = 1; level <= 3; level++)
            {
                Assert.That(inventory.TryAcquireOrUpgrade(definition), Is.True);
                fixture.Apply(fixture.Owner);
                Assert.That(fixture.Hits, Has.Count.EqualTo(3 + level));
                fixture.Apply(fixture.OtherSource);
                Assert.That(fixture.Hits, Has.Count.EqualTo(3));
            }
            Assert.That(inventory.TryAcquireOrUpgrade(definition), Is.False);
        }
        finally { inventory.RemoveAt(0); }
        fixture.Apply(fixture.Owner);
        Assert.That(fixture.Hits, Has.Count.EqualTo(3));
    }

    [Test]
    public void Paralysis_TriggersOnDirectNewAndRenewedShock_ButNotFollowersExtensionOtherAttackersOrRestore()
    {
        using var fixture = new Fixture(enemyTargets: true);
        var logic = fixture.Own(ScriptableObject.CreateInstance<RelicLogic_ShockParalysis_Managed>());
        var context = fixture.Equip(logic, 1);
        var extension = fixture.Own(ScriptableObject.CreateInstance<RelicLogic_ChainShockExtension_Managed>());
        extension.electrocutedStatusEffect = fixture.Status;
        fixture.Equip(extension, 1);
        var notifications = new List<ElectricShockApplicationContext>();
        using var observer = ElectricShockApplications.Register(notifications.Add);
        fixture.Apply(fixture.Owner);
        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications[0].Target, Is.EqualTo(fixture.Targets[0]));
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.True);
        for (int i = 1; i < 3; i++) Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[i]), Is.False);
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.75f));
        CombatHitPause2D.ClearParalysis(fixture.Targets[0]);
        fixture.Apply(fixture.Owner);
        Assert.That(notifications, Has.Count.EqualTo(2), "Renewing an already-present shock is still a direct application.");
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.True);
        Assert.That(fixture.Remaining(1), Is.EqualTo(2.25f));
        CombatHitPause2D.ClearParalysis(fixture.Targets[0]);
        fixture.Apply(fixture.OtherSource);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.False);
        var runner = fixture.Targets[0].GetComponent<GameplayEffectRunner>();
        runner.ClearAllActiveEffects();
        int notificationsBeforeRestore = notifications.Count;
        runner.RestoreActiveEffectSnapshots(new[] { new ActiveGameplayEffectSnapshot
        {
            effectId = fixture.Status.name, remainingTime = 2f, stackCount = 1
        } }, _ => fixture.Status, fixture.Targets[0], fixture.Owner);
        Assert.That(fixture.Remaining(0), Is.EqualTo(2f));
        Assert.That(notifications, Has.Count.EqualTo(notificationsBeforeRestore));
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.False);
        logic.OnUnequipped(context);
        fixture.Apply(fixture.Owner);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.False);
        logic.OnRestoreAttached(context);
        fixture.Apply(fixture.Owner);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.True);
        CombatHitPause2D.ClearParalysis(fixture.Targets[0]);
        Object.DestroyImmediate(fixture.Owner.GetComponent<RelicProcManager>());
        fixture.Apply(fixture.Owner);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.False);
    }

    [Test]
    public void Paralysis_SkipsBossImmuneAndDeadEnemies_AndFailedStatusApplications()
    {
        using var fixture = new Fixture(enemyTargets: true);
        fixture.Equip(fixture.Own(ScriptableObject.CreateInstance<RelicLogic_ShockParalysis_Managed>()), 1);
        fixture.Targets[0].AddComponent<ElectricRelicBossImmunityProbe>();
        fixture.Apply(fixture.Owner);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.False);
        Object.DestroyImmediate(fixture.Targets[0].GetComponent<ElectricRelicBossImmunityProbe>());
        fixture.Targets[0].GetComponent<Enemy>().RequestDeath();
        fixture.Apply(fixture.Owner);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Targets[0]), Is.False);
        int notifications = 0;
        using var observer = ElectricShockApplications.Register(_ => notifications++);
        typeof(GE_ElectricShockTrigger).GetField("electrocutedStatusEffect", Private).SetValue(fixture.Discharge, null);
        fixture.Apply(fixture.Owner);
        Assert.That(notifications, Is.Zero);
    }

    [TestCase("ResonanceBell", 3)]
    [TestCase("TinglingShackles", 1)]
    public void AuthoredTargetAndParalysisRelics_AreUnlockedAndHaveApprovedLeveledTooltips(string name, int levels)
    {
        var definition = AssetDatabase.LoadAssetAtPath<RelicDefinition>("Assets/_Project/Data/Items/Relics/Definitions/RD_" + name + ".asset");
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.relicId, Is.EqualTo("Relic.Electric." + name));
        Assert.That(definition.rarity, Is.EqualTo(ItemRarity.Rare));
        Assert.That(definition.maxLevel, Is.EqualTo(levels));
        Assert.That(definition.dropLevel, Is.EqualTo(1));
        Assert.That(definition.icon, Is.Not.Null);
        CollectionAssert.Contains(database.allRelics, definition);
        CollectionAssert.Contains(database.defaultUnlockedRelics, definition);
        if (name == "ResonanceBell")
        {
            Assert.That(definition.logic, Is.TypeOf<RelicLogic_ElectricChainTargets_Managed>());
            for (int level = 1; level <= 3; level++)
                Assert.That(definition.logic.BuildTooltip(definition, level, null).effectText, Does.Contain("+" + level));
        }
        else
        {
            Assert.That(definition.logic, Is.TypeOf<RelicLogic_ShockParalysis_Managed>());
            Assert.That(((RelicLogic_ShockParalysis_Managed)definition.logic).paralysisSeconds, Is.EqualTo(0.1f));
            Assert.That(definition.logic.BuildTooltip(definition, 1, null).effectText, Does.Contain("0.1"));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        private readonly List<(RelicLogic logic, RelicContext context)> equipped = new();
        public readonly GameObject Owner, OtherSource;
        public readonly GameplayEffect Status;
        public readonly GE_ElectricShockTrigger Discharge;
        public readonly List<GameObject> Targets = new(), Hits = new();
        public int BaseLimit => (int)typeof(GE_ElectricShockTrigger).GetField("maxTargetsPerDischarge", Private).GetValue(Discharge);
        public Fixture(bool enemyTargets = false)
        {
            Owner = Own(new GameObject("Electric relic hook owner"));
            Owner.AddComponent<AttributeSet>();
            Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            Owner.AddComponent<AbilitySystem>();
            OtherSource = Own(new GameObject("Other electric hook source"));
            Status = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
            Discharge = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")));
            var damage = Own(ScriptableObject.CreateInstance<ElectricChainExtensionPlayModeTests.RecordingDamage>());
            damage.MagnitudeKey = Status.grantedTags[0];
            damage.OnHit = Hits.Add;
            typeof(GE_ElectricShockTrigger).GetField("electricDamageEffect", Private).SetValue(Discharge, damage);
            typeof(GE_ElectricShockTrigger).GetField("electricDamageKey", Private).SetValue(Discharge, damage.MagnitudeKey);
            typeof(GE_ElectricShockTrigger).GetField("chainRadius", Private).SetValue(Discharge, 1.4f);
            typeof(GE_ElectricShockTrigger).GetField("chainVfxPrefab", Private).SetValue(Discharge, null);
            for (int i = 0; i < (enemyTargets ? 3 : 7); i++)
            {
                var position = new Vector3(2000f + i, 2000f, 0f);
                GameObject target;
                if (enemyTargets)
                    target = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab"), position, Quaternion.identity));
                else
                {
                    target = Own(new GameObject("Electric hook target"));
                    target.transform.position = position;
                    target.AddComponent<TagSystem>();
                    target.AddComponent<GameplayEffectRunner>();
                    var collider = target.AddComponent<CircleCollider2D>();
                    collider.radius = 0.05f;
                    collider.isTrigger = true;
                    target.AddComponent<CombatHurtbox2D>();
                }
                Targets.Add(target);
                if (i > 0)
                {
                    var runner = target.GetComponent<GameplayEffectRunner>();
                    runner.ApplyEffectSpec(new GameplayEffectSpec(Status, Owner), target);
                    runner.FindActiveEffect(Status, target).TimeRemaining = 1.25f;
                }
            }
            Physics2D.SyncTransforms();
        }
        public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public RelicContext Equip(RelicLogic logic, int level)
        {
            var context = new RelicContext
            {
                owner = Owner, abilitySystem = Owner.GetComponent<AbilitySystem>(), level = level,
                token = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>())
            };
            equipped.Add((logic, context));
            logic.OnEquipped(context);
            return context;
        }
        public void Apply(GameObject source) { Hits.Clear(); Discharge.Apply(new GameplayEffectSpec(Discharge, source), Targets[0]); }
        public float Remaining(int index) => Targets[index].GetComponent<GameplayEffectRunner>().GetRemainingTime(Status, Targets[index]);
        public void Dispose()
        {
            foreach (var entry in equipped) entry.logic.OnUnequipped(entry.context);
            foreach (var target in Targets)
                if (target != null) target.GetComponent<GameplayEffectRunner>().ClearAllActiveEffects();
            foreach (var value in owned)
                if (value is GameObject && value != null) Object.DestroyImmediate(value);
            foreach (var value in owned)
                if (value != null && value is not GameObject) Object.DestroyImmediate(value);
        }
    }
}

public sealed class ElectricRelicBossImmunityProbe : MonoBehaviour, ICombatHitPauseImmune { }
#endif
