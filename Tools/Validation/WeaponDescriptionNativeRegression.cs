// Run only in a disposable Unity project with freshly built Core/Gameplay/Infrastructure/UI DLLs.
// Native UI/input regression; does not open or modify gameplay scenes.
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityGAS;

[InitializeOnLoad]
public static class WeaponDescriptionNativeRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Pending = "WeaponDescriptionRegression.Pending";
    private static int checks;
    private static Keyboard keyboard;
    private static TMP_FontAsset font;
    static WeaponDescriptionNativeRegression()
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
        if (!Application.dataPath.Contains("CapstoneWeaponDescriptions-"))
            throw new Exception("Disposable validation project required.");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    private static void Call(object o, string name) => o.GetType().GetMethod(name, Private).Invoke(o, null);
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception(message);
    }
    private static GameObject Go(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(716, 100);
        return go;
    }
    private static TMP_Text Text(string name, Transform parent)
    {
        var t = Go(name, parent).AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = 18; t.raycastTarget = false;
        return t;
    }
    private static WeaponDescriptionHint Hint(Transform parent, out GameObject root, out TMP_Text caption)
    {
        root = Go("DescriptionHint", parent);
        caption = Text("Caption", root.transform);
        var hint = new WeaponDescriptionHint();
        Set(hint, "root", root); Set(hint, "label", caption);
        Set(hint, "keyIcon", Go("Glyph", root.transform).AddComponent<Image>());
        return hint;
    }
    private static AbilityDefinition Ability(string name, string shortCopy, string detail)
    {
        var a = ScriptableObject.CreateInstance<AbilityDefinition>();
        a.abilityName = name; a.simpleDescription = shortCopy; a.description = detail;
        return a;
    }
    private static string Bodies(Transform root) => string.Join("|", root.GetComponentsInChildren<TMP_Text>()
        .Where(t => t.name == "Body").Select(t => t.text));
    private static int Blocks(Transform root) => root.GetComponentsInChildren<WeaponAbilityBlockView>().Length;
    private static void Key(bool pressed)
    {
        InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(UnityEngine.InputSystem.Key.Backquote)
            : new KeyboardState());
        InputSystem.Update();
    }
    private static void Toggle(object owner)
    {
        Key(false); Key(true);
        Check(InputKeyCompatibility.WasPressedThisFrame(KeyCode.BackQuote), $"Injected keyboard edge missing: current={Keyboard.current == keyboard}, enabled={keyboard.enabled}, pressed={keyboard.backquoteKey.isPressed}, edge={keyboard.backquoteKey.wasPressedThisFrame}");
        Call(owner, "Update"); Key(false);
    }
    private static void Exercise()
    {
        try
        {
            SessionState.SetBool(Pending, false);
            font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            var canvas = Go("Canvas"); canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var template = Go("AbilityTemplate").AddComponent<WeaponAbilityBlockView>();
            Set(template, "titleText", Text("Title", template.transform));
            Set(template, "bodyText", Text("Body", template.transform));
            var layout = template.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 90;
            var templates = Go("Templates");
            templates.SetActive(false);
            template.transform.SetParent(templates.transform, false);

            var panel = Go("Panel", canvas.transform).AddComponent<ItemDetailPanel>();
            Set(panel, "openDuration", 0f); Set(panel, "closeDuration", 0f);
            var view = Go("WeaponView", panel.transform).AddComponent<WeaponDetailViewV2>();
            var abilities = Go("AbilityRoot", view.transform);
            var vertical = abilities.AddComponent<VerticalLayoutGroup>();
            vertical.childControlHeight = true; vertical.childForceExpandHeight = false;
            Set(view, "abilityRoot", abilities.transform); Set(view, "abilityBlockPrefab", template);
            Set(panel, "weaponViewV2", view);
            var hint = Hint(panel.transform, out var hintRoot, out var caption);
            Set(panel, "weaponDescriptionHint", hint);
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.weaponId = "Weapon.CrimsonBoundary";
            weapon.attack = Ability("Attack", "short attack", "detailed attack 100%");
            weapon.skill1 = Ability("Skill1", "short skill", "detailed skill 200%");
            weapon.skill2 = Ability("Skill2", "short finisher", "detailed finisher 300%");
            panel.ShowHover(weapon);
            Check(Blocks(abilities.transform) == 3, "Default abilities missing");
            Check(Bodies(abilities.transform).Contains("short attack") && !Bodies(abilities.transform).Contains("100%"), "Default is not simple");
            Check(hintRoot.activeSelf && caption.text.Contains("자세히 설명"), "Simple-mode caption");
            Toggle(panel);
            Check(Bodies(abilities.transform).Contains("100%") && caption.text.Contains("간단히 설명"), "Backquote did not show details");
            Toggle(panel);
            Check(Bodies(abilities.transform).Contains("short attack") && Blocks(abilities.transform) == 3, "Toggle-back duplicated rows or lost simple copy");
            Toggle(panel); panel.ShowHover(weapon);
            Check(Bodies(abilities.transform).Contains("100%"), "Same-item refresh reset mode");
            panel.HideHover(); panel.ShowHover(weapon);
            Check(Bodies(abilities.transform).Contains("short attack"), "Reopen did not reset to simple");
            weapon.attack.simpleDescription = "";
            panel.ShowHover(weapon);
            Check(Bodies(abilities.transform).Contains("100%"), "Missing simple copy lost detailed fallback");

            weapon.weaponId = "Weapon.LightningSpear";
            weapon.skill1.sourceObject = ScriptableObject.CreateInstance<LightningSpearSkill1Data>();
            panel.ShowHover(weapon);
            Check(Blocks(abilities.transform) == 3, "Lightning must show both skill variants and no basic attack");
            var visible = abilities.GetComponentsInChildren<WeaponAbilityBlockView>();
            Check(visible.All(v => !v.IsVariantSwitching), "Removed shuffle still active");
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)abilities.transform);
            for (int i = 1; i < visible.Length; i++)
                Check(((RectTransform)visible[i].transform).anchoredPosition.y < ((RectTransform)visible[i-1].transform).anchoredPosition.y,
                    "Variant rows overlap");
            Toggle(panel);
            Check(Blocks(abilities.transform) == 3 && Bodies(abilities.transform).Contains("1개"), "Detailed lightning variants missing");
            var flowering = ScriptableObject.CreateInstance<WeaponDefinition>();
            var loadout = ScriptableObject.CreateInstance<FloweringLoadout>();
            Set(loadout, "baseAttack", Ability("Base", "base short", "base detail"));
            Set(loadout, "bloomAttack", Ability("Bloom attack", "bloom short", "bloom detail"));
            Set(loadout, "bloomSkill", Ability("Bloom", "skill short", "skill detail"));
            flowering.abilityLoadout = loadout;
            panel.ShowHover(flowering);
            Check(Blocks(abilities.transform) == 1 && !Bodies(abilities.transform).Contains("bloom short") && !Bodies(abilities.transform).Contains("base short"),
                "Flowering basic attacks must stay hidden while its skill remains visible");
            Toggle(panel);
            Check(Bodies(abilities.transform).Contains("skill detail") && !Bodies(abilities.transform).Contains("bloom detail"), "Detailed mode exposed a basic attack");
            panel.ShowHover(ScriptableObject.CreateInstance<ConsumableDefinition>());
            Check(!hintRoot.activeSelf, "Weapon hint leaked into consumable");
            Toggle(panel); Check(!hintRoot.activeSelf, "Non-weapon shortcut enabled hint");
            panel.HideHover();

            var page = Go("Encyclopedia", canvas.transform).AddComponent<EncyclopediaItemRightPage>();
            Set(page, "titleText", Text("BookTitle", page.transform));
            Set(page, "storyText", Text("BookStory", page.transform));
            var bookAbilities = Go("BookAbilities", page.transform);
            Set(page, "abilityContainer", bookAbilities.transform); Set(page, "weaponAbilityRoot", bookAbilities);
            Set(page, "abilityBlockPrefab", template);
            Set(page, "resetScrollOnBind", false);
            Set(page, "weaponDescriptionHint", Hint(page.transform, out var bookHint, out var bookCaption));
            page.ShowWeapon(weapon);
            Check(Blocks(bookAbilities.transform) == 3 && bookCaption.text.Contains("자세히 설명"), "Book defaults/variants");
            Toggle(page);
            Check(Bodies(bookAbilities.transform).Contains("1개") && bookCaption.text.Contains("간단히 설명"), "Book detail toggle");
            page.gameObject.SetActive(false); page.gameObject.SetActive(true);
            Check(bookCaption.text.Contains("자세히 설명"), "Book reopen retained detailed mode");
            page.ShowConsumable(ScriptableObject.CreateInstance<ConsumableDefinition>());
            Check(!bookHint.activeSelf, "Book hint leaked into other item");

            var eventSystem = Go("EventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>();
            UnityEngine.EventSystems.EventSystem.current = eventSystem;
            var inputField = Go("InputField").AddComponent<TMP_InputField>();
            eventSystem.SetSelectedGameObject(inputField.gameObject);
            Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == inputField.gameObject,
                "Input field did not receive selection in test fixture");
            Key(true); Check(!WeaponDescriptionHint.WasTogglePressed(), "Typing triggers description toggle"); Key(false);
            Debug.Log($"WEAPON_DESCRIPTION_REGRESSION_PASS: {checks} assertions; native input, simple/detail copy, all variant rows, lifecycle, non-weapon isolation.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}

