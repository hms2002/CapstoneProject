using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGAS;

/// <summary>그림자 잔향의 프리팹/정적 메시/유물 데이터를 Unity API로 작성한다.</summary>
public static class ShadowEchoAuthoring
{
    private const string PrefabPath = "Assets/_Project/Prefabs/VFX/Relics/PF_ShadowEcho.prefab";
    private const string DefinitionPath = "Assets/_Project/Data/Items/Relics/Definitions/RD_ShadowEcho.asset";
    private const string StrategyPath = "Assets/_Project/Data/Items/Relics/Strategies/RL_ShadowEcho.asset";
    private const string MaterialPath = "Assets/_Project/Art/Materials/VFX/M_ShadowEcho.mat";
    private const string IconPath = "Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ShadowEcho.png";
    private static readonly Color Purple = new(0.66f, 0.40f, 0.95f, 1f);

    [MenuItem("Tools/Content/Relics/Author Shadow Echo")]
    public static void Apply()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        Mesh dashed = MakeRing("ShadowEchoDashedRing", 24, 0.62f, 0.975f);
        Mesh solid = MakeRing("ShadowEchoBurstRing", 64, 1f, 0.94f);
        Mesh disc = MakeDisc();
        GameObject root = new("PF_ShadowEcho");
        GameObject prefab;
        try
        {
            root.SetActive(false);
            ShadowEchoActor actor = root.AddComponent<ShadowEchoActor>();
            actor.echoColor = Purple;
            actor.ghost = Child(root, "FrozenPlayer").AddComponent<SpriteRenderer>();
            actor.ghost.sharedMaterial = material;
            actor.ghost.sortingLayerName = "Entity";
            actor.warningRing = MakeRenderer(root, "Range_Dashed", dashed, material);
            actor.burstRing = MakeRenderer(root, "Burst_Ring", solid, material);
            actor.burstFill = MakeRenderer(root, "Burst_Fill", disc, material);
            actor.wisps = MakeParticles(root, "RisingWisps", material, false);
            actor.burstParticles = MakeParticles(root, "BurstSparks", material, true);
            actor.burstRing.gameObject.SetActive(false);
            actor.burstFill.gameObject.SetActive(false);
            root.SetActive(true);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }

        var lantern = Load<RelicLogic_ChloeLantern>("Assets/_Project/Data/Items/Relics/Strategies/RL_ChloeLantern.asset");
        RelicLogic_ShadowEcho logic = LoadOrCreate<RelicLogic_ShadowEcho>(StrategyPath);
        logic.dashAbility = Load<AbilityDefinition>("Assets/_Project/Data/Abilities/Definitions/AD_Dash.asset");
        logic.echoPrefab = prefab.GetComponent<ShadowEchoActor>();
        logic.damageEffect = lantern.damageEffect;
        logic.targetLayers = lantern.targetLayers;
        logic.delay = 0.7f;
        logic.radius = 1.5f;
        logic.circleYScale = TopDownEllipseHitUtility2D.DefaultTopDownCircleYScale;
        logic.burstDuration = 0.35f;
        EditorUtility.SetDirty(logic);
        RelicDefinition definition = LoadOrCreate<RelicDefinition>(DefinitionPath);
        definition.relicId = "RD_ShadowEcho";
        definition.displayName = "그림자 잔향";
        definition.description = "대시 출발점에 남은 잔향이 잠시 후 폭발합니다.";
        definition.icon = ImportIcon();
        definition.rarity = ItemRarity.Epic;
        definition.maxLevel = 1;
        definition.dropLevel = 1;
        definition.logic = logic;
        EditorUtility.SetDirty(definition);
        ItemDatabase database = Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        database.allRelics ??= new List<RelicDefinition>();
        if (!database.allRelics.Contains(definition)) database.allRelics.Add(definition);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ShadowEcho] Authored RD_ShadowEcho; definition GUID=" + AssetDatabase.AssetPathToGUID(DefinitionPath) +
            "; delay=0.7; diameter=3; Y scale=" + logic.circleYScale);
    }

    [MenuItem("Tools/Content/Relics/Apply Shadow Echo Icon")]
    public static void ApplyIcon()
    {
        RelicDefinition definition = Load<RelicDefinition>(DefinitionPath);
        definition.icon = ImportIcon();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ShadowEcho] Applied supplied icon; GUID=" + AssetDatabase.AssetPathToGUID(IconPath));
    }

    private static Sprite ImportIcon()
    {
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return Load<Sprite>(IconPath);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        GameObject child = new(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static MeshRenderer MakeRenderer(GameObject parent, string name, Mesh mesh, Material material)
    {
        GameObject child = Child(parent, name);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "GroundAOE";
        renderer.sortingOrder = name == "Burst_Fill" ? 0 : 1;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static ParticleSystem MakeParticles(GameObject parent, string name, Material material, bool burst)
    {
        GameObject child = Child(parent, name);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = !burst;
        main.duration = burst ? 0.35f : 0.7f;
        main.startLifetime = burst ? new ParticleSystem.MinMaxCurve(0.2f, 0.32f) : new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.065f);
        main.startColor = Purple;
        main.startSpeed = burst ? new ParticleSystem.MinMaxCurve(1.5f, 4f) : new ParticleSystem.MinMaxCurve(0f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 32;
        var emission = particles.emission;
        emission.rateOverTime = burst ? 0f : 12f;
        if (burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = burst ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Box;
        shape.radius = 0.08f;
        shape.scale = burst ? Vector3.one : new Vector3(0.35f, 0.04f, 0.01f);
        if (burst) child.transform.localScale = new Vector3(1f, TopDownEllipseHitUtility2D.DefaultTopDownCircleYScale, 1f);
        else
        {
            child.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Purple, 0f), new GradientColorKey(Purple, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "GroundAOE";
        renderer.sortingOrder = 2;
        return particles;
    }

    private static Mesh MakeRing(string name, int segments, float coverage, float inner)
    {
        Mesh mesh = LoadOrCreateMesh(name);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int Steps = 4;
        for (int dash = 0; dash < segments; dash++)
        {
            for (int step = 0; step < Steps; step++)
            {
                float a = (dash + coverage * step / Steps) * Mathf.PI * 2f / segments;
                float b = (dash + coverage * (step + 1) / Steps) * Mathf.PI * 2f / segments;
                int start = vertices.Count;
                vertices.Add(Point(a, inner)); vertices.Add(Point(a, 1f));
                vertices.Add(Point(b, 1f)); vertices.Add(Point(b, inner));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }
        FinishMesh(mesh, vertices, triangles);
        return mesh;
    }

    private static Mesh MakeDisc()
    {
        Mesh mesh = LoadOrCreateMesh("ShadowEchoBurstDisc");
        var vertices = new List<Vector3> { Vector3.zero };
        var triangles = new List<int>();
        for (int i = 0; i <= 64; i++) vertices.Add(Point(i * Mathf.PI * 2f / 64f, 1f));
        for (int i = 1; i <= 64; i++) triangles.AddRange(new[] { 0, i, i + 1 });
        FinishMesh(mesh, vertices, triangles);
        return mesh;
    }

    private static Vector3 Point(float angle, float radius) => new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

    private static Mesh LoadOrCreateMesh(string name)
    {
        string path = "Assets/_Project/Art/Meshes/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        return mesh;
    }

    private static void FinishMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles)
    {
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        var colors = new List<Color>();
        var uv = new List<Vector2>();
        foreach (Vector3 vertex in vertices) { colors.Add(Color.white); uv.Add(new Vector2(vertex.x, vertex.y) * 0.5f + Vector2.one * 0.5f); }
        mesh.SetColors(colors);
        mesh.SetUVs(0, uv);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    public static void Localize()
    {
        var collection = Load<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        var mappings = new List<CsvColumns>
        {
            new KeyIdColumns { KeyFieldName = "key", IncludeId = false, IncludeSharedComments = false }
        };
        foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
            mappings.Add(new LocaleColumns { LocaleIdentifier = code, FieldName = code, IncludeComments = false });
        using (var reader = File.OpenText("Library/CodexShadowEcho/content.csv"))
            Csv.ImportInto(reader, collection, mappings, true, removeMissingEntries: false);
        EditorUtility.SetDirty(collection.SharedData);
        foreach (var table in collection.StringTables) EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ShadowEcho] Imported three keys in five locales.");
    }

    private static T Load<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
