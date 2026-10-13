// Run in a disposable Unity Editor project with freshly built runtime DLLs.
// Exercises native UI navigation/input without opening production scenes or preferences.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UiKeyboardNavigationRegression
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Pending = "UiKeyboardNavigationRegression.Pending";
    private static int checks;
    private static Keyboard keyboard;
    private static EventSystem eventSystem;
    private static InputSystemUIInputModule inputModule;

    static UiKeyboardNavigationRegression()
    {
        EditorApplication.update += () =>
        {
            if (EditorApplication.isPlaying && !EditorApplication.isCompiling &&
                SessionState.GetBool(Pending, false) && Time.frameCount > 2)
                Exercise();
        };
    }

    public static void Run()
    {
        if (!Application.dataPath.Contains("CapstoneUiKeyboard-") || Application.productName != "UiKeyboardRegression")
            throw new Exception("A disposable validation project is required.");

        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void Exercise()
    {
        try
        {
            SessionState.SetBool(Pending, false);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = new GameObject("Events", typeof(EventSystem)).GetComponent<EventSystem>();
            inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            EventSystem.current = eventSystem;
            inputModule.ActivateModule();

            new GameObject("KeyboardRegressionRunner").AddComponent<UiKeyboardNavigationProbeRunner>().StartCoroutine(RunGuarded());
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator RunGuarded()
    {
        // Unroll nested routines so assertion failures exit batchmode with a failure code.
        var routines = new Stack<IEnumerator>();
        routines.Push(TestDialogueEnter());
        routines.Push(TestPopupOwnership());
        routines.Push(TestSpaceConfirmation(false));
        routines.Push(TestSpaceConfirmation(true));
        routines.Push(TestCardNavigation());
        routines.Push(TestMenuNavigation());
        while (routines.Count > 0)
        {
            object next = null;
            try
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext())
                {
                    routines.Pop();
                    continue;
                }
                next = routine.Current;
                if (next is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Debug.Log($"UI_KEYBOARD_REGRESSION_PASS: {checks} assertions; native WASD/arrows/Enter, local Space submit/release guards, focus restoration, disabled controls, modal ownership, dialogue press/release gate.");
        EditorApplication.Exit(0);
    }

    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    private static object Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, InstanceMembers).Invoke(owner, args);

    private static object Field(object owner, string name) => owner.GetType().GetField(name, InstanceMembers).GetValue(owner);

    private static Button Button(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        if (parent != null) go.transform.SetParent(parent, false);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        return button;
    }

    private static object Group(bool horizontal, params Button[] buttons) =>
        Activator.CreateInstance(typeof(TitleMenuController).Assembly.GetType("ButtonNavigationGroup"), new object[] { horizontal, buttons });

    private static void State(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    private static IEnumerator Press(Key key)
    {
        State();
        yield return null;
        State(key);
        yield return null;
    }

    private static void Selected(Button button, string reason) =>
        Check(eventSystem.currentSelectedGameObject == button.gameObject,
            reason + $" Selected={eventSystem.currentSelectedGameObject?.name}, moveEnabled={inputModule.move.action.enabled}, move={inputModule.move.action.ReadValue<Vector2>()}, keyPressed={keyboard.sKey.isPressed}");

    private static IEnumerator TestMenuNavigation()
    {
        GameObject root = new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup));
        Button first = Button("Continue", root.transform);
        Button middle = Button("Settings", root.transform);
        Button last = Button("Quit", root.transform);
        object group = Group(false, first, middle, last);
        Call(group, "Refresh", true);
        Selected(first, "Menu did not select the first button.");
        yield return Press(Key.S); Selected(middle, "S did not move down.");
        yield return Press(Key.DownArrow); Selected(last, "Down arrow did not move down.");
        yield return Press(Key.S); Selected(last, "Navigation escaped the last menu button.");
        yield return Press(Key.W); Selected(middle, "W did not move up.");
        yield return Press(Key.UpArrow); Selected(first, "Up arrow did not move up.");
        yield return Press(Key.DownArrow); Selected(middle, "Menu selection could not be restored for submit.");
        int submissions = 0;
        middle.onClick.AddListener(() => submissions++);
        yield return Press(Key.Enter);
        Check(submissions == 1, $"Enter did not submit exactly once. Count={submissions}, submitEnabled={inputModule.submit.action.enabled}, pressed={inputModule.submit.action.WasPressedThisFrame()}");
        State(Key.Enter); yield return null;
        Check(submissions == 1, "Holding Enter submitted again.");
        State(); yield return null;

        Call(group, "Refresh", false);
        Check(eventSystem.currentSelectedGameObject == null, "Suspension retained the background menu selection.");
        Check(first.navigation.mode == Navigation.Mode.None, "Suspended buttons remained navigation targets.");
        Button overlay = Button("Overlay");
        eventSystem.SetSelectedGameObject(overlay.gameObject);
        Call(group, "Refresh", false);
        Selected(overlay, "Suspension cleared another screen's focus.");
        Call(group, "Refresh", true);
        Selected(middle, "Closing the overlay did not restore the prior menu button.");
        eventSystem.SetSelectedGameObject(null);
        Call(group, "Refresh", true);
        Selected(middle, "Background click permanently lost menu focus.");

        middle.interactable = false;
        Call(group, "Refresh", true);
        Selected(first, "Disabled selection did not fall back to an enabled button.");
        yield return Press(Key.S); Selected(last, "Navigation did not skip the disabled button.");
        last.gameObject.SetActive(false);
        Call(group, "Refresh", true);
        Selected(first, "Inactive selection did not fall back.");
        root.GetComponent<CanvasGroup>().interactable = false;
        Call(first, "OnCanvasGroupChanged");
        Call(group, "Reset"); Call(group, "Refresh", true);
        Check(eventSystem.currentSelectedGameObject == null, "Opening animation's CanvasGroup lock was bypassed.");
        root.GetComponent<CanvasGroup>().interactable = true;
        Call(first, "OnCanvasGroupChanged");
        Call(group, "Refresh", true);
        Selected(first, "Focus did not appear after the animation unlocked.");
        Call(group, "Reset");
    }

    private static IEnumerator TestCardNavigation()
    {
        Button first = Button("Card1"), second = Button("Card2"), third = Button("Card3");
        object group = Group(true, first, second, third);
        Call(group, "Refresh", true);
        Selected(first, "Card offer did not select its first card.");
        yield return Press(Key.D); Selected(second, "D did not select the next card.");
        yield return Press(Key.RightArrow); Selected(third, "Right arrow did not select the next card.");
        yield return Press(Key.A); Selected(second, "A did not select the prior card.");
        yield return Press(Key.LeftArrow); Selected(first, "Left arrow did not select the prior card.");
        yield return Press(Key.W); Selected(first, "Vertical input escaped the card group.");
        int picked = 0;
        first.onClick.AddListener(() => picked++);
        Call(group, "Refresh", false);
        yield return Press(Key.Enter);
        Check(picked == 0, "Enter selected a card while its group was suspended.");
        Call(group, "Refresh", true);
        yield return Press(Key.Enter);
        Check(picked == 1, "Enter did not select the focused card.");
        second.gameObject.SetActive(false);
        Call(group, "Refresh", true);
        yield return Press(Key.D); Selected(third, "A smaller offer did not skip an absent card.");
        Call(group, "Reset"); Call(group, "Refresh", true);
        Selected(first, "A refreshed offer did not reset selection.");
        Call(group, "Reset");
    }

    private static IEnumerator TestSpaceConfirmation(bool horizontal)
    {
        // Match screen open/unlock, modal resume and card-offer refresh while Space is held.
        GameObject root = new GameObject("SpaceGroup", typeof(RectTransform), typeof(CanvasGroup));
        Button button = Button(horizontal ? "SpaceCard" : "SpaceMenu", root.transform);
        int submissions = 0;
        button.onClick.AddListener(() => submissions++);
        State(Key.Space); yield return null;
        object group = Group(horizontal, button);
        Call(group, "Refresh", true);
        Check(submissions == 0, "Space held before screen opening submitted immediately.");
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 0, "An inherited Space hold submitted on the following frame.");
        State(); yield return null; Call(group, "Refresh", true);
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 1, "A fresh Space press did not submit the selected control exactly once.");
        Call(group, "Refresh", true);
        Check(submissions == 1, "Refreshing twice submitted the same Space press again.");
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 1, "Holding Space repeatedly submitted.");

        Call(group, "Refresh", false);
        Button overlay = Button("SpaceExcludedOverlay");
        int overlaySubmissions = 0;
        overlay.onClick.AddListener(() => overlaySubmissions++);
        eventSystem.SetSelectedGameObject(overlay.gameObject);
        yield return Press(Key.Space); Call(group, "Refresh", false);
        Check(submissions == 1, "A background group processed Space.");
        Check(overlaySubmissions == 0, "Space was incorrectly added to the global Submit action.");
        Selected(overlay, "Space suspension stole the overlay's focus.");
        Call(group, "Refresh", true);
        Check(submissions == 1, "Resuming while Space was held submitted immediately.");
        State(); yield return null; Call(group, "Refresh", true);
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 2, "Space release/repress after overlay return did not submit.");

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        Call(button, "OnCanvasGroupChanged"); Call(group, "Refresh", true);
        State(); yield return null; Call(group, "Refresh", true);
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 2, "Space bypassed a CanvasGroup animation lock.");
        canvasGroup.interactable = true;
        Call(button, "OnCanvasGroupChanged"); Call(group, "Refresh", true);
        Check(submissions == 2, "Unlocking while Space was held submitted immediately.");
        State(); yield return null; Call(group, "Refresh", true);
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 3, "Space did not submit after animation unlock and release.");

        State(); yield return null; Call(group, "Refresh", true);
        State(Key.Enter, Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 4, "Simultaneous Enter and Space submitted twice.");
        Call(group, "Reset"); Call(group, "Refresh", true);
        Check(submissions == 4, "A new offer inherited the prior Space press.");
        State(); yield return null; Call(group, "Refresh", true);
        State(Key.Space); yield return null; Call(group, "Refresh", true);
        Check(submissions == 5, "Space did not submit after the new offer's release guard.");
        Call(group, "Reset"); State(); yield return null;
    }

    private static IEnumerator TestPopupOwnership()
    {
        GameObject host = new GameObject("UIManagerProbe"); host.SetActive(false);
        UIManager manager = host.AddComponent<UIManager>();
        var stack = (PopupStackState)Field(manager, "popupStack");
        var menu = new PopupProbe(); var overlay = new PopupProbe();
        stack.Push(menu);
        Check(manager.CanProcessNavigationInput(menu), "The top menu could not receive navigation.");
        stack.Push(overlay);
        Check(!manager.CanProcessNavigationInput(menu), "The background menu received navigation.");
        stack.Remove(overlay);
        Check(manager.CanProcessNavigationInput(menu), "The menu did not resume after overlay removal.");
        GameObject blocker = new GameObject("FlowBlocker");
        ((HashSet<UnityEngine.Object>)Field(manager, "externalUiInputBlockOwners")).Add(blocker);
        Check(!manager.CanProcessNavigationInput(menu), "An external flow blocker was bypassed.");
        ((Dictionary<IStackableUI, UnityEngine.Object>)Field(manager, "popupExternalBlockOwners")).Add(menu, blocker);
        Check(manager.CanProcessNavigationInput(menu), "The session-owned popup could not use its own input-block exception.");
        GameObject otherBlocker = new GameObject("OtherFlowBlocker");
        ((HashSet<UnityEngine.Object>)Field(manager, "externalUiInputBlockOwners")).Add(otherBlocker);
        Check(!manager.CanProcessNavigationInput(menu), "The owner exception bypassed another flow's blocker.");
        ((HashSet<UnityEngine.Object>)Field(manager, "externalUiInputBlockOwners")).Clear();
        menu.Active = false;
        Check(!manager.CanProcessNavigationInput(menu), "A closed menu received navigation.");
        yield break;
    }

    private static IEnumerator TestDialogueEnter()
    {
        GameObject host = new GameObject("DialogueProbe"); host.SetActive(false);
        DialogueController controller = host.AddComponent<DialogueController>();
        InputBindingService bindings = InputBindingService.EnsureInstance();
        MethodInfo pressed = typeof(DialogueController).GetMethod("WasDialogueConfirmPressed", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (Key key in new[] { Key.Enter, Key.NumpadEnter })
        {
            yield return Press(key);
            Check((bool)pressed.Invoke(null, new object[] { bindings }), key + " did not advance/confirm dialogue.");
            Check((bool)Call(controller, "IsChoiceConfirmInputHeld", bindings), key + " did not hold the choice input gate.");
            State(key); yield return null;
            Check(!(bool)pressed.Invoke(null, new object[] { bindings }), key + " held input was treated as a new press.");
            State(); yield return null;
            Check(!(bool)Call(controller, "IsChoiceConfirmInputHeld", bindings), key + " release did not unblock choice input.");
        }
        yield return Press(Key.Space);
        Check((bool)pressed.Invoke(null, new object[] { bindings }), "Space dialogue confirmation regressed.");
        State(); yield return null;
    }

    private sealed class PopupProbe : IStackableUI
    {
        public bool Active = true;
        public bool IsActive => Active;
        public bool CanCloseOnEscape => true;
        public UIOpenGroup OpenGroup => UIOpenGroup.Overlay;
        public UIOpenGroup BlockedOpenGroups => UIOpenGroup.None;
        public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.None;
        public void OpenUI() { Active = true; }
        public void CloseUI() { Active = false; }
    }
}

public sealed class UiKeyboardNavigationProbeRunner : MonoBehaviour { }
