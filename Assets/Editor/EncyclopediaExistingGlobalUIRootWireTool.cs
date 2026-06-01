#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class EncyclopediaExistingGlobalUIRootWireTool
{
    private const string GlobalUIRootPrefabPath = "Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab";
    private const string EncyclopediaUIPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab";
    private const string ItemDatabasePath = "Assets/LeeJunMo/Datas/Looting/ItemDatabase.asset";
    private const string EntrySlotPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaEntrySlot.prefab";
    private const string AbilityBlockPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/Panel_AbilityBlock_Encyclopedia.prefab";
    private const string ContentAppearClipPath = "Assets/LeeJunMo/Animations/Encyclopedia/UIBook/ENC_UIBook_ContentAppear.anim";
    private const string BookControllerPath = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Book.controller";
    private const string BookIdleClipPath = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/BookIdle.anim";
    private const string BookOpenClipPath = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/BookOpen.anim";
    private const string BookCloseClipPath = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/BookClose.anim";
    private const string BookLeftPageClipPath = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/BookLeftPage.anim";
    private const string BookRightPageClipPath = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/BookRightPage.anim";
    private const string StandPrefabPath = "Assets/LeeJunMo/Prefab/Interactables/EncyclopediaStand.prefab";

    [MenuItem("Tools/Encyclopedia/Wire All Authoring Contracts")]
    public static void WireAllAuthoringContracts()
    {
        bool changed = false;
        changed |= WireBookAnimatorAssetsInternal(logSummary: true);
        changed |= WireAbilityBlockPrefabInternal(logSummary: true);
        changed |= WireEntrySlotPrefabInternal(logSummary: true);
        changed |= WireStandPrefabInternal(logSummary: true);
        changed |= WireEncyclopediaUIPrefabInternal(logSummary: true);
        changed |= WireExistingGlobalUIRootInternal(logSummary: true);

        if (changed)
            AssetDatabase.SaveAssets();

        ValidateAllAuthoringContracts();
    }

    [MenuItem("Tools/Encyclopedia/Validate Authoring Contracts")]
    public static void ValidateAllAuthoringContracts()
    {
        var report = new ContractReport("Encyclopedia authoring contract validation");
        ValidateAbilityBlockPrefab(report);
        ValidateEntrySlotPrefab(report);
        ValidateStandPrefab(report);
        ValidateEncyclopediaUIPrefab(report);
        ValidateGlobalUIRoot(report);
        report.Log();
    }

    [MenuItem("Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia")]
    public static void WireExistingGlobalUIRoot()
    {
        bool changed = WireBookAnimatorAssetsInternal(logSummary: true);
        changed |= WireAbilityBlockPrefabInternal(logSummary: true);
        changed |= WireExistingGlobalUIRootInternal(logSummary: true);
        if (changed)
            AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Encyclopedia/Wire EncyclopediaUI Prefab")]
    public static void WireEncyclopediaUIPrefab()
    {
        bool changed = WireBookAnimatorAssetsInternal(logSummary: true);
        changed |= WireAbilityBlockPrefabInternal(logSummary: true);
        changed |= WireEncyclopediaUIPrefabInternal(logSummary: true);
        if (changed)
            AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Encyclopedia/Repair Book Animator Assets")]
    public static void RepairBookAnimatorAssets()
    {
        if (WireBookAnimatorAssetsInternal(logSummary: true))
            AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Encyclopedia/Wire Entry Slot Prefab")]
    public static void WireEntrySlotPrefab()
    {
        if (WireEntrySlotPrefabInternal(logSummary: true))
            AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Encyclopedia/Wire Ability Block Prefab")]
    public static void WireAbilityBlockPrefab()
    {
        if (WireAbilityBlockPrefabInternal(logSummary: true))
            AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Encyclopedia/Wire Encyclopedia Stand Prefab")]
    public static void WireEncyclopediaStandPrefab()
    {
        if (WireStandPrefabInternal(logSummary: true))
            AssetDatabase.SaveAssets();
    }

    private static bool WireExistingGlobalUIRootInternal(bool logSummary)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GlobalUIRootPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[EncyclopediaWire] Could not load prefab: {GlobalUIRootPrefabPath}");
            return false;
        }

        try
        {
            var context = new WireContext(root);
            context.Wire();
            PrefabUtility.SaveAsPrefabAsset(root, GlobalUIRootPrefabPath);
            if (logSummary)
                Debug.Log("[EncyclopediaWire] Wired existing GlobalUIRoot encyclopedia references.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool WireEncyclopediaUIPrefabInternal(bool logSummary)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EncyclopediaUIPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[EncyclopediaWire] Could not load prefab: {EncyclopediaUIPrefabPath}");
            return false;
        }

        try
        {
            var context = new WireContext(root);
            context.Wire();
            PrefabUtility.SaveAsPrefabAsset(root, EncyclopediaUIPrefabPath);
            if (logSummary)
                Debug.Log("[EncyclopediaWire] Wired EncyclopediaUI prefab references.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia", true)]
    private static bool CanWireExistingGlobalUIRoot()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUIRootPrefabPath) != null;
    }

    [MenuItem("Tools/Encyclopedia/Wire EncyclopediaUI Prefab", true)]
    private static bool CanWireEncyclopediaUIPrefab()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(EncyclopediaUIPrefabPath) != null;
    }

    [MenuItem("Tools/Encyclopedia/Wire Entry Slot Prefab", true)]
    private static bool CanWireEntrySlotPrefab()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(EntrySlotPrefabPath) != null;
    }

    [MenuItem("Tools/Encyclopedia/Wire Ability Block Prefab", true)]
    private static bool CanWireAbilityBlockPrefab()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(AbilityBlockPrefabPath) != null;
    }

    [MenuItem("Tools/Encyclopedia/Repair Book Animator Assets", true)]
    private static bool CanRepairBookAnimatorAssets()
    {
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(BookControllerPath) != null;
    }

    [MenuItem("Tools/Encyclopedia/Wire Encyclopedia Stand Prefab", true)]
    private static bool CanWireEncyclopediaStandPrefab()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(StandPrefabPath) != null;
    }

    private static bool WireEntrySlotPrefabInternal(bool logSummary)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EntrySlotPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[EncyclopediaWire] Could not load prefab: {EntrySlotPrefabPath}");
            return false;
        }

        try
        {
            EncyclopediaEntryButton entryButton = GetOrAdd<EncyclopediaEntryButton>(root);
            SerializedObject so = new SerializedObject(entryButton);
            SetObject(so, "button", root.GetComponent<Button>() ?? root.GetComponentInChildren<Button>(true));
            SetObject(so, "indexText", FindComponentUnder<TMP_Text>(root.transform, "IndexText"));
            SetObject(so, "titleText", FindComponentUnder<TMP_Text>(root.transform, "TitleText"));
            SetObject(so, "iconImage", FindComponentUnder<Image>(root.transform, "Icon", "ItemIcon"));
            SetObject(so, "selectedMarker", FindObject(root.transform, "SelectedMarker", "SelectMarker", "SelectionMarker"));
            SetObject(so, "hoverMarker", FindObject(root.transform, "HoverMarker"));
            SetObject(so, "lockedMarker", FindObject(root.transform, "LockedMarker"));
            SetObject(so, "animator", root.GetComponent<Animator>());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entryButton);
            PrefabUtility.SaveAsPrefabAsset(root, EntrySlotPrefabPath);

            if (logSummary)
                Debug.Log("[EncyclopediaWire] Wired EncyclopediaEntrySlot prefab references.");

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool WireAbilityBlockPrefabInternal(bool logSummary)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(AbilityBlockPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[EncyclopediaWire] Could not load prefab: {AbilityBlockPrefabPath}");
            return false;
        }

        try
        {
            WeaponAbilityBlockView view = root.GetComponent<WeaponAbilityBlockView>();
            if (view == null)
            {
                Debug.LogError("[EncyclopediaWire] Ability block prefab root has no WeaponAbilityBlockView component.");
                return false;
            }

            Transform skillInfoGroup = Find(root.transform, "SkillInfoGroup");
            Transform detailPanel = Find(root.transform, "Panel_AbillityDetailInfo") ?? Find(root.transform, "Panel_AbilityDetailInfo");
            Transform switchingKeyImage = Find(root.transform, "SwitchingKeyImage");

            SerializedObject so = new SerializedObject(view);
            SetObject(so, "titleText", FindComponentUnder<TMP_Text>(skillInfoGroup, "Text_SkillName", "SkillName", "TitleText"));
            SetObject(so, "iconImage", FindComponentUnder<Image>(root.transform, "Image_SkillIcon", "SkillIcon", "Icon"));
            SetObject(so, "inputHintImage", FindComponentUnder<Image>(root.transform, "InputKeyImage", "InputHintImage", "InputHint"));
            SetObject(so, "cooldownText", FindComponentUnder<TMP_Text>(root.transform, "Text_coolDown", "Text_CoolDown", "CooldownText"));
            SetObject(so, "extraMetaText", null);
            SetObject(so, "bodyRoot", detailPanel != null ? detailPanel.gameObject : null);
            SetObject(so, "bodyText", FindComponentUnder<TMP_Text>(detailPanel, "BodyText", "DescriptionText", "Text", "Text (TMP)", "Text(TMP)"));
            SetObject(so, "variantSwitchGuideRoot", switchingKeyImage != null ? switchingKeyImage.gameObject : null);
            SetObject(so, "variantSwitchGuideIcon", switchingKeyImage != null ? switchingKeyImage.GetComponent<Image>() : null);
            SetObject(so, "variantSwitchGuideText", null);
            SetObject(so, "cardMotionRoot", Find(root.transform, "CardMotionRoot") as RectTransform);
            SetObject(so, "currentContainer", null);
            SetObject(so, "currentContainerGroup", null);
            SetObject(so, "nextContainer", null);
            SetObject(so, "nextContainerGroup", null);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            PrefabUtility.SaveAsPrefabAsset(root, AbilityBlockPrefabPath);

            if (logSummary)
                Debug.Log("[EncyclopediaWire] Wired Panel_AbilityBlock_Encyclopedia prefab references.");

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool WireStandPrefabInternal(bool logSummary)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(StandPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[EncyclopediaWire] Could not load prefab: {StandPrefabPath}");
            return false;
        }

        try
        {
            EncyclopediaInteractable interactable = root.GetComponentInChildren<EncyclopediaInteractable>(true);
            if (interactable == null)
            {
                Debug.LogError("[EncyclopediaWire] EncyclopediaStand prefab has no EncyclopediaInteractable component.");
                return false;
            }

            SerializedObject so = new SerializedObject(interactable);
            SetObject(so, "screen", null);
            SetObject(so, "itemDatabase", null);
            SetObject(so, "catalog", null);
            SetBool(so, "resolveSceneScreenIfMissing", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interactable);
            PrefabUtility.SaveAsPrefabAsset(root, StandPrefabPath);

            if (logSummary)
                Debug.Log("[EncyclopediaWire] Wired EncyclopediaStand prefab scene-screen fallback.");

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool WireBookAnimatorAssetsInternal(bool logSummary)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BookControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[EncyclopediaWire] Could not load book animator controller: {BookControllerPath}");
            return false;
        }

        bool changed = false;
        changed |= WireAnimatorStateMotion(controller, "BookIdle", AssetDatabase.LoadAssetAtPath<AnimationClip>(BookIdleClipPath));
        changed |= WireAnimatorStateMotion(controller, "BookOpen", AssetDatabase.LoadAssetAtPath<AnimationClip>(BookOpenClipPath));
        changed |= WireAnimatorStateMotion(controller, "BookClose", AssetDatabase.LoadAssetAtPath<AnimationClip>(BookCloseClipPath));
        changed |= WireAnimatorStateMotion(controller, "BookLeftPage", AssetDatabase.LoadAssetAtPath<AnimationClip>(BookLeftPageClipPath));
        changed |= WireAnimatorStateMotion(controller, "BookRightPage", AssetDatabase.LoadAssetAtPath<AnimationClip>(BookRightPageClipPath));
        changed |= WireDefaultAnimatorState(controller, "BookIdle");

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            if (logSummary)
                Debug.Log("[EncyclopediaWire] Repaired Book animator state motions.");
        }

        return changed;
    }

    private static bool WireAnimatorStateMotion(AnimatorController controller, string stateName, AnimationClip clip)
    {
        if (controller == null || clip == null)
            return false;

        AnimatorState state = FindAnimatorState(controller, stateName);
        if (state == null || state.motion == clip)
            return false;

        state.motion = clip;
        EditorUtility.SetDirty(state);
        return true;
    }

    private static bool WireDefaultAnimatorState(AnimatorController controller, string stateName)
    {
        if (controller == null || controller.layers.Length == 0)
            return false;

        AnimatorState state = FindAnimatorState(controller, stateName);
        if (state == null)
            return false;

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        if (stateMachine == null || stateMachine.defaultState == state)
            return false;

        stateMachine.defaultState = state;
        EditorUtility.SetDirty(stateMachine);
        return true;
    }

    private static void ValidateAbilityBlockPrefab(ContractReport report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityBlockPrefabPath);
        if (prefab == null)
        {
            report.Error($"Missing AbilityBlock prefab at {AbilityBlockPrefabPath}.");
            return;
        }

        WeaponAbilityBlockView view = prefab.GetComponent<WeaponAbilityBlockView>();
        if (view == null)
        {
            report.Error("AbilityBlock root is missing WeaponAbilityBlockView.");
            return;
        }

        SerializedObject so = new SerializedObject(view);
        RequireRef(report, so, "titleText", "AbilityBlock skill title text");
        RequireRef(report, so, "iconImage", "AbilityBlock skill icon image");
        RequireRef(report, so, "inputHintImage", "AbilityBlock input hint image");
        RequireRef(report, so, "cooldownText", "AbilityBlock cooldown text");
        RequireNullRef(report, so, "extraMetaText", "AbilityBlock extraMetaText should stay unassigned in encyclopedia authoring.");
        RequireRef(report, so, "bodyRoot", "AbilityBlock body root");
        RequireRef(report, so, "bodyText", "AbilityBlock body text");
        RequireRef(report, so, "variantSwitchGuideRoot", "AbilityBlock variant switch guide root");
        RequireRef(report, so, "variantSwitchGuideIcon", "AbilityBlock variant switch guide icon");
        RequireRef(report, so, "cardMotionRoot", "AbilityBlock card motion root");
    }

    private static void ValidateEntrySlotPrefab(ContractReport report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntrySlotPrefabPath);
        if (prefab == null)
        {
            report.Error($"Missing EntrySlot prefab at {EntrySlotPrefabPath}.");
            return;
        }

        EncyclopediaEntryButton entryButton = prefab.GetComponent<EncyclopediaEntryButton>();
        if (entryButton == null)
        {
            report.Error("EntrySlot root is missing EncyclopediaEntryButton.");
            return;
        }

        SerializedObject so = new SerializedObject(entryButton);
        RequireRef(report, so, "button", "EntrySlot button");
        RequireRef(report, so, "iconImage", "EntrySlot Icon image");
        RequireRef(report, so, "selectedMarker", "EntrySlot selected marker");
        RequireRef(report, so, "hoverMarker", "EntrySlot hover marker");
        RequireRef(report, so, "lockedMarker", "EntrySlot locked marker");
        WarnIfMissingRef(report, so, "indexText", "EntrySlot IndexText is optional but currently unassigned.");
        WarnIfMissingRef(report, so, "titleText", "EntrySlot TitleText is optional and currently unassigned; current authoring hides title text.");
    }

    private static void ValidateStandPrefab(ContractReport report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StandPrefabPath);
        if (prefab == null)
        {
            report.Error($"Missing EncyclopediaStand prefab at {StandPrefabPath}.");
            return;
        }

        EncyclopediaInteractable interactable = prefab.GetComponentInChildren<EncyclopediaInteractable>(true);
        if (interactable == null)
        {
            report.Error("EncyclopediaStand prefab is missing EncyclopediaInteractable.");
            return;
        }

        SerializedObject so = new SerializedObject(interactable);
        WarnIfAssignedRef(report, so, "screen", "Stand prefab keeps scene-specific screen unassigned by default.");
        WarnIfAssignedRef(report, so, "itemDatabase", "Stand prefab should not own itemDatabase; assign it on the scene screen or scene instance if needed.");
        WarnIfAssignedRef(report, so, "catalog", "Stand prefab should not own catalog data; scene instance override is allowed if needed.");
        RequireBool(report, so, "resolveSceneScreenIfMissing", true, "Stand scene-screen fallback");
    }

    private static void ValidateGlobalUIRoot(ContractReport report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUIRootPrefabPath);
        if (prefab == null)
        {
            report.Error($"Missing GlobalUIRoot prefab at {GlobalUIRootPrefabPath}.");
            return;
        }

        ValidateEncyclopediaLayout(report, prefab.transform, "GlobalUIRoot");
    }

    private static void ValidateEncyclopediaUIPrefab(ContractReport report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EncyclopediaUIPrefabPath);
        if (prefab == null)
        {
            report.Error($"Missing EncyclopediaUI prefab at {EncyclopediaUIPrefabPath}.");
            return;
        }

        ValidateEncyclopediaLayout(report, prefab.transform, "EncyclopediaUI prefab");
    }

    private static void ValidateEncyclopediaLayout(ContractReport report, Transform layoutRoot, string label)
    {
        Transform encyclopediaRoot = Find(layoutRoot, "EncyclopediaUI");
        if (encyclopediaRoot == null && layoutRoot != null && string.Equals(layoutRoot.name, "EncyclopediaUI", StringComparison.OrdinalIgnoreCase))
            encyclopediaRoot = layoutRoot;

        Transform book = Find(encyclopediaRoot, "Book") ?? Find(layoutRoot, "Book");
        Transform leftPage = Find(book, "LeftPage");
        Transform rightPage = Find(book, "RightPage");

        Require(report, encyclopediaRoot != null, $"{label} must contain EncyclopediaUI.");
        Require(report, book != null, $"{label} EncyclopediaUI must contain Book.");
        Require(report, leftPage != null, $"{label} Book must contain LeftPage.");
        Require(report, rightPage != null, $"{label} Book must contain RightPage.");
        if (book == null)
            return;

        EncyclopediaScreen screen = book.GetComponent<EncyclopediaScreen>();
        EncyclopediaBookPresentation bookPresentation = book.GetComponent<EncyclopediaBookPresentation>();
        EncyclopediaItemTab itemTab = book.GetComponent<EncyclopediaItemTab>();
        Require(report, screen != null, "Book must carry EncyclopediaScreen.");
        Require(report, bookPresentation != null, "Book must carry EncyclopediaBookPresentation.");
        Require(report, itemTab != null, "Book must carry EncyclopediaItemTab.");

        if (screen != null)
        {
            SerializedObject so = new SerializedObject(screen);
            RequireRef(report, so, "screenActiveRoot", "Screen active root");
            if (GetRef(so, "screenActiveRoot") != encyclopediaRoot?.gameObject)
                report.Error("Screen active root should be the authored EncyclopediaUI object.");
            RequireRef(report, so, "canvasGroup", "Screen CanvasGroup");
            RequireRef(report, so, "itemTab", "Screen ItemTab");
            RequireRef(report, so, "bookPresentation", "Screen BookPresentation");
            WarnIfMissingRef(report, so, "itemMainTabIcon", "Screen item main tab icon is unassigned.");
            WarnIfMissingRef(report, so, "monsterMainTabIcon", "Screen monster main tab icon is unassigned.");
            WarnIfMissingRef(report, so, "bossMainTabIcon", "Screen boss main tab icon is unassigned.");
        }

        if (bookPresentation != null)
        {
            SerializedObject so = new SerializedObject(bookPresentation);
            if (GetRef(so, "dimPanelGroup") == null && GetRef(so, "dimPanelGraphic") == null)
                report.Error("BookPresentation needs either dimPanelGroup or dimPanelGraphic.");
            RequireRef(report, so, "bookMotionRoot", "BookPresentation motion root");
            RequireRef(report, so, "bookAnimator", "BookPresentation book animator");
            RequireRef(report, so, "pageCoverImage", "BookPresentation page cover image");
            RequireRef(report, so, "contentAppearClip", "BookPresentation ContentAppear clip");
            RequireRef(report, so, "bookOpenClip", "BookPresentation BookOpen clip");
            RequireRef(report, so, "bookCloseClip", "BookPresentation BookClose clip");
            RequireRef(report, so, "leftPageClip", "BookPresentation BookLeftPage clip");
            RequireRef(report, so, "rightPageClip", "BookPresentation BookRightPage clip");
            RequireArrayRef(report, so, "pageContentRoots", 0, "BookPresentation LeftPage content root");
            RequireArrayRef(report, so, "pageContentRoots", 1, "BookPresentation RightPage content root");

            Animator animator = GetRef(so, "bookAnimator") as Animator;
            ValidateAnimatorStateMotion(report, animator, GetString(so, "bookOpenStateName", "BookOpen"), GetRef(so, "bookOpenClip") as AnimationClip, "BookPresentation BookOpen state motion");
            ValidateAnimatorStateMotion(report, animator, GetString(so, "bookCloseStateName", "BookClose"), GetRef(so, "bookCloseClip") as AnimationClip, "BookPresentation BookClose state motion");
            ValidateAnimatorStateMotion(report, animator, GetString(so, "leftPageStateName", "BookLeftPage"), GetRef(so, "leftPageClip") as AnimationClip, "BookPresentation BookLeftPage state motion");
            ValidateAnimatorStateMotion(report, animator, GetString(so, "rightPageStateName", "BookRightPage"), GetRef(so, "rightPageClip") as AnimationClip, "BookPresentation BookRightPage state motion");
            ValidateAnimatorDefaultState(report, animator, "BookIdle", "BookPresentation book animator default state");
        }

        if (itemTab != null)
        {
            SerializedObject so = new SerializedObject(itemTab);
            RequireRef(report, so, "itemDatabase", "ItemTab ItemDatabase");
            RequireRef(report, so, "leftPage", "ItemTab LeftPage presenter");
            RequireRef(report, so, "rightPage", "ItemTab RightPage presenter");
            RequireRef(report, so, "bookPresentation", "ItemTab BookPresentation");
        }

        ValidateLeftPage(report, leftPage);
        ValidateRightPage(report, rightPage);
    }

    private static void ValidateLeftPage(ContractReport report, Transform leftPage)
    {
        if (leftPage == null)
            return;

        if (leftPage.GetComponentsInChildren<EncyclopediaLeftPageView>(true).Length > 0)
            report.Error("LeftPage still contains legacy EncyclopediaLeftPageView.");

        EncyclopediaItemLeftPage presenter = leftPage.GetComponent<EncyclopediaItemLeftPage>();
        Require(report, presenter != null, "LeftPage must carry EncyclopediaItemLeftPage.");
        if (presenter == null)
            return;

        SerializedObject so = new SerializedObject(presenter);
        RequireRef(report, so, "weaponButton", "LeftPage weapon sub-tab button");
        RequireRef(report, so, "relicButton", "LeftPage relic sub-tab button");
        RequireRef(report, so, "consumableButton", "LeftPage consumable sub-tab button");
        WarnIfMissingRef(report, so, "weaponTabIcon", "LeftPage weapon sub-tab icon is unassigned.");
        WarnIfMissingRef(report, so, "relicTabIcon", "LeftPage relic sub-tab icon is unassigned.");
        WarnIfMissingRef(report, so, "consumableTabIcon", "LeftPage consumable sub-tab icon is unassigned.");
        RequireRef(report, so, "entryGridView", "LeftPage entry grid view");
        RequireRef(report, so, "previousPageButton", "LeftPage previous page button");
        RequireRef(report, so, "nextPageButton", "LeftPage next page button");
        RequireRef(report, so, "pageText", "LeftPage page text");

        Object gridObject = GetRef(so, "entryGridView");
        if (gridObject is EncyclopediaEntryGridView gridView)
        {
            SerializedObject gridSo = new SerializedObject(gridView);
            RequireRef(report, gridSo, "entryGridRoot", "EntryGrid root");
            RequireRef(report, gridSo, "entrySlotPrefab", "EntryGrid slot prefab");
            RequireInt(report, gridSo, "slotsPerPage", 16, "EntryGrid slots per page");
        }
    }

    private static void ValidateRightPage(ContractReport report, Transform rightPage)
    {
        if (rightPage == null)
            return;

        if (rightPage.GetComponentsInChildren<EncyclopediaDetailPanel>(true).Length > 0)
            report.Error("RightPage still contains legacy EncyclopediaDetailPanel.");
        if (rightPage.GetComponentsInChildren<ItemDetailPanel>(true).Length > 0)
            report.Error("RightPage still contains inventory ItemDetailPanel presenter component.");
        if (rightPage.GetComponentsInChildren<WeaponDetailViewV2>(true).Length > 0)
            report.Error("RightPage still contains inventory WeaponDetailViewV2.");
        if (rightPage.GetComponentsInChildren<RelicDetailView>(true).Length > 0)
            report.Error("RightPage still contains inventory RelicDetailView.");
        if (rightPage.GetComponentsInChildren<ConsumableDetailView>(true).Length > 0)
            report.Error("RightPage still contains inventory ConsumableDetailView.");
        if (Find(rightPage, "InvisibleCollisionPanel") != null)
            report.Error("RightPage still contains legacy InvisibleCollisionPanel.");

        EncyclopediaItemRightPage[] presenters = rightPage.GetComponentsInChildren<EncyclopediaItemRightPage>(true);
        if (presenters.Length != 1)
            report.Error($"RightPage should contain exactly one EncyclopediaItemRightPage, found {presenters.Length}.");

        EncyclopediaItemRightPage presenter = rightPage.GetComponent<EncyclopediaItemRightPage>();
        if (presenter == null)
            presenter = presenters.Length > 0 ? presenters[0] : null;
        if (presenter == null)
            return;

        SerializedObject so = new SerializedObject(presenter);
        RequireRef(report, so, "contentRoot", "RightPage content root");
        RequireRef(report, so, "titleText", "RightPage title text");
        RequireRef(report, so, "storyText", "RightPage shared story/description text");
        RequireRef(report, so, "weaponStatsRoot", "RightPage weapon stats root");
        RequireRef(report, so, "weaponStatsText", "RightPage weapon stats text");
        RequireRef(report, so, "relicPreviewRoot", "RightPage relic preview root");
        RequireRef(report, so, "relicLevelText", "RightPage relic level text");
        WarnIfMissingRef(report, so, "relicEffectRoot", "RightPage relic effect root is unassigned; relic effects should not fall back to StoryText.");
        WarnIfMissingRef(report, so, "relicEffectText", "RightPage relic effect text is unassigned; relic effects should not fall back to StoryText.");
        WarnIfSameRef(report, so, "relicEffectText", "storyText", "RightPage relic effect text should be separate from StoryText.");
        WarnIfSameRef(report, so, "weaponStatsRoot", "relicPreviewRoot", "RightPage weapon stats root and relic preview root should be separate authored objects.");
        WarnIfSameRef(report, so, "weaponStatsText", "relicLevelText", "RightPage weapon stats text and relic level text should be separate authored TMP texts.");
        RequireRef(report, so, "relicPreviewPreviousGuideIcon", "RightPage previous relic preview guide icon");
        RequireRef(report, so, "relicPreviewNextGuideIcon", "RightPage next relic preview guide icon");
        RequireRef(report, so, "abilityContainer", "RightPage ability container");
        RequireRef(report, so, "abilityBlockPrefab", "RightPage ability block prefab");
        RequireRef(report, so, "detailScrollRect", "RightPage detail ScrollRect");
        ValidateDetailScrollRect(report, GetRef(so, "detailScrollRect") as ScrollRect);
        ValidateGuideLayout(report, rightPage, "PrevPreview", "RightPage previous relic preview guide");
        ValidateGuideLayout(report, rightPage, "NextPreview", "RightPage next relic preview guide");
        WarnIfMissingRef(report, so, "iconImage", "RightPage iconImage is optional and currently unassigned.");
        WarnIfMissingRef(report, so, "namePanelRoot", "RightPage Name_Panel is optional but currently unassigned.");
        WarnIfMissingRef(report, so, "descriptionRoot", "RightPage DescriptionRoot is optional but currently unassigned.");
        WarnIfMissingRef(report, so, "emptyRoot", "RightPage EmptyRoot is optional but currently unassigned.");
    }

    private sealed class WireContext
    {
        private readonly GameObject root;
        private readonly ItemDatabase itemDatabase;
        private readonly EncyclopediaEntryButton entrySlotPrefab;
        private readonly WeaponAbilityBlockView abilityBlockPrefab;

        public WireContext(GameObject root)
        {
            this.root = root;
            itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            GameObject entrySlotPrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(EntrySlotPrefabPath);
            entrySlotPrefab = entrySlotPrefabObject != null ? entrySlotPrefabObject.GetComponent<EncyclopediaEntryButton>() : null;
            GameObject abilityBlockPrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityBlockPrefabPath);
            abilityBlockPrefab = abilityBlockPrefabObject != null ? abilityBlockPrefabObject.GetComponent<WeaponAbilityBlockView>() : null;
        }

        public void Wire()
        {
            Transform encyclopediaRoot = Find(root.transform, "EncyclopediaUI");
            Transform book = Find(encyclopediaRoot, "Book") ?? Find(root.transform, "Book");
            if (book == null)
                throw new System.InvalidOperationException("[EncyclopediaWire] Book object was not found under GlobalUIRoot.");

            Transform leftPage = Find(book, "LeftPage");
            Transform rightPage = Find(book, "RightPage");
            if (leftPage == null)
                throw new System.InvalidOperationException("[EncyclopediaWire] LeftPage object was not found under Book.");
            if (rightPage == null)
                throw new System.InvalidOperationException("[EncyclopediaWire] RightPage object was not found under Book.");

            Transform detailHost = Find(rightPage, "ItemDetailPanel") ?? rightPage;
            Transform slotGrid = Find(leftPage, "SlotGrid") ?? Find(leftPage, "EntryGridRoot") ?? Find(leftPage, "GridRoot");

            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(book.gameObject);
            EncyclopediaScreen screen = GetOrAdd<EncyclopediaScreen>(book.gameObject);
            EncyclopediaBookPresentation bookPresentation = GetOrAdd<EncyclopediaBookPresentation>(book.gameObject);
            EncyclopediaItemTab itemTab = GetOrAdd<EncyclopediaItemTab>(book.gameObject);
            EncyclopediaItemLeftPage itemLeftPage = GetOrAdd<EncyclopediaItemLeftPage>(leftPage.gameObject);
            EncyclopediaEntryGridView entryGridView = WireEntryGrid(slotGrid, leftPage);
            EncyclopediaItemRightPage itemRightPage = GetOrAdd<EncyclopediaItemRightPage>(rightPage.gameObject);
            RemoveLegacyLeftPagePresenters(leftPage);
            RemoveDuplicateItemRightPagePresenters(rightPage, itemRightPage);
            RemoveLegacyDetailPresenters(rightPage);

            WireBookPresentation(bookPresentation, root.transform, encyclopediaRoot, book, leftPage, rightPage);
            WireLeftPage(itemLeftPage, leftPage, entryGridView);
            WireRightPage(itemRightPage, rightPage, detailHost);
            WireItemTab(itemTab, itemLeftPage, itemRightPage, bookPresentation);
            WireScreen(screen, encyclopediaRoot, book, rightPage, canvasGroup, itemTab, bookPresentation);
        }

        private EncyclopediaEntryGridView WireEntryGrid(Transform slotGrid, Transform leftPage)
        {
            if (slotGrid == null)
            {
                Debug.LogWarning("[EncyclopediaWire] SlotGrid/EntryGridRoot was not found. No grid root was created; assign an authored grid root in the prefab.");
                return null;
            }

            EncyclopediaEntryGridView gridView = GetOrAdd<EncyclopediaEntryGridView>(slotGrid.gameObject);
            SerializedObject so = new SerializedObject(gridView);
            SetObject(so, "entryGridRoot", slotGrid);
            SetObject(so, "entrySlotPrefab", entrySlotPrefab);
            SetInt(so, "slotsPerPage", 16);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gridView);
            return gridView;
        }

        private void WireLeftPage(EncyclopediaItemLeftPage itemLeftPage, Transform leftPage, EncyclopediaEntryGridView entryGridView)
        {
            Transform titleGroup = Find(leftPage, "TitleGroup");
            TMP_Text titleText = FindComponentUnder<TMP_Text>(titleGroup, "TitleText", "Title", "Text", "Text (TMP)", "Text(TMP)") ??
                FindComponentUnder<TMP_Text>(leftPage, "TitleText");
            Image titleIcon = FindComponentUnder<Image>(titleGroup, "TitleIcon", "Icon", "Decoration");

            Button[] subTabs = ResolveSubTabButtons(leftPage);
            Transform pageButtonGroup = Find(leftPage, "PageButtonGroup");
            Button previousButton = FindComponentUnder<Button>(pageButtonGroup, "PreviousStepButton", "PreviousButton", "PrevButton", "Previous", "Prev");
            Button nextButton = FindComponentUnder<Button>(pageButtonGroup, "NextStepButton", "NextButton", "Next");
            if ((previousButton == null || nextButton == null) && pageButtonGroup != null)
            {
                Button[] pageButtons = pageButtonGroup.GetComponentsInChildren<Button>(true);
                if (previousButton == null && pageButtons.Length > 0)
                    previousButton = pageButtons[0];
                if (nextButton == null && pageButtons.Length > 1)
                    nextButton = pageButtons[1];
            }

            TMP_Text pageText = FindComponentUnder<TMP_Text>(pageButtonGroup, "PageText", "Page", "PageNum", "Text", "Text (TMP)", "Text(TMP)") ??
                FindComponentUnder<TMP_Text>(leftPage, "PageText", "PageNum");
            TMP_Text entryCountText = FindComponentUnder<TMP_Text>(leftPage, "EntryCountText", "CountText");
            TMP_Text noticeText = FindComponentUnder<TMP_Text>(leftPage, "ListNoticeText", "NoticeText", "EmptyText");

            SerializedObject so = new SerializedObject(itemLeftPage);
            SetObject(so, "titleText", titleText);
            SetObject(so, "titleIcon", titleIcon);
            SetObject(so, "weaponButton", subTabs.Length > 0 ? subTabs[0] : null);
            SetObject(so, "relicButton", subTabs.Length > 1 ? subTabs[1] : null);
            SetObject(so, "consumableButton", subTabs.Length > 2 ? subTabs[2] : null);
            SetObject(so, "weaponTabIcon", subTabs.Length > 0 ? FindTabIcon(subTabs[0].transform) : null);
            SetObject(so, "relicTabIcon", subTabs.Length > 1 ? FindTabIcon(subTabs[1].transform) : null);
            SetObject(so, "consumableTabIcon", subTabs.Length > 2 ? FindTabIcon(subTabs[2].transform) : null);
            SetObject(so, "weaponSelectedMarker", subTabs.Length > 0 ? FindMarker(subTabs[0].transform) : null);
            SetObject(so, "relicSelectedMarker", subTabs.Length > 1 ? FindMarker(subTabs[1].transform) : null);
            SetObject(so, "consumableSelectedMarker", subTabs.Length > 2 ? FindMarker(subTabs[2].transform) : null);
            SetObject(so, "entryGridView", entryGridView);
            SetObject(so, "previousPageButton", previousButton);
            SetObject(so, "nextPageButton", nextButton);
            SetObject(so, "pageText", pageText);
            SetObject(so, "entryCountText", entryCountText);
            SetObject(so, "noticeText", noticeText);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemLeftPage);
        }

        private void WireRightPage(EncyclopediaItemRightPage itemRightPage, Transform rightPage, Transform detailHost)
        {
            Transform contentRoot = Find(detailHost, "ItemRightContent") ?? Find(detailHost, "ContentRoot") ??
                Find(detailHost, "ScrollContent") ?? Find(detailHost, "ViewportContent") ?? Find(detailHost, "Content");
            Transform descriptionRoot = Find(detailHost, "DescriptionRoot") ?? Find(detailHost, "DescriptionSection") ??
                Find(detailHost, "CommonDescriptionRoot") ?? Find(detailHost, "DescriptionPanel");
            Transform weaponStatsRoot = Find(detailHost, "WeaponStatsRoot") ?? Find(detailHost, "WeaponStatsPanel") ??
                Find(detailHost, "WeaponStatsSection") ?? Find(detailHost, "StatTextPanel") ??
                Find(detailHost, "StatsRoot") ?? Find(detailHost, "StatRoot");
            Transform relicPreviewRoot = Find(detailHost, "RelicPreviewRoot") ?? Find(detailHost, "RelicLevelPreviewRoot") ??
                Find(detailHost, "LevelPreviewRoot") ?? Find(detailHost, "LvPanel") ?? Find(detailHost, "LevelPanel");
            if (weaponStatsRoot == null)
                weaponStatsRoot = relicPreviewRoot;
            if (relicPreviewRoot == null)
                relicPreviewRoot = weaponStatsRoot;

            Transform weaponAbilityRoot = Find(detailHost, "WeaponAbilityRoot") ?? Find(detailHost, "AbilityRoot") ??
                Find(detailHost, "AbilitySection") ?? Find(detailHost, "AbilityContainerRoot");
            Transform abilitySearchRoot = weaponAbilityRoot != null ? weaponAbilityRoot : detailHost;
            Transform abilityContainer = Find(abilitySearchRoot, "AbilityContainer") ?? Find(abilitySearchRoot, "AbilityBlockContainer") ??
                Find(detailHost, "AbilityContainer") ?? Find(detailHost, "AbilityBlockContainer");
            ConfigureAbilityContainerLayout(abilityContainer);
            RemoveLegacyInventoryDetailViews(detailHost);
            ConfigureDetailScrollHost(detailHost, contentRoot, abilityContainer);

            TMP_Text weaponStatsText = FindComponentUnder<TMP_Text>(
                weaponStatsRoot,
                "WeaponStatsText",
                "StatsText",
                "StatText",
                "StatValueText",
                "Text",
                "Text (TMP)",
                "Text(TMP)") ??
                FindComponentUnder<TMP_Text>(detailHost, "WeaponStatsText", "StatsText", "StatText");
            TMP_Text relicLevelText = FindComponentUnder<TMP_Text>(
                relicPreviewRoot,
                "LvTxt",
                "LvText",
                "LevelText",
                "RelicLevelText",
                "PreviewLevelText") ??
                FindComponentUnder<TMP_Text>(detailHost, "LvTxt", "LvText", "LevelText", "RelicLevelText", "PreviewLevelText");
            if (weaponStatsRoot == relicPreviewRoot)
            {
                weaponStatsText ??= relicLevelText;
                relicLevelText ??= weaponStatsText;
            }

            Transform relicPreviousGuide = Find(relicPreviewRoot, "RelicPreviewPreviousGuide") ?? Find(relicPreviewRoot, "PreviousGuide") ??
                Find(relicPreviewRoot, "PrevGuide") ?? Find(relicPreviewRoot, "PrevPreview") ?? Find(detailHost, "PrevPreview");
            Transform relicNextGuide = Find(relicPreviewRoot, "RelicPreviewNextGuide") ?? Find(relicPreviewRoot, "NextGuide") ??
                Find(relicPreviewRoot, "NextPreview") ?? Find(detailHost, "NextPreview");
            ConfigureRelicPreviewGuideLayout(relicPreviousGuide);
            ConfigureRelicPreviewGuideLayout(relicNextGuide);
            Transform relicEffectRoot = Find(detailHost, "RelicEffectRoot") ?? Find(detailHost, "EffectRoot") ??
                Find(detailHost, "RelicEffectSection");

            TMP_Text titleText = FindComponentUnder<TMP_Text>(detailHost, "TitleText", "NameText", "Name") ??
                FindComponentUnderParent<TMP_Text>(detailHost, "Name_Panel", "Text", "Text (TMP)", "Text(TMP)", "NameText", "Name") ??
                FindComponentUnderParent<TMP_Text>(detailHost, "NamePanel", "Text", "Text (TMP)", "Text(TMP)", "NameText", "Name") ??
                FindComponentUnder<TMP_Text>(rightPage, "TitleText", "NameText", "Name");
            Transform namePanel = Find(detailHost, "Name_Panel") ?? Find(detailHost, "NamePanel") ?? Find(detailHost, "HeaderNamePanel");
            TMP_Text storyText = FindComponentUnder<TMP_Text>(detailHost, "StoryText", "Story");

            SerializedObject so = new SerializedObject(itemRightPage);
            SetObject(so, "contentRoot", contentRoot != null ? contentRoot.gameObject : detailHost.gameObject);
            Transform emptyRoot = Find(rightPage, "EmptyRoot") ?? Find(detailHost, "EmptyRoot");
            SetObject(so, "emptyRoot", emptyRoot != null ? emptyRoot.gameObject : null);
            SetObject(so, "iconImage", FindComponentUnder<Image>(detailHost, "Icon", "ItemIcon", "DetailIcon", "DetailImage"));
            SetObject(so, "titleText", titleText);
            SetObject(so, "namePanelRoot", namePanel as RectTransform);
            SetObject(so, "descriptionRoot", descriptionRoot != null ? descriptionRoot.gameObject : null);
            SetObject(so, "descriptionTitleText", FindComponentUnder<TMP_Text>(descriptionRoot, "DescriptionTitleText", "SectionTitleText", "TitleText", "Title"));
            SetObject(so, "storyText", storyText);
            SetObject(so, "weaponStatsRoot", weaponStatsRoot != null ? weaponStatsRoot.gameObject : null);
            SetObject(so, "weaponStatsText", weaponStatsText);
            SetObject(so, "weaponAbilityRoot", weaponAbilityRoot != null ? weaponAbilityRoot.gameObject : null);
            SetObject(so, "abilityContainer", abilityContainer);
            SetObject(so, "abilityBlockPrefab", abilityBlockPrefab);
            SetObject(so, "relicPreviewRoot", relicPreviewRoot != null ? relicPreviewRoot.gameObject : null);
            SetObject(so, "relicLevelText", relicLevelText);
            SetObject(so, "relicPreviewPreviousGuideRoot", relicPreviousGuide != null ? relicPreviousGuide.gameObject : null);
            SetObject(so, "relicPreviewPreviousGuideIcon", FindGuideIcon(relicPreviousGuide));
            SetObject(so, "relicPreviewPreviousGuideCanvasGroup", relicPreviousGuide != null ? relicPreviousGuide.GetComponent<CanvasGroup>() : null);
            SetObject(so, "relicPreviewNextGuideRoot", relicNextGuide != null ? relicNextGuide.gameObject : null);
            SetObject(so, "relicPreviewNextGuideIcon", FindGuideIcon(relicNextGuide));
            SetObject(so, "relicPreviewNextGuideCanvasGroup", relicNextGuide != null ? relicNextGuide.GetComponent<CanvasGroup>() : null);
            SetObject(so, "relicEffectRoot", relicEffectRoot != null ? relicEffectRoot.gameObject : null);
            SetObject(so, "relicEffectText", FindComponentUnder<TMP_Text>(relicEffectRoot, "RelicEffectText", "EffectText", "BodyText", "DescriptionText"));
            SetObject(so, "detailScrollRect", detailHost.GetComponent<ScrollRect>() ?? detailHost.GetComponentInChildren<ScrollRect>(true));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemRightPage);

            if (weaponStatsRoot != null && weaponStatsRoot == relicPreviewRoot)
                Debug.LogWarning("[EncyclopediaWire] Weapon stats root and relic preview root resolved to the same object. Author a separate WeaponStatsRoot/StatTextPanel to keep weapon stat text separate.", itemRightPage);
            if (weaponStatsText != null && weaponStatsText == relicLevelText)
                Debug.LogWarning("[EncyclopediaWire] Weapon stats text and relic level text resolved to the same TMP text. Author a separate WeaponStatsText/StatText and RelicLevelText/LvTxt pair.", itemRightPage);
        }

        private static void RemoveDuplicateItemRightPagePresenters(Transform rightPage, EncyclopediaItemRightPage owner)
        {
            if (rightPage == null || owner == null)
                return;

            EncyclopediaItemRightPage[] presenters = rightPage.GetComponentsInChildren<EncyclopediaItemRightPage>(true);
            for (int i = 0; i < presenters.Length; i++)
            {
                EncyclopediaItemRightPage presenter = presenters[i];
                if (presenter == null || presenter == owner)
                    continue;

                Object.DestroyImmediate(presenter, allowDestroyingAssets: true);
            }
        }

        private static void RemoveLegacyDetailPresenters(Transform rightPage)
        {
            if (rightPage == null)
                return;

            EncyclopediaDetailPanel[] legacyPanels = rightPage.GetComponentsInChildren<EncyclopediaDetailPanel>(true);
            for (int i = 0; i < legacyPanels.Length; i++)
            {
                if (legacyPanels[i] != null)
                    Object.DestroyImmediate(legacyPanels[i], allowDestroyingAssets: true);
            }
        }

        private static void RemoveLegacyInventoryDetailViews(Transform detailHost)
        {
            if (detailHost == null)
                return;

            ItemDetailPanel legacyPanel = detailHost.GetComponent<ItemDetailPanel>();
            if (legacyPanel != null)
                Object.DestroyImmediate(legacyPanel, allowDestroyingAssets: true);

            RemoveChildObjectByName(detailHost, "InvisibleCollisionPanel");
            RemoveComponentGameObjects<WeaponDetailViewV2>(detailHost);
            RemoveComponentGameObjects<RelicDetailView>(detailHost);
            RemoveComponentGameObjects<ConsumableDetailView>(detailHost);
        }

        private static void RemoveChildObjectByName(Transform root, string childName)
        {
            Transform child = Find(root, childName);
            if (child != null)
                Object.DestroyImmediate(child.gameObject, allowDestroyingAssets: true);
        }

        private static void RemoveComponentGameObjects<T>(Transform root) where T : Component
        {
            if (root == null)
                return;

            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null)
                {
                    if (component.transform == root)
                        Object.DestroyImmediate(component, allowDestroyingAssets: true);
                    else
                        Object.DestroyImmediate(component.gameObject, allowDestroyingAssets: true);
                }
            }
        }

        private static void RemoveLegacyLeftPagePresenters(Transform leftPage)
        {
            if (leftPage == null)
                return;

            EncyclopediaLeftPageView[] legacyViews = leftPage.GetComponentsInChildren<EncyclopediaLeftPageView>(true);
            for (int i = 0; i < legacyViews.Length; i++)
            {
                if (legacyViews[i] != null)
                    Object.DestroyImmediate(legacyViews[i], allowDestroyingAssets: true);
            }
        }

        private void WireBookPresentation(
            EncyclopediaBookPresentation bookPresentation,
            Transform globalRoot,
            Transform encyclopediaRoot,
            Transform book,
            Transform leftPage,
            Transform rightPage)
        {
            Transform dimPanel = Find(encyclopediaRoot, "DimPanel") ?? Find(globalRoot, "DimPanel");
            Transform bookMotionRoot = Find(book, "BookMotionRoot") ?? Find(book, "BookMotion") ?? Find(book, "MotionRoot");
            CanvasGroup pageContentGroup = FindComponentUnder<CanvasGroup>(book, "PageContentGroup", "PageContent", "ContentRoot", "Pages");
            Animator bookAnimator = FindBookAnimator(book);
            Image pageCoverImage = EnsurePageCoverImage(book);
            AnimationClip openedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BookIdleClipPath);
            AnimationClip closedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BookIdleClipPath);
            AnimationClip bookOpenClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BookOpenClipPath);
            AnimationClip bookCloseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BookCloseClipPath);
            AnimationClip leftPageClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BookLeftPageClipPath);
            AnimationClip rightPageClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BookRightPageClipPath);
            AnimationClip contentAppearClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ContentAppearClipPath);
            RectTransform motionRect = (bookMotionRoot ?? book) as RectTransform;

            SerializedObject so = new SerializedObject(bookPresentation);
            SetObject(so, "dimPanelGroup", dimPanel != null ? dimPanel.GetComponent<CanvasGroup>() : null);
            SetObject(so, "dimPanelGraphic", dimPanel != null ? dimPanel.GetComponent<Graphic>() : null);
            SetObject(so, "bookMotionRoot", motionRect);
            SetObject(so, "pageContentGroup", pageContentGroup);
            SetObjectArray(so, "pageContentRoots", leftPage != null ? leftPage.gameObject : null, rightPage != null ? rightPage.gameObject : null);
            SetObject(so, "bookAnimator", bookAnimator);
            SetObject(so, "pageCoverImage", pageCoverImage);
            SetObject(so, "pageCoverAnimator", pageCoverImage != null ? pageCoverImage.GetComponent<Animator>() : null);
            SetString(so, "openedStateName", "BookIdle");
            SetString(so, "closedStateName", "BookIdle");
            SetString(so, "bookOpenStateName", "BookOpen");
            SetString(so, "bookCloseStateName", "BookClose");
            SetString(so, "leftPageStateName", "BookLeftPage");
            SetString(so, "rightPageStateName", "BookRightPage");
            SetObject(so, "openedClip", openedClip);
            SetObject(so, "closedClip", closedClip);
            SetObject(so, "bookOpenClip", bookOpenClip);
            SetObject(so, "bookCloseClip", bookCloseClip);
            SetObject(so, "leftPageClip", leftPageClip);
            SetObject(so, "rightPageClip", rightPageClip);
            SetObject(so, "contentAppearClip", contentAppearClip);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bookPresentation);
        }

        private static Image EnsurePageCoverImage(Transform book)
        {
            Image pageCoverImage = FindComponentUnder<Image>(
                book,
                "RevealOverlay",
                "PageCover",
                "ContentAppearCover",
                "PageCoverImage");
            if (pageCoverImage != null)
            {
                ConfigurePageCoverImage(pageCoverImage);
                return pageCoverImage;
            }

            if (book == null)
                return null;

            GameObject pageCover = new GameObject("PageCover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = pageCover.GetComponent<RectTransform>();
            rect.SetParent(book, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsLastSibling();

            pageCoverImage = pageCover.GetComponent<Image>();
            ConfigurePageCoverImage(pageCoverImage);
            EditorUtility.SetDirty(pageCover);
            return pageCoverImage;
        }

        private static void ConfigurePageCoverImage(Image pageCoverImage)
        {
            if (pageCoverImage == null)
                return;

            pageCoverImage.raycastTarget = false;
            pageCoverImage.enabled = false;
            pageCoverImage.gameObject.SetActive(false);
            EditorUtility.SetDirty(pageCoverImage.gameObject);
            EditorUtility.SetDirty(pageCoverImage);
        }

        private static void ConfigureRelicPreviewGuideLayout(Transform guide)
        {
            if (guide == null)
                return;

            if (guide is RectTransform rect)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 32f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);
                EditorUtility.SetDirty(rect);
            }

            LayoutElement layoutElement = GetOrAdd<LayoutElement>(guide.gameObject);
            layoutElement.ignoreLayout = false;
            layoutElement.minWidth = 32f;
            layoutElement.minHeight = 32f;
            layoutElement.preferredWidth = 32f;
            layoutElement.preferredHeight = 32f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
            EditorUtility.SetDirty(layoutElement);
        }

        private void WireItemTab(
            EncyclopediaItemTab itemTab,
            EncyclopediaItemLeftPage itemLeftPage,
            EncyclopediaItemRightPage itemRightPage,
            EncyclopediaBookPresentation bookPresentation)
        {
            SerializedObject so = new SerializedObject(itemTab);
            SetObject(so, "itemDatabase", itemDatabase);
            SetObject(so, "leftPage", itemLeftPage);
            SetObject(so, "rightPage", itemRightPage);
            SetObject(so, "bookPresentation", bookPresentation);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemTab);
        }

        private void WireScreen(
            EncyclopediaScreen screen,
            Transform encyclopediaRoot,
            Transform book,
            Transform rightPage,
            CanvasGroup canvasGroup,
            EncyclopediaItemTab itemTab,
            EncyclopediaBookPresentation bookPresentation)
        {
            Button[] mainTabs = ResolveMainTabButtons(rightPage);
            SerializedObject so = new SerializedObject(screen);
            SetObject(so, "screenActiveRoot", encyclopediaRoot != null ? encyclopediaRoot.gameObject : book.gameObject);
            SetObject(so, "canvasGroup", canvasGroup);
            SetObject(so, "closeButton", FindComponentUnder<Button>(book, "CloseButton", "Close"));
            SetObject(so, "itemTab", itemTab);
            SetObject(so, "itemMainTabButton", mainTabs.Length > 0 ? mainTabs[0] : null);
            SetObject(so, "monsterMainTabButton", mainTabs.Length > 1 ? mainTabs[1] : null);
            SetObject(so, "bossMainTabButton", mainTabs.Length > 2 ? mainTabs[2] : null);
            SetObject(so, "itemMainTabIcon", mainTabs.Length > 0 ? FindTabIcon(mainTabs[0].transform) : null);
            SetObject(so, "monsterMainTabIcon", mainTabs.Length > 1 ? FindTabIcon(mainTabs[1].transform) : null);
            SetObject(so, "bossMainTabIcon", mainTabs.Length > 2 ? FindTabIcon(mainTabs[2].transform) : null);
            SetObject(so, "itemMainSelectedMarker", mainTabs.Length > 0 ? FindMarker(mainTabs[0].transform) : null);
            SetObject(so, "monsterMainSelectedMarker", mainTabs.Length > 1 ? FindMarker(mainTabs[1].transform) : null);
            SetObject(so, "bossMainSelectedMarker", mainTabs.Length > 2 ? FindMarker(mainTabs[2].transform) : null);
            SetObject(so, "revealPresentation", book.GetComponentInChildren<BookPixelRevealPresentation>(true));
            SetObject(so, "bookPresentation", bookPresentation);
            SetObject(so, "rootSlideFadePresentation", book.GetComponent<UISlideFadePresentation>());
            SetBool(so, "closeOnRuntimeAwake", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(screen);
        }

        private Button[] ResolveSubTabButtons(Transform leftPage)
        {
            Button weapon = FindComponentUnder<Button>(leftPage, "WeaponTab", "WeaponTabButton", "Weapon", "WeaponButton");
            Button relic = FindComponentUnder<Button>(leftPage, "RelicTab", "RelicTabButton", "Relic", "RelicButton");
            Button consumable = FindComponentUnder<Button>(leftPage, "ConsumableTab", "ConsumableTabButton", "Consumable", "ConsumableButton");
            if (weapon != null && relic != null && consumable != null)
                return new[] { weapon, relic, consumable };

            Transform tabGroup = Find(leftPage, "TabButtonGroup");
            Button[] buttons = tabGroup != null ? tabGroup.GetComponentsInChildren<Button>(true) : CollectNamedButtons(leftPage, "TabButton");
            if (buttons.Length >= 3)
                return new[] { buttons[0], buttons[1], buttons[2] };

            Debug.LogWarning("[EncyclopediaWire] One or more item sub-tab buttons were not found. Missing buttons were left unassigned.");
            return new[]
            {
                weapon,
                relic,
                consumable
            };
        }

        private static Button[] ResolveMainTabButtons(Transform rightPage)
        {
            Button item = FindComponentUnder<Button>(rightPage, "ItemTab", "ItemTabButton", "Item", "ItemButton");
            Button monster = FindComponentUnder<Button>(rightPage, "MonsterTab", "MonsterTabButton", "Monster", "MonsterButton");
            Button boss = FindComponentUnder<Button>(rightPage, "BossTab", "BossTabButton", "Boss", "BossButton");
            if (item != null && monster != null && boss != null)
                return new[] { item, monster, boss };

            Transform tabGroup = Find(rightPage, "TabButtonGroup");
            Button[] buttons = tabGroup != null ? tabGroup.GetComponentsInChildren<Button>(true) : CollectNamedButtons(rightPage, "TabButton");
            return buttons.Length >= 3 ? new[] { buttons[0], buttons[1], buttons[2] } : System.Array.Empty<Button>();
        }

        private static Animator FindBookAnimator(Transform book)
        {
            Animator directAnimator = book != null ? book.GetComponent<Animator>() : null;
            if (LooksLikeBookAnimator(directAnimator))
                return directAnimator;

            Transform namedOwner = Find(book, "EarthTome") ?? Find(book, "Tome") ?? Find(book, "BookAnimator") ??
                Find(book, "TomeAnimator") ?? Find(book, "BookFrame") ?? Find(book, "Book");
            if (namedOwner != null)
            {
                Animator namedAnimator = namedOwner.GetComponent<Animator>() ?? namedOwner.GetComponentInChildren<Animator>(true);
                if (LooksLikeBookAnimator(namedAnimator))
                    return namedAnimator;
            }

            Animator[] animators = book != null ? book.GetComponentsInChildren<Animator>(true) : Array.Empty<Animator>();
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                if (LooksLikeBookAnimator(animator))
                    return animator;
            }

            Debug.LogWarning("[EncyclopediaWire] Book animator was not assigned because no named Book/Tome animator with expected states or clips was found.");
            return null;
        }

        private static bool LooksLikeBookAnimator(Animator animator)
        {
            if (animator == null)
                return false;

            return HasState(animator, "BookOpen") ||
                HasState(animator, "Open") ||
                HasState(animator, "BookClose") ||
                HasState(animator, "Close") ||
                HasState(animator, "BookLeftPage") ||
                HasState(animator, "BookRightPage") ||
                HasClip(animator, "BookOpen", "Open", "BookClose", "Close", "BookLeftPage", "BookRightPage");
        }

        private static bool HasState(Animator animator, string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
                return false;

            int shortHash = Animator.StringToHash(stateName);
            if (animator.HasState(0, shortHash))
                return true;

            return animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
        }

        private static bool HasClip(Animator animator, params string[] clipNames)
        {
            if (animator == null || animator.runtimeAnimatorController == null || clipNames == null)
                return false;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                for (int j = 0; j < clipNames.Length; j++)
                {
                    if (string.Equals(clip.name, clipNames[j], System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static void ConfigureAbilityContainerLayout(Transform abilityContainer)
        {
            if (abilityContainer == null)
                return;

            VerticalLayoutGroup verticalLayout = abilityContainer.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
                return;

            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            EditorUtility.SetDirty(verticalLayout);
        }

        private static void ConfigureDetailScrollHost(Transform detailHost, Transform contentRoot, Transform abilityContainer)
        {
            if (detailHost == null)
                return;

            ScrollRect scrollRect = detailHost.GetComponent<ScrollRect>() ?? detailHost.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect == null)
                return;

            Transform contentPanel = Find(detailHost, "ContentPanel") ?? contentRoot;
            if (contentPanel is RectTransform contentRect && contentRect != scrollRect.transform)
                scrollRect.content = contentRect;

            if (scrollRect.viewport == null && detailHost is RectTransform detailRect)
                scrollRect.viewport = detailRect;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            EditorUtility.SetDirty(scrollRect);

            if (scrollRect.viewport != null)
            {
                RectMask2D rectMask = scrollRect.viewport.GetComponent<RectMask2D>();
                if (rectMask == null)
                {
                    rectMask = scrollRect.viewport.gameObject.AddComponent<RectMask2D>();
                    EditorUtility.SetDirty(rectMask);
                }

                Graphic viewportGraphic = scrollRect.viewport.GetComponent<Graphic>();
                if (viewportGraphic != null)
                {
                    viewportGraphic.raycastTarget = true;
                    EditorUtility.SetDirty(viewportGraphic);
                }
            }

            ConfigureVerticalLayout(contentPanel);
            if (contentRoot != null && contentRoot != contentPanel)
                ConfigureVerticalLayout(contentRoot);
            ConfigureVerticalLayout(abilityContainer);
        }

        private static void ConfigureVerticalLayout(Transform root)
        {
            if (root == null)
                return;

            VerticalLayoutGroup verticalLayout = root.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
                return;

            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            EditorUtility.SetDirty(verticalLayout);
        }

        private static Button[] CollectNamedButtons(Transform root, string namePrefix)
        {
            if (root == null)
                return System.Array.Empty<Button>();

            var buttons = new List<Button>();
            Button[] allButtons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < allButtons.Length; i++)
            {
                Button button = allButtons[i];
                if (button != null && button.name.StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase))
                    buttons.Add(button);
            }

            buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return buttons.ToArray();
        }

        private static GameObject FindMarker(Transform owner)
        {
            Transform marker = Find(owner, "SelectedMarker") ?? Find(owner, "SelectMarker") ?? Find(owner, "Selected") ??
                Find(owner, "Selection") ?? Find(owner, "Highlight") ?? Find(owner, "Highlighter");
            return marker != null ? marker.gameObject : null;
        }
    }

    private static void Require(ContractReport report, bool condition, string message)
    {
        if (!condition)
            report.Error(message);
    }

    private static void RequireRef(ContractReport report, SerializedObject so, string propertyName, string label)
    {
        if (GetRef(so, propertyName) == null)
            report.Error($"{label} is unassigned.");
    }

    private static void RequireNamedRef(ContractReport report, SerializedObject so, string propertyName, string expectedName, string label)
    {
        Object value = GetRef(so, propertyName);
        if (value == null)
        {
            report.Error($"{label} is unassigned.");
            return;
        }

        string actualName = value is Component component ? component.name : value.name;
        if (!string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase))
            report.Error($"{label} should reference {expectedName}, found {actualName}.");
    }

    private static void ValidateGuideLayout(ContractReport report, Transform root, string guideName, string label)
    {
        Transform guide = Find(root, guideName);
        if (guide == null)
        {
            report.Error($"{label} object was not found.");
            return;
        }

        LayoutElement layoutElement = guide.GetComponent<LayoutElement>();
        bool layoutHasSize = layoutElement != null &&
            Mathf.Max(layoutElement.minWidth, layoutElement.preferredWidth) > 0f &&
            Mathf.Max(layoutElement.minHeight, layoutElement.preferredHeight) > 0f;
        bool rectHasSize = guide is RectTransform rect && rect.sizeDelta.x > 0f && rect.sizeDelta.y > 0f;
        if (!layoutHasSize && !rectHasSize)
            report.Error($"{label} must have authored LayoutElement or RectTransform size.");
    }

    private static void ValidateDetailScrollRect(ContractReport report, ScrollRect scrollRect)
    {
        if (scrollRect == null)
            return;

        if (!scrollRect.vertical)
            report.Error("RightPage detail ScrollRect must allow vertical scrolling.");
        if (scrollRect.horizontal)
            report.Error("RightPage detail ScrollRect should not allow horizontal scrolling.");
        if (scrollRect.content == null)
        {
            report.Error("RightPage detail ScrollRect content is unassigned.");
        }
        else
        {
            ValidateVerticalLayout(report, scrollRect.content, "RightPage detail ScrollRect content");
        }

        if (scrollRect.viewport == null)
        {
            report.Error("RightPage detail ScrollRect viewport is unassigned.");
            return;
        }

        if (scrollRect.viewport.GetComponent<RectMask2D>() == null)
            report.Error("RightPage detail ScrollRect viewport must have RectMask2D so overflowing ability blocks are clipped.");

        Graphic viewportGraphic = scrollRect.viewport.GetComponent<Graphic>();
        if (viewportGraphic == null || !viewportGraphic.raycastTarget)
            report.Error("RightPage detail ScrollRect viewport must have a raycastTarget Graphic so blank panel areas receive scroll input.");
    }

    private static void ValidateVerticalLayout(ContractReport report, RectTransform rectTransform, string label)
    {
        if (rectTransform == null)
            return;

        VerticalLayoutGroup verticalLayout = rectTransform.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
            return;

        if (!verticalLayout.childControlHeight || verticalLayout.childForceExpandHeight)
            report.Error($"{label} VerticalLayoutGroup must control child height without forcing height expansion.");
    }

    private static void ValidateAnimatorStateMotion(
        ContractReport report,
        Animator animator,
        string stateName,
        AnimationClip expectedClip,
        string label)
    {
        if (animator == null)
            return;

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            report.Error($"{label} cannot be validated because the book animator does not use an AnimatorController asset.");
            return;
        }

        AnimatorState state = FindAnimatorState(controller, stateName);
        if (state == null)
        {
            report.Error($"{label} state '{stateName}' was not found.");
            return;
        }

        if (expectedClip == null)
            return;

        if (state.motion != expectedClip)
            report.Error($"{label} should use clip {expectedClip.name}, found {(state.motion != null ? state.motion.name : "null")}.");
    }

    private static void ValidateAnimatorDefaultState(
        ContractReport report,
        Animator animator,
        string expectedStateName,
        string label)
    {
        if (animator == null)
            return;

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null || controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
            return;

        AnimatorState defaultState = controller.layers[0].stateMachine.defaultState;
        if (defaultState == null)
        {
            report.Error($"{label} is unassigned.");
            return;
        }

        if (!string.Equals(defaultState.name, expectedStateName, StringComparison.OrdinalIgnoreCase))
            report.Error($"{label} should be {expectedStateName}, found {defaultState.name}.");
    }

    private static AnimatorState FindAnimatorState(AnimatorController controller, string stateName)
    {
        if (controller == null || string.IsNullOrWhiteSpace(stateName))
            return null;

        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            AnimatorState state = FindAnimatorState(layers[i].stateMachine, stateName);
            if (state != null)
                return state;
        }

        return null;
    }

    private static AnimatorState FindAnimatorState(AnimatorStateMachine stateMachine, string stateName)
    {
        if (stateMachine == null)
            return null;

        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state != null && string.Equals(state.name, stateName, StringComparison.OrdinalIgnoreCase))
                return state;
        }

        ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
        for (int i = 0; i < childMachines.Length; i++)
        {
            AnimatorState state = FindAnimatorState(childMachines[i].stateMachine, stateName);
            if (state != null)
                return state;
        }

        return null;
    }

    private static void RequireNullRef(ContractReport report, SerializedObject so, string propertyName, string message)
    {
        if (GetRef(so, propertyName) != null)
            report.Error(message);
    }

    private static void WarnIfMissingRef(ContractReport report, SerializedObject so, string propertyName, string message)
    {
        if (GetRef(so, propertyName) == null)
            report.Warning(message);
    }

    private static void WarnIfAssignedRef(ContractReport report, SerializedObject so, string propertyName, string message)
    {
        if (GetRef(so, propertyName) != null)
            report.Warning(message);
    }

    private static void WarnIfSameRef(ContractReport report, SerializedObject so, string firstPropertyName, string secondPropertyName, string message)
    {
        Object first = GetRef(so, firstPropertyName);
        Object second = GetRef(so, secondPropertyName);
        if (first != null && first == second)
            report.Warning(message);
    }

    private static void RequireBool(ContractReport report, SerializedObject so, string propertyName, bool expectedValue, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Boolean)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.boolValue != expectedValue)
            report.Error($"{label} should be {expectedValue}.");
    }

    private static void RequireInt(ContractReport report, SerializedObject so, string propertyName, int expectedValue, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Integer)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.intValue != expectedValue)
            report.Error($"{label} should be {expectedValue}, found {property.intValue}.");
    }

    private static void RequireArrayRef(ContractReport report, SerializedObject so, string propertyName, int index, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray || property.arraySize <= index)
        {
            report.Error($"{label} is missing from {propertyName}.");
            return;
        }

        if (property.GetArrayElementAtIndex(index).objectReferenceValue == null)
            report.Error($"{label} is unassigned.");
    }

    private static Object GetRef(SerializedObject so, string propertyName)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static string GetString(SerializedObject so, string propertyName, string fallback)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.String)
            return fallback;

        return string.IsNullOrWhiteSpace(property.stringValue) ? fallback : property.stringValue;
    }

    private static GameObject FindObject(Transform root, params string[] names)
    {
        Transform found = FindAny(root, names);
        return found != null ? found.gameObject : null;
    }

    private static Transform FindAny(Transform root, params string[] names)
    {
        if (names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            Transform found = Find(root, names[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private sealed class ContractReport
    {
        private readonly string title;
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public ContractReport(string title)
        {
            this.title = title;
        }

        public void Error(string message)
        {
            errors.Add(message);
        }

        public void Warning(string message)
        {
            warnings.Add(message);
        }

        public void Log()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[EncyclopediaWire] {title}");
            builder.AppendLine($"Errors: {errors.Count}");
            for (int i = 0; i < errors.Count; i++)
                builder.AppendLine($"- ERROR: {errors[i]}");

            builder.AppendLine($"Warnings: {warnings.Count}");
            for (int i = 0; i < warnings.Count; i++)
                builder.AppendLine($"- WARN: {warnings[i]}");

            if (errors.Count > 0)
                Debug.LogError(builder.ToString());
            else if (warnings.Count > 0)
                Debug.LogWarning(builder.ToString());
            else
                Debug.Log(builder.ToString());
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return children[i];
        }

        return null;
    }

    private static T FindComponentUnder<T>(Transform root, params string[] names) where T : Component
    {
        if (root == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            Transform child = Find(root, names[i]);
            if (child == null)
                continue;

            T component = child.GetComponent<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    private static Image FindTabIcon(Transform owner)
    {
        return FindComponentUnder<Image>(owner, "TabIcon", "Icon", "ButtonIcon", "CategoryIcon");
    }

    private static Image FindGuideIcon(Transform guide)
    {
        if (guide == null)
            return null;

        Image childIcon = FindComponentUnder<Image>(guide, "Icon", "GuideIcon");
        return childIcon != null ? childIcon : guide.GetComponent<Image>();
    }

    private static T FindComponentUnderParent<T>(Transform root, string parentName, params string[] childNames) where T : Component
    {
        Transform parent = Find(root, parentName);
        if (parent == null)
            return null;

        T component = FindComponentUnder<T>(parent, childNames);
        if (component != null)
            return component;

        return parent.GetComponentInChildren<T>(true);
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetObjectArray(SerializedObject so, string propertyName, params Object[] values)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = values != null ? values.Length : 0;
        for (int i = 0; values != null && i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif
