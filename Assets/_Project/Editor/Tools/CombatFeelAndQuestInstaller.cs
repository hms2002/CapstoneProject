using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>Authors the requested HUD and upgrade assets using Unity serialization.</summary>
public static class CombatFeelAndQuestInstaller
{
    private const string GlobalUiPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    private const string DataFolder = "Assets/_Project/Data/Progression/Upgrades/Effect";

    [MenuItem("Tools/Gameplay/Install Combat Feel and Quest HUD")]
    public static void Install()
    {
        InstallUi(GlobalUiPath, true);
        InstallUi("Assets/_Project/Prefabs/UI/ChestUI.prefab", false);
        InstallBuff();
        AssetDatabase.SaveAssets();
        Debug.Log("[CombatFeelAndQuestInstaller] Installed authored quest HUD, chest counter and +50% travel upgrade effect.");
    }

    private static void InstallUi(string path, bool quest)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);
            if (font == null) throw new InvalidOperationException("No authored TMP font in " + path);
            foreach (ChestScreen screen in root.GetComponentsInChildren<ChestScreen>(true))
            {
                SerializedObject data = new(screen);
                Transform panel = data.FindProperty("chestPanelRect").objectReferenceValue as Transform;
                if (panel == null) panel = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ChestPanel");
                Transform upper = panel.GetComponentsInChildren<Transform>(true).First(t => t.name == "TopChestFrame");
                TMP_Text label = data.FindProperty("acquisitionCountLabel").objectReferenceValue as TMP_Text;
                if (label == null) label = upper.Find("AcquisitionCount")?.GetComponent<TMP_Text>();
                if (label == null)
                {
                    label = Text(upper, "AcquisitionCount", "획득 가능 0 / 2", font, 26f, new Vector2(480f, 40f));
                    RectTransform rect = label.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -16f);
                    label.alignment = TextAlignmentOptions.Center;
                }
                label.rectTransform.SetParent(upper, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                CanvasGroup counterGroup = label.GetComponent<CanvasGroup>();
                if (counterGroup == null) counterGroup = label.gameObject.AddComponent<CanvasGroup>();
                counterGroup.alpha = 0f;
                counterGroup.interactable = counterGroup.blocksRaycasts = false;
                data.FindProperty("acquisitionCountGroup").objectReferenceValue = counterGroup;
                label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset");
                label.fontSharedMaterial = label.font.material;
                LayoutElement layout = label.GetComponent<LayoutElement>();
                if (layout == null) layout = label.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                data.FindProperty("acquisitionCountLabel").objectReferenceValue = label;
                TMP_Text hint = screen.transform.Find("ChestCloseHint")?.GetComponent<TMP_Text>();
                if (hint == null) hint = Text(screen.transform, "ChestCloseHint", "ESC를 눌러 닫기", label.font, 22f, new Vector2(480f, 40f));
                hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(0.5f, 1f);
                hint.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                hint.alignment = TextAlignmentOptions.Center;
                LayoutElement hintLayout = hint.GetComponent<LayoutElement>();
                if (hintLayout == null) hintLayout = hint.gameObject.AddComponent<LayoutElement>();
                hintLayout.ignoreLayout = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (quest && root.GetComponentInChildren<QuestHudView>(true) == null)
            {
                Transform canvas = root.GetComponentsInChildren<Canvas>(true).First(c => c.name == "GameplayHUDCanvas").transform;
                RectTransform hud = Rect(canvas, "QuestHUD", new Vector2(480f, 360f));
                hud.anchoredPosition = new Vector2(36f, -240f);
                TMP_Text heading = Text(hud, "Heading", "퀘스트", font, 26f, new Vector2(480f, 38f));
                RectTransform rows = Rect(hud, "QuestRows", new Vector2(480f, 310f));
                rows.anchoredPosition = new Vector2(0f, -44f);
                RectTransform row = Rect(rows, "QuestRowTemplate", new Vector2(480f, 86f));
                TMP_Text title = Text(row, "Title", "파셀의 소포 배달", font, 23f, new Vector2(480f, 34f));
                TMP_Text description = Text(row, "Description", "다음 층으로 소포를 배달하세요!", font, 21f, new Vector2(480f, 48f));
                description.rectTransform.anchoredPosition = new Vector2(0f, -36f);
                QuestHudRowView rowView = row.gameObject.AddComponent<QuestHudRowView>();
                Set(rowView, "titleLabel", title);
                Set(rowView, "descriptionLabel", description);
                row.gameObject.SetActive(false);
                QuestHudView view = hud.gameObject.AddComponent<QuestHudView>();
                Set(view, "heading", heading);
                Set(view, "rowsRoot", rows);
                Set(view, "rowTemplate", rowView);
                heading.gameObject.SetActive(false);
            }
            if (quest) ConfigureQuestSections(root.GetComponentInChildren<QuestHudView>(true));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureQuestSections(QuestHudView view)
    {
        if (view == null) return;
        SerializedObject data = new(view);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset");
        RectTransform main = data.FindProperty("mainGroup").objectReferenceValue as RectTransform;
        if (main == null) main = Rect(view.transform, "MainQuestGroup", new Vector2(480f, 140f));
        main.anchoredPosition = Vector2.zero;
        TMP_Text heading = data.FindProperty("heading").objectReferenceValue as TMP_Text;
        heading.rectTransform.SetParent(main, false);
        heading.rectTransform.anchoredPosition = Vector2.zero;
        heading.text = "메인 퀘스트";
        heading.gameObject.SetActive(true);
        TMP_Text description = data.FindProperty("mainDescription").objectReferenceValue as TMP_Text;
        if (description == null) description = Text(main, "MainQuestDescription", "마왕을 토벌하기 위해 전진하자.", font, 22f, new Vector2(480f, 96f));
        description.rectTransform.anchoredPosition = new Vector2(0f, -44f);
        TMP_Text sub = data.FindProperty("subHeading").objectReferenceValue as TMP_Text;
        if (sub == null) sub = Text(view.transform, "SubQuestHeading", "서브 퀘스트", font, 26f, new Vector2(480f, 38f));
        sub.rectTransform.anchoredPosition = new Vector2(0f, -152f);
        var rows = data.FindProperty("rowsRoot").objectReferenceValue as RectTransform;
        rows.anchoredPosition = new Vector2(0f, -196f);
        var template = data.FindProperty("rowTemplate").objectReferenceValue as QuestHudRowView;
        template.Rect.sizeDelta = new Vector2(480f, 64f);
        SerializedObject row = new(template);
        var title = row.FindProperty("titleLabel").objectReferenceValue as TMP_Text;
        title.gameObject.SetActive(false);
        var body = row.FindProperty("descriptionLabel").objectReferenceValue as TMP_Text;
        body.rectTransform.anchoredPosition = Vector2.zero;
        body.rectTransform.sizeDelta = new Vector2(480f, 64f);
        foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.color = Color.white;
        }
        heading.color = new Color(1f, 0.55f, 0.15f);
        sub.color = new Color(0.35f, 0.65f, 0.8f);
        data.FindProperty("mainGroup").objectReferenceValue = main;
        data.FindProperty("mainDescription").objectReferenceValue = description;
        data.FindProperty("subHeading").objectReferenceValue = sub;
        data.FindProperty("mainQuestRoutes").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>("Assets/_Project/Data/SceneFlow/Routes/RunRouteCatalog.asset");
        data.ApplyModifiedPropertiesWithoutUndo();
        main.gameObject.SetActive(false);
        sub.gameObject.SetActive(false);
    }

    private static void InstallBuff()
    {
        DurationModifierEffect effect = Asset<DurationModifierEffect>(DataFolder + "/GE_OutOfCombatMoveSpeed.asset");
        effect.effectName = "비전투 이동속도 증가";
        effect.description = "방 전투가 끝난 동안 이동속도가 50% 증가합니다.";
        effect.duration = 86400f;
        effect.attribute = AssetDatabase.LoadAssetAtPath<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/MoveSpeedMulAttribute.asset");
        effect.type = ModifierType.Percent;
        effect.value = 0.5f;
        effect.canStack = false;
        EditorUtility.SetDirty(effect);

        StatusHudDefinition hud = Asset<StatusHudDefinition>(DataFolder + "/SHD_OutOfCombatMoveSpeed.asset");
        StatusHudDefinition iconSource = AssetDatabase.LoadAssetAtPath<StatusHudDefinition>("Assets/_Project/Data/Abilities/Effects/SHD_Relic_MoveSpeedOnKill.asset");
        SerializedObject data = new(hud);
        data.FindProperty("statusId").stringValue = "upgrade_out_of_combat_speed";
        data.FindProperty("group").enumValueIndex = (int)StatusHudGroup.Buff;
        data.FindProperty("nameText").stringValue = "가벼운 발걸음";
        data.FindProperty("effectText").stringValue = "비전투 상태에서 이동속도 +50%";
        data.FindProperty("showStacksByDefault").boolValue = false;
        data.FindProperty("showDurationByDefault").boolValue = false;
        data.FindProperty("icon").objectReferenceValue = iconSource.Icon;
        data.ApplyModifiedPropertiesWithoutUndo();

        CombatBuffDebuffApplicationDefinition buff = Asset<CombatBuffDebuffApplicationDefinition>(DataFolder + "/Buff_OutOfCombatMoveSpeed.asset");
        Set(buff, "gameplayEffect", effect);
        Set(buff, "statusHudDefinition", hud);
        OutOfCombatMoveSpeedUpgradeEffectSO upgrade = Asset<OutOfCombatMoveSpeedUpgradeEffectSO>(DataFolder + "/Effect_OutOfCombatMoveSpeed.asset");
        Set(upgrade, "buff", buff);
        upgrade.rewardText = "비전투 이동속도 +50%";
        upgrade.rewardIcon = iconSource.Icon;
        EditorUtility.SetDirty(upgrade);
    }

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
    {
        SerializedObject data = new(owner);
        data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Vector2 bounds)
    {
        RectTransform rect = Rect(parent, name, bounds);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.color = Color.white;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }
}
