// Run with freshly built Core/Gameplay/UI DLLs in an isolated Unity 6000.4 project.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class ChestStatPreviewRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0001f, $"{message}: {actual} != {expected}");
    private static AttributeDefinition Attribute(string name, float value, float max = 1000)
    {
        var result = ScriptableObject.CreateInstance<AttributeDefinition>();
        result.name = name; result.defaultBaseValue = value; result.maxValue = max;
        return result;
    }
    private static RelicDefinition Relic(string id, AttributeDefinition attribute, float value, ModifierType type = ModifierType.Flat)
    {
        var relic = ScriptableObject.CreateInstance<RelicDefinition>();
        relic.relicId = id; relic.maxLevel = 3;
        var logic = ScriptableObject.CreateInstance<RelicLogic_StatModifiers>();
        logic.entries.Add(new RelicLogic_StatModifiers.Entry { attribute = attribute, type = type, value = value });
        relic.logic = logic; return relic;
    }
    public static void Run()
    {
        try { Exercise(); Debug.Log($"CHEST_STAT_PREVIEW_PASS: {checks} assertions"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Exercise()
    {
        var attack = Attribute("Attack", 10);
        var health = Attribute("HP", 5);
        var maxHealth = Attribute("MaxHP", 10);
        var move = Attribute("Move", 1);
        var crit = Attribute("Crit", 0, 1);
        Set(health, "mutationPolicy", AttributeMutationPolicy.BaseOnly);
        var catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>();
        Set(catalog, "attributes", new[] { health, attack, crit, move, maxHealth });
        var host = new GameObject("Preview owner"); host.SetActive(false);
        var set = host.AddComponent<AttributeSet>();
        Set(set, "attributeCatalog", catalog);
        Set(set, "maxLinks", new List<AttributeSet.MaxLink> { new() { value = health, max = maxHealth } });
        set.InitializeFromInitialData();
        var relics = host.AddComponent<RelicInventory>(); Call(relics, "Awake");
        var external = ScriptableObject.CreateInstance<AttributeDefinition>();
        var timer = new AttributeModifier(ModifierType.Percent, .2f, external, 20);
        set.TryAddModifier(attack, timer);
        var power = Relic("power", attack, 5);
        Check(relics.TryAcquireOrUpgrade(power), "Equip baseline relic");
        Near(set.GetCurrentValue(attack), 18, "Baseline");
        int statEvents = 0, inventoryEvents = 0;
        set.OnAttributeChanged += (_, __, ___) => statEvents++;
        relics.OnChanged += () => inventoryEvents++;
        var penalty = Relic("penalty", attack, -2);
        var snapshot = set.CreatePreviewSnapshot();
        relics.ProjectAcquisitions(snapshot, new[] { (power, 1), (penalty, 1) });
        Near(snapshot[attack].CurrentValue, 21.6f, "Upgrade replaces old effect; second selection combines before percent");
        Near(set.GetCurrentValue(attack), 18, "Live attack unchanged");
        Near(timer.TimeRemaining, 20, "Live modifier timer unchanged");
        Check(statEvents == 0 && inventoryEvents == 0 && relics.GetRelicLevelInSlot(0) == 1, "Preview has no events or inventory writes");
        Check(relics.TryAcquireOrUpgrade(power) && relics.TryAcquireOrUpgrade(penalty), "Commit reference acquisitions");
        Near(set.GetCurrentValue(attack), snapshot[attack].CurrentValue, "Preview equals actual acquisition");

        var heart = Relic("heart", maxHealth, 2);
        snapshot = set.CreatePreviewSnapshot();
        relics.ProjectAcquisitions(snapshot, new[] { (heart, 1), (heart, 1) });
        Near(snapshot[maxHealth].CurrentValue, 14, "Two copies of new relic merge to level two");
        Near(snapshot[health].CurrentValue, 9, "Linked HP compensation");
        Near(set.GetCurrentValue(health), 5, "Live HP unchanged");
        Check(relics.TryAcquireOrUpgrade(heart) && relics.TryAcquireOrUpgrade(heart), "Commit HP acquisitions");
        Near(set.GetCurrentValue(health), snapshot[health].CurrentValue, "HP projection equals actual");

        var criticalRelic = ScriptableObject.CreateInstance<RelicDefinition>(); criticalRelic.relicId = "crit";
        var criticalLogic = ScriptableObject.CreateInstance<RelicLogic_CritFromBonusMoveSpeed_Managed>();
        criticalLogic.moveSpeedMultiplierAttributeFallback = move; criticalLogic.critChanceAddAttribute = crit;
        criticalRelic.logic = criticalLogic;
        Check(relics.TryAcquireOrUpgrade(criticalRelic), "Equip reactive crit relic");
        var speed = Relic("speed", move, .2f, ModifierType.Percent);
        snapshot = set.CreatePreviewSnapshot();
        relics.ProjectAcquisitions(snapshot, new[] { (speed, 1) });
        Near(snapshot[move].CurrentValue, 1.2f, "Projected movement");
        Near(snapshot[crit].CurrentValue, .02f, "Existing reactive relic sees projected movement");
        Check(relics.TryAcquireOrUpgrade(speed), "Commit movement");
        Near(set.GetCurrentValue(crit), snapshot[crit].CurrentValue, "Reactive projection equals actual");

        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.statModifiers.Add(new WeaponDefinition.WeaponStatModifier { attribute = move, type = ModifierType.Flat, value = .1f });
        snapshot = set.CreatePreviewSnapshot();
        relics.ProjectAcquisitions(snapshot, Array.Empty<(RelicDefinition, int)>(), null, weapon);
        Near(snapshot[move].CurrentValue, 1.32f, "Weapon and relic modifiers combine");
        Near(snapshot[crit].CurrentValue, .03f, "Weapon change updates conditional crit");

        var bindings = ScriptableObject.CreateInstance<StatTypeBindings>();
        Set(bindings, "bindings", new List<StatTypeBindings.Binding> {
            new() { id = StatId.AttackBase, attribute = attack }, new() { id = StatId.MoveSpeedBase, attribute = move } });
        Set(bindings, "composites", new List<StatTypeBindings.Composite> {
            new() { finalId = StatId.AttackFinal, baseId = StatId.AttackBase } });
        var provider = new AttributeStatProvider(set, bindings);
        Near(provider.Get(StatId.AttackFinal, a => snapshot[a].CurrentValue), snapshot[attack].CurrentValue, "Composite missing multiplier defaults to one");

        var chest = new ChestInventory(2); chest.SetRelicWithLevel(0, power, 1); chest.SetRelicWithLevel(1, penalty, 1);
        using var source = new ChestContainerAdapter(chest);
        using var target = new PlayerRelicContainerAdapter(relics);
        Check(ChestSelectionTransferService.TryCreatePlan(source, new[] { 0 }, null, null, target, out var plan, out _), "Valid upgrade plan");
        var viewHost = new GameObject("Stats view"); viewHost.SetActive(false);
        var view = viewHost.AddComponent<PlayerStatPanelView>(); view.Bind(host.transform);
        var row = ScriptableObject.CreateInstance<StatInfoUIDefinition>(); Set(row, "valueAttribute", attack);
        view.SetSelectionPreview(plan);
        string text = (string)Call(view, "ResolveValueText", row);
        Check(text.Contains("#66DD88") && text.Contains("(+6)"), "Positive delta is green and uses final stat calculation");
        view.SetSelectionPreview(null);
        Check(!((string)Call(view, "ResolveValueText", row)).Contains("color"), "Deselection clears delta");
        Check(ChestSelectionTransferService.TryCreatePlan(source, new[] { 1 }, null, null, target, out plan, out _), "Negative upgrade plan");
        view.SetSelectionPreview(plan);
        Check(((string)Call(view, "ResolveValueText", row)).Contains("#FF6666"), "Negative delta is red");
        Set(row, "displayFormat", PlayerStatDisplayFormat.Percent); Set(row, "valueMultiplier", 100f);
        Check(((string)Call(view, "ResolveValueText", row)).Contains("-240%"), "Percentage formatting and multiplier");
        Call(view, "OnDisable");
        Check(!((string)Call(view, "ResolveValueText", row)).Contains("color"), "Disable clears preview");
        Near(set.GetCurrentValue(attack), 21.6f, "UI preview never commits stats");
        Check(chest.Get(0) == power && chest.Get(1) == penalty, "UI preview never consumes rewards");
        var weapons = host.AddComponent<WeaponInventory2D>();
        var old = ScriptableObject.CreateInstance<WeaponDefinition>();
        Set(weapons, "slots", new[] { old, weapon }); Set(weapons, "activeIndex", 1);
        Check(weapons.PreviewEquippedAfterAcquisitions(new[] { (0, old) }) == weapon, "Replacing inactive slot preserves active weapon");
        Check(weapons.PreviewEquippedAfterAcquisitions(new[] { (0, weapon), (1, old) }) == old, "Two replacements preserve active index");
        Set(weapons, "activeIndex", -1);
        Check(weapons.PreviewEquippedAfterAcquisitions(new[] { (0, old), (1, weapon) }) == old, "No equipped weapon selects first available slot");
    }
}
