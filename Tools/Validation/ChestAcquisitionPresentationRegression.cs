// Run in an isolated Unity 6000.4 project with freshly built gameplay/UI assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ChestAcquisitionPresentationRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Pending = "ChestAcquisitionPresentationRegression.Pending";
    private static Fixture timed;
    private static double started;
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    private static object Call(object o, string name) => o.GetType().GetMethod(name, Private).Invoke(o, null);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < .1f, message + $": {a} != {b}");
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    [InitializeOnLoadMethod]
    private static void Resume() { if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (timed == null)
            {
                Time.timeScale = 0f;
                timed = new Fixture(2, true);
                timed.Confirm();
                started = EditorApplication.timeSinceStartup;
                return;
            }
            if (EditorApplication.timeSinceStartup - started < .12) return;
            Check(timed.Motion.Elapsed() > 0f, "Presentation must advance while timeScale is zero");
            timed.VerifyTwo();
            timed.Dispose();
            using (var one = new Fixture(1, true))
            {
                one.Confirm(); one.Motion.Goto(.16f / 1.2f);
                Near(one.Copy(one.Old[0]).transform.position, one.Drop.TransformPoint(one.Drop.rect.center), "One outgoing slot centered");
                one.Motion.Complete(true);
                one.VerifyFinished();
            }
            using (var empty = new Fixture(1, false))
            {
                empty.Confirm(); Check(empty.Copies.Count == 2, "Empty destination needs only source and empty stand-in");
                empty.Motion.Complete(true); empty.VerifyFinished();
            }
            using (var interrupted = new Fixture(2, true))
            {
                interrupted.Confirm(); interrupted.Motion.Goto(.2f / 1.2f);
                interrupted.Screen.ClearChestBinding();
                Check(interrupted.EventSystem.enabled, "Interruption restores input");
                Check(interrupted.Copies.All(x => !x.gameObject.activeSelf), "Interruption hides temporary copies");
                Check(interrupted.Committed == 1 && interrupted.Inventory.AcquiredCount == 2, "Interruption finalizes once without undoing acquisition");
                interrupted.Screen.ClearChestBinding();
                Check(interrupted.Committed == 1, "Repeated cleanup re-emits completion");
            }
            using (var invalid = new Fixture(1, true))
            {
                Set(invalid.Screen, "returnHighlightSlots", Array.Empty<ItemSlotUI>());
                invalid.Confirm();
                Check(invalid.Inventory.AcquiredCount == 0 && invalid.EventSystem.enabled, "Missing destination must not commit or lock input");
            }
            using (var rejected = new Fixture(1, true)) rejected.VerifyRejectedCommit();
            Time.timeScale = 1f;
            Debug.Log("CHEST_ACQUISITION_PRESENTATION_PASS: paused playback, ordered full-slot travel, one/two/empty weapon targets, centered drops and symmetric reflow, sizing, close gate, duplicate confirm, completion timing and interruption cleanup.");
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick; EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }

    private sealed class Root : IStackableUI
    {
        public int Closed;
        public bool IsActive => Closed == 0;
        public bool CanCloseOnEscape => true;
        public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
        public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
        public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;
        public void OpenUI() { }
        public void CloseUI() { Closed++; }
    }

    private sealed class RejectingContainer : IItemContainer
    {
        public int SlotCount => 1;
        public event Action OnChanged { add { } remove { } }
        public ScriptableObject Get(int index) => null;
        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1) => true;
        public bool TrySet(int index, ScriptableObject item) => false;
        public bool TrySwap(int a, int b) => false;
    }

    private sealed class Fixture : IDisposable
    {
        public readonly ChestScreen Screen;
        public readonly ChestInventory Inventory;
        public readonly EventSystem EventSystem;
        public readonly RectTransform Drop;
        public readonly WeaponDefinition[] Old;
        public readonly WeaponDefinition[] Incoming;
        public readonly ItemSlotUI[] Targets;
        public List<ItemSlotUI> Copies;
        public Sequence Motion;
        public int Committed;
        private readonly List<GameObject> roots = new();
        private readonly Root root = new();
        private readonly ChestContainerAdapter source;
        private readonly PlayerWeaponContainerAdapter target;
        private GameObject Host(string name, params Type[] types)
        {
            var host = new GameObject(name, types); host.SetActive(false); roots.Add(host); return host;
        }
        public Fixture(int count, bool occupied)
        {
            EventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (EventSystem == null)
            {
                EventSystem = Host("Input", typeof(EventSystem)).GetComponent<EventSystem>();
                EventSystem.gameObject.SetActive(true);
            }
            typeof(UIManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            var canvas = Host("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.gameObject.SetActive(true);
            var weapons = Host("Weapons").AddComponent<WeaponInventory2D>(); Call(weapons, "Awake");
            var dropPrefab = Host("World drop", typeof(BoxCollider2D)).AddComponent<WeaponDrop2D>();
            Set(weapons, "dropPrefab", dropPrefab);
            target = new PlayerWeaponContainerAdapter(weapons);
            Old = new[] { Weapon("old-a"), Weapon("old-b") };
            Incoming = new[] { Weapon("new-a"), Weapon("new-b") };
            if (occupied) { Check(weapons.TrySetWeaponSlot(0, Old[0]), "fixture A"); Check(weapons.TrySetWeaponSlot(1, Old[1]), "fixture B"); }
            Inventory = new ChestInventory(2);
            for (int i = 0; i < count; i++) Inventory.Set(i, Incoming[i]);
            source = new ChestContainerAdapter(Inventory, selectionOnly: true);
            Screen = Host("Chest", typeof(RectTransform)).AddComponent<ChestScreen>();
            Screen.SetRootOwner(root); Screen.SelectionCommitted += () => Committed++;
            Set(Screen, "chestInventory", Inventory); Set(Screen, "chestContainer", source);
            Set(Screen, "selectedItemsRoot", canvas.transform);
            var prefab = Slot("Slot template", null, 0, new Vector2(-800, -800), canvas.transform);
            Set(Screen, "chestSlotPrefab", prefab);
            Targets = new ItemSlotUI[2];
            for (int i = 0; i < 2; i++) Targets[i] = Slot("Target", target, i, new Vector2(150 + 120 * i, 100), canvas.transform);
            foreach (var slot in Targets) slot.SlotRect.sizeDelta = new Vector2(96, 96);
            Set(Screen, "returnHighlightSlots", Targets);
            var selected = Get<List<ItemSlotUI>>(Screen, "selectedSlots");
            for (int i = 0; i < count; i++) selected.Add(Slot("Selected", source, i, new Vector2(-250 + 85 * i, 100), canvas.transform));
            var panel = Host("Panel", typeof(RectTransform)).AddComponent<PlayerInventoryPanelView>();
            var zone = Host("DropZone", typeof(RectTransform)).AddComponent<DropZoneUI>();
            Drop = (RectTransform)zone.transform; Drop.SetParent(canvas.transform, false); Drop.localPosition = new Vector3(180, -170, 0);
            Drop.pivot = new Vector2(.5f, 1f); Drop.sizeDelta = new Vector2(400, 160);
            Set(panel, "dropZone", zone); Set(Screen, "selectionInventoryPanel", panel);
            ItemContainerGroupRegistry.SetGroup(source, null, target, null);
        }
        private static WeaponDefinition Weapon(string id) { var w = ScriptableObject.CreateInstance<WeaponDefinition>(); w.weaponId = id; return w; }
        private ItemSlotUI Slot(string name, IItemContainer container, int index, Vector2 position, Transform parent)
        {
            var host = Host(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            host.transform.SetParent(parent, false);
            var slot = host.AddComponent<ItemSlotUI>(); Set(slot, "backgroundImage", host.GetComponent<Image>());
            host.SetActive(true); slot.Bind(container, index);
            slot.SlotRect.sizeDelta = new Vector2(72, 72); slot.transform.localPosition = position;
            return slot;
        }
        public ItemSlotUI Copy(ScriptableObject item) => Copies.First(x => x.CurrentItem == item);
        public void Confirm()
        {
            Call(Screen, "ConfirmSelection");
            object presentation = Get<object>(Screen, "acquisitionPresentation");
            if (presentation != null) { Copies = new List<ItemSlotUI>(Get<List<ItemSlotUI>>(presentation, "copies")); Motion = Get<Sequence>(presentation, "motion"); }
        }
        public void VerifyTwo()
        {
            Check(Inventory.AcquiredCount == 2 && root.Closed == 0 && Committed == 0, "Commit data once but delay completion");
            Check(!EventSystem.enabled && Screen.TryHandleCloseRequest(), "Input and close gate during acquisition");
            Call(Screen, "ConfirmSelection"); Check(Inventory.AcquiredCount == 2, "Duplicate acquisition");
            Vector3 secondStart = Copy(Incoming[1]).transform.position;
            Motion.Goto(.16f / 1.2f);
            Near(Copy(Old[0]).transform.position, Drop.TransformPoint(Drop.rect.center), "First drop centered");
            Near(Copy(Incoming[1]).transform.position, secondStart, "Second choice moved before first completed");
            Motion.Goto(.355f / 1.2f);
            Near(Copy(Incoming[0]).transform.position, Targets[0].transform.position, "First arrival");
            Near(Copy(Incoming[0]).SlotRect.sizeDelta, Targets[0].SlotRect.sizeDelta, "Arrival size");
            Motion.Goto(.56f / 1.2f);
            Vector3 a = Copy(Old[0]).transform.position, b = Copy(Old[1]).transform.position;
            Near((a + b) * .5f, Drop.TransformPoint(Drop.rect.center), "Symmetric reflow center");
            Check(a.x < b.x && b.x - a.x >= 96, "Drop slots overlap");
            Motion.Complete(true); VerifyFinished();
        }
        public void VerifyFinished()
        {
            Check(root.Closed == 1 && Committed == 1 && EventSystem.enabled, "Completion must close once and restore input");
            Check(!Screen.TryHandleCloseRequest(), "Completion left close gate locked");
            Check(Copies.All(x => !x.gameObject.activeSelf), "Temporary views remain visible");
            foreach (var slot in Targets) Check(slot.GetComponent<CanvasGroup>() == null || slot.GetComponent<CanvasGroup>().alpha == 1f, "Target visibility not restored");
        }
        public void VerifyRejectedCommit()
        {
            var rejected = new RejectingContainer();
            Inventory.Set(0, ScriptableObject.CreateInstance<ConsumableDefinition>());
            Targets[0].Bind(rejected, 0);
            ItemContainerGroupRegistry.SetGroup(source, rejected, null, null);
            Confirm();
            Check(Inventory.AcquiredCount == 0 && Inventory.Get(0) != null, "Rejected commit consumed reward");
            Check(EventSystem.enabled && Committed == 0 && root.Closed == 0, "Rejected commit failed to restore interaction or closed chest");
            Check(Get<object>(Screen, "acquisitionPresentation") == null, "Rejected commit retained temporary presentation");
            Check(Targets[0].GetComponent<CanvasGroup>().alpha == 1f, "Rejected commit hid destination");
        }
        public void Dispose()
        {
            Screen.ClearChestBinding(); source.Dispose(); target.Dispose(); ItemContainerGroupRegistry.Clear();
            foreach (var host in roots) if (host != null) UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
