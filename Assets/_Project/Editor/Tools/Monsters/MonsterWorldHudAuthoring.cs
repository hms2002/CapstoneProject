using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Builds and connects the approved authored monster HUD. No scene or combat data edits.</summary>
[InitializeOnLoad]
public static class MonsterWorldHudAuthoring
{
    public const string HudPath = "Assets/_Project/Prefabs/UI/MonsterWorldHud.prefab";
    private const string Marker = "Temp/MonsterWorldHud.runonce";
    private const string Report = "Temp/MonsterWorldHud-report.txt";
    private const string Root = "Assets/_Project/Prefabs/Monsters/";
    private const string MaterialPath = "Assets/_Project/Art/Materials/MonsterStatusNumbers.mat";
    private static readonly string[] Small = {
        "CommonCorridor/GoblinWarrior", "CommonCorridor/GoblinGunner", "SlimeCorridor/Knight",
        "SlimeCorridor/Wizard", "ShadowCorridor/ShadowMonster", "BeerMonster", "SlimeCorridor/Pawn" };
    private static readonly string[] Medium = {
        "CommonCorridor/GoblinTank", "CommonCorridor/LizardWarrior", "CommonCorridor/LizardMage",
        "ShadowCorridor/ShadowServant/ShadowServant", "ShadowCorridor/Dead'sSkeleton",
        "ShadowCorridor/CorridorCandlestickMonster", "ShadowCorridor/StrangeCandlestick/StrangeCandlestick", "TreasureMonster" };
    private static readonly string[] Large = {
        "CommonCorridor/ArcaneMeleeGolem", "CommonCorridor/ArcaneTankGolem", "SlimeCorridor/Bishop", "SlimeCorridor/Rook" };

    private const string BossRoot = "Assets/_Project/Prefabs/Bosses/";
    private static readonly string[] Bosses = {
        "SlimeQueen/SlimeQueen", "SlimeQueen/SlimeQueenP2Long", "SlimeQueen/SlimeQueenP2Short",
        "ShadowBoss/Witch", "DragonBoss/DragonBoss", "DemonKing/DemonKing" };

    static MonsterWorldHudAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Marker) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Marker);
        try { Build(); File.WriteAllText(Report, "PASS: authored HUD and 19 monster, 6 boss and 1 training dummy prefabs validated; damage trail timing passed."); }
        catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Authoring/Build Approved Monster World HUD")]
    public static void Build()
    {
        GameObject hud = CreateHud();
        foreach (string path in Small) Install(path, MonsterSizeCategory.Small, hud);
        foreach (string path in Medium) Install(path, MonsterSizeCategory.Medium, hud);
        foreach (string path in Large) Install(path, MonsterSizeCategory.Large, hud);
        foreach (string path in Bosses) Install(path, MonsterSizeCategory.Medium, hud, true);
        Install("TrainingDummy", MonsterSizeCategory.Small, hud);
        AssetDatabase.SaveAssets();
        Validate();
        ValidateDamageTrail();
        RenderPreview();
        Debug.Log("[MonsterWorldHud] Built and validated 19 monster, 6 boss and 1 training dummy prefabs.");
    }

    private static TMP_FontAsset CreatePixelNumberFont()
    {
        const string path = "Assets/_Project/Art/Font/MonsterStatusPixelNumbers.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;
        // Original 7x9 numeric glyphs. A baked one-pixel black outline remains square at every corner.
        string[] patterns = {
            "0111110/1100011/1100111/1101111/1111011/1110011/1100011/1100011/0111110",
            "0011100/0111100/1101100/0001100/0001100/0001100/0001100/0001100/1111111",
            "0111110/1100011/0000011/0000011/0001110/0011100/0110000/1100000/1111111",
            "1111110/0000011/0000011/0000110/0011110/0000011/0000011/1100011/0111110",
            "0001110/0011110/0110110/1100110/1100110/1111111/0000110/0000110/0000110",
            "1111111/1100000/1100000/1111110/0000011/0000011/0000011/1100011/0111110",
            "0011110/0110000/1100000/1111110/1100011/1100011/1100011/1100011/0111110",
            "1111111/0000011/0000110/0000110/0001100/0001100/0011000/0011000/0011000",
            "0111110/1100011/1100011/1100011/0111110/1100011/1100011/1100011/0111110",
            "0111110/1100011/1100011/1100011/0111111/0000011/0000011/0000110/0111100",
            "00/00/00/00/00/00/00/11/11",
            "00000/00000/01111/11000/11000/01110/00011/00011/11110"
        };
        const string chars = "0123456789.s";
        var atlas = new Texture2D(128, 16, TextureFormat.RGBA32, false) { name = "MonsterStatusPixelAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        atlas.SetPixels(new Color[128 * 16]);
        font = ScriptableObject.CreateInstance<TMP_FontAsset>();
        font.name = "MonsterStatusPixelNumbers";
        var data = new SerializedObject(font); data.FindProperty("m_Version").stringValue = "1.1.0"; data.ApplyModifiedPropertiesWithoutUndo();
        font.faceInfo = new UnityEngine.TextCore.FaceInfo { familyName = "Monster Status Pixel", styleName = "Regular", pointSize = 11, scale = 1f, lineHeight = 12f, ascentLine = 10f, capLine = 9f, meanLine = 7f, baseline = 0f, descentLine = -1f };
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        data.Update(); data.FindProperty("m_AtlasWidth").intValue = 128; data.FindProperty("m_AtlasHeight").intValue = 16; data.FindProperty("m_AtlasPadding").intValue = 0;
        data.FindProperty("m_AtlasRenderMode").intValue = (int)UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER; data.ApplyModifiedPropertiesWithoutUndo();
        font.atlasTextures = new[] { atlas };
        font.glyphTable.Clear();
        font.characterTable.Clear();
        for (int i = 0; i < patterns.Length; i++)
        {
            var rows = patterns[i].Split('/'); int width = rows[0].Length; int x = i * 10;
            for (int y = 0; y < 9; y++) for (int column = 0; column < width; column++)
                if (rows[y][column] == '1')
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) atlas.SetPixel(x + column + 1 + dx, 9 - y + dy, Color.black);
            for (int y = 0; y < 9; y++) for (int column = 0; column < width; column++)
                if (rows[y][column] == '1') atlas.SetPixel(x + column + 1, 9 - y, Color.white);
            var glyph = new UnityEngine.TextCore.Glyph((uint)i + 1, new UnityEngine.TextCore.GlyphMetrics(width + 2, 11, -1, 10, width + 1), new UnityEngine.TextCore.GlyphRect(x, 0, width + 2, 11), 1f, 0);
            font.glyphTable.Add(glyph); font.characterTable.Add(new TMP_Character(chars[i], font, glyph));
        }
        atlas.Apply();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(Shader.Find("UI/Default")); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = Shader.Find("UI/Default"); material.mainTexture = atlas; material.color = Color.white;
        font.material = material;
        AssetDatabase.CreateAsset(font, path); AssetDatabase.AddObjectToAsset(atlas, font);
        font.ReadFontAssetDefinition(); EditorUtility.SetDirty(font); EditorUtility.SetDirty(material);
        return font;
    }

    private static GameObject CreateHud()
    {
        var font = CreatePixelNumberFont();
        var material = font.material;
        var icons = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/UI/Debuff_Icon.png").OfType<Sprite>().ToArray();
        Sprite burn = icons.Single(s => s.name == "Debuff_Icon_Fire");
        Sprite electric = icons.Single(s => s.name == "Debuff_Icon_23");
        var root = Rect("MonsterWorldHud", null, new Vector2(140f, 55f));
        try
        {
            root.localScale = Vector3.one * 0.014f;
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 10;
            var content = Rect("Visual", root, new Vector2(140f, 55f));
            var bar = Rect("HealthBar", content, new Vector2(100f, 10f));
            Image border = bar.gameObject.AddComponent<Image>(); border.color = Color.black; border.raycastTarget = false;
            var frame = Rect("Frame", bar, Vector2.zero); Stretch(frame, 1f);
            var frameImage = frame.gameObject.AddComponent<Image>(); frameImage.color = new Color(0.56f, 0.55f, 0.57f); frameImage.raycastTarget = false;
            var track = Rect("Track", frame, Vector2.zero); Stretch(track, 1f);
            var trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = new Color(0.025f, 0.02f, 0.03f, 1f); trackImage.raycastTarget = false;
            var area = Rect("FillArea", track, Vector2.zero); Stretch(area, 1f);
            var trailRect = Rect("DamageTrail", area, Vector2.zero); Stretch(trailRect, 0f);
            var trail = trailRect.gameObject.AddComponent<Image>(); trail.color = Color.white; trail.raycastTarget = false;
            var fillRect = Rect("Fill", area, Vector2.zero); Stretch(fillRect, 0f);
            var fill = fillRect.gameObject.AddComponent<Image>(); fill.color = new Color(0.8f, 0.025f, 0.08f); fill.raycastTarget = false;
            // Plain rectangular geometry: no rounded default UI sprite or softened sprite edges.
            fill.type = Image.Type.Simple;
            var shine = Rect("Highlight", fillRect, Vector2.zero); Stretch(shine, 0f); shine.anchorMin = new Vector2(0f, 0.65f);
            var shineImage = shine.gameObject.AddComponent<Image>(); shineImage.color = new Color(1f, 0.17f, 0.23f); shineImage.raycastTarget = false;
            var row = Rect("Statuses", content, new Vector2(140f, 18f)); row.pivot = new Vector2(0f, 0.5f); row.anchoredPosition = new Vector2(-50f, -15f);
            var view = root.gameObject.AddComponent<MonsterStackStatusWorldView>();
            var so = new SerializedObject(view);
            Set(so, "visualRoot", content); Set(so, "healthBar", bar); Set(so, "healthFill", fill); Set(so, "damageTrail", trail); Set(so, "statusRow", row);
            Set(so, "healthAttribute", AssetDatabase.LoadAssetAtPath<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/HealthAttribute.asset"));
            Set(so, "maxHealthAttribute", AssetDatabase.LoadAssetAtPath<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/MaxHealthAttribute.asset"));
            var slots = so.FindProperty("slots"); slots.arraySize = 2;
            Slot(slots.GetArrayElementAtIndex(0), row, "Burn", burn, "12", new Color(1f, 0.28f, 0.02f), 44f, 0f, font, material);
            Slot(slots.GetArrayElementAtIndex(1), row, "Electrocuted", electric, "3.2s", new Color(0.85f, 0.67f, 0f), 62f, 49f, font, material);
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root.gameObject, HudPath);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static void Slot(SerializedProperty property, RectTransform row, string id, Sprite sprite, string text, Color color, float width, float x, TMP_FontAsset font, Material material)
    {
        var root = Rect(id, row, new Vector2(width, 18f)); root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f); root.pivot = new Vector2(0f, 0.5f); root.anchoredPosition = new Vector2(x, 0f);
        var icon = Rect("Icon", root, new Vector2(20f, 20f)); icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f); icon.anchoredPosition = new Vector2(10f, 0f);
        var image = icon.gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        var number = Rect("Value", root, new Vector2(width - 22f, 20f)); number.anchorMin = number.anchorMax = new Vector2(0f, 0.5f); number.pivot = new Vector2(0f, 0.5f); number.anchoredPosition = new Vector2(22f, 0f);
        var tmp = number.gameObject.AddComponent<TextMeshProUGUI>(); tmp.font = font; tmp.fontSharedMaterial = material; tmp.fontSize = 14f; tmp.fontStyle = FontStyles.Normal;
        tmp.text = text; tmp.color = color; tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.raycastTarget = false; tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.overflowMode = TextOverflowModes.Overflow;
        property.FindPropertyRelative("statusId").stringValue = id;
        property.FindPropertyRelative("root").objectReferenceValue = root;
        property.FindPropertyRelative("icon").objectReferenceValue = icon;
        property.FindPropertyRelative("valueText").objectReferenceValue = tmp;
        property.FindPropertyRelative("width").floatValue = width;
    }

    private static void Install(string relativePath, MonsterSizeCategory size, GameObject hud, bool boss = false)
    {
        string path = (boss ? BossRoot : Root) + relativePath + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null && root.GetComponent<TrainingDummy2D>() == null) throw new InvalidOperationException(path + " is not an Enemy prefab.");
            var profile = root.GetComponent<MonsterSizeProfile>() ?? root.AddComponent<MonsterSizeProfile>();
            Transform anchor = root.transform.Find("MonsterHudAnchor");
            if (anchor == null)
            {
                anchor = new GameObject("MonsterHudAnchor").transform; anchor.SetParent(root.transform, false);
                var enemyData = enemy != null ? new SerializedObject(enemy) : null;
                var body = enemyData != null ? enemyData.FindProperty("sprite").objectReferenceValue as SpriteRenderer : root.GetComponent<SpriteRenderer>();
                float bottom = body != null && body.sprite != null ? root.transform.InverseTransformPoint(new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z)).y : -0.5f;
                anchor.localPosition = new Vector3(0f, bottom - 0.15f, 0f);
            }
            var profileData = new SerializedObject(profile); profileData.FindProperty("size").enumValueIndex = (int)size;
            profileData.FindProperty("showHealthBar").boolValue = !boss && relativePath != "SlimeCorridor/Pawn" && relativePath != "TrainingDummy";
            Set(profileData, "hudAnchor", anchor); profileData.ApplyModifiedPropertiesWithoutUndo();
            var runtime = root.GetComponent<MonsterStatusRuntime>() ?? root.AddComponent<MonsterStatusRuntime>();
            var runtimeData = new SerializedObject(runtime);
            Set(runtimeData, "electrocutedEffect", AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset"));
            runtimeData.ApplyModifiedPropertiesWithoutUndo();
            // The new HUD replaces the legacy world gauge presentation, not its combat buildup rules.
            foreach (var installer in root.GetComponents<MonsterElementGaugeViewInstaller>()) Object.DestroyImmediate(installer);
            foreach (var oldView in root.GetComponentsInChildren<MonsterElementGaugeView>(true)) Object.DestroyImmediate(oldView.gameObject);
            var existing = root.GetComponentInChildren<MonsterStackStatusWorldView>(true);
            if (existing == null) PrefabUtility.InstantiatePrefab(hud, root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Validate()
    {
        foreach (string relativePath in Bosses)
        {
            string path = BossRoot + relativePath + ".prefab";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var profile = root != null ? root.GetComponent<MonsterSizeProfile>() : null;
            if (profile == null || profile.HudAnchor == null || profile.ShowHealthBar || root.GetComponent<MonsterStatusRuntime>() == null || root.GetComponentsInChildren<MonsterStackStatusWorldView>(true).Length != 1)
                throw new InvalidOperationException("Boss status HUD incomplete: " + path);
        }
        foreach (string relativePath in Small.Concat(Medium).Concat(Large).Concat(new[] { "TrainingDummy" }))
        {
            string path = Root + relativePath + ".prefab";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null || root.GetComponent<MonsterSizeProfile>()?.HudAnchor == null || root.GetComponent<MonsterStatusRuntime>() == null || root.GetComponentsInChildren<MonsterStackStatusWorldView>(true).Length != 1)
                throw new InvalidOperationException("HUD references incomplete: " + path);
            if (root.GetComponent<MonsterElementGaugeViewInstaller>() != null) throw new InvalidOperationException("Duplicate legacy HUD: " + path);
            bool pawn = relativePath == "SlimeCorridor/Pawn" || relativePath == "TrainingDummy";
            if (root.GetComponent<MonsterSizeProfile>().ShowHealthBar == pawn) throw new InvalidOperationException("Pawn visibility mismatch: " + path);
        }
    }

    private static void ValidateDamageTrail()
    {
        var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        try
        {
            var view = hud.GetComponent<MonsterStackStatusWorldView>();
            view.enabled = false;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var apply = typeof(MonsterStackStatusWorldView).GetMethod("ApplyHealthRatio", flags);
            var tick = typeof(MonsterStackStatusWorldView).GetMethod("UpdateHealthTrail", flags);
            var data = new SerializedObject(view);
            var red = (Image)data.FindProperty("healthFill").objectReferenceValue;
            var white = (Image)data.FindProperty("damageTrail").objectReferenceValue;
            void Apply(float ratio, bool damage, float time) => apply.Invoke(view, new object[] { ratio, damage, time });
            void Tick(float time) => tick.Invoke(view, new object[] { time });
            void Check(float actual, float expected)
            {
                if (Mathf.Abs(actual - expected) > 0.001f) throw new InvalidOperationException($"Damage trail expected {expected}, got {actual}.");
            }
            Apply(0.8f, false, 0f);
            Apply(0.45f, true, 1f);
            Check(red.rectTransform.anchorMax.x, 0.45f);
            Tick(1.29f); Check(white.rectTransform.anchorMax.x, 0.8f);
            Tick(1.55f); Check(white.rectTransform.anchorMax.x, 0.625f);
            Apply(0.3f, true, 1.6f);
            Tick(1.89f); Check(white.rectTransform.anchorMax.x, 0.59f);
            Tick(2.4f); Check(white.rectTransform.anchorMax.x, 0.3f);
            Apply(0.7f, false, 2.5f); Check(white.rectTransform.anchorMax.x, 0.7f);
        }
        finally { Object.DestroyImmediate(hud); }
    }

    private static void RenderPreview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var texture = new RenderTexture(1000, 420, 24);
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("HUD preview camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, -0.2f, -10f);
            camera.orthographic = true; camera.orthographicSize = 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.075f, 0.075f, 0.085f);
            camera.targetTexture = texture;
            var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hud, scene);
            hud.GetComponent<MonsterStackStatusWorldView>().enabled = false;
            hud.transform.localScale = Vector3.one * 0.05f;
            hud.transform.position = new Vector3(0f, 0.2f, 0f);
            hud.GetComponent<Canvas>().worldCamera = camera;
            var fill = hud.transform.Find("Visual/HealthBar/Frame/Track/FillArea/Fill") as RectTransform;
            fill.anchorMax = new Vector2(0.7f, 1f);
            foreach (var text in hud.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            image = new Texture2D(1000, 420, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1000f, 420f), 0, 0); image.Apply();
            File.WriteAllBytes("Temp/MonsterWorldHud-style-preview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            texture.Release(); Object.DestroyImmediate(texture);
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect, float inset)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
    private static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
}
