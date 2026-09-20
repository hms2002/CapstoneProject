// Isolated Unity Editor harness using freshly built UI/Gameplay/Core and their dependencies.
// -batchmode -nographics -executeMethod PrototypeChestNavigationRegression.Run
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public static class PrototypeChestNavigationRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
    private static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, Private).GetValue(o);
    private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Private).Invoke(o, a);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static GameObject Host(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.SetActive(false);
        return go;
    }

    public static void Run()
    {
        try
        {
            Exercise();
            Debug.Log("CHEST_TUTORIAL_PASS: owner isolation; normal selection; only first-slot right click; selection unlock; keyboard/stack restoration; cancellation; tutorial stage completion.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Exercise()
    {
        // Keep screens inactive to avoid starting the game's persistent bootstrap in an Edit Mode fixture.
        var screenGo = Host("Chest fixture");
        var screen = screenGo.AddComponent<ChestScreen>();
        var grid = Host("Grid");
        var selectedRoot = Host("Selected");
        for (int i = 0; i < 2; i++)
        {
            var cell = Host("SelectedItemSlot " + i);
            cell.transform.SetParent(selectedRoot.transform, false);
            ((RectTransform)cell.transform).sizeDelta = new Vector2(72, 72);
        }
        var item = ScriptableObject.CreateInstance<ConsumableDefinition>();
        var inventory = new ChestInventory(2);
        inventory.Set(0, ScriptableObject.CreateInstance<ConsumableDefinition>());
        inventory.Set(1, item);
        using var adapter = new ChestContainerAdapter(inventory);
        Set(screen, "chestInventory", inventory);
        Set(screen, "chestContainer", adapter);
        Set(screen, "chestGridRoot", grid.transform);
        Set(screen, "selectedItemsRoot", (RectTransform)selectedRoot.transform);
        var slots = Get<List<ItemSlotUI>>(screen, "spawnedChestSlots");
        for (int i = 0; i < 2; i++)
        {
            var go = Host("Slot " + i);
            go.transform.SetParent(grid.transform, false);
            var slot = go.AddComponent<ItemSlotUI>();
            Set(slot, "container", adapter);
            Set(slot, "index", i);
            ((RectTransform)go.transform).sizeDelta = new Vector2(100, 100);
            go.transform.position = new Vector3(200 + i * 200, 200, 0);
            slots.Add(slot);
        }
        var chosen = Get<List<ItemSlotUI>>(screen, "selectedSlots");
        var otherOwner = Host("Unrelated owner");
        var chestGo = Host("Tutorial chest");
        var chest = chestGo.AddComponent<TreasureChest>();
        Set(chest, "inventory", inventory);
        var tutorialGo = Host("Tutorial owner");
        var tutorial = tutorialGo.AddComponent<PrototypeTutorialUpgrade>();
        Set(tutorial, "gates", Array.Empty<GameObject>());
        Set(tutorial, "bullets", Array.Empty<Transform>());
        Set(tutorial, "stage", 4);
        var guideGo = Host("Guidance");
        var guide = guideGo.AddComponent<PrototypeChestNavigation>();
        var shield = Host("Shield");
        var shieldImage = shield.AddComponent<Image>();
        var panels = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            panels[i] = (RectTransform)Host("Shade").transform;
            panels[i].gameObject.AddComponent<Image>();
        }
        var iconGo = Host("Icon");
        var icon = iconGo.AddComponent<Image>();
        var label = Host("Instruction");
        label.AddComponent<Image>();
        Set(guide, "targetChest", chest);
        Set(guide, "tutorialPotion", item);
        Set(guide, "tutorial", tutorial);
        Set(guide, "inputShield", (RectTransform)shield.transform);
        Set(guide, "shadePanels", panels);
        Set(guide, "rightClickGlyph", icon);
        Set(guide, "instruction", (RectTransform)label.transform);
        var eventsGo = new GameObject("Events", typeof(EventSystem));
        var events = eventsGo.GetComponent<EventSystem>();
        Call(events, "OnEnable"); // Edit Mode does not automatically register the runtime EventSystem.
        EventSystem.current = events;
        events.sendNavigationEvents = true;
        var backend = new StackProbe();
        UiStackPlayback.RegisterBackend(backend);

        // A normal chest still accepts the existing selection path without guidance ownership.
        Call(screen, "ToggleSelection", slots[1]);
        Check(chosen.Count == 1 && chosen[0] == slots[1], "Normal chest selection regressed");
        Call(screen, "StopSelectionMotion");
        Call(screen, "ToggleSelection", slots[1]);
        Call(screen, "StopSelectionMotion");
        Check(chosen.Count == 0, "Normal chest deselection regressed");

        Set(tutorial, "stage", 3);
        Call(guide, "OnChestOpened", chest);
        Check(!backend.Blocked, "Chest guidance started before the tutorial stage");
        Set(tutorial, "stage", 4);
        Call(guide, "OnChestOpened", chest);
        Check(backend.Blocked && !events.sendNavigationEvents && shield.activeSelf, "Guidance did not acquire all input restrictions");
        // Exercise unscaled presentation independently of the inactive screen fixture's LateUpdate.
        Check(screen.FindVisibleSlot(item) == slots[1], "Potion lookup depended on first-slot order");
        Call(guide, "LayoutSpotlight", slots[1].SlotRect, false);
        Set(guide, "showing", true);
        Call(guide, "TickPresentation", .06f);
        float openingAlpha = icon.canvasRenderer.GetAlpha();
        Check(openingAlpha > 0f && openingAlpha < 1f && shieldImage.raycastTarget,
            "Opening must fade smoothly while retaining the input shield");
        Vector2 rest = Get<Vector2>(guide, "instructionPosition");
        Check(label.transform.localPosition.y < rest.y, "Opening instruction must rise from below");
        Call(guide, "TickPresentation", .06f);
        Check(Mathf.Approximately(icon.canvasRenderer.GetAlpha(), 1f), "Opening must reach full visibility");
        Check(!screen.AcquireGuidedSelection(otherOwner), "Unrelated owner replaced the guidance owner");
        screen.ReleaseGuidedSelection(otherOwner);
        Check(!screen.TrySelectGuidedFirstSlot(otherOwner), "Unrelated owner forwarded selection");
        Call(screen, "ToggleSelection", slots[1]);
        Call(screen, "ConfirmSelection");
        Check(chosen.Count == 0 && inventory.Count == 2, "Blocked selection/confirm changed inventory");
        Check(!(bool)Call(screen, "CanStartRerollHold"), "Reroll accepted while guidance owned input");

        var click = new PointerEventData(events) { position = new Vector2(200, 200), button = PointerEventData.InputButton.Left };
        guide.OnPointerClick(click);
        Check(chosen.Count == 0 && backend.Blocked, "Wrong-slot left click advanced the tutorial");
        click.button = PointerEventData.InputButton.Right;
        click.position = new Vector2(200, 200);
        guide.OnPointerClick(click);
        Check(chosen.Count == 0 && backend.Blocked, "Another slot advanced the tutorial");
        click.position = new Vector2(400, 200);
        click.button = PointerEventData.InputButton.Left;
        guide.OnPointerClick(click);
        Check(chosen.Count == 1 && chosen[0] == slots[1], "Potion-slot left click failed");
        Check(!backend.Blocked && events.sendNavigationEvents && !shieldImage.raycastTarget && shield.activeSelf,
            "Selection must immediately restore input while the exit remains visible");
        Call(guide, "TickPresentation", .05f);
        Check(icon.canvasRenderer.GetAlpha() > 0f && icon.canvasRenderer.GetAlpha() < 1f,
            "Exit must fade rather than hide immediately");
        Call(guide, "TickPresentation", .05f);
        Check(!shield.activeSelf, "Exit must deactivate the shield on completion");
        Check(tutorial.Stage == 4, "Selection prematurely completed the chest quest");
        Call(screen, "StopSelectionMotion");
        Call(screen, "ToggleSelection", slots[1]);
        Call(screen, "StopSelectionMotion");
        Call(screen, "ToggleSelection", slots[0]);
        Call(screen, "StopSelectionMotion");
        Call(screen, "ConfirmSelection");
        Check(inventory.Count == 2 && chosen.Count == 1, "Confirmation without the potion must be rejected");
        Call(screen, "ToggleSelection", slots[1]);
        Check(chosen.Count == 2, "Selecting the potion again must preserve normal optional selection");
        Call(screen, "StopSelectionMotion");
        Call(guide, "Cleanup");

        Call(guide, "OnChestOpened", chest);
        Call(guide, "OnDisable");
        Check(!backend.Blocked && events.sendNavigationEvents && !shield.activeSelf, "Cancellation leaked input restrictions");
        Check(screen.AcquireGuidedSelection(otherOwner), "Cancellation leaked chest ownership");
        screen.ReleaseGuidedSelection(otherOwner);
        Call(guide, "OnCommitted");
        Check(tutorial.Stage == 5, "Committed chest did not complete tutorial quest");
        tutorial.CompleteChestTutorial();
        Check(tutorial.Stage == 5, "Chest completion was not idempotent");
        UiStackPlayback.RegisterBackend(null);
        DOTween.KillAll();
    }

    private sealed class StackProbe : IUiStackBackend
    {
        public bool Blocked;
        public bool SetExternalUiInputBlocked(UnityEngine.Object owner, bool value) { Blocked = value; return true; }
        public bool CanOpenUIForExternalBlockOwner(UnityEngine.Object owner, IStackableUI ui) => !Blocked;
        public bool TryPushUIForExternalBlockOwner(UnityEngine.Object owner, IStackableUI ui) => !Blocked;
    }
}
