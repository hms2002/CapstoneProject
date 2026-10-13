#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Verifies panel-owned view reuse, adapter disposal, and real inventory transfer semantics.</summary>
public sealed class PlayerInventorySlotReusePlayModeTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type PanelType = Type.GetType("PlayerInventoryPanelView, UI", true);
    private static readonly Type SlotType = Type.GetType("ItemSlotUI, UI", true);
    private static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);

    [Test]
    public void RepeatedPlayerRebind_ReusesInstancesAndDisposesOldAdaptersWithSingleSubscriptions()
    {
        using (var panel = new PanelFixture())
        using (var firstPlayer = new PlayerFixture())
        using (var secondPlayer = new PlayerFixture())
        {
            panel.Bind(firstPlayer);
            Component[] instances = panel.Slots();
            IItemContainer previous = panel.Container("ConsumableContainer");
            Assert.That(EventCount(previous, "OnChanged"), Is.EqualTo(firstPlayer.Consumables.SlotCount));
            for (int i = 0; i < 3; i++)
            {
                panel.Clear();
                Assert.That(EventCount(previous, "OnChanged"), Is.Zero);
                Assert.That(EventCount(firstPlayer.Consumables, "OnChanged"), Is.Zero);
                foreach (Component slot in instances)
                {
                    Assert.That(BoundContainer(slot), Is.Null);
                    Assert.That(slot.gameObject.activeSelf, Is.False);
                }
                panel.Bind(secondPlayer);
                CollectionAssert.AreEqual(instances, panel.Slots());
                IItemContainer current = panel.Container("ConsumableContainer");
                Assert.That(current, Is.Not.SameAs(previous));
                Assert.That(EventCount(current, "OnChanged"), Is.EqualTo(secondPlayer.Consumables.SlotCount));
                Assert.That(EventCount(secondPlayer.Consumables, "OnChanged"), Is.EqualTo(1));
                previous = current;
            }

            var potion = ScriptableObject.CreateInstance<ConsumableDefinition>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                potion.icon = sprite;
                Assert.That(secondPlayer.Consumables.TrySetConsumableSlot(0, potion), Is.True);
                Component slot = panel.GridSlots(panel.ConsumableRoot)[0];
                Component icon = (Component)SlotType.GetField("icon", Fields).GetValue(slot);
                Assert.That(ImageType.GetProperty("sprite").GetValue(icon), Is.SameAs(sprite),
                    "The new adapter must still refresh the reused Image synchronously.");
                Assert.That(firstPlayer.Consumables.TrySetConsumableSlot(0, potion), Is.True);
                Assert.That(BoundContainer(slot), Is.SameAs(previous));
            }
            finally
            {
                Object.DestroyImmediate(potion);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }

    [Test]
    public void InactivePreparedBind_ClearAndRebind_DetachesSubscriptionsBeforeFirstEnable()
    {
        using (var panel = new PanelFixture(active: false))
        using (var player = new PlayerFixture())
        {
            panel.Bind(player);
            Component[] original = panel.Slots();
            IItemContainer old = panel.Container("RelicContainer");
            panel.Clear();
            panel.Clear();
            Assert.That(EventCount(old, "OnChanged"), Is.Zero);
            Assert.That(EventCount(player.Relics, "OnChanged"), Is.Zero);
            foreach (Component slot in original) Assert.That(BoundContainer(slot), Is.Null);
            panel.Bind(player);
            CollectionAssert.AreEqual(original, panel.Slots());
            panel.Root.SetActive(true);
            Assert.That(EventCount(player.Relics, "OnChanged"), Is.EqualTo(1));
            Assert.That(panel.GridSlots(panel.RelicRoot).Length, Is.EqualTo(Mathf.Max(24, player.Relics.Capacity)));
        }
    }

    [Test]
    public void ConfigurationCountAndExternalParentChanges_DiscardOnlyAffectedGroupWithoutDormantExtras()
    {
        using (var panel = new PanelFixture())
        using (var player = new PlayerFixture())
        {
            panel.Bind(player);
            Component[] oldConsumables = panel.GridSlots(panel.ConsumableRoot);
            Component[] weapons = panel.GridSlots(panel.WeaponRoot);
            typeof(PlayerConsumableInventory).GetField("slots", Fields).SetValue(player.Consumables, new ConsumableDefinition[2]);
            panel.Bind(player);
            Assert.That(panel.GridSlots(panel.ConsumableRoot).Length, Is.EqualTo(2));
            foreach (Component old in oldConsumables) Assert.That(old.transform.parent, Is.Null);
            CollectionAssert.AreEqual(weapons, panel.GridSlots(panel.WeaponRoot));

            Component[] previous = panel.GridSlots(panel.ConsumableRoot);
            SlotType.GetField("ownerInventoryScreen", Fields).SetValue(previous[0], null);
            previous[0].transform.SetParent(panel.WeaponRoot, false);
            panel.Bind(player);
            foreach (Component old in previous) Assert.That(old.transform.parent, Is.Null);
            Assert.That(panel.GridSlots(panel.WeaponRoot).Length, Is.EqualTo(player.Weapons.SlotCount));

            var alternate = new GameObject("AlternateConsumableGrid", typeof(RectTransform));
            alternate.transform.SetParent(panel.Root.transform, false);
            previous = panel.GridSlots(panel.ConsumableRoot);
            PanelType.GetField("consumableGridRoot", Fields).SetValue(panel.Panel, alternate.transform);
            PanelType.GetField("consumableSlotPrefab", Fields).SetValue(panel.Panel, panel.LoadSlot("ItemSlotUI.prefab"));
            panel.Bind(player);
            Assert.That(panel.GridSlots(panel.ConsumableRoot).Length, Is.Zero);
            Assert.That(panel.GridSlots(alternate.transform).Length, Is.EqualTo(2));
            foreach (Component old in previous) Assert.That(old.transform.parent, Is.Null);
            foreach (Component slot in panel.GridSlots(alternate.transform))
                Assert.That(slot.transform.GetSiblingIndex(), Is.EqualTo((int)SlotType.GetProperty("BoundIndex").GetValue(slot)));

            player.Relics.SetRuntimeCapacityBonus(player.Root, 32);
            panel.Bind(player);
            Assert.That(panel.GridSlots(panel.RelicRoot).Length, Is.EqualTo(player.Relics.Capacity));
            player.Relics.SetRuntimeCapacityBonus(player.Root, 0);
            panel.Bind(player);
            Assert.That(panel.GridSlots(panel.RelicRoot).Length, Is.EqualTo(Mathf.Max(24, player.Relics.Capacity)));
            Assert.That(panel.Slots().Length, Is.EqualTo(2 + player.Weapons.SlotCount + Mathf.Max(24, player.Relics.Capacity)));
        }
    }

    [Test]
    public void ClearBinding_ResetsPanelInteractionState_WithoutChangingGenericChestDisableMeaning()
    {
        using (var panel = new PanelFixture())
        using (var player = new PlayerFixture())
        {
            panel.Bind(player);
            Component slot = panel.GridSlots(panel.ConsumableRoot)[0];
            IItemContainer original = BoundContainer(slot);
            Action<Component> selected = _ => Assert.Fail("A reused player slot cannot retain an old selection closure.");
            Type callbackType = SlotType.GetMethod("SetSelectionClickHandler").GetParameters()[0].ParameterType;
            Delegate callback = Delegate.CreateDelegate(callbackType, selected.Target, selected.Method);
            SlotType.GetMethod("SetSelectionClickHandler").Invoke(slot, new object[] { callback });
            SlotType.GetMethod("SetChestSelectionOverlay").Invoke(slot, new object[] { true });
            SlotType.GetMethod("SetChestReturnHighlight").Invoke(slot, new object[] { true });
            SlotType.GetMethod("SetSelectionBlocked").Invoke(slot, new object[] { true });
            SlotType.GetField("isPointerOver", Fields).SetValue(slot, true);
            SlotType.GetField("isPointerPressed", Fields).SetValue(slot, true);
            slot.gameObject.SetActive(false);
            Assert.That(BoundContainer(slot), Is.SameAs(original), "Generic chest/snapshot disable retains binding metadata.");
            Assert.That(SlotType.GetField("selectionClicked", Fields).GetValue(slot), Is.SameAs(callback));

            panel.Clear();
            Assert.That(BoundContainer(slot), Is.Null);
            Assert.That((int)SlotType.GetProperty("BoundIndex").GetValue(slot), Is.EqualTo(-1));
            foreach (string field in new[] { "isPointerOver", "isPointerPressed", "isDraggingThisSlot", "wasChestReturnCandidate", "selectionBlocked", "chestSelectionOverlay" })
                Assert.That((bool)SlotType.GetField(field, Fields).GetValue(slot), Is.False, field);
            Assert.That(SlotType.GetField("selectionClicked", Fields).GetValue(slot), Is.Null);
            Assert.That(SlotType.GetField("ownerInventoryScreen", Fields).GetValue(slot), Is.Null);
            Assert.That(SlotType.GetField("hoverHighlightRoutine", Fields).GetValue(slot), Is.Null);
            panel.Bind(player);
            Assert.That(panel.GridSlots(panel.ConsumableRoot)[0], Is.SameAs(slot));
        }
    }

    [Test]
    public void ReusedSlots_ActualDropTransfer_UsesNewAdaptersAndPreservesItems()
    {
        using (var sourcePanel = new PanelFixture())
        using (var targetPanel = new PanelFixture())
        using (var sourcePlayer = new PlayerFixture())
        using (var targetPlayer = new PlayerFixture())
        {
            var potion = ScriptableObject.CreateInstance<ConsumableDefinition>();
            Type drag = Type.GetType("ItemDragContext, UI", true);
            bool previousInspectionOnly = ItemContainerGroupRegistry.IsInspectionOnly;
            try
            {
                ItemContainerGroupRegistry.SetInspectionOnly(false);
                sourcePanel.Bind(sourcePlayer);
                targetPanel.Bind(targetPlayer);
                Component targetSlot = targetPanel.GridSlots(targetPanel.ConsumableRoot)[0];
                targetPanel.Clear();
                targetPanel.Bind(targetPlayer);
                Assert.That(targetPanel.GridSlots(targetPanel.ConsumableRoot)[0], Is.SameAs(targetSlot));
                Assert.That(sourcePlayer.Consumables.TrySetConsumableSlot(0, potion), Is.True);
                drag.GetMethod("Begin").Invoke(null, new object[] { sourcePanel.Container("ConsumableContainer"), 0, potion, 0 });
                SlotType.GetMethod("OnDrop").Invoke(targetSlot, new object[] { null });
                Assert.That(sourcePlayer.Consumables.GetConsumableInSlot(0), Is.Null);
                Assert.That(targetPlayer.Consumables.GetConsumableInSlot(0), Is.SameAs(potion));
                Assert.That((bool)drag.GetProperty("Active").GetValue(null), Is.False);
            }
            finally
            {
                drag.GetMethod("CancelActiveDragSession").Invoke(null, null);
                ItemContainerGroupRegistry.SetInspectionOnly(previousInspectionOnly);
                Object.DestroyImmediate(potion);
            }
        }
    }

    [Test]
    public void MatchingHoverAndOwnedDrag_ClearBeforeAdapterDisposal()
    {
        using (var panel = new PanelFixture())
        using (var player = new PlayerFixture())
        {
            Type hoverType = Type.GetType("ItemHoverController, UI", true);
            FieldInfo singleton = hoverType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = singleton.GetValue(null);
            var hoverRoot = new GameObject("InventoryReuseHoverFixture");
            hoverRoot.SetActive(false);
            Component hover = hoverRoot.AddComponent(hoverType);
            var potion = ScriptableObject.CreateInstance<ConsumableDefinition>();
            Type drag = Type.GetType("ItemDragContext, UI", true);
            try
            {
                singleton.SetValue(null, hover);
                panel.Bind(player);
                Component slot = panel.GridSlots(panel.ConsumableRoot)[0];
                IItemContainer adapter = BoundContainer(slot);
                hoverType.GetField("currentSlotRect", Fields).SetValue(hover, slot.transform);
                hoverType.GetField("currentSourceContainer", Fields).SetValue(hover, adapter);
                hoverType.GetField("currentSourceIndex", Fields).SetValue(hover, 0);
                drag.GetMethod("Begin").Invoke(null, new object[] { adapter, 0, potion, 0 });
                panel.Clear();
                Assert.That(hoverType.GetField("currentSourceContainer", Fields).GetValue(hover), Is.Null);
                Assert.That(hoverType.GetField("currentSlotRect", Fields).GetValue(hover), Is.Null);
                Assert.That((bool)drag.GetProperty("Active").GetValue(null), Is.False);
                Assert.That(EventCount(adapter, "OnChanged"), Is.Zero);
            }
            finally
            {
                drag.GetMethod("CancelActiveDragSession").Invoke(null, null);
                singleton.SetValue(null, previous);
                Object.DestroyImmediate(hoverRoot);
                Object.DestroyImmediate(potion);
            }
        }
    }

    [UnityTest]
    public IEnumerator ActiveOwnerDestroy_ReleasesAdaptersAndRetainedViews_ConfigureRemainsFillOnly()
    {
        using (var player = new PlayerFixture())
        using (var panel = new PanelFixture())
        {
            var other = new GameObject("IgnoredConfigureRoot", typeof(RectTransform));
            try
            {
                panel.Configure(other.transform, other.transform, other.transform);
                Assert.That(PanelType.GetField("consumableGridRoot", Fields).GetValue(panel.Panel), Is.SameAs(panel.ConsumableRoot));
                panel.Bind(player);
                Component[] slots = panel.Slots();
                IItemContainer old = panel.Container("ConsumableContainer");
                panel.Root.SetActive(false);
                Assert.That(EventCount(old, "OnChanged"), Is.Zero);
                panel.Root.SetActive(true);
                panel.Bind(player);
                Object.Destroy(panel.Root);
                yield return null;
                Assert.That(EventCount(player.Consumables, "OnChanged"), Is.Zero);
                Assert.That(EventCount(player.Relics, "OnChanged"), Is.Zero);
                yield return null;
                foreach (Component slot in slots) Assert.That(slot == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }

    private static IItemContainer BoundContainer(Component slot) => (IItemContainer)SlotType.GetProperty("BoundContainer").GetValue(slot);
    private static int EventCount(object owner, string name) =>
        ((Delegate)owner.GetType().GetField(name, Fields).GetValue(owner))?.GetInvocationList().Length ?? 0;

    private sealed class PlayerFixture : IDisposable
    {
        public readonly GameObject Root = new GameObject("InventoryReusePlayer");
        public readonly PlayerConsumableInventory Consumables;
        public readonly WeaponInventory2D Weapons;
        public readonly RelicInventory Relics;
        public PlayerFixture()
        {
            Consumables = Root.AddComponent<PlayerConsumableInventory>();
            Weapons = Root.AddComponent<WeaponInventory2D>();
            Relics = Root.AddComponent<RelicInventory>();
        }
        public void Dispose() => Object.DestroyImmediate(Root);
    }

    private sealed class PanelFixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly Component Panel;
        public readonly Transform ConsumableRoot;
        public readonly Transform WeaponRoot;
        public readonly Transform RelicRoot;
        public PanelFixture(bool active = true)
        {
            Root = new GameObject("InventorySlotReusePanel", typeof(RectTransform));
            Root.SetActive(false);
            ConsumableRoot = MakeGrid("Consumables");
            WeaponRoot = MakeGrid("Weapons");
            RelicRoot = MakeGrid("Relics");
            Panel = Root.AddComponent(PanelType);
            Configure(ConsumableRoot, WeaponRoot, RelicRoot);
            if (active) Root.SetActive(true);
        }
        public Component LoadSlot(string filename) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Inventory/Common/" + filename).GetComponent(SlotType);
        public void Configure(Transform consumables, Transform weapons, Transform relics) =>
            PanelType.GetMethod("Configure").Invoke(Panel, new object[] { consumables, weapons, relics, null,
                LoadSlot("ItemSlotUI_Consumable.prefab"), LoadSlot("ItemSlotUI_Weapon.prefab"), LoadSlot("ItemSlotUI.prefab"), null, Root.transform });
        public void Bind(PlayerFixture player) => PanelType.GetMethod("Bind").Invoke(Panel,
            new object[] { player.Consumables, player.Weapons, player.Relics, player.Root.transform, player.Root.transform });
        public void Clear() => PanelType.GetMethod("ClearBinding").Invoke(Panel, null);
        public IItemContainer Container(string name) => (IItemContainer)PanelType.GetProperty(name).GetValue(Panel);
        public Component[] Slots() => Root.GetComponentsInChildren(SlotType, true);
        public Component[] GridSlots(Transform root) => root.GetComponentsInChildren(SlotType, true);
        private Transform MakeGrid(string name)
        {
            var grid = new GameObject(name, typeof(RectTransform));
            grid.transform.SetParent(Root.transform, false);
            return grid.transform;
        }
        public void Dispose()
        {
            if (Root == null) return;
            Clear();
            Object.DestroyImmediate(Root);
        }
    }
}
#endif
