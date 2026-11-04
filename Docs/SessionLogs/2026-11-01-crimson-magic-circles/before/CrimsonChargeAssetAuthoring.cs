#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CapstonePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityGAS;

// Explicit one-time authoring command. Never runs on import or at runtime.
public static class CrimsonChargeAssetAuthoring
{
    public const string Art = "Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/ChargeSixColor";
    public const string Vfx = "Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor";
    public const string PrefabPath = Vfx + "/CrimsonChargePresentation.prefab";
    public const string DataPath = "Assets/_Project/Data/Items/Weapons/LogicData/ALData_CrimsonBoundaryCharge.asset";
    public const string AbilityPath = "Assets/_Project/Data/Abilities/Definitions/AD_CrimsonBoundaryIgnite.asset";
    private const string LogicPath = "Assets/_Project/Data/Items/Weapons/Strategies/AL_CrimsonBoundaryCharge.asset";

    [MenuItem("Tools/Weapons/Crimson Boundary/Author Six Color Charge")]
    public static void Author()
    {
        Directory.CreateDirectory(Vfx);
        Directory.CreateDirectory(Path.GetDirectoryName(LogicPath));
        AssetDatabase.Refresh();
        ImportArt();
        GameObject root = new("CrimsonChargePresentation");
        try
        {
            var view = root.AddComponent<CrimsonBoundaryChargePresentation>();
            var serialized = new SerializedObject(view);
            Transform warning = Child(root.transform, "ChargeWarning");
            MeshFilter disc = Mesh(warning, "Disc", Material("WarningDisc", null, new Color(0, 0, 0, 0.5f)), 0);
            MeshFilter dashes = Mesh(warning, "Dashes", Material("WarningDashes", null, new Color(0.9f, 0.15f, 0.25f)), 1);
            BuildGauge(root.transform, serialized);

            Transform meteor = Child(root.transform, "MeteorFlight");
            Transform sprites = Child(meteor, "Sprites");
            SpriteRenderer flame = Sprite(sprites, "FlameFlipbook", Frame("meteor_flame", 1), "Projectile", 1);
            Sprite(sprites, "Rock", Frame("meteor_core", 1), "Projectile", 2);
            var trailSmoke = Particles(meteor, "TrailSmoke", "smoke_puff_A", 4, 1, 0, 0.48f, 1.65f, 0f, 0.1f, true, 14, "Projectile", 0);
            Drift(trailSmoke, 0f, 0.65f);
            Fade(trailSmoke, 0.6f);
            var wisps = Particles(meteor, "DetachedFlames", "flame_wisp", 4, 1, 0, 0.3f, 1.6f, 0f, 0.12f, true, 10, "Projectile", 0);
            Drift(wisps, 0.2f, 0.3f);
            // The sprite points northeast. Rotate it to the rightward tail of the leftward meteor.
            var wispMain = wisps.main;
            wispMain.startRotation = 45f * Mathf.Deg2Rad;

            Transform impact = Child(root.transform, "Impact");
            SpriteRenderer scorch = Sprite(impact, "Scorch", Frame("scorch", 1), "GroundAOE", 1);
            scorch.color = new Color(1, 1, 1, 0);
            SpriteRenderer flash = Sprite(impact, "ImpactFlash", Frame("impact_core", 1), "FloatingAOE", 8);
            flash.transform.localScale = Vector3.one * 1.15f;
            var fireA = Particles(impact, "FireLobesA", "fire_puff_A", 4, 1, 4, 0.4f, 2.8f, 2.8f, 0.65f, false, 0, "FloatingAOE", 5);
            var fireB = Particles(impact, "FireLobesB", "fire_puff_B", 4, 1, 3, 0.43f, 2.65f, 2.5f, 0.65f, false, 0, "FloatingAOE", 5);
            var fireShapeA = fireA.shape; fireShapeA.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            var fireShapeB = fireB.shape; fireShapeB.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            fireB.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var smokeA = Particles(impact, "ResidualSmokeA", "smoke_puff_A", 4, 1, 2, 0.7f, 2.45f, 1.1f, 0.8f, false, 0, "FloatingAOE", 4);
            var smokeB = Particles(impact, "ResidualSmokeB", "smoke_puff_B", 4, 1, 2, 0.65f, 2.35f, 1.3f, 0.9f, false, 0, "FloatingAOE", 4);
            Delay(smokeA, 0.16f); Delay(smokeB, 0.22f);
            Drift(smokeA, 0f, 0.65f); Drift(smokeB, 0f, 0.65f);
            Fade(smokeA, 0.8f); Fade(smokeB, 0.8f);
            var rays = Particles(impact, "ImpactRays", "ray", 3, 1, 15, 0.13f, 1.6f, 7.8f, 0.55f, false, 0, "FloatingAOE", 6, false);
            var rayMain = rays.main;
            rayMain.startSize3D = true;
            rayMain.startSizeX = 0.4f;
            rayMain.startSizeY = 1.6f;
            rayMain.startSizeZ = 1f;
            var rayShape = rays.shape;
            rayShape.alignToDirection = true;
            var embers = Particles(impact, "Embers", "ember", 3, 1, 8, 0.75f, 0.65f, 5.5f, 0.7f, false, 0, "FloatingAOE", 7, false);
            var emberLimit = embers.limitVelocityOverLifetime;
            emberLimit.enabled = true;
            emberLimit.limit = 2.5f;
            emberLimit.dampen = 0.15f;
            Fade(embers, 1f);
            Transform debrisRoot = Child(impact, "BouncingDebris");
            var fragments = Particles(debrisRoot, "StoneParticles", "debris", 4, 1, 0, 1f, 0.65f, 0f, 0f, false, 0, "FloatingAOE", 3, false);
            var debrisMain = fragments.main;
            debrisMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            // The existing bounce emitter owns positions/lifetimes; no Rigidbody or Collision module.
            var bounce = debrisRoot.gameObject.AddComponent<TopDownDebrisBounceEmitter2D>();
            bounce.ApplyEditorPreset(fragments, null, new TopDownDebrisBounceEmitter2D.AuthoringPreset
            {
                MinFragments = 10, MaxFragments = 10, MaxSimulationSeconds = 1.3f,
                GroundSpeedRange = new Vector2(2.8f, 4.8f), GroundSpreadScale = Vector2.one,
                GroundFriction = 0.58f, VerticalSpeedRange = new Vector2(2.75f, 4.1f), Gravity = 12f,
                HeightScreenOffset = 1f, HeightSizeBoost = 0.08f, MaxBounces = 2, BounceDamping = 0.43f,
                MinBounceVelocity = 0.6f, FragmentSizeRange = new Vector2(0.5f, 0.7f),
                FragmentSpinDegreesRange = new Vector2(-360, 360), FragmentColorA = Color.white,
                FragmentColorB = Color.white, HotFragmentChance = 0, ContactColor = Color.white
            });
            Set(serialized, "warning", warning); Set(serialized, "disc", disc); Set(serialized, "dashes", dashes);
            Set(serialized, "meteor", meteor); Set(serialized, "meteorSprites", sprites.gameObject); Set(serialized, "flame", flame);
            Array(serialized, "flameFrames", Enumerable.Range(1, 4).Select(i => Frame("meteor_flame", i)).ToArray());
            Array(serialized, "trailParticles", new[] { trailSmoke, wisps });
            Set(serialized, "impact", impact); Set(serialized, "flash", flash); Set(serialized, "scorch", scorch); Set(serialized, "debris", bounce);
            Array(serialized, "flashFrames", Enumerable.Range(1, 3).Select(i => Frame("impact_core", i)).ToArray());
            Array(serialized, "impactParticles", new[] { fireA, fireB, smokeA, smokeB, rays, embers });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            meteor.gameObject.SetActive(false); impact.gameObject.SetActive(false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            var charge = LoadOrCreate<CrimsonBoundaryChargeData>(DataPath);
            charge.weaponData = AssetDatabase.LoadAssetAtPath<CrimsonBoundaryWeaponData>("Assets/_Project/Data/Items/Weapons/LogicData/ALData_CrimsonBoundary.asset");
            charge.presentationPrefab = prefab;
            EditorUtility.SetDirty(charge);
            var logic = LoadOrCreate<AbilityLogic_CrimsonBoundaryCharge>(LogicPath);
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AbilityPath);
            ability.logic = logic;
            ability.sourceObject = charge;
            ability.cooldown = 8f;
            ability.startCooldownOnEnd = true;
            UpdateDescriptions(ability);
            EditorUtility.SetDirty(ability);
            AssetDatabase.SaveAssets();
            Debug.Log("CRIMSON_CHARGE_AUTHOR_OK: prefab, six-color imports, charge data and Skill1 binding saved; Q untouched.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
    }

    private static void ImportArt()
    {
        foreach (string path in Directory.GetFiles(Art, "*.png", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalized);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Vector2 desiredPivot = normalized.Contains("meteor_flame") ? new Vector2(0.25f, 0.25f) : Vector2.one * 0.5f;
            if (importer.spritePixelsPerUnit == 16f && importer.filterMode == FilterMode.Point &&
                !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed &&
                settings.spritePivot == desiredPivot && settings.spriteMeshType == SpriteMeshType.FullRect &&
                importer.npotScale == TextureImporterNPOTScale.None) continue;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = desiredPivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }

    private static ParticleSystem Particles(Transform parent, string name, string sheet, int columns, int rows,
        int burstCount, float life, float size, float speed, float radius, bool loop, float rate,
        string layer, int order, bool animate = true)
    {
        var p = Child(parent, name).gameObject.AddComponent<ParticleSystem>();
        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = p.main;
        main.duration = 1f; main.loop = loop; main.playOnAwake = false; main.maxParticles = 32;
        main.startLifetime = life;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.88f, size * 1.08f);
        main.startSpeed = speed; main.startColor = Color.white; main.gravityModifier = 0;
        main.simulationSpace = loop ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = p.emission;
        emission.enabled = rate > 0 || burstCount > 0;
        emission.rateOverTime = rate;
        if (burstCount > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });
        var shape = p.shape;
        shape.enabled = radius > 0;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.5f;
        var texture = p.textureSheetAnimation;
        texture.enabled = true;
        texture.mode = ParticleSystemAnimationMode.Grid;
        texture.numTilesX = columns; texture.numTilesY = rows;
        texture.cycleCount = 1;
        texture.frameOverTime = animate
            ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 0.999f))
            : new ParticleSystem.MinMaxCurve(0f, 0.999f);
        var renderer = p.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Material(sheet, AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Sheets/" + sheet + ".png"), Color.white);
        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.maxParticleSize = 1f;
        return p;
    }

    private static void Drift(ParticleSystem p, float x, float y)
    {
        var velocity = p.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = x; velocity.y = y; velocity.z = 0;
    }
    private static void Delay(ParticleSystem p, float seconds) { var main = p.main; main.startDelay = seconds; }
    private static void Fade(ParticleSystem p, float alpha)
    {
        var color = p.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(alpha, 0), new GradientAlphaKey(alpha, 0.65f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
    }
    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
    }
    private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, string layer, int order)
    {
        var renderer = Child(parent, name).gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sortingLayerName = layer; renderer.sortingOrder = order; return renderer;
    }
    private static MeshFilter Mesh(Transform parent, string name, Material material, int order)
    {
        Transform child = Child(parent, name);
        var mesh = child.gameObject.AddComponent<MeshFilter>();
        var renderer = child.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingLayerName = "AttackTelegraph"; renderer.sortingOrder = order;
        return mesh;
    }
    [MenuItem("Tools/Weapons/Crimson Boundary/Restore Reference Gauge")]
    public static void RestoreReferenceGauge()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var serialized = new SerializedObject(root.GetComponent<CrimsonBoundaryChargePresentation>());
            BuildGauge(root.transform, serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("CRIMSON_GAUGE_RESTORED: authored 32x10 Aseprite v2 geometry/palette using uGUI Filled Images only.");
    }

    private static void BuildGauge(Transform parent, SerializedObject serialized)
    {
        // Exact top-left pixel rectangles from the approved charge-assets-v2 Aseprite source.
        // All parts are authored UI Images sharing one white pixel; runtime only projects charge state.
        Transform existing = parent.Find("HeadChargeGauge");
        var go = existing != null ? existing.gameObject : new GameObject("HeadChargeGauge", typeof(RectTransform), typeof(Canvas));
        if (existing == null) go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Preserve dark reference colors in linear rendering instead of quantizing vertex colors early.
        canvas.vertexColorAlwaysGammaSpace = true;
        canvas.sortingLayerName = "UI"; canvas.sortingOrder = 5;
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(32, 10);
        rect.localScale = Vector3.one / 16f;
        Sprite pixel = GaugePixel();
        Transform oldBorder = rect.Find("BlackBorder");
        if (oldBorder != null) oldBorder.name = "OuterVertical";
        GaugeRect(rect, "OuterVertical", 2, 1, 28, 8, "0C0A12", pixel);
        GaugeRect(rect, "OuterHorizontal", 1, 2, 30, 6, "0C0A12", pixel);
        GaugeRect(rect, "RimHighlight", 3, 2, 26, 1, "AA919E", pixel);
        GaugeRect(rect, "RimLeft", 2, 3, 1, 4, "695664", pixel);
        GaugeRect(rect, "RimRight", 29, 3, 1, 4, "695664", pixel);
        GaugeRect(rect, "RimBottom", 3, 7, 26, 1, "695664", pixel);
        GaugeRect(rect, "Trough", 3, 3, 26, 4, "241723", pixel);
        GaugeRect(rect, "TroughInset", 3, 3, 26, 1, "170F1B", pixel);
        var fill = GaugeRect(rect, "Fill", 3, 4, 26, 2, "DD2E46", pixel);
        var highlight = GaugeRect(rect, "FillHighlight", 3, 3, 26, 1, "FF665B", pixel);
        var shadow = GaugeRect(rect, "FillShadow", 3, 6, 26, 1, "801730", pixel);
        foreach (var image in new[] { fill, highlight, shadow })
        {
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            image.fillOrigin = 0; image.fillAmount = 0;
        }
        Transform existingFullFrame = rect.Find("FullFrame");
        var fullFrame = existingFullFrame != null ? existingFullFrame.gameObject : new GameObject("FullFrame", typeof(RectTransform));
        if (existingFullFrame == null) fullFrame.transform.SetParent(rect, false);
        fullFrame.transform.SetAsLastSibling();
        ((RectTransform)fullFrame.transform).sizeDelta = new Vector2(32, 10);
        GaugeRect(fullFrame.transform, "Top", 3, 2, 26, 1, "FFCB6F", pixel);
        GaugeRect(fullFrame.transform, "Left", 2, 3, 1, 4, "FFCB6F", pixel);
        GaugeRect(fullFrame.transform, "Right", 29, 3, 1, 4, "FFCB6F", pixel);
        GaugeRect(fullFrame.transform, "Bottom", 3, 7, 26, 1, "D47439", pixel);
        fullFrame.SetActive(false);
        Set(serialized, "gauge", rect); Set(serialized, "gaugeFill", fill);
        Set(serialized, "gaugeHighlight", highlight); Set(serialized, "gaugeShadow", shadow);
        Set(serialized, "gaugeFullFrame", fullFrame);
        serialized.FindProperty("gaugeOffset").vector3Value = new Vector3(0, 1.3f, 0);
        string[] fields = { "chargingColor", "readyColor", "fullColor", "chargingHighlight", "readyHighlight", "fullHighlight",
            "chargingShadow", "readyShadow", "fullShadow" };
        string[] colors = { "DD2E46", "F07826", "FF9D41", "FF665B", "FFAB56", "FFEBAB", "801730", "9E3C21", "CF4629" };
        for (int i = 0; i < fields.Length; i++) serialized.FindProperty(fields[i]).colorValue = Hex(colors[i]);
    }

    private static UnityEngine.UI.Image GaugeRect(Transform parent, string name, int x, int y, int width, int height, string rgb, Sprite pixel)
    {
        Transform existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        if (existing == null) go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, -y);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = pixel; image.color = Hex(rgb); image.raycastTarget = false; return image;
    }
    private static Sprite GaugePixel()
    {
        string path = Vfx + "/GaugePixel.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Sprite Frame(string id, int number) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/Frames/{id}_{number:00}.png");
    private static Material Material(string name, Texture2D texture, Color color)
    {
        string path = Vfx + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, path); }
        material.mainTexture = texture; material.color = color; EditorUtility.SetDirty(material); return material;
    }
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value == null) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); }
        return value;
    }
    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    private static void Set(SerializedObject obj, string name, UnityEngine.Object value) => obj.FindProperty(name).objectReferenceValue = value;
    private static void Array<T>(SerializedObject obj, string name, T[] values) where T : UnityEngine.Object
    {
        var property = obj.FindProperty(name); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    public static void RefreshDescriptions()
    {
        var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AbilityPath);
        UpdateDescriptions(ability);
        EditorUtility.SetDirty(ability);
        AssetDatabase.SaveAssets();
    }

    private static void UpdateDescriptions(AbilityDefinition ability)
    {
        string[] locales = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        string[] shortText = {
            "길게 눌러 운석을 조준하고 놓으면 발사합니다. 차징할수록 범위가 넓어지며, 착탄 시 피해와 [[화상]]을 줍니다.",
            "Hold to aim and charge a meteor, then release to launch. Charging expands the impact area. The impact deals damage and applies [[Burn]].",
            "長押しで隕石をチャージして狙い、離すと発射します。チャージで着弾範囲が広がり、着弾時にダメージと[[火傷]]を与えます。",
            "长按蓄力并瞄准陨石，松开发射。蓄力扩大冲击范围，命中时造成伤害并施加[[灼烧]]。",
            "長按蓄力並瞄準隕石，鬆開發射。蓄力擴大衝擊範圍，命中時造成傷害並施加[[灼燒]]。"
        };
        string[] details = {
            "● {val:0.7초} 이상 차징 후 해제하면 발사 ({val:1.4초}에 최대 차징)\n● 폭발 지름 {val:3~6칸}, 비행 {val:0.5초}\n● 화염 최종 능력치의 {val:3배} 피해와 [[화상]] {val:10중첩}\n● 최대 차징: {val:4배} 피해와 [[화상]] {val:12중첩}\n● 발사 시 쿨다운 {val:8초}, 최소 차징 전 취소는 쿨다운 없음",
            "● Release after {val:0.7s}; full charge at {val:1.4s}\n● Impact diameter {val:3–6 tiles}; flight {val:0.5s}\n● {val:3×} final Fire damage and {val:10} [[Burn]] stacks\n● Full charge: {val:4×} damage and {val:12} [[Burn]] stacks\n● Cooldown {val:8s} starts on launch; early cancellation costs no cooldown",
            "● {val:0.7秒}以上で発射、{val:1.4秒}で最大チャージ\n● 爆発直径 {val:3～6マス}、飛行 {val:0.5秒}\n● 最終火炎能力値の{val:3倍}ダメージ、[[火傷]]{val:10スタック}\n● 最大時：{val:4倍}ダメージ、[[火傷]]{val:12スタック}\n● 発射時からクールダウン{val:8秒}。早期キャンセルは消費なし",
            "● 蓄力{val:0.7秒}后可发射，{val:1.4秒}满蓄力\n● 爆炸直径{val:3～6格}，飞行{val:0.5秒}\n● 最终火焰属性{val:3倍}伤害与{val:10层}[[灼烧]]\n● 满蓄力：{val:4倍}伤害与{val:12层}[[灼烧]]\n● 发射时开始{val:8秒}冷却，提前取消不消耗冷却",
            "● 蓄力{val:0.7秒}後可發射，{val:1.4秒}滿蓄力\n● 爆炸直徑{val:3～6格}，飛行{val:0.5秒}\n● 最終火焰屬性{val:3倍}傷害與{val:10層}[[灼燒]]\n● 滿蓄力：{val:4倍}傷害與{val:12層}[[灼燒]]\n● 發射時開始{val:8秒}冷卻，提前取消不消耗冷卻"
        };
        ability.simpleDescription = shortText[0]; ability.description = details[0];
        string key = "asset." + AssetDatabase.AssetPathToGUID(AbilityPath);
        for (int i = 0; i < locales.Length; i++)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/_Project/Data/Localization/GameText_{locales[i]}.asset");
            // Glossary identifiers are canonical Korean tokens in every locale.
            string Canonical(string text) => text.Replace("[[Burn]]", "[[화상]]").Replace("[[火傷]]", "[[화상]]")
                .Replace("[[灼烧]]", "[[화상]]").Replace("[[灼燒]]", "[[화상]]");
            table.GetEntry(key + ".simpleDescription").Value = Canonical(shortText[i]);
            table.GetEntry(key + ".description").Value = Canonical(details[i]);
            EditorUtility.SetDirty(table);
        }
    }
}
#endif
