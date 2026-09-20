// Isolated Unity 6000.4 Editor harness; never runs the main project bootstrap.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using TMPro;

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
            MixedCanvasMotion();
            Debug.Log("TUTORIAL_GUIDANCE_PASS: chest ownership; dash recovery; sequential glyphs; animated inventory open/close/return; moving spotlight; cancellation; other owners preserved.");
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
        Check(tutorial.Stage == 6 && tutorial.PromptAction == InputActionId.ConsumableSlot1, "Inventory must lead to potion lesson");
        tutorial.CompletePotionTutorial(); tutorial.CompletePotionTutorial();
        Check(tutorial.Stage == 7 && tutorial.IsMovementPrompt, "Potion completion must advance exactly once");
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
        var potions = playerHost.AddComponent<PlayerConsumableInventory>();
        var health = ScriptableObject.CreateInstance<AttributeDefinition>(); health.defaultBaseValue = 9; health.maxValue = 10;
        var catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>(); Set(catalog, "attributes", new[] { health });
        var attributes = playerHost.AddComponent<AttributeSet>(); Set(attributes, "attributeCatalog", catalog);
        var potion = ScriptableObject.CreateInstance<ConsumableDefinition>(); Set(potion, "targetAttribute", health);
        var otherPotion = ScriptableObject.CreateInstance<ConsumableDefinition>();
        potions.TryAcquire(otherPotion); potions.TryAcquire(potion);
        playerHost.AddComponent<WeaponInventory2D>();
        playerHost.AddComponent<RelicInventory>();
        typeof(PlayerRuntimeRegistry).GetField("<CurrentPlayer>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, player);
        var screenHost = RectHost("Inventory window");
        var screenCanvas = screenHost.AddComponent<Canvas>(); screenCanvas.sortingOrder = 100;
        var screen = screenHost.AddComponent<InventoryScreen>();
        screen.enabled = false;
        var manager = host.AddComponent<InventoryUIManager>(); Set(manager, "inventoryScreen", screen);
        typeof(InventoryUIManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, manager);
        var handler = host.AddComponent<InventoryUIOpenRequestHandler>(); Set(handler, "inventoryUIManager", manager);
        var buttonHost = RectHost("HUD button");
        buttonHost.transform.SetParent(RectHost("HUD parent").transform, false);
        var button = buttonHost.AddComponent<InventoryOpenHudButton>(); Set(button, "hudRoot", buttonHost);
        var buttonRect = (RectTransform)buttonHost.transform;
        buttonRect.pivot = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(70, 70);
        buttonRect.anchoredPosition3D = new Vector3(-480, -330, 0);
        buttonRect.localScale = new Vector3(1.2f, 1.2f, 1f);
        Vector3 originalPosition = buttonRect.anchoredPosition3D, originalScale = buttonRect.localScale;
        var shield = RectHost("Shield"); shield.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 700);
        var canvasHost = RectHost("Guidance canvas");
        var guideCanvas = canvasHost.AddComponent<Canvas>(); guideCanvas.renderMode = RenderMode.WorldSpace; guideCanvas.sortingOrder = 1000;
        shield.transform.SetParent(canvasHost.transform, false); canvasHost.SetActive(true);
        var image = shield.AddComponent<UnityEngine.UI.Image>();
        var glyph = RectHost("Glyph").AddComponent<UnityEngine.UI.Image>();
        glyph.transform.SetParent(shield.transform, false);
        var instruction = RectHost("Instruction"); instruction.transform.SetParent(shield.transform, false);
        var label = instruction.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF.asset");
        Check(label.font != null, "Copy the authored Galmuri9 font into the isolated fixture");
        label.fontSize = 28f; label.textWrappingMode = TextWrappingModes.Normal;
        var shades = new RectTransform[4];
        for (int i = 0; i < 4; i++) { var shade = RectHost("Shade"); shade.transform.SetParent(shield.transform, false); shade.AddComponent<UnityEngine.UI.Image>(); shades[i] = shade.GetComponent<RectTransform>(); }
        Set(guide, "tutorial", tutorial);
        Set(guide, "inventoryButton", button); Set(guide, "inventoryHandler", handler);
        Set(guide, "inputShield", shield.GetComponent<RectTransform>()); Set(guide, "shieldImage", image);
        Set(guide, "shadePanels", shades); Set(guide, "rightClickGlyph", glyph); Set(guide, "instruction", instruction.GetComponent<RectTransform>());
        Set(guide, "tutorialPotion", potion);
        foreach (string field in new[] { "potionRangeEndGlyph", "potionUseGlyph" })
        {
            var extra = RectHost(field); extra.transform.SetParent(shield.transform, false);
            Set(guide, field, extra.AddComponent<UnityEngine.UI.Image>());
        }
        foreach (string field in new[] { "potionRangeSeparator", "potionUseInstruction" })
        {
            var extra = RectHost(field); extra.transform.SetParent(shield.transform, false);
            var text = extra.AddComponent<TextMeshProUGUI>(); text.font = label.font; Set(guide, field, text);
        }
        var hudHost = RectHost("Potion HUD"); hudHost.transform.SetParent(canvasHost.transform, false);
        var hud = hudHost.AddComponent<PlayerConsumableHUD2D>(); hud.enabled = false;
        var hudRect = (RectTransform)hudHost.transform; hudRect.anchoredPosition = new Vector2(-200, 200);
        Vector3 hudOriginal = hudRect.anchoredPosition3D;
        for (int i = 0; i < 4; i++)
        {
            var slot = RectHost("Consumable slot"); slot.transform.SetParent(hudHost.transform, false);
            var rect = (RectTransform)slot.transform; rect.sizeDelta = new Vector2(60, 60); rect.anchoredPosition = new Vector2(i * 65, 0);
            Set(hud, "slot" + (i + 1) + "UI", new PlayerConsumableHUD2D.ConsumableSlotUI { root = slot });
        }
        hudHost.SetActive(true);
        var input = new InputProbe(player);
        InputActionQuery.RegisterBackend(input);
        var pause = new PauseProbe(); TimeScalePausePlayback.RegisterBackend(pause);
        pause.Acquire(player);
        InputActionQuery.SetPressBlocked(InputActionId.Dash, player, true);
        Call(guide, "TickInventoryGuidance");
        Check(pause.IsHeldBy(guide) && InputActionQuery.IsPressBlocked(InputActionId.InventoryToggle), "Intro must pause and block early V");
        Check(!instruction.activeSelf && !glyph.gameObject.activeSelf, "Instruction appeared before motion");
        Call(guide, "TickInventoryPresentation", .4f);
        Check((buttonRect.localScale - originalScale * 1.75f).sqrMagnitude < .001f, "Midpoint scale is wrong");
        Check((buttonRect.anchoredPosition3D - originalPosition - new Vector3(1000f / 6f, 700f / 6f)).sqrMagnitude < .001f, "Midpoint displacement is wrong");
        Check(!instruction.activeSelf, "Instruction appeared at midpoint");
        CheckHole(buttonRect, (RectTransform)shield.transform, shades);
        Call(guide, "TickInventoryPresentation", .4f);
        Check((buttonRect.localScale - originalScale * 2.5f).sqrMagnitude < .001f, "Final scale must be 2.5x authored scale");
        Check(instruction.activeSelf && !InputActionQuery.IsPressBlocked(InputActionId.InventoryToggle), "V prompt not enabled at .8 seconds");
        label.ForceMeshUpdate();
        Check(label.textWrappingMode == TextWrappingModes.NoWrap && label.textInfo.lineCount == 1 &&
              label.text.EndsWith("를 눌러 인벤토리를 열 수 있습니다."), "Instruction must remain one line with the requested wording");
        CheckHole(buttonRect, (RectTransform)shield.transform, shades);
        input.Pressed = true;
        Call(guide, "TickInventoryPresentation", 0f);
        Check(tutorial.Stage == 5, "A rejected V open completed the lesson");
        screenHost.SetActive(true); input.Pressed = false; InputActionQuery.RegisterBackend(input);
        Call(guide, "TickInventoryPresentation", 0f);
        Check(tutorial.Stage == 5, "An already-open window without a fresh V completed the lesson");
        input.Pressed = true; InputActionQuery.RegisterBackend(input);
        Call(guide, "TickInventoryPresentation", 0f);
        Check(tutorial.Stage == 5 && !image.raycastTarget && !instruction.activeSelf, "Opening must allow inspection without completing");
        Check(guideCanvas.sortingLayerID == screenCanvas.sortingLayerID && guideCanvas.sortingOrder == screenCanvas.sortingOrder - 1,
            $"Guidance must draw behind the open inventory: guide={guideCanvas.sortingOrder}, inventory={screenCanvas.sortingOrder}, found={UnityEngine.Object.FindFirstObjectByType<InventoryScreen>()}");
        Call(guide, "TickInventoryPresentation", 2f);
        Check(tutorial.Stage == 5 && pause.IsHeldBy(guide), "Open/closing window must retain tutorial pause and stage");
        screenHost.SetActive(false);
        Call(guide, "TickInventoryPresentation", .4f);
        Check(tutorial.Stage == 5 && image.raycastTarget && guideCanvas.sortingOrder == 1000, "Return must still gate the portal and restore overlay order");
        Check((buttonRect.localScale - originalScale * 1.75f).sqrMagnitude < .001f, "Return midpoint scale is wrong");
        CheckHole(buttonRect, (RectTransform)shield.transform, shades);
        Call(guide, "TickInventoryPresentation", .4f);
        Check(tutorial.Stage == 6 && image.raycastTarget && shield.activeSelf && pause.IsHeldBy(guide), "Inventory return must hand off directly to paused potion guidance");
        Check(potions.GetConsumableInSlot(0) == potion && potions.GetConsumableInSlot(1) == otherPotion, "Potion must swap into slot 1 without losing its occupant");
        Check(InputActionQuery.IsPressBlocked(InputActionId.ConsumableSlot1) && !instruction.activeSelf, "Potion intro must block use and hide text");
        Set(guide, "potionMotionElapsed", 0f);
        Call(guide, "TickPotionPresentation", .4f);
        Check(Vector3.Distance(hudRect.localScale, Vector3.one * 1.75f) < .001f, "Potion midpoint must scale smoothly");
        Check(hudRect.anchoredPosition.x > hudOriginal.x && hudRect.anchoredPosition.y < hudOriginal.y, "Potion must travel right and down");
        Check(!instruction.activeSelf, "Potion instruction appeared during motion");
        Call(guide, "TickPotionPresentation", .4f);
        Check(Vector3.Distance(hudRect.localScale, Vector3.one * 2.5f) < .001f, "Potion final scale must be 2.5x");
        Check(!InputActionQuery.IsPressBlocked(InputActionId.ConsumableSlot1) && InputActionQuery.IsPressBlocked(InputActionId.ConsumableSlot2), "Only lesson potion key may be used");
        var useLabel = (TMP_Text)guide.GetType().GetField("potionUseInstruction", Flags).GetValue(guide);
        Check(instruction.activeSelf && useLabel.gameObject.activeSelf && useLabel.text.EndsWith("을 눌러 포션 사용."), "Potion captions must be visible");
        Check(useLabel.rectTransform.anchoredPosition.y >= 0, "Bottom instruction must fit the screen");
        Check(potions.TryUseAt(0), "The wounded player must be able to consume the potion");
        Check(attributes.GetCurrentValue(health) == 10 && tutorial.Stage == 6 && pause.IsHeldBy(guide), "Successful healing must wait for HUD return");
        Call(guide, "TickPotionPresentation", .4f);
        Check(Vector3.Distance(hudRect.localScale, Vector3.one * 1.75f) < .001f && tutorial.Stage == 6, "Potion return midpoint must retain the lesson");
        Call(guide, "TickPotionPresentation", .4f);
        Check(tutorial.Stage == 7 && hudRect.localScale == Vector3.one, "Potion return must restore scale before completion");
        Call(guide, "TickPresentation", .2f);
        Check(!shield.activeSelf && hudRect.anchoredPosition3D == hudOriginal, "Potion completion must hide guidance and restore HUD");
        Check(buttonRect.anchoredPosition3D == originalPosition && buttonRect.localScale == originalScale, "Return must restore exact authored pose");
        Check(label.fontSize == 28f && label.textWrappingMode == TextWrappingModes.Normal, "Return leaked inventory-only text settings");
        Check(!pause.IsHeldBy(guide) && pause.IsHeldBy(player), "Completion released another pause owner or leaked its own");
        Check(InputActionQuery.IsPressBlocked(InputActionId.Dash), "Completion released another input owner");
        // Interrupted/restarted guidance must restore the shared HUD and leave the lesson incomplete.
        Set(tutorial, "stage", 5); input.Pressed = false;
        Call(guide, "TickInventoryGuidance"); Call(guide, "TickInventoryPresentation", .4f);
        Call(guide, "OnDisable");
        Check(tutorial.Stage == 5 && buttonRect.anchoredPosition3D == originalPosition && buttonRect.localScale == originalScale, "Disable advanced or stranded HUD pose");
        Check(!pause.IsHeldBy(guide) && !InputActionQuery.IsPressBlocked(InputActionId.InventoryToggle), "Disable leaked pause or inventory input lock");
        InputActionQuery.SetPressBlocked(InputActionId.Dash, player, false);
        pause.Release(player); TimeScalePausePlayback.RegisterBackend(null);
        InputActionQuery.UnregisterBackend(input);
        typeof(PlayerRuntimeRegistry).GetField("<CurrentPlayer>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        tutorialHost.SetActive(false);
    }

    public static void RunMixedCanvasMotion()
    {
        try { MixedCanvasMotion(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void MixedCanvasMotion()
    {
        var host = RectHost("Mixed canvas motion");
        var guide = host.AddComponent<PrototypeChestNavigation>();
        var overlayHost = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas));
        overlayHost.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var shield = (RectTransform)overlayHost.transform;
        var cameraHost = new GameObject("HUD camera", typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>();
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        var hudHost = new GameObject("Camera HUD", typeof(RectTransform), typeof(Canvas));
        var canvas = hudHost.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;
        var buttonHost = RectHost("Moving button");
        var button = (RectTransform)buttonHost.transform;
        button.SetParent(hudHost.transform, false);
        button.pivot = Vector2.zero;
        button.anchorMin = button.anchorMax = Vector2.zero;
        button.anchoredPosition3D = new Vector3(20, 20, 0);
        Vector3 original = button.anchoredPosition3D;
        Set(guide, "inputShield", shield);
        Set(guide, "inventoryButtonRect", button);
        Set(guide, "inventoryOriginalPosition", original);
        Set(guide, "inventoryOriginalScale", Vector3.one);
        // Full viewport, top/bottom bars, and side bars; vary camera world scale too.
        foreach (Rect viewport in new[] { new Rect(0, 0, 1, 1), new Rect(0, .05f, 1, .9f), new Rect(.12f, 0, .76f, 1) })
        foreach (float size in new[] { 5f, 12f })
        {
            camera.rect = viewport;
            camera.orthographicSize = size;
            Canvas.ForceUpdateCanvases();
            button.anchoredPosition3D = original;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(camera, button.position);
            var bounds = new Vector3[4]; shield.GetWorldCorners(bounds);
            Vector2 expected = (RectTransformUtility.WorldToScreenPoint(null, bounds[2]) -
                                RectTransformUtility.WorldToScreenPoint(null, bounds[0])) / 3f;
            foreach (float progress in new[] { .5f, 1f, .5f, 0f })
            {
                Call(guide, "PoseInventoryButton", progress);
                Vector2 actual = RectTransformUtility.WorldToScreenPoint(camera, button.position) - start;
                Check(Vector2.Distance(actual, expected * progress) < .1f,
                    $"Mixed canvas pixel displacement: viewport={viewport}, size={size}, progress={progress}, actual={actual}, expected={expected * progress}");
                Check(Vector3.Distance(button.localScale, Vector3.one * Mathf.Lerp(1, 2.5f, progress)) < .001f, "Mixed canvas scale changed");
            }
        }
        Debug.Log("TUTORIAL_MIXED_CANVAS_PASS: full/letterbox/pillarbox; two camera scales; outward and return pixel displacement.");
        UnityEngine.Object.DestroyImmediate(host);
        UnityEngine.Object.DestroyImmediate(overlayHost);
        UnityEngine.Object.DestroyImmediate(hudHost);
        UnityEngine.Object.DestroyImmediate(cameraHost);
    }

    private static void CheckHole(RectTransform button, RectTransform shield, RectTransform[] shades)
    {
        var corners = new Vector3[4]; button.GetWorldCorners(corners);
        Vector3 min = shield.InverseTransformPoint(corners[0]);
        Vector3 max = shield.InverseTransformPoint(corners[2]);
        float leftEdge = shield.rect.xMin + shades[2].sizeDelta.x;
        float rightEdge = shield.rect.xMin + shades[3].anchoredPosition.x;
        Check(Mathf.Abs(leftEdge - min.x) < .01f && Mathf.Abs(rightEdge - max.x) < .01f, "Spotlight did not track moving/scaling button horizontally");
        Check(Mathf.Abs(shield.rect.yMin + shades[1].sizeDelta.y - min.y) < .01f &&
              Mathf.Abs(shield.rect.yMin + shades[0].anchoredPosition.y - max.y) < .01f, "Spotlight did not track vertically");
    }

    private sealed class PauseProbe : ITimeScalePauseBackend, ITimeScaleRampBackend
    {
        private readonly System.Collections.Generic.HashSet<UnityEngine.Object> owners = new();
        public bool IsPaused => owners.Count > 0;
        public bool IsHeldBy(UnityEngine.Object owner) => owners.Contains(owner);
        public bool Acquire(UnityEngine.Object owner) => owners.Add(owner);
        public bool Release(UnityEngine.Object owner) => owners.Remove(owner);
        public bool SetOwnedTimeScale(UnityEngine.Object owner, float scale) => scale == 0f && Acquire(owner);
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
