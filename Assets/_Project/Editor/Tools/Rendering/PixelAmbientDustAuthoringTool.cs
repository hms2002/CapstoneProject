using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class PixelAmbientDustAuthoringTool
{
    public const string ScenePath = "Assets/_Project/Scenes/Tests/ShadowCorridorForLight.unity";
    public const string PrefabPath = "Assets/_Project/Prefabs/VFX/Particle/PF_PixelAmbientDust.prefab";
    public const string MaterialPath = "Assets/_Project/Art/Materials/M_PixelAmbientDust.mat";
    public const string TexturePath = "Assets/_Project/Art/Textures/T_PixelAmbientDust.png";
    public const string MeshPath = "Assets/_Project/Prefabs/VFX/Particle/ShadowCorridorDustEmission.asset";
    public const string RootName = "Ambient Pixel Dust";
    public const float PixelsPerUnit = 24f;

    private const float Density = 0.4f;
    private const float MeanLifetime = 13f;
    private static readonly float[] Fractions = { 0.72f, 0.2f, 0.08f };

    [MenuItem("Tools/Rendering/Pixel Lighting/Install ShadowCorridor Ambient Dust")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open ShadowCorridorForLight in Edit Mode before installing dust.");
        if (scene.GetRootGameObjects().Any(root => root.name == RootName))
            throw new InvalidOperationException("Ambient Pixel Dust is already installed. Tune the existing instance.");

        Tilemap ground = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Tilemap>(true))
            .Single(map => map.name == "Ground");
        var cells = new List<Bounds>();
        foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
        {
            if (!ground.HasTile(cell)) continue;
            Vector3 min = ground.CellToWorld(cell);
            Vector3 max = ground.CellToWorld(cell + new Vector3Int(1, 1, 0));
            cells.Add(new Bounds((min + max) * 0.5f, max - min));
        }

        GameObject instance = PlaceOverGround(scene, cells);
        Undo.RegisterCreatedObjectUndo(instance, "Install ambient pixel dust");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not save the dust scene.");
        Selection.activeGameObject = instance;
        Debug.Log($"Ambient pixel dust installed over {cells.Count} ground cells.", instance);
    }

    public static GameObject PlaceOverGround(Scene scene, IReadOnlyList<Bounds> cells)
    {
        if (cells.Count == 0) throw new ArgumentException("No ground cells supplied.", nameof(cells));
        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
            throw new InvalidOperationException("The dust emission mesh already exists; keep its authored settings.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? CreatePrefab();
        var vertices = new List<Vector3>(cells.Count * 4);
        var triangles = new List<int>(cells.Count * 6);
        float area = 0f;
        foreach (Bounds cell in cells)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(cell.min.x, cell.min.y, 0f));
            vertices.Add(new Vector3(cell.min.x, cell.max.y, 0f));
            vertices.Add(new Vector3(cell.max.x, cell.max.y, 0f));
            vertices.Add(new Vector3(cell.max.x, cell.min.y, 0f));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            area += cell.size.x * cell.size.y;
        }

        var mesh = new Mesh { name = "ShadowCorridorDustEmission", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, MeshPath);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = RootName;
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem system = systems[index];
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh = mesh;
            shape.scale = Vector3.one;
            shape.useMeshColors = false;
            SetDensity(system, area, Fractions[index]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(system);
        }
        AssetDatabase.SaveAssets();
        return instance;
    }

    private static GameObject CreatePrefab()
    {
        Material material = CreateMaterial();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        var root = new GameObject("PF_PixelAmbientDust");
        try
        {
            CreateSystem(root, material, "Fine 1x1", 1, 1, 0);
            CreateSystem(root, material, "Flecks 2x1", 2, 1, 1);
            CreateSystem(root, material, "Soft 2x2", 2, 2, 2);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Material CreateMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Capstone/2D/Pixel Ambient Dust");
        if (shader == null) throw new InvalidOperationException("Pixel Ambient Dust shader is missing.");
        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        if (!File.Exists(TexturePath))
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        var material = new Material(shader) { name = "M_PixelAmbientDust" };
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetFloat("_PixelsPerUnit", PixelsPerUnit);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static void CreateSystem(GameObject root, Material material, string name, int width, int height, int index)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        system.useAutoRandomSeed = false;
        system.randomSeed = (uint)(34871 + index * 113);
        var main = system.main;
        main.duration = 18f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 18f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = width / PixelsPerUnit;
        main.startSizeY = height / PixelsPerUnit;
        main.startSizeZ = 1f / PixelsPerUnit;
        main.startRotation = 0f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
        main.stopAction = ParticleSystemStopAction.None;
        float alpha = index == 2 ? 0.28f : 0.52f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.66f, 0.73f, 0.8f, alpha * 0.65f), new Color(0.9f, 0.89f, 0.81f, alpha));

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 8f, 0f);
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.025f, 0.04f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = system.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.055f;
        noise.strengthY = 0.04f;
        noise.strengthZ = 0f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.07f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.rotationAmount = 0f;
        noise.sizeAmount = 0f;
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.85f, 0.65f), new GradientAlphaKey(0f, 1f)
            }
        };

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.allowRoll = false;
        renderer.sortingLayerName = "Projectile";
        renderer.sortingOrder = -20;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.1f;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
        SetDensity(system, 96f, Fractions[index]);
    }

    private static void SetDensity(ParticleSystem system, float area, float fraction)
    {
        var emission = system.emission;
        emission.rateOverTime = area * Density * fraction / MeanLifetime;
        var main = system.main;
        main.maxParticles = Mathf.CeilToInt(area * Density * fraction * 1.8f) + 8;
    }
}
