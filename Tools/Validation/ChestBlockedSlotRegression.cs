using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class ChestBlockedSlotRegression
{
    sealed class Container : IItemContainer
    {
        public ScriptableObject[] Items = new ScriptableObject[2];
        public int SlotCount => Items.Length;
        public event Action OnChanged { add {} remove {} }
        public ScriptableObject Get(int i) => Items[i];
        public bool CanPlace(ScriptableObject item, int i, int ignoreIndex = -1) => true;
        public bool TrySet(int i, ScriptableObject item) { Items[i] = item; return true; }
        public bool TrySwap(int a, int b) { (Items[a], Items[b]) = (Items[b], Items[a]); return true; }
    }
    static void Check(bool c, string m) { if (!c) throw new Exception(m); }
    public static void Run()
    {
        try
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var inventory = new ChestInventory(2); inventory.Set(0, weapon); inventory.Set(1, weapon);
            using var source = new ChestContainerAdapter(inventory);
            var target = new Container(); target.Items[0] = weapon;
            Check(!ChestSelectionTransferService.TryCreatePlanWithFailure(source, new List<int>{0,1}, null, target, null,
                out _, out _, out int failed) && failed == 1, "Only overflow selection should fail");
            Check(inventory.Get(0) == weapon && target.Items[1] == null && inventory.AcquiredCount == 0, "Preview mutated inventory");
            target.Items[0] = null;
            Check(ChestSelectionTransferService.TryCreatePlanWithFailure(source, new List<int>{0,1}, null, target, null,
                out _, out _, out failed) && failed == -1, "Freeing space must clear failure");
            Check(!ChestSelectionTransferService.TryCreatePlanWithFailure(source, new List<int>{0}, null, null, null,
                out _, out _, out failed) && failed == 0, "Missing destination must identify its source");
            var host = new GameObject("Slot", typeof(RectTransform)); host.SetActive(false);
            var slot = host.AddComponent<ItemSlotUI>();
            var border = new GameObject("Hover border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            border.transform.SetParent(host.transform);
            var graphic = border.GetComponent<Image>(); graphic.color = Color.white;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ItemSlotUI).GetField("hoverHighlightRoot", flags).SetValue(slot, border);
            slot.SetSelectionBlocked(true);
            Check(graphic.color == Color.red, "Blocked border must be red");
            var show = typeof(ItemSlotUI).GetMethod("ShouldShowHoverHighlight", flags);
            Check((bool)show.Invoke(slot, new object[]{weapon}), "Blocked item must highlight without hover");
            slot.SetSelectionBlocked(false);
            Check(graphic.color == Color.white && !(bool)show.Invoke(slot, new object[]{weapon}), "Clear must restore normal hover state");
            Debug.Log("CHEST_BLOCKED_SLOT_PASS: capacity attribution, no preview writes, recovery, missing destination, red border and restoration.");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
