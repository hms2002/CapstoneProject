#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

// Responsibility: verify authored monster HP and approved weapon damage while preserving stat-growth behavior.
public sealed class MonsterHealthAndWeaponSkillBalancePlayModeTests
{
    private const string Prefabs = "Assets/_Project/Prefabs/Monsters/";
    private const string LogicData = "Assets/_Project/Data/Items/Weapons/LogicData/";
    private const string Formulas = "Assets/_Project/Data/Attributes/Formulas/";

    [TestCase("CommonCorridor/GoblinWarrior.prefab", 45f, 1f)]
    [TestCase("CommonCorridor/GoblinGunner.prefab", 25f, 1f)]
    [TestCase("CommonCorridor/GoblinTank.prefab", 180f, 0.5f)]
    [TestCase("CommonCorridor/LizardWarrior.prefab", 45f, 1f)]
    [TestCase("CommonCorridor/LizardMage.prefab", 30f, 1f)]
    [TestCase("CommonCorridor/ArcaneMeleeGolem.prefab", 45f, 1f)]
    [TestCase("CommonCorridor/ArcaneTankGolem.prefab", 180f, 0.5f)]
    [TestCase("SlimeCorridor/Pawn.prefab", 15f, 1f)]
    [TestCase("SlimeCorridor/Knight.prefab", 45f, 1f)]
    [TestCase("SlimeCorridor/Bishop.prefab", 30f, 1f)]
    [TestCase("SlimeCorridor/Rook.prefab", 90f, 1f)]
    [TestCase("SlimeCorridor/Wizard.prefab", 30f, 1f)]
    [TestCase("ShadowCorridor/ShadowMonster.prefab", 45f, 1f)]
    [TestCase("ShadowCorridor/ShadowServant/ShadowServant.prefab", 45f, 1f)]
    [TestCase("ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab", 75f, 1f)]
    public void MonsterAwakeAndDifficulty_PreserveAuthoredHp(string relativePath, float baseHp, float roleMultiplier)
    {
        GameObject prefab = Load<GameObject>(Prefabs + relativePath);
        GameObject instance = Object.Instantiate(prefab, new Vector3(10000f, 10000f), Quaternion.identity);
        try
        {
            AttributeSet attributes = instance.GetComponent<AttributeSet>();
            Assert.That(attributes, Is.Not.Null);
            AttributeDefinition health = LoadGuid<AttributeDefinition>("3ff045849daafe84d97370c69cd17747");
            AttributeDefinition maxHealth = LoadGuid<AttributeDefinition>("0e177e1d15e428745b5859fac08ce203");
            Assert.That(attributes.GetAttributeValue(maxHealth), Is.EqualTo(baseHp).Within(0.01f));
            Assert.That(attributes.GetAttributeValue(health), Is.EqualTo(baseHp).Within(0.01f));

            MonsterDifficultyReceiver receiver = instance.GetComponent<MonsterDifficultyReceiver>();
            if (receiver == null)
                receiver = instance.AddComponent<MonsterDifficultyReceiver>();

            for (int stage = 0; stage < 3; stage++)
            {
                float multiplier = (1f + 0.55f * stage) * roleMultiplier;
                receiver.ApplyDifficulty(new DifficultyModifiers { hpMultiplier = multiplier });
                float expected = baseHp * multiplier;
                Assert.That(attributes.GetAttributeValue(maxHealth), Is.EqualTo(expected).Within(0.01f));
                Assert.That(attributes.GetAttributeValue(health), Is.EqualTo(expected).Within(0.01f));

                // Reapplying a spawn modifier must use original profile HP, not compound the last result.
                receiver.ApplyDifficulty(new DifficultyModifiers { hpMultiplier = multiplier });
                Assert.That(attributes.GetAttributeValue(maxHealth), Is.EqualTo(expected).Within(0.01f));
            }

            attributes.TrySetBaseValue(health, attributes.GetAttributeValue(maxHealth) * 0.5f, receiver);
            receiver.ApplyDifficulty(new DifficultyModifiers { hpMultiplier = roleMultiplier });
            Assert.That(attributes.GetAttributeValue(maxHealth), Is.EqualTo(baseHp * roleMultiplier).Within(0.01f));
            Assert.That(attributes.GetAttributeValue(health), Is.EqualTo(baseHp * roleMultiplier * 0.5f).Within(0.01f));

            if (instance.TryGetComponent(out Slime slime))
            {
                float hpBeforeSplitSetup = attributes.GetAttributeValue(maxHealth) * 0.5f;
                attributes.TrySetBaseValue(health, hpBeforeSplitSetup, slime);
                float maxBeforeSplitSetup = attributes.GetAttributeValue(maxHealth);
                slime.InitSplit(null);
                Assert.That(attributes.GetAttributeValue(health), Is.EqualTo(hpBeforeSplitSetup).Within(0.01f));
                Assert.That(attributes.GetAttributeValue(maxHealth), Is.EqualTo(maxBeforeSplitSetup).Within(0.01f));
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [TestCase("ALData_ApprenticeHeroSwordDashStab", "damage.damageFormula", 45f)]
    [TestCase("ALData_LightningSpearSkill1", "markRushHit.damageFormula", 40f)]
    [TestCase("ALData_LightningSpearSkill1", "noMarkSweepHit.damageFormula", 50f)]
    [TestCase("ALData_LightningSpearSkill1", "recoveredSpearProjectileHit.damageFormula", 15f)]
    [TestCase("ALData_LightningSpearSkill2", "landingHit.damageFormula", 50f)]
    [TestCase("ALData_FragmentBladeRecall", "damageFormula", 10f)]
    public void SkillFormulaReferences_ProduceApprovedDamageAndScaleWithAttack(string asset, string property, float expected)
    {
        ScaledStatFormula formula = ReadFormula(asset, property);
        Assert.That(formula, Is.Not.SameAs(Load<ScaledStatFormula>(Formulas + "SF_ATKx1.asset")));
        Assert.That(formula.Evaluate(null, new AttackStats(10f)), Is.EqualTo(expected).Within(0.001f));
        Assert.That(formula.Evaluate(null, new AttackStats(20f)), Is.EqualTo(expected * 1.3f).Within(0.001f));
        Assert.That(formula.Evaluate(null, new AttackStats(0f)), Is.EqualTo(expected * 0.7f).Within(0.001f));
        Assert.That(formula.Evaluate(null, new AttackStats(30f)), Is.EqualTo(expected * 1.6f).Within(0.001f));
    }

    [TestCase(0f, 60f)]
    [TestCase(0.25f, 60f)]
    [TestCase(0.5f, 73.333336f)]
    [TestCase(1f, 100f)]
    [TestCase(2f, 100f)]
    public void ChargeSpin_UsesAuthoredDamageEndpoints(float seconds, float expected)
    {
        ApprenticeHeroSwordChargeSpinData data = Load<ApprenticeHeroSwordChargeSpinData>(
            LogicData + "ALData_ApprenticeHeroSwordChargeSpin.asset");
        float damage = data.Damage.DamageFormula.Evaluate(null, new AttackStats(10f)) * data.ResolveDamageScale(seconds);
        Assert.That(damage, Is.EqualTo(expected).Within(0.001f));
        float grownDamage = data.Damage.DamageFormula.Evaluate(null, new AttackStats(20f)) * data.ResolveDamageScale(seconds);
        Assert.That(grownDamage, Is.EqualTo(expected * 1.3f).Within(0.001f));
    }

    [Test]
    public void ChargeSpin_EqualChargeEndpointsUseMaximumWithoutDivisionByZero()
    {
        ApprenticeHeroSwordChargeSpinData data = Object.Instantiate(
            Load<ApprenticeHeroSwordChargeSpinData>(LogicData + "ALData_ApprenticeHeroSwordChargeSpin.asset"));
        try
        {
            typeof(ApprenticeHeroSwordChargeSpinData).GetField("maxChargeSeconds",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, data.MinChargeSeconds);
            Assert.That(data.ResolveDamageScale(0f), Is.EqualTo(data.MaxDamageScale));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void FollowupsAndFixedDamage_UseApprovedValuesWithoutChangingNormals()
    {
        var stats = new AttackStats(10f);
        Assert.That(Load<ScaledStatFormula>(Formulas + "SF_ATKx1.asset").Evaluate(null, stats), Is.EqualTo(10f));
        ScaledStatFormula fragmentNormal = ReadFormula("ALData_FragmentBladeAttack", "damageFormula");
        Assert.That(fragmentNormal.Evaluate(null, stats), Is.EqualTo(10f));
        Assert.That(fragmentNormal.Evaluate(null, new AttackStats(20f)), Is.EqualTo(20f));
        Assert.That(ReadFloat("ALData_FragmentBladeAttack", "minimumDamageScale"), Is.EqualTo(0.35f));
        ScaledStatFormula fragmentPiercing = ReadFormula("ALData_FragmentBladeAttack", "piercingDamageFormula");
        Assert.That(fragmentPiercing.Evaluate(null, stats) *
            ReadFloat("ALData_FragmentBladeAttack", "piercingDamageScale") * 6, Is.EqualTo(90f));
        Assert.That(fragmentPiercing.Evaluate(null, new AttackStats(20f)) *
            ReadFloat("ALData_FragmentBladeAttack", "piercingDamageScale") * 6, Is.EqualTo(117f).Within(0.001f));
        Assert.That(ReadFormula("ALData_FragmentBladeRecall", "damageFormula").Evaluate(null, stats) * 6, Is.EqualTo(60f));

        Assert.That(ReadFormula("ALData_FloweringBloom", "dashSlashDamageFormula").Evaluate(null, stats) *
            ReadFloat("ALData_FloweringBloom", "dashSlashDamageScale"), Is.EqualTo(25f));
        Assert.That(ReadFormula("ALData_FloweringBloom", "dashSlashDamageFormula").Evaluate(null, new AttackStats(20f)) *
            ReadFloat("ALData_FloweringBloom", "dashSlashDamageScale"), Is.EqualTo(32.5f).Within(0.001f));
        using (var bloom = new SerializedObject(Load<ScriptableObject>(LogicData + "ALData_FloweringBloom.asset")))
            Assert.That(bloom.FindProperty("dashSlashCount").intValue, Is.EqualTo(3));

        Assert.That(ReadFloat("ALData_OddIronShot", "fixedDamage"), Is.EqualTo(90f));
        Assert.That(ReadFloat("ALData_OddIronThrow", "fixedDamage"), Is.EqualTo(90f));
        Assert.That(ReadFloat("ALData_CrimsonBoundary", "burnConsumptionMultiplier"), Is.EqualTo(6f));
        Assert.That(ReadFloat("ALData_CrimsonBoundary", "skill2BaseMultiplier"), Is.EqualTo(40f));
    }

    [Test]
    public void FragmentPiercing_UsesSeparateFormulaAndPreservesLegacyAssets()
    {
        var data = Object.Instantiate(Load<UnityGAS.Sample.FragmentBladeAttackData>(
            LogicData + "ALData_FragmentBladeAttack.asset"));
        try
        {
            Assert.That(data.PiercingDamageFormula, Is.SameAs(data.piercingDamageFormula));
            Assert.That(data.PiercingDamageFormula, Is.Not.SameAs(data.damageFormula));
            data.piercingDamageFormula = null;
            Assert.That(data.PiercingDamageFormula, Is.SameAs(data.damageFormula));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [TestCase(5f, 200f, 30f)]
    [TestCase(10f, 260f, 39f)]
    [TestCase(15f, 320f, 48f)]
    public void CrimsonSkillFormula_PreservesBaseDamageAndLimitsFireGrowth(float fire, float direct, float perStack)
    {
        CrimsonBoundaryWeaponData data = Load<CrimsonBoundaryWeaponData>(LogicData + "ALData_CrimsonBoundary.asset");
        Assert.That(data.skillFireFormula, Is.Not.Null);
        float scaledFire = data.skillFireFormula.Evaluate(null, new AttackStats(100f, fire));
        Assert.That(scaledFire * data.skill2BaseMultiplier, Is.EqualTo(direct).Within(0.001f));
        Assert.That(scaledFire * data.burnConsumptionMultiplier, Is.EqualTo(perStack).Within(0.001f));
    }

    [TestCase(5f, 200f, 30f)]
    [TestCase(10f, 260f, 39f)]
    public void CrimsonDamage_SeparatesNormalAndSkillScalingAndPreservesPostProcessing(float fire, float direct, float perStack)
    {
        GameObject instance = Object.Instantiate(Load<GameObject>(Prefabs + "CommonCorridor/GoblinGunner.prefab"),
            new Vector3(10000f, 10000f), Quaternion.identity);
        try
        {
            AbilitySystem system = instance.GetComponent<AbilitySystem>();
            CrimsonBoundaryWeaponData data = Load<CrimsonBoundaryWeaponData>(LogicData + "ALData_CrimsonBoundary.asset");
            SetBoundStat(system, StatId.FireBase, fire);
            SetBoundStat(system, StatId.FireAdd, 0f);
            SetBoundStat(system, StatId.FireMul, 1f);
            SetBoundStat(system, StatId.CritChanceBase, 0f);
            SetBoundStat(system, StatId.CritChanceAdd, 0f);
            SetBoundStat(system, StatId.CritChanceMul, 1f);
            SetBoundStat(system, StatId.FinalMul, 1f);
            Assert.That(CrimsonBoundaryUtility.CalculateDirectDamage(system, 1f, out bool normalCrit), Is.EqualTo(fire));
            Assert.That(normalCrit, Is.False);
            Assert.That(CrimsonBoundaryUtility.CalculateDirectDamage(system, data.skill2BaseMultiplier, out bool skillCrit,
                data.skillFireFormula), Is.EqualTo(direct));
            Assert.That(skillCrit, Is.False);
            Assert.That(CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, 3, data.burnConsumptionMultiplier,
                data.skillFireFormula), Is.EqualTo(perStack * 3f));
            Assert.That(CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, 0, data.burnConsumptionMultiplier,
                data.skillFireFormula), Is.Zero);
            Assert.That(CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, 1, data.burnConsumptionMultiplier),
                Is.EqualTo(fire * data.burnConsumptionMultiplier));

            SetBoundStat(system, StatId.FinalMul, 2f);
            SetBoundStat(system, StatId.CritChanceBase, 1f);
            SetBoundStat(system, StatId.CritMultiplier, 2f);
            Assert.That(CrimsonBoundaryUtility.CalculateDirectDamage(system, data.skill2BaseMultiplier, out skillCrit,
                data.skillFireFormula), Is.EqualTo(direct * 4f));
            Assert.That(skillCrit, Is.True);
            Assert.That(CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, 1, data.burnConsumptionMultiplier,
                data.skillFireFormula), Is.EqualTo(perStack * 2f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void CrimsonDamage_MissingSourceDoesNotProduceFlatDamage()
    {
        CrimsonBoundaryWeaponData data = Load<CrimsonBoundaryWeaponData>(LogicData + "ALData_CrimsonBoundary.asset");
        Assert.That(CrimsonBoundaryUtility.CalculateDirectDamage(null, 40f, out bool critical, data.skillFireFormula), Is.Zero);
        Assert.That(critical, Is.False);
        Assert.That(CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(null, 3, 6f, data.skillFireFormula), Is.Zero);
    }

    private static void SetBoundStat(AbilitySystem system, StatId id, float value)
    {
        Assert.That(system.DamageProfile.GetStatBindings().TryGetBinding(id, out var binding), Is.True, id.ToString());
        Assert.That(system.AttributeSet.TrySetBaseValue(binding.attribute, value, system), Is.True, id.ToString());
    }

    [TestCase(10f, 5.05f, 75.75f)]
    [TestCase(20f, 5.05f, 98.475f)]
    [TestCase(10f, 20.2f, 303f)]
    [TestCase(20f, 20.2f, 393.9f)]
    [TestCase(10f, 0f, 0f)]
    public void SpeedStrike_LimitsAttackGrowthButKeepsFullSpeedLink(float attack, float speed, float expected)
    {
        using var serialized = new SerializedObject(Load<ScriptableObject>(LogicData + "ALData_RW_Skill2_SpeedStrike.asset"));
        var formula = serialized.FindProperty("damageFormula").objectReferenceValue as StackStatFormula;
        Assert.That(formula, Is.Not.Null);
        Assert.That(formula.TryValidate(out string message), Is.True, message);
        Assert.That(formula.Evaluate(null, new AttackStats(attack, speed: speed)), Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenAttributeOverrideInitProfile.asset", 1000f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenP2ShortAttributeOverrideInitProfile.asset", 400f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenP2LongAttributeOverrideInitProfile.asset", 400f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/WitchAttributeOverrideInitProfile.asset", 1500f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/DragonBossAttributeOverrideInitProfile.asset", 2000f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/WitchBossAttributeOverrideInitProfile.asset", 2800f)]
    public void BossProfiles_UseApprovedHealth(string path, float expected)
    {
        AttributeInitProfileSO profile = Load<AttributeInitProfileSO>(path);
        Assert.That(ReadProfileValue(profile, "3ff045849daafe84d97370c69cd17747"), Is.EqualTo(expected));
        Assert.That(ReadProfileValue(profile, "0e177e1d15e428745b5859fac08ce203"), Is.EqualTo(expected));
    }

    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/WitchAttributeOverrideInitProfile.asset", 600f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/SlimeQueenGroggyOverrideInitProfile.asset", 500f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/DragonBossAttributeOverrideInitProfile.asset", 1000f)]
    [TestCase("Assets/_Project/Data/Attributes/InitProfiles/WitchBossAttributeOverrideInitProfile.asset", 1100f)]
    public void BossProfiles_UseApprovedMaximumStagger(string path, float expected)
    {
        AttributeInitProfileSO profile = Load<AttributeInitProfileSO>(path);
        Assert.That(ReadProfileValue(profile, "ddbd05a9b77349441a875c479e48212d"), Is.EqualTo(expected));
    }

    [TestCase(
        "Assets/_Project/Prefabs/Bosses/SlimeQueen/SlimeQueen.prefab",
        "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenAttributeOverrideInitProfile.asset")]
    [TestCase(
        "Assets/_Project/Prefabs/Bosses/SlimeQueen/SlimeQueenP2Short.prefab",
        "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenP2ShortAttributeOverrideInitProfile.asset")]
    [TestCase(
        "Assets/_Project/Prefabs/Bosses/SlimeQueen/SlimeQueenP2Long.prefab",
        "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenP2LongAttributeOverrideInitProfile.asset")]
    [TestCase(
        "Assets/_Project/Prefabs/Bosses/ShadowBoss/Witch.prefab",
        "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/WitchAttributeOverrideInitProfile.asset")]
    public void BossPrefabs_ReferenceApprovedHealthProfile(string prefabPath, string profilePath)
    {
        GameObject prefab = Load<GameObject>(prefabPath);
        AttributeSet attributes = prefab.GetComponent<AttributeSet>();
        Assert.That(attributes, Is.Not.Null, prefabPath);

        AttributeInitProfileSO expected = Load<AttributeInitProfileSO>(profilePath);
        using var serialized = new SerializedObject(attributes);
        SerializedProperty overrides = serialized.FindProperty("overrideInitProfiles");
        Assert.That(overrides, Is.Not.Null, prefabPath);

        bool found = false;
        for (int i = 0; i < overrides.arraySize; i++)
        {
            if (overrides.GetArrayElementAtIndex(i).objectReferenceValue == expected)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True, $"{prefabPath} does not reference {profilePath}.");

        AttributeDefinition health = LoadGuid<AttributeDefinition>("3ff045849daafe84d97370c69cd17747");
        AttributeDefinition maxHealth = LoadGuid<AttributeDefinition>("0e177e1d15e428745b5859fac08ce203");
        SerializedProperty maxLinks = serialized.FindProperty("maxLinks");
        Assert.That(maxLinks, Is.Not.Null, prefabPath);

        bool fillsHealthToMax = false;
        for (int i = 0; i < maxLinks.arraySize; i++)
        {
            SerializedProperty link = maxLinks.GetArrayElementAtIndex(i);
            if (link.FindPropertyRelative("value").objectReferenceValue == health &&
                link.FindPropertyRelative("max").objectReferenceValue == maxHealth)
            {
                fillsHealthToMax = link.FindPropertyRelative("fillToMaxOnInitialize").boolValue;
                break;
            }
        }

        Assert.That(fillsHealthToMax, Is.True,
            $"{prefabPath} must fill current health after applying its maximum-health profile.");
    }

    [Test]
    public void ShadowBossScene_DoesNotOverrideDedicatedHealthWithDemonKingProfile()
    {
        const string path = "Assets/_Project/Scenes/HeoMinSeok_Boss_Shadow.unity";
        string sceneYaml = File.ReadAllText(path);
        Assert.That(sceneYaml, Does.Contain("900e8eeac487b5b41b95ccbd78628ce3"));
        Assert.That(sceneYaml, Does.Not.Contain("5c6cbc85ca649e6428489e9939c28342"));
    }

    [Test]
    public void MonsterStageScaling_UsesApprovedFiftyFivePercentHealthStep()
    {
        ScriptableObject settings = Load<ScriptableObject>(
            "Assets/_Project/Resources/MonsterStageHpScalingSettings.asset");
        using var serialized = new SerializedObject(settings);
        Assert.That(serialized.FindProperty("hpMultiplierPerClearedStage").floatValue,
            Is.EqualTo(0.55f).Within(0.0001f));
    }

    [TestCase("Assets/_Project/Data/Loot/Tables/Table_Stage1.asset")]
    [TestCase("Assets/_Project/Data/Loot/Tables/Table_Stage2.asset")]
    [TestCase("Assets/_Project/Data/Loot/Tables/Table_Stage3.asset")]
    public void StageLootTables_UseApprovedCandidateCounts(string path)
    {
        using var serialized = new SerializedObject(Load<StageLootTable>(path));
        AssertFixedCountProfile(serialized.FindProperty("chestWeaponCountProfile"), 2);
        AssertFixedCountProfile(serialized.FindProperty("chestRelicCountProfile"), 4);
        AssertFixedCountProfile(serialized.FindProperty("bossWeaponCountProfile"), 2);
        AssertFixedCountProfile(serialized.FindProperty("bossRelicCountProfile"), 4);
    }

    [Test]
    public void ChestRefreshUpgrade_UsesApprovedSingleRefresh()
    {
        ScriptableObject effect = Load<ScriptableObject>(
            "Assets/_Project/Data/Progression/Upgrades/Effect/ChestRunModifierEffect.asset");
        using var serialized = new SerializedObject(effect);
        Assert.That(serialized.FindProperty("chestRefreshCount").intValue, Is.EqualTo(1));
    }

    [Test]
    public void LightningSpearAndCurrentShard_UseApprovedElectricProgression()
    {
        WeaponDefinition spear = Load<WeaponDefinition>(
            "Assets/_Project/Data/Items/Weapons/Definitions/WD_LightningSpear.asset");
        Assert.That(spear.statModifiers.Count, Is.EqualTo(1));
        WeaponDefinition.WeaponStatModifier spearElectric = spear.statModifiers[0];
        Assert.That(spearElectric.attribute,
            Is.SameAs(LoadGuid<AttributeDefinition>("418ca09dfa4c417ca3cadc4fd73bd414")));
        Assert.That(spearElectric.type, Is.EqualTo(ModifierType.Flat));
        Assert.That(spearElectric.value, Is.EqualTo(5f));

        ScriptableObject shard = Load<ScriptableObject>(
            "Assets/_Project/Data/Items/Relics/Strategies/Relic Logic_Common_CurrentShard.asset");
        using var shardSerialized = new SerializedObject(shard);
        SerializedProperty entry = shardSerialized.FindProperty("entries").GetArrayElementAtIndex(0);
        Assert.That(entry.FindPropertyRelative("attribute").objectReferenceValue,
            Is.SameAs(LoadGuid<AttributeDefinition>("f79c86d599c44c8e95bdf6949914f4e3")));
        Assert.That(entry.FindPropertyRelative("type").enumValueIndex,
            Is.EqualTo((int)ModifierType.Flat));
        SerializedProperty values = entry.FindPropertyRelative("valueByLevel");
        Assert.That(values.arraySize, Is.EqualTo(3));
        Assert.That(values.GetArrayElementAtIndex(0).floatValue, Is.EqualTo(2f));
        Assert.That(values.GetArrayElementAtIndex(1).floatValue, Is.EqualTo(4f));
        Assert.That(values.GetArrayElementAtIndex(2).floatValue, Is.EqualTo(6f));

        ElementBuildUpFormulaProfile profile = Load<ElementBuildUpFormulaProfile>(
            "Assets/_Project/Data/Attributes/ElementGauges/ElementBuildUpFormulaProfile.asset");
        Assert.That(CalculateElementBuildUp(profile, 5f), Is.EqualTo(20f).Within(0.001f));
        Assert.That(CalculateElementBuildUp(profile, 11f), Is.EqualTo(28.333334f).Within(0.001f));
    }

    [TestCase("Relic Logic_Common_BlackIncense.asset", 1, 0, -0.03f)]
    [TestCase("Relic Logic_Common_BlackIncense.asset", 1, 1, -0.02f)]
    [TestCase("Relic Logic_Common_BlackIncense.asset", 1, 2, -0.01f)]
    [TestCase("Relic Logic_Common_BerserkerCord.asset", 0, 0, -1f)]
    [TestCase("Relic Logic_Common_BerserkerCord.asset", 0, 1, -1f)]
    [TestCase("Relic Logic_Common_BerserkerCord.asset", 0, 2, -1f)]
    [TestCase("Relic Logic_Common_BrokenClock.asset", 1, 0, -0.03f)]
    [TestCase("Relic Logic_Common_BrokenClock.asset", 1, 1, -0.02f)]
    [TestCase("Relic Logic_Common_BrokenClock.asset", 1, 2, -0.01f)]
    [TestCase("Relic Logic_Common_BluntShield.asset", 0, 0, -0.06f)]
    [TestCase("Relic Logic_Common_BluntShield.asset", 0, 1, -0.04f)]
    [TestCase("Relic Logic_Common_BluntShield.asset", 0, 2, -0.02f)]
    [TestCase("Relic Logic_Common_BrokenCrown.asset", 1, 0, -1f)]
    [TestCase("Relic Logic_Common_BrokenCrown.asset", 1, 1, -1f)]
    [TestCase("Relic Logic_Common_BrokenCrown.asset", 1, 2, -1f)]
    [TestCase("Relic Logic_Common_HeavyBracelet.asset", 0, 0, -0.04f)]
    [TestCase("Relic Logic_Common_HeavyBracelet.asset", 0, 1, -0.03f)]
    [TestCase("Relic Logic_Common_HeavyBracelet.asset", 0, 2, -0.02f)]
    [TestCase("Relic Logic_Common_WarriorOath.asset", 1, 0, -0.03f)]
    [TestCase("Relic Logic_Common_WarriorOath.asset", 1, 1, -0.02f)]
    [TestCase("Relic Logic_Common_WarriorOath.asset", 1, 2, -0.01f)]
    public void TradeoffRelicPenalties_UseApprovedCurve(
        string assetName,
        int entryIndex,
        int levelIndex,
        float expected)
    {
        ScriptableObject logic = Load<ScriptableObject>(
            "Assets/_Project/Data/Items/Relics/Strategies/" + assetName);
        using var serialized = new SerializedObject(logic);
        SerializedProperty entries = serialized.FindProperty("entries");
        Assert.That(entries, Is.Not.Null);
        Assert.That(entryIndex, Is.LessThan(entries.arraySize));
        SerializedProperty values = entries.GetArrayElementAtIndex(entryIndex)
            .FindPropertyRelative("valueByLevel");
        Assert.That(values, Is.Not.Null);
        Assert.That(levelIndex, Is.LessThan(values.arraySize));
        Assert.That(values.GetArrayElementAtIndex(levelIndex).floatValue,
            Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase("Relic Logic_Attack Flat Bonus.asset", "entries", 0, "valueByLevel", 2, 3f)]
    [TestCase("Relic Logic_SpeedMedalBonus.asset", "entries", 0, "valueByLevel", 4, 0.12f)]
    [TestCase("Relic Logic_SpeedMedalBonus.asset", "entries", 1, "valueByLevel", 4, 0.1f)]
    [TestCase("Relic Logic_Speed Mul Bonus.asset", "entries", 0, "valueByLevel", 2, 0.15f)]
    [TestCase("Relic Logic_WindTablet.asset", "entries", 1, "valueByLevel", 4, -1f)]
    [TestCase("Relic Logic_Common_BluntShield.asset", "entries", 1, "valueByLevel", 2, 2f)]
    [TestCase("Relic Logic_Common_BrokenCrown.asset", "entries", 0, "valueByLevel", 2, 0.13f)]
    [TestCase("Relic Logic_Common_HeavyBracelet.asset", "entries", 1, "valueByLevel", 2, 0.16f)]
    [TestCase("Relic Logic_Common_BerserkerCord.asset", "entries", 1, "valueByLevel", 2, 0.16f)]
    [TestCase("Relic Logic_Common_SurvivorNecklace.asset", "entries", 1, "valueByLevel", 2, 0.07f)]
    [TestCase("Relic Logic_Common_ThinArmorShard.asset", "entries", 1, "valueByLevel", 2, 0.04f)]
    public void RelicStatTables_UseApprovedBalance(
        string assetName,
        string entriesProperty,
        int entryIndex,
        string valuesProperty,
        int levelIndex,
        float expected)
    {
        ScriptableObject logic = Load<ScriptableObject>(
            "Assets/_Project/Data/Items/Relics/Strategies/" + assetName);
        using var serialized = new SerializedObject(logic);
        SerializedProperty entries = serialized.FindProperty(entriesProperty);
        Assert.That(entries, Is.Not.Null);
        SerializedProperty values = entries.GetArrayElementAtIndex(entryIndex)
            .FindPropertyRelative(valuesProperty);
        Assert.That(values.GetArrayElementAtIndex(levelIndex).floatValue,
            Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void ManagedRelics_UseApprovedLevelCurvesAndThresholds()
    {
        ScriptableObject feather = Load<ScriptableObject>(
            "Assets/_Project/Data/Items/Relics/Strategies/Relic Logic_Feather Orbit_Managed.asset");
        using (var serialized = new SerializedObject(feather))
        {
            SerializedProperty values = serialized.FindProperty("damageCoefByLevel");
            Assert.That(values.arraySize, Is.EqualTo(5));
            Assert.That(values.GetArrayElementAtIndex(0).floatValue, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(values.GetArrayElementAtIndex(4).floatValue, Is.EqualTo(0.75f).Within(0.0001f));
        }

        ScriptableObject tonic = Load<ScriptableObject>(
            "Assets/_Project/Data/Items/Relics/Strategies/Relic Logic_Crit From Bonus Move Speed_Managed.asset");
        using (var serialized = new SerializedObject(tonic))
        {
            SerializedProperty values = serialized.FindProperty("critPerStepByLevel");
            Assert.That(values.arraySize, Is.EqualTo(5));
            Assert.That(values.GetArrayElementAtIndex(4).floatValue, Is.EqualTo(0.02f).Within(0.0001f));
        }

        ScriptableObject hawk = Load<ScriptableObject>(
            "Assets/_Project/Data/Items/Relics/Strategies/Relic Logic_Evasion From Bonus Move Speed_Managed.asset");
        using (var serialized = new SerializedObject(hawk))
        {
            Assert.That(serialized.FindProperty("bonusMoveStep").floatValue,
                Is.EqualTo(0.2f).Within(0.0001f));
        }

        RelicDefinition bandage = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_ToughBandage.asset");
        Assert.That(bandage.maxLevel, Is.EqualTo(2));

        RelicDefinition strengthCharm = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_AttackBonusRelic.asset");
        Assert.That(strengthCharm.maxLevel, Is.EqualTo(3));
        using (var serialized = new SerializedObject(strengthCharm.logic))
        {
            SerializedProperty values = serialized.FindProperty("entries")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("valueByLevel");
            Assert.That(values.arraySize, Is.EqualTo(3));
        }

        RelicDefinition bronzeDice = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_BronzeDice.asset");
        Assert.That(bronzeDice.rarity, Is.EqualTo(ItemRarity.Rare));
    }

    [Test]
    public void RelicTooltips_MatchApprovedEffects()
    {
        Assert.That(RelicTooltipFormatter.ShouldDisplayAsPercent(
            null,
            "넉백 저항",
            ModifierType.Flat), Is.True);

        RelicDefinition ironBall = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_RotatingIornBall.asset");
        using (var serialized = new SerializedObject(ironBall.logic))
        {
            Assert.That(serialized.FindProperty("attackPercentBonus").floatValue,
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(serialized.FindProperty("percentBonus").floatValue,
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(serialized.FindProperty("durationSeconds").floatValue,
                Is.EqualTo(4f).Within(0.0001f));
        }

        string ironBallTooltip = ironBall.logic.BuildTooltip(
            ironBall,
            1,
            default).effectText;
        Assert.That(ironBallTooltip, Does.Contain("공격력"));
        Assert.That(ironBallTooltip, Does.Contain("이동속도"));
        Assert.That(ironBallTooltip, Does.Contain("+10%"));

        RelicDefinition feather = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_FeatherOrbit.asset");
        string featherTooltip = feather.logic.BuildTooltip(
            feather,
            1,
            default).effectText;
        Assert.That(featherTooltip, Does.Contain("35%"));
    }

    [Test]
    public void DrunkenSpirit_UsesThreeLevelFinalDamageCurve()
    {
        RelicDefinition definition = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_DrunkenRush.asset");
        Assert.That(definition.displayName, Is.EqualTo("독한 술기운"));
        Assert.That(definition.maxLevel, Is.EqualTo(3));

        using var serialized = new SerializedObject(definition.logic);
        SerializedProperty values = serialized.FindProperty("valueByLevel");
        Assert.That(values.arraySize, Is.EqualTo(3));
        Assert.That(values.GetArrayElementAtIndex(0).floatValue, Is.EqualTo(0.04f).Within(0.0001f));
        Assert.That(values.GetArrayElementAtIndex(2).floatValue, Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(serialized.FindProperty("durationSeconds").floatValue, Is.EqualTo(6f));
        Assert.That(serialized.FindProperty("refreshDuration").boolValue, Is.True);
    }

    [Test]
    public void ScorchingAwl_UsesNonBurningTargetStarterCurve()
    {
        RelicDefinition definition = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_ScorchingSong.asset");
        Assert.That(definition.displayName, Is.EqualTo("작열하는 송곳"));
        Assert.That(definition.maxLevel, Is.EqualTo(5));

        using var serialized = new SerializedObject(definition.logic);
        SerializedProperty values = serialized.FindProperty("starterStacksByLevel");
        Assert.That(values.arraySize, Is.EqualTo(5));
        Assert.That(values.GetArrayElementAtIndex(0).intValue, Is.EqualTo(2));
        Assert.That(values.GetArrayElementAtIndex(4).intValue, Is.EqualTo(6));
        Assert.That(serialized.FindProperty("minimumFireForBurn").floatValue, Is.EqualTo(2f));
        Assert.That(serialized.FindProperty("allowCritical").boolValue, Is.False);

        RelicDefinition crown = Load<RelicDefinition>(
            "Assets/_Project/Data/Items/Relics/Definitions/RD_CrimsonKing.asset");
        Assert.That(crown.displayName, Is.EqualTo("홍련의 왕관"));
    }

    private static float ReadProfileValue(AttributeInitProfileSO profile, string attributeGuid)
    {
        Object expectedAttribute = LoadGuid<AttributeDefinition>(attributeGuid);
        using var serialized = new SerializedObject(profile);
        SerializedProperty entries = serialized.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("attribute").objectReferenceValue == expectedAttribute)
                return entry.FindPropertyRelative("baseValue").floatValue;
        }

        Assert.Fail($"{profile.name} does not define attribute {attributeGuid}.");
        return 0f;
    }

    private static void AssertFixedCountProfile(SerializedProperty profile, int expected)
    {
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.FindPropertyRelative("minCount").intValue, Is.EqualTo(expected));
        Assert.That(profile.FindPropertyRelative("maxCount").intValue, Is.EqualTo(expected));
        SerializedProperty weights = profile.FindPropertyRelative("weights");
        Assert.That(weights.arraySize, Is.EqualTo(1));
        Assert.That(weights.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue,
            Is.EqualTo(expected));
    }

    private static float CalculateElementBuildUp(ElementBuildUpFormulaProfile profile, float stat)
    {
        return profile.baseValue +
               (stat * profile.maxCap) / (stat + Mathf.Max(0.0001f, profile.curveConstant));
    }

    private static ScaledStatFormula ReadFormula(string asset, string property)
    {
        using var serialized = new SerializedObject(Load<ScriptableObject>(LogicData + asset + ".asset"));
        SerializedProperty value = serialized.FindProperty(property);
        Assert.That(value, Is.Not.Null, asset + "/" + property);
        var formula = value.objectReferenceValue as ScaledStatFormula;
        Assert.That(formula, Is.Not.Null, asset + "/" + property);
        return formula;
    }

    private static float ReadFloat(string asset, string property)
    {
        using var serialized = new SerializedObject(Load<ScriptableObject>(LogicData + asset + ".asset"));
        SerializedProperty value = serialized.FindProperty(property);
        Assert.That(value, Is.Not.Null, asset + "/" + property);
        return value.floatValue;
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AuthoringTool_BindsNewProfileButPreservesExistingOverrides(bool editsExistingPrefab)
    {
        var instance = new GameObject("ProfileAuthoringTest");
        instance.SetActive(false);
        try
        {
            AttributeSet attributes = instance.AddComponent<AttributeSet>();
            const string profiles = "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Mobs/";
            AttributeInitProfileSO previousProfile = Load<AttributeInitProfileSO>(
                profiles + "RookAttributeOverrideInitProfile.asset");
            using (var initial = new SerializedObject(attributes))
            {
                SerializedProperty overrides = initial.FindProperty("overrideInitProfiles");
                overrides.arraySize = 1;
                overrides.GetArrayElementAtIndex(0).objectReferenceValue = previousProfile;
                SerializedProperty links = initial.FindProperty("maxLinks");
                links.arraySize = 1;
                SerializedProperty link = links.GetArrayElementAtIndex(0);
                link.FindPropertyRelative("value").objectReferenceValue =
                    LoadGuid<AttributeDefinition>("3ff045849daafe84d97370c69cd17747");
                link.FindPropertyRelative("max").objectReferenceValue =
                    LoadGuid<AttributeDefinition>("0e177e1d15e428745b5859fac08ce203");
                link.FindPropertyRelative("fillToMaxOnInitialize").boolValue = false;
                initial.ApplyModifiedPropertiesWithoutUndo();
            }

            System.Type generator = System.Type.GetType("CommonMonsterAuthoringGenerator, Editor");
            Assert.That(generator, Is.Not.Null);
            MethodInfo configure = generator.GetMethod("ConfigureInitialAttributes",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(configure, Is.Not.Null);
            configure.Invoke(null, new object[] { instance, "GoblinWarrior", editsExistingPrefab });

            using var result = new SerializedObject(attributes);
            Object expectedProfile = editsExistingPrefab ? previousProfile :
                Load<AttributeInitProfileSO>(profiles + "GoblinWarriorAttributeOverrideInitProfile.asset");
            Assert.That(result.FindProperty("overrideInitProfiles").GetArrayElementAtIndex(0).objectReferenceValue,
                Is.SameAs(expectedProfile));
            Assert.That(result.FindProperty("maxLinks").GetArrayElementAtIndex(0)
                .FindPropertyRelative("fillToMaxOnInitialize").boolValue, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static T LoadGuid<T>(string guid) where T : Object => Load<T>(AssetDatabase.GUIDToAssetPath(guid));

    // Responsibility: supply deterministic offensive stats for formula checks without scene/player dependencies.
    private sealed class AttackStats : IStatProvider
    {
        private readonly float attack;
        private readonly float fire;
        private readonly float speed;

        public AttackStats(float attack, float fire = 0f, float speed = 0f)
        {
            this.attack = attack;
            this.fire = fire;
            this.speed = speed;
        }

        public float Get(StatId id) => id switch
        {
            StatId.AttackFinal => attack,
            StatId.FireFinal => fire,
            StatId.MoveSpeedFinal => speed,
            _ => 0f
        };
    }
}
#endif
