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

public sealed class LevelRewardStatOfferPlayModeTests
{
    private const string Root = "Assets/_Project/Data/Progression/Leveling/";
    private List<LevelRewardDefinitionSO> rewards;

    [SetUp]
    public void SetUp() => rewards = AssetDatabase.LoadAssetAtPath<LevelRewardCatalogSO>(Root + "Rewards/Catalog/LevelRewardCatalog.asset")
        .Rewards.OrderBy(value => value.RewardId, StringComparer.Ordinal).ToList();

    [Test]
    public void AuthoredCatalog_PreservesSeventeenOneShotsAndAddsTwelveThreeTierStatFamilies()
    {
        Assert.That(rewards.Count, Is.EqualTo(53));
        Assert.That(rewards.Select(value => value.RewardId).Distinct().Count(), Is.EqualTo(53));
        var stats = rewards.Where(value => StatIncreaseLevelRewardEffectSO.Find(value) != null).ToList();
        Assert.That(stats.Count, Is.EqualTo(36));
        Assert.That(stats.All(value => value.AllowMultipleSelections), Is.True);
        Assert.That(rewards.Except(stats).All(value => !value.AllowMultipleSelections), Is.True);
        var families = stats.GroupBy(LevelRewardOfferRoller.Kind).ToList();
        Assert.That(families.Count, Is.EqualTo(12));
        foreach (var family in families)
        {
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, family.Select(value => StatIncreaseLevelRewardEffectSO.Find(value).Tier));
            Assert.That(family.All(value => value.Icon != null && value.Icon.rect.size == new Vector2(80, 128)), Is.True);
            Assert.That(family.All(value => AssetDatabase.GetAssetPath(value.Icon).EndsWith("/Stat_Card.png")), Is.True);
        }
        Assert.That(families.Any(family => StatIncreaseLevelRewardEffectSO.Find(family.First()).Stat == StatId.MaxHealth), Is.False);
    }

    [Test]
    public void SixThousandOffers_HaveDistinctKindsAndExpectedCompositionTiersAndElementGroupWeight()
    {
        var output = new List<string>();
        var previous = new HashSet<string>();
        var byId = rewards.ToDictionary(value => value.RewardId);
        int twoStat = 0, singleStat = 0, singleElement = 0, statTotal = 0;
        var tiers = new int[3];
        var random = new System.Random(102930);
        for (int i = 0; i < 6000; i++)
        {
            Assert.That(LevelRewardOfferRoller.Roll(rewards, previous, random, output), Is.True);
            Assert.That(output.Count, Is.EqualTo(3));
            Assert.That(output.Select(id => LevelRewardOfferRoller.Kind(byId[id])).Distinct().Count(), Is.EqualTo(3));
            var stats = output.Select(id => StatIncreaseLevelRewardEffectSO.Find(byId[id])).Where(value => value != null).ToList();
            Assert.That(stats.Count, Is.InRange(1, 2));
            if (stats.Count == 2) twoStat++;
            else { singleStat++; if (stats[0].IsElement) singleElement++; }
            foreach (var stat in stats) { tiers[stat.Tier]++; statTotal++; }
        }
        Assert.That(twoStat / 6000f, Is.InRange(.58f, .62f));
        Assert.That(tiers[0] / (float)statTotal, Is.InRange(.68f, .72f));
        Assert.That(tiers[1] / (float)statTotal, Is.InRange(.23f, .27f));
        Assert.That(tiers[2] / (float)statTotal, Is.InRange(.04f, .06f));
        Assert.That(singleElement / (float)singleStat, Is.InRange(.085f, .14f));
    }

    [Test]
    public void SeedAndOfferIdentity_FixKindsValuesAndOrder()
    {
        var first = new List<string>();
        var second = new List<string>();
        var previous = new HashSet<string>();
        LevelRewardOfferRoller.Roll(rewards, previous, new System.Random(9876), first);
        LevelRewardOfferRoller.Roll(rewards, previous, new System.Random(9876), second);
        CollectionAssert.AreEqual(first, second);
    }

    [TestCase("Attack", 3)]
    [TestCase("CritDamage", 3)]
    [TestCase("SkillDamage", 1)]
    public void AuthoredCardView_ProjectsNewArtworkValueAndSelectionId(string family, int tier)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        var viewType = Type.GetType("LevelRewardCardView, UI", true);
        var template = prefab.GetComponentsInChildren(viewType, true).First();
        var holder = new GameObject("Stat card presentation test");
        holder.SetActive(false);
        try
        {
            var instance = Object.Instantiate(template.gameObject, holder.transform);
            var view = instance.GetComponent(viewType);
            var definition = AssetDatabase.LoadAssetAtPath<LevelRewardDefinitionSO>(Root + "Rewards/Definitions/Reward_Stat_" + family + "_" + tier + ".asset");
            string selected = null;
            viewType.GetMethod("Bind").Invoke(view, new object[] { definition, 0, null, new Action<string>(id => selected = id) });
            var serialized = new SerializedObject(view);
            var image = (Component)serialized.FindProperty("iconImage").objectReferenceValue;
            Assert.That(image.gameObject, Is.EqualTo(instance));
            Assert.That(image.GetType().GetProperty("sprite").GetValue(image), Is.EqualTo(definition.Icon));
            var title = serialized.FindProperty("displayNameText").objectReferenceValue;
            Assert.That(title.GetType().GetProperty("text").GetValue(title), Is.EqualTo(definition.DisplayName));
            var description = serialized.FindProperty("descriptionText").objectReferenceValue;
            Assert.That(description.GetType().GetProperty("text").GetValue(description), Is.EqualTo(definition.Description));
            var button = viewType.GetProperty("SelectButton").GetValue(view);
            var click = button.GetType().GetProperty("onClick").GetValue(button);
            click.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(click, null);
            Assert.That(selected, Is.EqualTo(definition.RewardId));
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void RerollsPreferNovelKinds_AndCannotUseAnotherAttackTierAsANewKind()
    {
        var attack = rewards.Single(value => value.RewardId == "level_reward.stat.attack.t1");
        var fire = rewards.Single(value => value.RewardId == "level_reward.stat.fire.t3");
        var special = rewards.First(value => StatIncreaseLevelRewardEffectSO.Find(value) == null);
        var previous = new HashSet<string>(new[] { attack, fire, special }.Select(LevelRewardOfferRoller.Kind));
        var byId = rewards.ToDictionary(value => value.RewardId);
        var result = new List<string>();
        for (int seed = 0; seed < 100; seed++)
        {
            LevelRewardOfferRoller.Roll(rewards, previous, new System.Random(seed), result);
            Assert.That(result.All(id => !previous.Contains(LevelRewardOfferRoller.Kind(byId[id]))), Is.True);
        }
        Assert.That(LevelRewardOfferRoller.Kind(attack), Is.EqualTo(LevelRewardOfferRoller.Kind(
            rewards.Single(value => value.RewardId == "level_reward.stat.attack.t3"))));
    }

    [TestCase(0, 3)]
    [TestCase(1, 3)]
    [TestCase(2, 3)]
    public void ExhaustedSpecials_BackfillWithDistinctStats(int specialCount, int expectedCards)
    {
        var eligible = rewards.Where(value => StatIncreaseLevelRewardEffectSO.Find(value) != null)
            .Concat(rewards.Where(value => StatIncreaseLevelRewardEffectSO.Find(value) == null).Take(specialCount)).ToList();
        var result = new List<string>();
        var byId = eligible.ToDictionary(value => value.RewardId);
        for (int seed = 0; seed < 40; seed++)
        {
            LevelRewardOfferRoller.Roll(eligible, new HashSet<string>(), new System.Random(seed), result);
            Assert.That(result.Count, Is.EqualTo(expectedCards));
            Assert.That(result.Select(id => LevelRewardOfferRoller.Kind(byId[id])).Distinct().Count(), Is.EqualTo(expectedCards));
            if (specialCount == 0) Assert.That(result.All(id => StatIncreaseLevelRewardEffectSO.Find(byId[id]) != null), Is.True);
        }
    }

    [TestCase(0, 1)]
    [TestCase(39, 1)]
    [TestCase(40, 2)]
    [TestCase(220, 5)]
    [TestCase(2540, 17)]
    [TestCase(3570, 20)]
    [TestCase(3810, 20)]
    public void TwentyLevelCurve_PreservesOverflowAndCalibratesTheOldBudget(int experience, int level)
    {
        var config = AssetDatabase.LoadAssetAtPath<LevelProgressionConfigSO>(Root + "LevelProgressionConfig.asset");
        Assert.That(config.MaxLevel, Is.EqualTo(20));
        var state = new LevelProgressionState();
        LevelProgressionCalculator.GrantExperience(state, experience, config.NextLevelRequirements);
        Assert.That(state.level, Is.EqualTo(level));
        Assert.That(state.pendingRewardCount, Is.EqualTo(level - 1));
        if (level == 20)
        {
            LevelProgressionCalculator.GrantExperience(state, 100000, config.NextLevelRequirements);
            Assert.That(state.pendingRewardCount, Is.EqualTo(19));
            Assert.That(state.currentExperience, Is.Zero);
        }
    }

    [TestCase("CritChance", .02f, .04f, .06f)]
    [TestCase("CritDamage", .04f, .08f, .12f)]
    [TestCase("AttackSpeed", .02f, .04f, .06f)]
    [TestCase("NormalDamage", .02f, .04f, .06f)]
    [TestCase("SkillDamage", .02f, .04f, .06f)]
    public void AuthoredCombatStatTiers_MatchTheApprovedBalance(string family, float first, float second, float third)
    {
        var values = Enumerable.Range(1, 3).Select(tier => AssetDatabase.LoadAssetAtPath<StatIncreaseLevelRewardEffectSO>(
            Root + "Rewards/Effects/Effect_Stat_" + family + "_" + tier + ".asset").Value).ToArray();
        CollectionAssert.AreEqual(new[] { first, second, third }, values);
    }
}

public sealed class LevelRewardStatRuntimePlayModeTests
{
    private const string Rewards = "Assets/_Project/Data/Progression/Leveling/Rewards/";
    private const string Attributes = "Assets/_Project/Data/Attributes/Definitions/";
    private readonly List<Object> owned = new();
    private GameObject owner;
    private AttributeSet attributes;
    private AbilitySystem system;
    private PlayerCombatInput2D input;
    private PlayerInteractor2D player, originalPlayer;
    private GamePlayData data;
    private bool originalActive;
    private LevelProgressionState originalProgression;

    [SetUp]
    public void SetUp()
    {
        data = GamePlayDataManager.EnsureInstance().Data;
        originalActive = data.isRunActive;
        originalProgression = data.levelProgression;
        originalPlayer = PlayerRuntimeRegistry.CurrentPlayer;
        data.isRunActive = true;
        data.levelProgression = new LevelProgressionState { pendingRewardCount = 100 };
        owner = Own(new GameObject("Level reward stat owner"));
        owner.SetActive(false);
        attributes = ConfigureAttributes(owner);
        owner.AddComponent<TagSystem>();
        owner.AddComponent<GameplayEffectRunner>();
        system = owner.AddComponent<AbilitySystem>();
        Set(system, "damageProfile", Load<DamageProfileDefinition>("Assets/_Project/Data/Attributes/Formulas/DamageProfile.asset"));
        input = owner.AddComponent<PlayerCombatInput2D>();
        input.enabled = false;
        var sensor = new GameObject("InteractionSensor");
        sensor.transform.SetParent(owner.transform, false);
        sensor.AddComponent<BoxCollider2D>();
        sensor.AddComponent<PlayerInteractionSensor2D>();
        player = owner.AddComponent<PlayerInteractor2D>();
        player.enabled = false;
        owner.SetActive(true);
        RunLevelRewards.RegisterCatalog(Load<LevelRewardCatalogSO>(Rewards + "Catalog/LevelRewardCatalog.asset"));
        PlayerRuntimeRegistry.Register(player);
    }

    [TearDown]
    public void TearDown()
    {
        if (data != null) { data.isRunActive = false; RunLevelRewards.RebuildActiveEffects(); }
        if (player != null) PlayerRuntimeRegistry.Unregister(player);
        if (data != null) { data.isRunActive = originalActive; data.levelProgression = originalProgression; }
        if (originalPlayer != null) PlayerRuntimeRegistry.Register(originalPlayer);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [TestCase("Attack")]
    [TestCase("CritChance")]
    [TestCase("CritDamage")]
    [TestCase("Evasion")]
    [TestCase("AttackSpeed")]
    [TestCase("MoveSpeed")]
    [TestCase("NormalDamage")]
    [TestCase("SkillDamage")]
    [TestCase("Fire")]
    [TestCase("Electric")]
    [TestCase("Poison")]
    [TestCase("Bleed")]
    public void RepeatSelections_StackSurviveJsonAndPlayerRebuildAndRemoveOnlyOneSelection(string family)
    {
        var reward = Reward(family, 1);
        var effect = StatIncreaseLevelRewardEffectSO.Find(reward);
        var attribute = Field<AttributeDefinition>(effect, "attribute");
        var type = Field<ModifierType>(effect, "modifierType");
        float before = attributes.GetAttributeValue(attribute);
        for (int i = 0; i < 3; i++) Assert.That(RunLevelRewards.TrySelect(reward, out var error), Is.True, error);
        float Expected(int count) => type == ModifierType.Flat ? before + effect.Value * count : before * (1f + effect.Value * count);
        Assert.That(attributes.GetAttributeValue(attribute), Is.EqualTo(Expected(3)).Within(.0001f));

        string json = JsonUtility.ToJson(data.levelProgression);
        PlayerRuntimeRegistry.Unregister(player);
        Assert.That(attributes.GetAttributeValue(attribute), Is.EqualTo(before).Within(.0001f));
        data.levelProgression = JsonUtility.FromJson<LevelProgressionState>(json);
        PlayerRuntimeRegistry.Register(player);
        RunLevelRewards.RebuildActiveEffects();
        Assert.That(attributes.GetAttributeValue(attribute), Is.EqualTo(Expected(3)).Within(.0001f));
        Assert.That(RunLevelRewards.TryRemoveSelection(data.levelProgression.selectedRewards[1], out var failure), Is.True, failure);
        Assert.That(attributes.GetAttributeValue(attribute), Is.EqualTo(Expected(2)).Within(.0001f));
        data.levelProgression.Reset();
        RunLevelRewards.RebuildActiveEffects();
        Assert.That(attributes.GetAttributeValue(attribute), Is.EqualTo(before).Within(.0001f));
    }

    [Test]
    public void OfferReopenAndJsonRestore_PreserveCandidateIdsTiersAndRerollUsage()
    {
        data.levelProgression.rewardRandomSeed = 214358;
        Assert.That(RunLevelRewardOffers.TryEnsureOffer(5, out var error), Is.True, error);
        var ids = data.levelProgression.activeRewardOffer.candidateRewardIds.ToArray();
        Assert.That(RunLevelRewardOffers.TryEnsureOffer(5, out error), Is.True, error);
        CollectionAssert.AreEqual(ids, data.levelProgression.activeRewardOffer.candidateRewardIds);
        var previousKinds = RunLevelRewardOffers.CurrentCandidates.Select(LevelRewardOfferRoller.Kind).ToArray();
        Assert.That(RunLevelRewardOffers.TryReroll(out error), Is.True, error);
        Assert.That(RunLevelRewardOffers.CurrentCandidates.All(value => !previousKinds.Contains(LevelRewardOfferRoller.Kind(value))), Is.True);
        ids = data.levelProgression.activeRewardOffer.candidateRewardIds.ToArray();
        data.levelProgression = JsonUtility.FromJson<LevelProgressionState>(JsonUtility.ToJson(data.levelProgression));
        Assert.That(RunLevelRewardOffers.TryEnsureOffer(5, out error), Is.True, error);
        CollectionAssert.AreEqual(ids, data.levelProgression.activeRewardOffer.candidateRewardIds);
        Assert.That(RunLevelRewardOffers.RerollsUsed, Is.EqualTo(1));
    }

    [Test]
    public void CriticalAndEvasionOverflow_RemainsSelectableAndRawValuesExceedOne()
    {
        for (int i = 0; i < 20; i++) Assert.That(RunLevelRewards.TrySelect(Reward("CritChance", 3), out var error), Is.True, error);
        for (int i = 0; i < 40; i++) Assert.That(RunLevelRewards.TrySelect(Reward("Evasion", 3), out var error), Is.True, error);
        var provider = AbilityStatProviderFactory.Create(system);
        Assert.That(provider.Get(StatId.CritChanceFinal), Is.GreaterThan(1.2f));
        Assert.That(provider.Get(StatId.EvasionFinal), Is.GreaterThan(1.3f));
        Assert.That(Reward("CritChance", 3).CanSelect(new LevelRewardEligibilityContext(player, data.levelProgression), out _), Is.True);
        RunLevelRewards.RebuildActiveEffects();
        Assert.That(provider.Get(StatId.CritChanceFinal), Is.GreaterThan(1.2f));
        Assert.That(DamageFormulaUtil.PostProcess(provider, 10f, 0f, .999f).isCrit, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NormalAndSkillCards_ChangeActualHpDamageAndFreezeAnExistingPayload(bool skill)
    {
        var ability = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        Field<HashSet<AbilityDefinition>>(input, skill ? "knownWeaponSkillAbilities" : "knownBasicAttackAbilities").Add(ability);
        var reward = Reward(skill ? "SkillDamage" : "NormalDamage", 3);
        Assert.That(RunLevelRewards.TrySelect(reward, out var error), Is.True, error);
        var fire = Load<GameplayTag>("Assets/_Project/Resources/Tags/Element.Fire.asset");
        var originalElements = new[] { new ElementDamageResult { elementType = fire, damage = 5f } };
        var snapshot = new CombatDamageSnapshot(10f, 2f, 3f, false, Array.Empty<ElementDamageResult>(), true, originalElements);
        var damage = Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_Damage_Spec.asset");
        var payload = CombatHitPayload.FromSnapshot(system, new AbilitySpec(ability), damage, null, snapshot, null, owner);
        Assert.That(payload.TotalHpDamage, Is.EqualTo(15.9f).Within(.0001f));
        Assert.That(originalElements[0].damage, Is.EqualTo(5f));
        Assert.That(payload.finalStaggerBuildUp, Is.EqualTo(2f));
        Assert.That(RunLevelRewards.TrySelect(reward, out error), Is.True, error);
        Assert.That(payload.TotalHpDamage, Is.EqualTo(15.9f).Within(.0001f));

        var target = Own(new GameObject("Stat reward damage target"));
        target.SetActive(false);
        var targetAttributes = ConfigureAttributes(target);
        target.SetActive(true);
        targetAttributes.TrySetBaseValue(Load<AttributeDefinition>(Attributes + "MaxHealthAttribute.asset"), 100f, target);
        var health = Load<AttributeDefinition>(Attributes + "HealthAttribute.asset");
        targetAttributes.TrySetBaseValue(health, 100f, target);
        Assert.That(CombatHitPayloadApplier.Apply(target, payload), Is.True);
        Assert.That(targetAttributes.GetAttributeValue(health), Is.EqualTo(84.1f).Within(.0001f));
    }

    [Test]
    public void UnknownDamageSources_DoNotBorrowNormalOrSkillBonuses()
    {
        RunLevelRewards.TrySelect(Reward("NormalDamage", 3), out _);
        RunLevelRewards.TrySelect(Reward("SkillDamage", 3), out _);
        var unknown = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var snapshot = new CombatDamageSnapshot(10f, 2f, 3f, false);
        Assert.That(CombatAttackDamageScaling.Capture(snapshot, system, new AbilitySpec(unknown)).FinalHpDamage, Is.EqualTo(10f));
        Assert.That(CombatAttackDamageScaling.Capture(snapshot, system, null).FinalHpDamage, Is.EqualTo(10f));
    }

    [Test]
    public void OddIron_PreservesFixedNoncriticalPhysicalDamageWhileItsElementBonusUsesTheAttackCategory()
    {
        var ability = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        Field<HashSet<AbilityDefinition>>(input, "knownBasicAttackAbilities").Add(ability);
        Assert.That(RunLevelRewards.TrySelect(Reward("NormalDamage", 3), out var error), Is.True, error);
        Assert.That(RunLevelRewards.TrySelect(Reward("Fire", 1), out error), Is.True, error);
        var utility = typeof(WeaponDefinition).Assembly.GetType("UnityGAS.Sample.OddIronAbilityUtility", true);
        var payload = (CombatHitPayload)utility.GetMethod("BuildFixedPayload", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { system, new AbilitySpec(ability),
                new DamagePayloadConfig { includeElementBuildUp = false, includeStaggerBuildUp = false },
                Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_Damage_Spec.asset"), null, 2f, 0f, 0f });
        Assert.That(payload.finalHpDamage, Is.EqualTo(2f));
        Assert.That(payload.isCriticalHit, Is.False);
        Assert.That(payload.elementHpDamages.Single().damage, Is.EqualTo(1.06f).Within(.0001f));
        Assert.That(payload.TotalHpDamage, Is.EqualTo(3.06f).Within(.0001f));
    }

    private static AttributeSet ConfigureAttributes(GameObject target)
    {
        var attributes = target.AddComponent<AttributeSet>();
        Set(attributes, "attributeCatalog", Load<AttributeCatalogSO>(Attributes + "AttributeCatalog.asset"));
        Set(attributes, "baseInitProfile", Load<AttributeInitProfileSO>("Assets/_Project/Data/Attributes/InitProfiles/BASEAttributeInitProfile.asset"));
        Set(attributes, "overrideInitProfiles", new[] { Load<AttributeInitProfileSO>("Assets/_Project/Data/Attributes/InitProfiles/PlayerAttributeOverrideProfile.asset") });
        return attributes;
    }
    private static LevelRewardDefinitionSO Reward(string family, int tier) =>
        Load<LevelRewardDefinitionSO>(Rewards + "Definitions/Reward_Stat_" + family + "_" + tier + ".asset");
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException(path);
    private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
#endif
