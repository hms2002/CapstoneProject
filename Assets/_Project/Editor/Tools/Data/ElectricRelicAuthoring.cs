using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityGAS;

/// <summary>전기 계수, 조건부 전기 속성 유물과 연쇄 감전 연장 유물을 Unity 직렬화 API로 제작하고 재검증한다.</summary>
public static class ElectricRelicAuthoring
{
    private const string Definitions = "Assets/_Project/Data/Items/Relics/Definitions/";
    private const string Strategies = "Assets/_Project/Data/Items/Relics/Strategies/";
    private const string DatabasePath = "Assets/_Project/Data/Items/ItemDatabase.asset";
    private const string ShockPath = "Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset";
    private static readonly string[] Names = { "ThunderHeart", "StormStep", "DashCoil", "ThunderKnot", "ResonanceBell", "TinglingShackles", "CurrentJar", "EchoCoil", "ConductorRing" };
    private static readonly string[] StrategyNames = { "ElectricThunderHeart", "ElectricStormStep", "ElectricDashCoil", "ElectricThunderKnot", "ElectricResonanceBell", "ElectricTinglingShackles", "ElectricCurrentJar", "ElectricEchoCoil", "ElectricConductorRing" };
    private static readonly string[] DisplayNames = { "천둥의 심장", "생명의 전류", "대시 코일", "뇌명의 매듭", "공명의 종", "저릿한 족쇄", "전류 항아리", "메아리 코일", "도체 고리" };
    private static readonly string[] Descriptions =
    {
        "감전된 적의 수에 비례해 전기 속성이 증가합니다.",
        "최대 체력에 비례해 전기 속성이 증가합니다.",
        "이동 스킬을 사용하면 3초 동안 전기 속성이 증가합니다. 최대 4중첩이며 다시 사용하면 모든 중첩의 지속시간이 갱신됩니다.",
        "연쇄에 맞은 적의 감전 지속시간이 늘어납니다. 최대 4초까지 늘어납니다.",
        "전기 연쇄가 맞힐 수 있는 최대 대상 수가 증가합니다.",
        "적에게 감전을 부여하면 0.1초 동안 마비시킵니다. 보스에게는 적용되지 않습니다.",
        "내가 부여한 감전의 지속시간이 끝나면 전기 속성의 40%만큼 피해를 줍니다.",
        "연쇄 번개를 50% 피해로 한 번 더 반복합니다. 최초 대상은 제외하며 감전 지속시간은 늘어나지 않습니다.",
        "연쇄될 때마다 연쇄 피해가 20%씩 증가합니다. 최대 60%까지 증가합니다."
    };
    private static readonly string[] IconSources = { "CurrentShard", "SmallHeart", "FlyingBoots", "CurrentShard", "MeltedBell", "ChainCharm", "SmokyIncense", "DashCoil", "ChainCharm" };

    public static void ApplyBatch() => Run(true);
    public static void ReadbackBatch() => Run(false);

    public static void LocalizeBatch()
    {
        try
        {
            var collection = Load<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
            string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
            var mappings = new List<CsvColumns>
            {
                new KeyIdColumns { KeyFieldName = "key", IncludeId = false, IncludeSharedComments = false }
            };
            foreach (string code in codes)
                mappings.Add(new LocaleColumns { LocaleIdentifier = code, FieldName = code, IncludeComments = false });
            var manifest = JsonUtility.FromJson<Report>(File.ReadAllText("Library/CodexElectricRelics/authoring.json"));
            var keys = new List<string>
            {
                "ui.item_keyword.electrocuted.description", "relic.electric.nearby.template",
                "relic.electric.move.template", "relic.electric.dash.template",
                "relic.electric.scene.template", "relic.electric.health.template", "relic.electric.extension.template",
                "relic.electric.targets.template", "relic.electric.paralysis.template",
                "relic.electric.expiry.template", "relic.electric.echo.template", "relic.electric.conductor.template"
            };
            foreach (var entry in manifest.entries)
            {
                keys.Add(entry.displayNameKey);
                keys.Add(entry.descriptionKey);
            }
            var expected = JsonUtility.FromJson<LocalizedReport>(File.ReadAllText("Library/CodexElectricRelics/expected.json"));
            var expectedByKey = expected.rows.ToDictionary(row => row.key, row => row.values);
            Require(keys.Count == 30 && keys.Distinct().Count() == keys.Count && expectedByKey.Count == keys.Count && keys.All(expectedByKey.ContainsKey),
                "Electric localization must contain exactly the thirty reviewed keys.");
            Require(expectedByKey.Values.All(values => values != null && values.Length == codes.Length), "Expected locale column count mismatch.");
            using (var reader = File.OpenText("Library/CodexElectricRelics/content.csv"))
                Csv.ImportInto(reader, collection, mappings, true, removeMissingEntries: false);
            for (int localeIndex = 0; localeIndex < codes.Length; localeIndex++)
            {
                string code = codes[localeIndex];
                var table = collection.StringTables.Single(t => t.LocaleIdentifier.Code == code);
                foreach (string key in keys)
                    Require(!string.IsNullOrWhiteSpace(table.GetEntry(key)?.Value) && table.GetEntry(key).Value == expectedByKey[key][localeIndex],
                        "Electric translation differs from prepared CSV: " + code + "/" + key);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log("[ElectricRelics] " + keys.Count + " content keys populated in five locales.");
            Run(false);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Run(bool apply)
    {
        try
        {
            var attribute = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementAddAttribute.asset");
            var maxHealth = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/MaxHealthAttribute.asset");
            var tag = Load<GameplayTag>("Assets/_Project/Resources/Tags/State.Status.Electrocuted.asset");
            var dash = Load<AbilityDefinition>("Assets/_Project/Data/Abilities/Definitions/AD_Dash.asset");
            var electrocutedStatus = Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
            var database = Load<ItemDatabase>(DatabasePath);
            var shockEffect = Load<GE_ElectricShockTrigger>(ShockPath);
            var shock = new SerializedObject(shockEffect);
            Require(shock.FindProperty("maxTargetsPerDischarge").intValue == 3, "Existing discharge cap must remain 3.");
            var fallbackIcon = Load<RelicDefinition>(Definitions + "RD_CurrentShard.asset").icon;
            Require(fallbackIcon != null, "CurrentShard fallback icon is missing.");
            MigrateCurrentShard(apply, attribute, database);
            AuthorMovementClassification(apply);
            var report = new Report();
            for (int i = 0; i < Names.Length; i++)
            {
                string definitionPath = Definitions + "RD_" + Names[i] + ".asset";
                string strategyPath = Strategies + "RL_" + StrategyNames[i] + ".asset";
                var iconSource = AssetDatabase.LoadAssetAtPath<RelicDefinition>(Definitions + "RD_" + IconSources[i] + ".asset");
                var icon = iconSource != null && iconSource.icon != null ? iconSource.icon : fallbackIcon;
                RelicLogic logic = i switch
                {
                    3 => apply ? Ensure<RelicLogic_ChainShockExtension_Managed>(strategyPath) : Load<RelicLogic_ChainShockExtension_Managed>(strategyPath),
                    4 => apply ? Ensure<RelicLogic_ElectricChainTargets_Managed>(strategyPath) : Load<RelicLogic_ElectricChainTargets_Managed>(strategyPath),
                    5 => apply ? Ensure<RelicLogic_ShockParalysis_Managed>(strategyPath) : Load<RelicLogic_ShockParalysis_Managed>(strategyPath),
                    6 => apply ? Ensure<RelicLogic_ElectricExpiryDamage_Managed>(strategyPath) : Load<RelicLogic_ElectricExpiryDamage_Managed>(strategyPath),
                    7 => apply ? Ensure<RelicLogic_ElectricEcho_Managed>(strategyPath) : Load<RelicLogic_ElectricEcho_Managed>(strategyPath),
                    8 => apply ? Ensure<RelicLogic_ElectricChainDamage_Managed>(strategyPath) : Load<RelicLogic_ElectricChainDamage_Managed>(strategyPath),
                    _ => apply ? Ensure<RelicLogic_ElectricAttributeBonus_Managed>(strategyPath) : Load<RelicLogic_ElectricAttributeBonus_Managed>(strategyPath)
                };
                var definition = apply ? Ensure<RelicDefinition>(definitionPath) : Load<RelicDefinition>(definitionPath);
                int maxLevel = i <= 1 ? 5 : i >= 5 ? 1 : 3;
                float[] bonusCurve = i <= 1 ? new[] { 1f, 1f, 2f, 2f, 3f } : new[] { 1f, 2f, 3f };
                float[] temporaryCurve = { 1f, 1f, 2f };
                if (apply)
                {
                    if (logic is RelicLogic_ElectricAttributeBonus_Managed bonus)
                    {
                        bonus.mode = i == 1 ? RelicLogic_ElectricAttributeBonus_Managed.BonusMode.MaxHealth
                            : (RelicLogic_ElectricAttributeBonus_Managed.BonusMode)i;
                        bonus.electricAddAttribute = attribute; bonus.electrocutedTag = tag; bonus.triggerDashAbility = dash;
                        bonus.maxUnits = 3;
                        if (i == 0) bonus.bonusPerUnitByLevel = new List<float>(bonusCurve);
                        if (i == 2) bonus.flatBonusByLevel = new List<float>(temporaryCurve);
                        bonus.durationSeconds = 3f;
                        if (i == 1) { bonus.maxHealthAttribute = maxHealth; bonus.healthStep = 1f; }
                    }
                    else if (logic is RelicLogic_ChainShockExtension_Managed extension)
                    {
                        extension.electrocutedStatusEffect = electrocutedStatus;
                        extension.extensionSecondsByLevel = new List<float> { 0.5f, 1f, 1.5f };
                    }
                    else if (logic is RelicLogic_ElectricChainTargets_Managed targets)
                        targets.additionalTargetsByLevel = new List<int> { 1, 2, 3 };
                    else if (logic is RelicLogic_ShockParalysis_Managed paralysis)
                        paralysis.paralysisSeconds = 0.1f;
                    else if (logic is RelicLogic_ElectricExpiryDamage_Managed expiry)
                    {
                        expiry.electrocutedStatusEffect = electrocutedStatus;
                        expiry.electricTriggerEffect = shockEffect;
                        expiry.electricCoefficient = 0.4f;
                    }
                    else if (logic is RelicLogic_ElectricEcho_Managed echo)
                        echo.damageRatio = 0.5f;
                    else if (logic is RelicLogic_ElectricChainDamage_Managed conductor)
                    {
                        conductor.bonusPerChainStep = 0.2f;
                        conductor.maxChainBonus = 0.6f;
                    }
                    definition.relicId = "Relic.Electric." + Names[i]; definition.displayName = DisplayNames[i];
                    definition.description = Descriptions[i]; definition.icon = icon; definition.rarity = ItemRarity.Rare;
                    definition.maxLevel = maxLevel; definition.dropLevel = 1; definition.logic = logic;
                    database.allRelics ??= new List<RelicDefinition>();
                    database.defaultUnlockedRelics ??= new List<RelicDefinition>();
                    if (!database.allRelics.Contains(definition)) database.allRelics.Add(definition);
                    if (!database.defaultUnlockedRelics.Contains(definition)) database.defaultUnlockedRelics.Add(definition);
                    EditorUtility.SetDirty(logic); EditorUtility.SetDirty(definition);
                }
                Require(definition.relicId == "Relic.Electric." + Names[i] && definition.displayName == DisplayNames[i]
                    && definition.description == Descriptions[i] && definition.icon == icon && definition.rarity == ItemRarity.Rare
                    && definition.maxLevel == maxLevel && definition.dropLevel == 1 && definition.logic == logic, "Definition mismatch: " + definitionPath);
                if (logic is RelicLogic_ElectricAttributeBonus_Managed attributeBonus)
                {
                    Require((int)attributeBonus.mode == (i == 1 ? 3 : i) && attributeBonus.electricAddAttribute == attribute
                        && attributeBonus.electrocutedTag == tag && attributeBonus.triggerDashAbility == dash && attributeBonus.maxUnits == 3
                        && Mathf.Approximately(attributeBonus.durationSeconds, 3f), "Strategy mismatch: " + strategyPath);
                    if (i <= 1) Require(attributeBonus.bonusPerUnitByLevel.SequenceEqual(bonusCurve), "Five-level attribute curve mismatch: " + strategyPath);
                    if (i == 2) Require(attributeBonus.flatBonusByLevel.SequenceEqual(temporaryCurve), "Movement stack curve mismatch: " + strategyPath);
                    if (i == 1) Require(attributeBonus.maxHealthAttribute == maxHealth && Mathf.Approximately(attributeBonus.healthStep, 1f),
                        "Maximum health strategy reference or step mismatch: " + strategyPath);
                }
                else if (logic is RelicLogic_ChainShockExtension_Managed chainExtension)
                    Require(chainExtension.electrocutedStatusEffect == electrocutedStatus
                        && chainExtension.extensionSecondsByLevel.SequenceEqual(new[] { 0.5f, 1f, 1.5f }), "Extension strategy mismatch: " + strategyPath);
                else if (logic is RelicLogic_ElectricChainTargets_Managed chainTargets)
                    Require(chainTargets.additionalTargetsByLevel.SequenceEqual(new[] { 1, 2, 3 }), "Chain target strategy mismatch: " + strategyPath);
                else if (logic is RelicLogic_ShockParalysis_Managed shockParalysis)
                    Require(Mathf.Approximately(shockParalysis.paralysisSeconds, 0.1f), "Paralysis strategy mismatch: " + strategyPath);
                else if (logic is RelicLogic_ElectricExpiryDamage_Managed expiryDamage)
                    Require(expiryDamage.electrocutedStatusEffect == electrocutedStatus && expiryDamage.electricTriggerEffect == shockEffect
                        && Mathf.Approximately(expiryDamage.electricCoefficient, 0.4f), "Expiry damage strategy mismatch: " + strategyPath);
                else if (logic is RelicLogic_ElectricEcho_Managed electricEcho)
                    Require(Mathf.Approximately(electricEcho.damageRatio, 0.5f), "Echo strategy mismatch: " + strategyPath);
                else if (logic is RelicLogic_ElectricChainDamage_Managed electricConductor)
                    Require(Mathf.Approximately(electricConductor.bonusPerChainStep, 0.2f)
                        && Mathf.Approximately(electricConductor.maxChainBonus, 0.6f), "Conductor strategy mismatch: " + strategyPath);
                Require(database.allRelics.Count(r => r == definition) == 1 && database.defaultUnlockedRelics.Count(r => r == definition) == 1,
                    "Database reference must appear exactly once: " + definitionPath);
                Require(database.allRelics.Count(r => r != null && r.relicId == definition.relicId) == 1, "Duplicate relic ID: " + definition.relicId);
                string guid = AssetDatabase.AssetPathToGUID(definitionPath);
                report.entries.Add(new Entry
                {
                    definitionPath = definitionPath, strategyPath = strategyPath, definitionGuid = guid,
                    strategyGuid = AssetDatabase.AssetPathToGUID(strategyPath),
                    displayNameIdentity = "RelicDefinition/" + definition.name + "/displayName",
                    descriptionIdentity = "RelicDefinition/" + definition.name + "/description",
                    displayNameKey = "asset." + guid + ".displayName", descriptionKey = "asset." + guid + ".description",
                    displayName = definition.displayName, description = definition.description
                });
            }
            if (apply)
            {
                shock.FindProperty("initialElectricCoefficient").floatValue = 1f;
                shock.FindProperty("chainElectricCoefficient").floatValue = 0.5f;
                shock.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(shock.targetObject);
                EditorUtility.SetDirty(database); AssetDatabase.SaveAssets();
            }
            shock.Update();
            Require(Mathf.Approximately(shock.FindProperty("initialElectricCoefficient").floatValue, 1f)
                && Mathf.Approximately(shock.FindProperty("chainElectricCoefficient").floatValue, 0.5f)
                && shock.FindProperty("maxTargetsPerDischarge").intValue == 3, "Electric coefficient or cap mismatch.");
            Directory.CreateDirectory("Library/CodexElectricRelics");
            File.WriteAllText("Library/CodexElectricRelics/authoring.json", JsonUtility.ToJson(report, true));
            Debug.Log("[ElectricRelics] Ten relics, movement classifications and nine-entry manifest validated.");
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.delayCall += () => EditorApplication.Exit(1); }
    }

    private static void MigrateCurrentShard(bool apply, AttributeDefinition electricAttribute, ItemDatabase database)
    {
        const string definitionPath = Definitions + "RD_CurrentShard.asset";
        const string strategyPath = Strategies + "Relic Logic_Common_CurrentShard.asset";
        var definition = Load<RelicDefinition>(definitionPath);
        var logic = Load<RelicLogic_StatModifiers>(strategyPath);
        Require(AssetDatabase.AssetPathToGUID(definitionPath) == "a53b2ba473004d28a0fb7db9af49ad7a"
            && AssetDatabase.AssetPathToGUID(strategyPath) == "0794133353074729a623d0fab2a83a78"
            && definition.relicId == "RD_CurrentShard" && definition.logic == logic && definition.rarity == ItemRarity.Common
            && database.allRelics.Count(relic => relic == definition) == 1
            && database.defaultUnlockedRelics.Count(relic => relic == definition) == 1, "Current Shard identity or registration mismatch.");
        Require(logic.entries.Count == 1 && logic.entries[0].attribute == electricAttribute
            && logic.entries[0].type == ModifierType.Flat, "Current Shard modifier identity mismatch.");
        float[] curve = { 1f, 2f, 3f, 4f, 5f, 6f };
        if (apply)
        {
            var entry = logic.entries[0];
            entry.value = 1f; entry.valueByLevel = new List<float>(curve);
            logic.entries[0] = entry; definition.maxLevel = 6;
            EditorUtility.SetDirty(logic); EditorUtility.SetDirty(definition);
        }
        Require(definition.maxLevel == 6 && definition.dropLevel == 1 && logic.entries[0].value == 1f
            && logic.entries[0].valueByLevel.SequenceEqual(curve), "Current Shard six-level curve mismatch.");
    }

    private static void AuthorMovementClassification(bool apply)
    {
        const string tagPath = "Assets/_Project/Resources/Tags/Ability.Movement.asset";
        string[] abilityPaths =
        {
            "Assets/_Project/Data/Abilities/Definitions/AD_Dash.asset",
            "Assets/_Project/Data/Abilities/Definitions/AD_ApprenticeHeroSwordSkill2_DashStab.asset",
            "Assets/_Project/Data/Abilities/Definitions/AD_LightningSpearSkill1.asset",
            "Assets/_Project/Data/Abilities/Definitions/AD_RW_Skill1_Rush.asset"
        };
        var originalIds = AssetDatabase.FindAssets("t:GameplayTag")
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameplayTag>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(tag => tag != null).ToDictionary(tag => tag, tag => tag.Id);
        var movement = AssetDatabase.LoadAssetAtPath<GameplayTag>(tagPath);
        if (movement == null && apply)
        {
            Require(AssetDatabase.LoadMainAssetAtPath(tagPath) == null, "Movement tag path has an incompatible asset.");
            movement = ScriptableObject.CreateInstance<GameplayTag>();
            movement.name = "Ability.Movement";
            movement.Editor_SetName(string.Empty); movement.Editor_SetParent(null);
            movement.Editor_SetId(originalIds.Values.DefaultIfEmpty(0).Max() + 1);
            AssetDatabase.CreateAsset(movement, tagPath);
        }
        Require(movement != null && movement.Path == "Ability.Movement" && movement.Id > 0
            && originalIds.All(pair => pair.Key == movement || pair.Value != movement.Id), "Movement tag path or stable ID mismatch.");
        Require(originalIds.All(pair => pair.Key.Id == pair.Value), "Existing serialized tag IDs changed.");
        foreach (string path in abilityPaths)
        {
            var ability = Load<AbilityDefinition>(path);
            var previousTags = ability.abilityTags?.ToArray() ?? Array.Empty<GameplayTag>();
            if (apply && !previousTags.Contains(movement))
            {
                ability.abilityTags ??= new List<GameplayTag>();
                ability.abilityTags.Add(movement); EditorUtility.SetDirty(ability);
            }
            Require(ability.abilityTags.Count(tag => tag == movement) == 1
                && ability.abilityTags.Where(tag => tag != movement).SequenceEqual(previousTags.Where(tag => tag != movement)),
                "Movement identity tags changed unexpectedly: " + path);
        }
        foreach (string name in new[] { "AD_SwordSkill1", "AD_ApprenticeHeroSwordAttack", "AD_LightningSpearAttack", "AD_FloweringAttack_Base", "AD_SwordCombo2D", "AD_FloweringBloom" })
        {
            var ability = Load<AbilityDefinition>("Assets/_Project/Data/Abilities/Definitions/" + name + ".asset");
            Require(!ability.abilityTags.Contains(movement), "Non-movement ability was tagged: " + name);
        }
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing or incompatible asset: " + path);
        return asset;
    }

    private static T Ensure<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        Require(AssetDatabase.LoadMainAssetAtPath(path) == null, "Incompatible asset already exists: " + path);
        asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [Serializable] private sealed class Report { public List<Entry> entries = new(); }
    [Serializable] private sealed class LocalizedReport { public List<LocalizedRow> rows = new(); }
    [Serializable] private sealed class LocalizedRow { public string key; public string[] values; }
    [Serializable] private sealed class Entry
    {
        public string definitionPath, strategyPath, definitionGuid, strategyGuid;
        public string displayNameIdentity, descriptionIdentity, displayNameKey, descriptionKey, displayName, description;
    }
}
