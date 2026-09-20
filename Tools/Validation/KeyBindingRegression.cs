// Run only in an isolated Unity Editor project with freshly built project DLLs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class KeyBindingRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    private static object Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    private static void Set(object owner, string name, object value) =>
        owner.GetType().GetField(name, Private).SetValue(owner, value);

    public static void Run()
    {
        if (Application.productName != "KeyBindingRegression")
            throw new Exception("Isolated regression project required; do not touch the game's preferences.");
        try
        {
            Exercise();
            Debug.Log($"KEY_BINDING_REGRESSION_PASS: {checks} assertions; defaults, fixed keys, swap sweep, persistence, migration, minimap input.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void Exercise()
    {
        PlayerPrefs.DeleteAll(); // Guarded above: separate disposable product name.
        var defaults = ScriptableObject.CreateInstance<InputBindingDefaultsSO>();
        var entries = new List<InputBindingEntry>();
        foreach (Match match in Regex.Matches(File.ReadAllText("Assets/defaults.txt"),
                     @"- action: (\d+)\s+primary: (\d+)\s+secondary: (\d+)"))
            entries.Add(new InputBindingEntry((InputActionId)int.Parse(match.Groups[1].Value),
                (KeyCode)int.Parse(match.Groups[2].Value), (KeyCode)int.Parse(match.Groups[3].Value)));
        Set(defaults, "defaultBindings", entries);
        typeof(InputBindingDefaultsSO).GetField("runtimeInstance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, defaults);
        InputBindingService input = InputBindingService.EnsureInstance();
        Check(input.GetPrimaryKey(InputActionId.InventoryToggle) == KeyCode.V && input.GetSecondaryKey(InputActionId.InventoryToggle) == KeyCode.I, "Inventory V/I");
        Check(input.GetPrimaryKey(InputActionId.MinimapExpand) == KeyCode.M && input.GetSecondaryKey(InputActionId.MinimapExpand) == KeyCode.Equals, "Expand M/+");
        Check(input.GetPrimaryKey(InputActionId.MinimapShrink) == KeyCode.N && input.GetSecondaryKey(InputActionId.MinimapShrink) == KeyCode.Minus, "Shrink N/-");
        Check(input.GetPrimaryKey(InputActionId.InventoryDrop) == KeyCode.F && input.GetPrimaryKey(InputActionId.LevelRewardOpen) == KeyCode.R, "Drop/reward defaults");
        Check(!InputBindingDefaultsSO.IsRemappable(InputActionId.DialogueAdvance), "Fixed dialogue advertised as remappable");
        input.SetBinding(InputActionId.DialogueAdvance, new InputBinding(KeyCode.None));
        input.SwapBindings(InputActionId.DialogueAdvance, false, InputActionId.Skill1, false);
        Check(input.GetPrimaryKey(InputActionId.DialogueAdvance) == KeyCode.Space && input.GetPrimaryKey(InputActionId.Skill1) == KeyCode.Mouse1, "Fixed API protection");

        var panelHost = new GameObject("Inactive binding panel");
        panelHost.SetActive(false);
        var panel = panelHost.AddComponent<KeyBindingPanelUI>();
        Set(panel, "actionOrder", new List<InputActionId> { InputActionId.MoveUp, InputActionId.DialogueAdvance });
        var rows = (IReadOnlyList<InputActionId>)Call(panel, "ResolveActionOrder");
        Check(rows.Count == 19 && !new List<InputActionId>(rows).Contains(InputActionId.DialogueAdvance), "Stale authored row list");

        var keys = new[] { KeyCode.Space, KeyCode.F, KeyCode.R, KeyCode.I, KeyCode.V, KeyCode.M, KeyCode.N,
            KeyCode.Equals, KeyCode.Minus, KeyCode.W, KeyCode.UpArrow, KeyCode.Q, KeyCode.Mouse0, KeyCode.Mouse1 };
        foreach (InputActionId action in input.GetRemappableActions())
        foreach (bool secondary in new[] { false, true })
        foreach (KeyCode key in keys)
        {
            input.ResetAllBindings();
            Call(panel, "LoadWorkingBindingsFromService");
            InputBinding desired = input.GetBinding(action);
            if (secondary) desired.secondary = key; else desired.primary = key;
            var preview = Preview(panel, action, desired);
            Check((secondary ? preview[action].secondary : preview[action].primary) == key, "Requested slot reverted: " + action);
            AssertNoConflicts(preview);
            Check(input.GetPrimaryKey(InputActionId.DialogueAdvance) == KeyCode.Space, "Preview changed fixed key");
        }

        input.ResetAllBindings();
        input.SetPrimaryKey(InputActionId.Dash, KeyCode.T);
        Call(panel, "LoadWorkingBindingsFromService");
        var reset = Preview(panel, InputActionId.Dash, input.GetDefaultBinding(InputActionId.Dash));
        Check(reset[InputActionId.Dash].primary == KeyCode.Space && !reset.ContainsKey(InputActionId.DialogueAdvance), "Reset swaps dialogue");
        var secondarySpace = Preview(panel, InputActionId.Skill1, new InputBinding(KeyCode.Mouse1, KeyCode.Space));
        Check(secondarySpace[InputActionId.Skill1].secondary == KeyCode.Space, "Secondary Space rejected");
        Call(panel, "ApplyWorkingBindings", secondarySpace);
        Call(panel, "HandleApply");
        Call(input, "LoadBindings");
        Check(input.GetSecondaryKey(InputActionId.Skill1) == KeyCode.Space && input.GetPrimaryKey(InputActionId.DialogueAdvance) == KeyCode.Space, "Apply/reload");

        PlayerPrefs.SetInt("settings.input.DialogueAdvance.primary", (int)KeyCode.Mouse1);
        PlayerPrefs.SetInt("settings.input.DialogueAdvance.secondary", (int)KeyCode.R);
        Call(input, "LoadBindings");
        Check(input.GetPrimaryKey(InputActionId.DialogueAdvance) == KeyCode.Space && !PlayerPrefs.HasKey("settings.input.DialogueAdvance.primary"), "Corrupt dialogue migration");
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("settings.input.InventoryToggle.primary", (int)KeyCode.V);
        PlayerPrefs.SetInt("settings.input.InventoryToggle.secondary", 0);
        Call(input, "LoadBindings");
        Check(input.GetSecondaryKey(InputActionId.InventoryToggle) == KeyCode.I, "Legacy empty secondary migration");
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("settings.input.Skill1.primary", (int)KeyCode.M);
        PlayerPrefs.SetInt("settings.input.Skill2.primary", (int)KeyCode.I);
        PlayerPrefs.SetInt("settings.input.InventoryToggle.secondary", 0);
        Call(input, "LoadBindings");
        Check(input.GetPrimaryKey(InputActionId.Skill1) == KeyCode.M && input.GetPrimaryKey(InputActionId.MinimapExpand) == KeyCode.None, "Migration stole M");
        Check(input.GetPrimaryKey(InputActionId.Skill2) == KeyCode.I && input.GetSecondaryKey(InputActionId.InventoryToggle) == KeyCode.None, "Migration stole I");
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("settings.input.Skill2.primary", (int)KeyCode.I);
        Call(input, "LoadBindings");
        Check(input.GetSecondaryKey(InputActionId.InventoryToggle) == KeyCode.None, "Absent inventory override duplicated I");

        input.ResetAllBindings();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            foreach (var sample in new[] { (Key.M, InputActionId.MinimapExpand), (Key.Equals, InputActionId.MinimapExpand),
                         (Key.N, InputActionId.MinimapShrink), (Key.Minus, InputActionId.MinimapShrink),
                         (Key.V, InputActionId.InventoryToggle), (Key.I, InputActionId.InventoryToggle),
                         (Key.F, InputActionId.InventoryDrop), (Key.R, InputActionId.LevelRewardOpen) })
            {
                typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Editor });
                InputState.Change(keyboard, new KeyboardState(), InputUpdateType.Editor);
                typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Editor });
                InputState.Change(keyboard, new KeyboardState(sample.Item1), InputUpdateType.Editor);
                // EditMode injection validates held-state routing; press-edge timing needs a running player loop.
                Check(input.IsPressed(sample.Item2), $"Keyboard state routing failed: {sample}; current={Keyboard.current == keyboard}, pressed={keyboard[sample.Item1].isPressed}, edge={keyboard[sample.Item1].wasPressedThisFrame}, binding={input.GetPrimaryKey(sample.Item2)}/{input.GetSecondaryKey(sample.Item2)}");
            }
        }
        finally { InputSystem.RemoveDevice(keyboard); }

        TestMinimap();
        UnityEngine.Object.DestroyImmediate(panelHost);
    }

    private static Dictionary<InputActionId, InputBinding> Preview(KeyBindingPanelUI panel, InputActionId action, InputBinding desired)
    {
        object[] args = { action, desired, null, null };
        Check((bool)Call(panel, "TryBuildPreviewForBindingChange", args), "Preview failed");
        return (Dictionary<InputActionId, InputBinding>)args[2];
    }

    private static void AssertNoConflicts(Dictionary<InputActionId, InputBinding> bindings)
    {
        var slots = new List<(InputActionId action, KeyCode key)>();
        foreach (var pair in bindings)
        {
            slots.Add((pair.Key, pair.Value.primary));
            slots.Add((pair.Key, pair.Value.secondary));
        }
        for (int i = 0; i < slots.Count; i++)
        for (int j = i + 1; j < slots.Count; j++)
            Check(slots[i].key == KeyCode.None || slots[i].key != slots[j].key ||
                InputBindingDefaultsSO.CanShareKey(slots[i].action, slots[j].action),
                $"Duplicate {slots[i].key}: {slots[i].action}/{slots[j].action}");
    }

    private static void TestMinimap()
    {
        var host = new GameObject("Minimap", typeof(RectTransform), typeof(CanvasGroup));
        host.SetActive(false);
        var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
        body.SetParent(host.transform);
        var map = host.AddComponent<DungeonMinimapSizeController>();
        Set(map, "mapBody", body);
        Call(map, "Awake");
        var probe = new InputProbe { Owner = map, Action = InputActionId.MinimapExpand };
        InputActionQuery.RegisterBackend(probe);
        Call(map, "Update");
        Check(body.localScale.x > 2f, "Expand action not routed");
        probe.Action = InputActionId.MinimapShrink;
        Call(map, "Update");
        Check(body.localScale == Vector3.one, "Shrink action not routed");
        Call(map, "Update");
        Check(!body.gameObject.activeSelf, "Collapse action not routed");
        host.GetComponent<CanvasGroup>().alpha = 0f;
        probe.Action = InputActionId.MinimapExpand;
        Call(map, "Update");
        Check(!body.gameObject.activeSelf, "Hidden map consumed input");
        InputActionQuery.UnregisterBackend(probe);
        UnityEngine.Object.DestroyImmediate(host);
    }

    private sealed class InputProbe : IInputActionQueryBackend
    {
        public Component Owner;
        public InputActionId Action;
        public Component BackendComponent => Owner;
        public Sprite GetBindingIcon(InputActionId action) => null;
        public bool WasPressedThisFrame(InputActionId action) => action == Action;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f) => Vector3.zero;
    }
}
