// Run in an isolated Unity Editor project with freshly built gameplay/UI assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ChestSelectionRecoveryRegression
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object value) => o.GetType().GetField(n, Private).SetValue(o, value);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, Private).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Private).Invoke(o, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static GameObject Host(string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.SetActive(false); return go;
    }
    static void Singleton(Type type, object value) => type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    static WeaponDefinition Weapon(string id) { var w = ScriptableObject.CreateInstance<WeaponDefinition>(); w.weaponId = id; return w; }
    static RelicDefinition Relic(string id) { var r = ScriptableObject.CreateInstance<RelicDefinition>(); r.relicId = id; r.maxLevel = 10; return r; }
    static int Drops => Resources.FindObjectsOfTypeAll<WeaponDrop2D>().Count(d => d.name.EndsWith("(Clone)"));

    public static void Run()
    {
        try
        {
            Weapons(); RerollAndGrid(); LootTableReservations(); FixedInitialSize();
            Debug.Log("CHEST_RECOVERY_PASS: one/two weapon replacement, empty slot, duplicate and drop failure safety, mixed acquisition failure, retained relic levels, reroll budget, selected grid parenting and panel visibility.");
            Debug.Log("CHEST_POLICY_SIZE_PASS: retained type quotas, weapon exclusion, retained maximum relic, stage/override/consumable rules, fixed initial frame/grid through selection/reroll/return and reset for next chest.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Weapons()
    {
        var inv = Host("Weapons").AddComponent<WeaponInventory2D>();
        Call(inv, "Awake");
        var dropHost = Host("Drop prefab"); dropHost.AddComponent<BoxCollider2D>();
        var drop = dropHost.AddComponent<WeaponDrop2D>();
        Set(inv, "dropPrefab", drop);
        using var target = new PlayerWeaponContainerAdapter(inv);
        var a = Weapon("A"); var b = Weapon("B"); var c = Weapon("C"); var d = Weapon("D");
        Check(inv.TrySetWeaponSlot(0, a) && inv.TrySetWeaponSlot(1, b), "fixture weapons");
        inv.Equip(1);
        int before = Drops;
        Acquire(c);
        Check(inv.GetWeaponInSlot(0) == c && inv.GetWeaponInSlot(1) == b && inv.ActiveIndex == 1, "one weapon replaces slot 1, not active slot 2");
        Check(Drops == before + 1, "one outgoing world drop");
        Check(Resources.FindObjectsOfTypeAll<WeaponDrop2D>().Any(x => x.name.EndsWith("(Clone)") && x.Weapon == a), "drop contains replaced weapon");
        before = Drops;
        Acquire(b, d); // B is currently in slot 2: final pair validation must allow this batch.
        Check(inv.GetWeaponInSlot(0) == b && inv.GetWeaponInSlot(1) == d && Drops == before + 2, "two weapons replace both old weapons");
        before = Drops;
        Check(!inv.PreviewChestSelection(new[] { d }, out _) && !inv.TryAcquireChestSelection(new[] { a, a }), "duplicate rejection");
        Check(Drops == before && inv.GetWeaponInSlot(0) == b, "rejected batch changed weapons");
        Set(inv, "dropPrefab", null);
        Check(!inv.TryAcquireChestSelection(new[] { c }), "missing drop prefab must not lose the old weapon");
        Set(inv, "dropPrefab", drop);
        Check(inv.TrySetWeaponSlot(1, null), "free slot fixture");
        var retainedRuntime = inv.GetRuntimeDataInSlot(0);
        Acquire(c);
        Check(inv.GetWeaponInSlot(0) == b && inv.GetWeaponInSlot(1) == c && Drops == before, "empty slot must not drop existing weapon");
        Check(ReferenceEquals(retainedRuntime, inv.GetRuntimeDataInSlot(0)), "unchanged weapon runtime reset");

        var chest = new ChestInventory(2); chest.Set(0, a);
        var consumable = ScriptableObject.CreateInstance<ConsumableDefinition>(); chest.Set(1, consumable);
        using var source = new ChestContainerAdapter(chest);
        var rejection = new RejectingContainer();
        Check(ChestSelectionTransferService.TryCreatePlan(source, new[] { 0, 1 }, rejection, target, null, out var plan, out _), "mixed plan");
        Check(!ChestSelectionTransferService.TryCommitPlan(plan).Succeeded, "mixed transfer should reject");
        Check(chest.Count == 2 && chest.AcquiredCount == 0 && Drops == before && inv.GetWeaponInSlot(0) == b, "failed companion transfer dropped weapon or consumed loot");
        using (var seal = inv.TryAcquireSlotSeal(1))
        {
            Check(seal != null, "seal fixture");
            Acquire(a, d);
            Check(inv.ActiveIndex == 0 && !inv.IsSlotAccessible(1) && inv.GetWeaponInSlot(1) == d, "sealed slot storage or equip policy changed");
        }

        void Acquire(params WeaponDefinition[] incoming)
        {
            var inventory = new ChestInventory(2);
            for (int i = 0; i < incoming.Length; i++) inventory.Set(i, incoming[i]);
            using var chestSource = new ChestContainerAdapter(inventory);
            Check(ChestSelectionTransferService.TryCreatePlan(chestSource, Enumerable.Range(0, incoming.Length).ToArray(), null, target, null, out var p, out _), "weapon plan");
            Check(inventory.AcquiredCount == 0 && inventory.Count == incoming.Length, "preview mutated chest");
            Check(ChestSelectionTransferService.TryCommitPlan(p).Succeeded, "weapon commit");
            Check(inventory.Count == 0 && inventory.AcquiredCount == incoming.Length, "weapon receipts/source clear");
            int dropCount = Drops;
            Check(!ChestSelectionTransferService.TryCommitPlan(p).Succeeded && Drops == dropCount, "repeated commit duplicated weapons");
        }
    }

    sealed class RejectingContainer : IItemContainer
    {
        public int SlotCount => 1;
        public event Action OnChanged { add {} remove {} }
        public ScriptableObject Get(int index) => null;
        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1) => true;
        public bool TrySet(int index, ScriptableObject item) => false;
        public bool TrySwap(int a, int b) => false;
    }

    static void RerollAndGrid()
    {
        var modifiers = Host("Modifiers").AddComponent<RunModifierService>();
        Set(modifiers, "hasLoadedFromSave", true);
        Set(modifiers, "chestModifiers", new ChestRunModifierDelta { chestRefreshCount = 2 });
        Singleton(typeof(RunModifierService), modifiers);
        var loot = Host("Loot").AddComponent<LootManager>();
        Set(loot, "stageTables", new List<StageLootTable> { ScriptableObject.CreateInstance<StageLootTable>() });
        Call(loot, "RefreshServices", true);
        Singleton(typeof(LootManager), loot);
        int generated = 0;
        var serviceType = typeof(LootManager).Assembly.GetType("ChestLootGenerationService");
        var service = Activator.CreateInstance(serviceType, new object[] { new LootPoolService(), new LootRollService(),
            (Func<RelicDefinition>)(() => Relic("Generated" + generated++)), (Func<ItemRarity, RelicDefinition>)(_ => null) });
        Set(loot, "chestLootGenerationService", service);
        var profile = new ChestLootOverrideProfile();
        Set(profile, "totalLootCount", 4);
        Set(profile, "weaponCountProfile", FixedCount(1));
        Set(profile, "relicCountProfile", FixedCount(3));
        var inventory = new ChestInventory(8);
        var retained = Relic("Retained");
        inventory.SetRelicWithLevel(0, retained, 3);
        inventory.Set(1, Weapon("RetainedWeapon")); inventory.Set(2, Relic("Old2")); inventory.Set(3, Relic("Old3"));
        var chest = Host("Chest").AddComponent<TreasureChest>();
        Set(chest, "inventory", inventory); Set(chest, "isGenerated", true);
        Set(chest, "lootMode", ChestLootMode.OverrideProfile); Set(chest, "lootOverrideProfile", profile);
        var manager = Host("Chest manager").AddComponent<ChestUIManager>();
        Singleton(typeof(ChestUIManager), manager); Set(manager, "openedChest", chest);
        var screen = Host("Screen").AddComponent<ChestScreen>();
        using var adapter = new ChestContainerAdapter(inventory, selectionOnly: true);
        Set(screen, "chestInventory", inventory); Set(screen, "chestContainer", adapter);
        var grid = Host("Offers"); grid.AddComponent<GridLayoutGroup>();
        var selected = Host("Selected grid"); selected.AddComponent<GridLayoutGroup>();
        for (int i = 0; i < 2; i++)
        {
            var cell = Host("SelectedItemSlot" + i); cell.transform.SetParent(selected.transform, false);
            ((RectTransform)cell.transform).sizeDelta = new Vector2(72, 72);
        }
        Set(screen, "chestGridRoot", grid.transform); Set(screen, "selectedItemsRoot", (RectTransform)selected.transform);
        var group = Host("Selected panel").AddComponent<CanvasGroup>(); Set(screen, "selectedItemsGroup", group);
        var prefab = Host("Slot prefab").AddComponent<ItemSlotUI>(); Set(screen, "chestSlotPrefab", prefab);
        Call(screen, "BuildChestSlots");
        var slots = Get<List<ItemSlotUI>>(screen, "spawnedChestSlots");
        var first = slots[0]; var second = slots[1];
        Call(screen, "ToggleSelection", first); Call(screen, "StopSelectionMotion");
        Call(screen, "ToggleSelection", second); Call(screen, "StopSelectionMotion");
        Check(first.transform.parent == selected.transform.GetChild(0) && second.transform.parent == selected.transform.GetChild(1), "selected items must be inside fixed grid cells");
        Call(screen, "BeginRerollHold"); Call(screen, "RefreshSelectionControls");
        Check(group.alpha == 1f && !group.interactable, "selected panel hidden during reroll");
        Call(screen, "CompleteRerollHold");
        Check(inventory.Get(0) == retained && inventory.GetRelicLevelInSlot(0) == 3 && inventory.Count == 4, "reroll changed retained item/level or offer count");
        Check(inventory.AcquiredCount == 0 && chest.RefreshCountUsed == 1, "reroll acquisition/use accounting");
        Check(slots.Contains(first) && slots.Contains(second) && Get<List<ItemSlotUI>>(screen, "selectedSlots").Count == 2, "reroll replaced selected UI objects");
        Check(group.alpha == 1f, "selected panel hidden after reroll");
        Call(screen, "ResetRerollHoldState");
        Call(screen, "ToggleSelection", first); Call(screen, "StopSelectionMotion");
        Check(first.transform.parent == grid.transform && second.transform.parent == selected.transform.GetChild(0), "deselection reflow broke cells");
        Check(!chest.TryRefreshLoot(new[] { 7 }) && chest.RefreshCountUsed == 1, "invalid preservation charged reroll");
        Check(chest.TryRefreshLoot(new[] { 1 }) && chest.RefreshCountUsed == 2, "second reroll");
        Check(!chest.TryRefreshLoot(new[] { 1 }) && chest.RefreshCountUsed == 2, "exhausted reroll changed budget");
        Call(screen, "ClearChestSlots");
        Check(selected.transform.childCount == 2 && selected.transform.GetChild(0).childCount == 0, "cleanup removed authored cells or leaked selected slot");
    }

    static CountRangeWeightProfile FixedCount(int count) => new CountRangeWeightProfile { minCount = count, maxCount = count,
        weights = new List<DropCountOption> { new DropCountOption { count = count, weight = 100 } } };

    static void LootTableReservations()
    {
        var a = Weapon("Weapon.ApprenticeHeroSword"); var b = Weapon("Weapon.LightningSpear");
        var retainedRelic = Relic("RetainedMax"); retainedRelic.maxLevel = 1;
        var otherRelic = Relic("OtherRelic");
        var potion = ScriptableObject.CreateInstance<ConsumableDefinition>();
        var database = ScriptableObject.CreateInstance<ItemDatabase>();
        database.allWeapons = database.defaultUnlockedWeapons = new List<WeaponDefinition> { a, b };
        database.allRelics = database.defaultUnlockedRelics = new List<RelicDefinition> { retainedRelic, otherRelic };
        database.allConsumables = new List<ConsumableDefinition> { potion };
        var manager = Host("Item database fixture").AddComponent<ItemManager>();
        Set(manager, "database", database); manager.Initialize(null); Singleton(typeof(ItemManager), manager);
        var table = ScriptableObject.CreateInstance<StageLootTable>();
        Set(table, "chestWeaponCountProfile", FixedCount(2)); Set(table, "chestRelicCountProfile", FixedCount(2));
        Set(table, "chestConsumableCountProfile", FixedCount(1));
        var profile = new ChestLootOverrideProfile();
        Set(profile, "weaponCountProfile", FixedCount(2)); Set(profile, "relicCountProfile", FixedCount(2));
        Set(profile, "totalLootCount", 5);
        var serviceType = typeof(LootManager).Assembly.GetType("ChestLootGenerationService");
        var service = Activator.CreateInstance(serviceType, new object[] { new LootPoolService(), new LootRollService(),
            (Func<RelicDefinition>)(() => retainedRelic), (Func<ItemRarity, RelicDefinition>)(_ => retainedRelic) });
        var method = serviceType.GetMethod("Generate");
        foreach (var overrideProfile in new ChestLootOverrideProfile[] { null, profile })
        {
            for (int trial = 0; trial < 12; trial++)
            {
                var request = new ChestLootRequest(default, LootPoolContext.PlayerInventory, overrideProfile,
                    new ScriptableObject[] { a, retainedRelic });
                var result = (ChestLootResult)method.Invoke(service, new object[] { table, request, default(ChestRunModifierDelta) });
                Check(result.Items.Count == 3 && result.Items.OfType<WeaponDefinition>().Single() == b &&
                    result.Items.OfType<RelicDefinition>().Count() == 1 && result.Items.OfType<ConsumableDefinition>().Count() == 1,
                    "retained type quotas or weapon ban list violated");
            }
            var potionRequest = new ChestLootRequest(default, LootPoolContext.PlayerInventory, overrideProfile,
                new ScriptableObject[] { a, potion });
            var potionResult = (ChestLootResult)method.Invoke(service, new object[] { table, potionRequest, default(ChestRunModifierDelta) });
            Check(potionResult.Items.Count == 3 && !potionResult.Items.OfType<ConsumableDefinition>().Any(), "retained consumable counted twice");
        }
        Set(profile, "fillRemainingWithConsumables", false); Set(profile, "consumableCountProfile", FixedCount(3));
        var fixedRequest = new ChestLootRequest(default, LootPoolContext.PlayerInventory, profile, new ScriptableObject[] { a, potion });
        var fixedResult = (ChestLootResult)method.Invoke(service, new object[] { table, fixedRequest, default(ChestRunModifierDelta) });
        Check(fixedResult.Items.OfType<ConsumableDefinition>().Count() == 2, "fixed override consumable quota");

        // Exercise the real chest fill path: a maxed retained relic must never be offered again.
        var modifiers = RunModifierService.Instance; Set(modifiers, "chestModifiers", new ChestRunModifierDelta { chestRefreshCount = 2 });
        var loot = LootManager.Instance;
        Set(loot, "stageTables", new List<StageLootTable> { table }); Call(loot, "RefreshServices", true);
        Set(loot, "chestLootGenerationService", service);
        var inventory = new ChestInventory(8); inventory.Set(0, a); inventory.SetRelicWithLevel(1, retainedRelic, 1);
        var chest = Host("Reserved loot chest").AddComponent<TreasureChest>();
        Set(chest, "inventory", inventory); Set(chest, "isGenerated", true);
        Check(chest.TryRefreshLoot(new[] { 0, 1 }), "reserved loot refresh rejected");
        var items = Enumerable.Range(0, inventory.Capacity).Select(inventory.Get).Where(x => x != null).ToArray();
        Check(items.Length == 5 && items.Count(x => x == a) == 1 && items.Count(x => x == retainedRelic) == 1 &&
            items.Contains(b) && items.Contains(otherRelic), "retained relic cap/weapon uniqueness not applied during fill");
        Singleton(typeof(ItemManager), null);
    }

    static void FixedInitialSize()
    {
        var host = Host("Reveal fixture");
        var presentation = host.AddComponent<ChestFirstOpenRevealPresentation>();
        Set(presentation, "applyLayoutInEditMode", false);
        var panel = (RectTransform)Host("ChestPanel").transform; panel.SetParent(host.transform, false);
        var middle = (RectTransform)Host("MiddleFrame").transform; middle.SetParent(panel, false); middle.gameObject.SetActive(true);
        var left = (RectTransform)Host("Left decoration").transform; left.SetParent(middle, false); left.gameObject.SetActive(true);
        left.sizeDelta = new Vector2(56, 415);
        var grid = (RectTransform)Host("Grid").transform; grid.SetParent(middle, false); grid.gameObject.SetActive(true);
        var right = (RectTransform)Host("Right decoration").transform; right.SetParent(middle, false); right.gameObject.SetActive(true);
        right.sizeDelta = new Vector2(56, 415);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
        layout.cellSize = new Vector2(90, 90); layout.spacing = new Vector2(5, 5);
        layout.padding = new RectOffset(10, 10, 30, 10);
        var fitter = grid.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Set(presentation, "chestPanel", panel); Set(presentation, "middleFrame", middle); Set(presentation, "gridRoot", grid);
        var items = new List<Transform>();
        for (int i = 0; i < 5; i++)
        {
            var item = Host("Item " + i); item.transform.SetParent(grid, false); item.SetActive(true); items.Add(item.transform);
        }
        presentation.CaptureInitialLayout(); Call(presentation, "ApplyRevealPose", 1f, null);
        Vector2 initialPanel = panel.rect.size; Vector2 initialGrid = grid.rect.size;
        Check(!fitter.enabled && initialGrid.y == 225f, "initial two-row capture failed");
        Check(middle.rect.height == 225f && left.rect.height == 225f && right.rect.height == 225f,
            "authored decoration height overrode initial item count");
        for (int i = 4; i < 5; i++) items[i].SetParent(host.transform, false);
        Call(presentation, "ApplyRevealPose", 1f, null);
        Check(panel.rect.size == initialPanel && grid.rect.size == initialGrid, "selection row reduction shrank frame/grid");
        Call(presentation, "ApplyRevealPose", 0f, null);
        Check(panel.rect.height < initialPanel.y, "existing close animation was removed");
        Call(presentation, "ApplyRevealPose", 1f, null);
        Check(panel.rect.size == initialPanel && grid.rect.size == initialGrid, "reroll reopened at smaller size");
        for (int i = 4; i < 5; i++) items[i].SetParent(grid, false);
        Call(presentation, "ApplyRevealPose", 1f, null);
        Check(panel.rect.size == initialPanel && grid.rect.size == initialGrid, "returning selected items resized chest");
        presentation.ReleaseInitialLayout(); Check(fitter.enabled, "fitter not restored on unbind");
        for (int i = 4; i < 5; i++) items[i].SetParent(host.transform, false);
        presentation.CaptureInitialLayout(); Call(presentation, "ApplyRevealPose", 1f, null);
        Check(grid.rect.height == 130f && middle.rect.height == 130f && left.rect.height == 130f &&
            right.rect.height == 130f && panel.rect.height < initialPanel.y, "next chest inherited stale dimensions");
        presentation.ReleaseInitialLayout();
    }
}
