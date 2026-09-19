// Isolated Unity 6000.4 Editor harness; never runs the main project bootstrap.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class TutorialGuidanceRecoveryRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run()
    {
        try
        {
            typeof(PrototypeChestNavigationRegression).GetMethod("Exercise", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Exercise();
            InventoryCompletion();
            Debug.Log("TUTORIAL_GUIDANCE_PASS: chest ownership preserved; dash locked before cue and released on disable; rebind lock; sequential glyph objectives; chest then inventory completion; other input owners preserved.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Exercise()
    {
        var host = new GameObject("Tutorial fixture"); host.SetActive(false);
        var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
        Set(tutorial, "skillTargets", Array.Empty<Transform>());
        Set(tutorial, "gates", Array.Empty<GameObject>());
        Set(tutorial, "chargeSkill", ScriptableObject.CreateInstance<AbilityDefinition>());
        Set(tutorial, "thrustSkill", ScriptableObject.CreateInstance<AbilityDefinition>());
        Call(tutorial, "Awake");
        Call(tutorial, "OnEnable");
        var playerHost = new GameObject("Player fixture"); playerHost.SetActive(false);
        playerHost.AddComponent<AbilitySystem>();
        var player = playerHost.AddComponent<PlayerInteractor2D>();
        Call(tutorial, "BindPlayer", player);
        Check(InputActionQuery.IsPressBlocked(InputActionId.Dash), "Dash accepted before the cue");
        Check(tutorial.IsMovementPrompt && tutorial.PromptAction == InputActionId.MoveUp, "Entry must only show movement");
        Call(tutorial, "OnDisable");
        Check(!InputActionQuery.IsPressBlocked(InputActionId.Dash), "Disable before cue leaked dash block");
        Call(tutorial, "OnEnable"); Call(tutorial, "BindPlayer", player);
        Check(InputActionQuery.IsPressBlocked(InputActionId.Dash), "Rebind lost pre-cue block");
        Set(tutorial, "stage", 1);
        Check(!tutorial.IsDashPromptVisible && tutorial.IsMovementPrompt, "Dash hint appeared before camera cue");
        var phase = tutorial.GetType().GetField("dashIntro", Flags).FieldType;
        Set(tutorial, "dashIntro", Enum.Parse(phase, "ZoomIn"));
        Check(tutorial.IsDashPromptVisible && tutorial.PromptAction == InputActionId.Dash, "Cue must present Dash glyph");
        object otherOwner = new object();
        InputActionQuery.SetPressBlocked(InputActionId.Dash, otherOwner, true);
        Call(tutorial, "CleanupDashIntro");
        Check(InputActionQuery.IsPressBlocked(InputActionId.Dash), "Cleanup released another owner's block");
        InputActionQuery.SetPressBlocked(InputActionId.Dash, otherOwner, false);
        Check(!InputActionQuery.IsPressBlocked(InputActionId.Dash), "Cue completion retained dash block");
        Set(tutorial, "stage", 3); Set(tutorial, "chargeKills", 0);
        Check(tutorial.PromptAction == InputActionId.Skill1, "Right click must precede Q");
        Set(tutorial, "chargeKills", 4);
        Check(tutorial.PromptAction == InputActionId.Skill2, "Q glyph did not follow charged kills");
        Set(tutorial, "stage", 4);
        tutorial.CompleteInventoryTutorial();
        Check(tutorial.Stage == 4, "Inventory completed before chest");
        tutorial.CompleteChestTutorial();
        Check(tutorial.Stage == 5 && tutorial.PromptAction == InputActionId.InventoryToggle, "Chest skipped inventory lesson");
        tutorial.CompleteChestTutorial();
        Check(tutorial.Stage == 5, "Repeated chest completion skipped lesson");
        tutorial.CompleteInventoryTutorial(); tutorial.CompleteInventoryTutorial();
        Check(tutorial.Stage == 6 && tutorial.IsMovementPrompt, "Inventory completion must advance exactly once");
        Call(tutorial, "OnDisable");
        UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(playerHost);
    }
    private static GameObject RectHost(string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.SetActive(false); return go;
    }

    private static void InventoryCompletion()
    {
        var host = RectHost("Inventory guide fixture");
        var guide = host.AddComponent<PrototypeChestNavigation>();
        var tutorialHost = RectHost("Stage fixture");
        var tutorial = tutorialHost.AddComponent<PrototypeTutorialUpgrade>();
        Set(tutorial, "stage", 5); Set(tutorial, "gates", Array.Empty<GameObject>());
        // Enable only the component logically through an inactive parent; run the private tick explicitly.
        // isActiveAndEnabled requires an active host, so provide the minimal initialized scene director.
        Set(tutorial, "skillTargets", Array.Empty<Transform>());
        Call(tutorial, "Awake");
        tutorialHost.SetActive(true); Set(tutorial, "stage", 5);
        var playerHost = RectHost("Guidance player");
        var player = playerHost.AddComponent<PlayerInteractor2D>();
        typeof(PlayerRuntimeRegistry).GetField("<CurrentPlayer>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, player);
        var screenHost = RectHost("Inventory window"); screenHost.transform.SetParent(host.transform);
        var screen = screenHost.AddComponent<InventoryScreen>();
        var manager = host.AddComponent<InventoryUIManager>(); Set(manager, "inventoryScreen", screen);
        var handler = host.AddComponent<InventoryUIOpenRequestHandler>(); Set(handler, "inventoryUIManager", manager);
        var buttonHost = RectHost("HUD button");
        var button = buttonHost.AddComponent<InventoryOpenHudButton>(); Set(button, "hudRoot", buttonHost);
        var shield = RectHost("Shield"); shield.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 700);
        var image = shield.AddComponent<UnityEngine.UI.Image>();
        var glyph = RectHost("Glyph").AddComponent<UnityEngine.UI.Image>();
        var instruction = RectHost("Instruction"); instruction.AddComponent<UnityEngine.UI.Image>();
        var shades = new RectTransform[4];
        for (int i = 0; i < 4; i++) { var shade = RectHost("Shade"); shade.AddComponent<UnityEngine.UI.Image>(); shades[i] = shade.GetComponent<RectTransform>(); }
        Set(guide, "tutorial", tutorial); Set(guide, "inventoryGuidance", true); Set(guide, "inventoryInputArmed", true);
        Set(guide, "inventoryButton", button); Set(guide, "inventoryHandler", handler);
        Set(guide, "inputShield", shield.GetComponent<RectTransform>()); Set(guide, "shieldImage", image);
        Set(guide, "shadePanels", shades); Set(guide, "rightClickGlyph", glyph); Set(guide, "instruction", instruction.GetComponent<RectTransform>());
        var input = new InputProbe(player);
        InputActionQuery.RegisterBackend(input);
        input.Pressed = true;
        Call(guide, "TickInventoryGuidance");
        Check(tutorial.Stage == 5, "A rejected V open completed the lesson");
        screenHost.SetActive(true); input.Pressed = false; InputActionQuery.RegisterBackend(input);
        Call(guide, "TickInventoryGuidance");
        Check(tutorial.Stage == 5, "An already-open window without a fresh V completed the lesson");
        input.Pressed = true; InputActionQuery.RegisterBackend(input);
        InputActionQuery.SetPressBlocked(InputActionId.Dash, guide, true);
        Call(guide, "TickInventoryGuidance");
        Check(tutorial.Stage == 6 && !image.raycastTarget, "Successful V open did not complete and release the shield");
        Check(!InputActionQuery.IsPressBlocked(InputActionId.Dash), "Inventory completion leaked its dash block");
        InputActionQuery.UnregisterBackend(input);
        typeof(PlayerRuntimeRegistry).GetField("<CurrentPlayer>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        tutorialHost.SetActive(false);
    }

    private sealed class InputProbe : IInputActionQueryBackend
    {
        public bool Pressed;
        public Component BackendComponent { get; }
        public InputProbe(Component component) { BackendComponent = component; }
        public Sprite GetBindingIcon(InputActionId action) => null;
        public bool WasPressedThisFrame(InputActionId action) => action == InputActionId.InventoryToggle && Pressed;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => action == InputActionId.InventoryToggle && Pressed;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f) => Vector3.zero;
    }

}
