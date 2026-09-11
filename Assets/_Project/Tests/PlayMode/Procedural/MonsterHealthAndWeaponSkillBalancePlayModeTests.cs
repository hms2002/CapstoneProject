#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

// Responsibility: verify authored monster HP, 30% skill stat growth, and unchanged normal/fixed damage and speed synergy.
public sealed class MonsterHealthAndWeaponSkillBalancePlayModeTests
{
    private const string Prefabs = "Assets/_Project/Prefabs/Monsters/";
    private const string LogicData = "Assets/_Project/Data/Items/Weapons/LogicData/";
    private const string Formulas = "Assets/_Project/Data/Attributes/Formulas/";

    [TestCase("CommonCorridor/GoblinWarrior.prefab", 450f, 1f)]
    [TestCase("CommonCorridor/GoblinGunner.prefab", 20f, 1f)]
    [TestCase("CommonCorridor/GoblinTank.prefab", 900f, 0.5f)]
    [TestCase("CommonCorridor/LizardWarrior.prefab", 500f, 1f)]
    [TestCase("CommonCorridor/LizardMage.prefab", 350f, 1f)]
    [TestCase("CommonCorridor/ArcaneMeleeGolem.prefab", 450f, 1f)]
    [TestCase("CommonCorridor/ArcaneTankGolem.prefab", 900f, 0.5f)]
    [TestCase("SlimeCorridor/Pawn.prefab", 80f, 1f)]
    [TestCase("SlimeCorridor/Knight.prefab", 400f, 1f)]
    [TestCase("SlimeCorridor/Bishop.prefab", 350f, 1f)]
    [TestCase("SlimeCorridor/Rook.prefab", 1100f, 1f)]
    [TestCase("SlimeCorridor/Wizard.prefab", 220f, 1f)]
    [TestCase("ShadowCorridor/ShadowMonster.prefab", 220f, 1f)]
    [TestCase("ShadowCorridor/ShadowServant/ShadowServant.prefab", 320f, 1f)]
    [TestCase("ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab", 600f, 1f)]
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
                float multiplier = (1f + 0.15f * stage) * roleMultiplier;
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

    [TestCase("ALData_ApprenticeHeroSwordDashStab", "damage.damageFormula", 120f)]
    [TestCase("ALData_LightningSpearSkill1", "markRushHit.damageFormula", 80f)]
    [TestCase("ALData_LightningSpearSkill1", "noMarkSweepHit.damageFormula", 120f)]
    [TestCase("ALData_LightningSpearSkill1", "recoveredSpearProjectileHit.damageFormula", 30f)]
    [TestCase("ALData_LightningSpearSkill2", "landingHit.damageFormula", 100f)]
    [TestCase("ALData_FragmentBladeRecall", "damageFormula", 50f)]
    public void SkillFormulaReferences_ProduceApprovedDamageAndScaleWithAttack(string asset, string property, float expected)
    {
        ScaledStatFormula formula = ReadFormula(asset, property);
        Assert.That(formula, Is.Not.SameAs(Load<ScaledStatFormula>(Formulas + "SF_ATKx1.asset")));
        Assert.That(formula.Evaluate(null, new AttackStats(10f)), Is.EqualTo(expected).Within(0.001f));
        Assert.That(formula.Evaluate(null, new AttackStats(20f)), Is.EqualTo(expected * 1.3f).Within(0.001f));
        Assert.That(formula.Evaluate(null, new AttackStats(0f)), Is.EqualTo(expected * 0.7f).Within(0.001f));
        Assert.That(formula.Evaluate(null, new AttackStats(30f)), Is.EqualTo(expected * 1.6f).Within(0.001f));
    }

    [TestCase(0f, 180f)]
    [TestCase(0.25f, 180f)]
    [TestCase(0.5f, 220f)]
    [TestCase(1f, 300f)]
    [TestCase(2f, 300f)]
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
        Assert.That(ReadFormula("ALData_FragmentBladeRecall", "damageFormula").Evaluate(null, stats) * 6, Is.EqualTo(300f));

        Assert.That(ReadFormula("ALData_FloweringBloom", "dashSlashDamageFormula").Evaluate(null, stats) *
            ReadFloat("ALData_FloweringBloom", "dashSlashDamageScale"), Is.EqualTo(120f));
        Assert.That(ReadFormula("ALData_FloweringBloom", "dashSlashDamageFormula").Evaluate(null, new AttackStats(20f)) *
            ReadFloat("ALData_FloweringBloom", "dashSlashDamageScale"), Is.EqualTo(156f).Within(0.001f));
        using (var bloom = new SerializedObject(Load<ScriptableObject>(LogicData + "ALData_FloweringBloom.asset")))
            Assert.That(bloom.FindProperty("dashSlashCount").intValue, Is.EqualTo(3));

        Assert.That(ReadFloat("ALData_OddIronShot", "fixedDamage"), Is.EqualTo(700f));
        Assert.That(ReadFloat("ALData_OddIronThrow", "fixedDamage"), Is.EqualTo(700f));
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

    [TestCase(10f, 5.05f, 151.5f)]
    [TestCase(20f, 5.05f, 196.95f)]
    [TestCase(10f, 20.2f, 606f)]
    [TestCase(20f, 20.2f, 787.8f)]
    [TestCase(10f, 0f, 0f)]
    public void SpeedStrike_LimitsAttackGrowthButKeepsFullSpeedLink(float attack, float speed, float expected)
    {
        using var serialized = new SerializedObject(Load<ScriptableObject>(LogicData + "ALData_RW_Skill2_SpeedStrike.asset"));
        var formula = serialized.FindProperty("damageFormula").objectReferenceValue as StackStatFormula;
        Assert.That(formula, Is.Not.Null);
        Assert.That(formula.TryValidate(out string message), Is.True, message);
        Assert.That(formula.Evaluate(null, new AttackStats(attack, speed: speed)), Is.EqualTo(expected).Within(0.001f));
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
