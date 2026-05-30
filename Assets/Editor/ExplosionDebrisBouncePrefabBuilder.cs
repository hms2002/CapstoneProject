using System;
using CapstonePresentation;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

internal static class ExplosionDebrisBouncePrefabBuilder
{
    private const string OutputFolder = "Assets/LeeJunMo/Prefab/Effect/Particle/ExplosionDebrisBounce";
    private const string DemonKingRuntimeOutputFolder = "Assets/Resources/DemonKing/Vfx";
    private const string DemonKingHighArcPrefabPath = "Assets/Resources/DemonKing/Vfx/PF_ExplosionDebrisBounce_HighArc.prefab";
    private const string SpriteDefaultMaterialName = "Sprites-Default.mat";
    private const string DefaultParticleMaterialName = "Default-Particle.mat";
    private const string FallbackParticleMaterialPath = "Assets/Material/FireParticle_Sheet1.mat";
    private const string SortingLayerName = "Projectile";

    private static readonly PrefabSpec[] Prefabs =
    {
        new(
            "PF_ExplosionDebrisBounce_HighArc",
            DemonKingHighArcPrefabPath,
            new TopDownDebrisBounceEmitter2D.AuthoringPreset
            {
                MinFragments = 18,
                MaxFragments = 26,
                MaxSimulationSeconds = 3.45f,
                GroundSpeedRange = new Vector2(1.15f, 2.95f),
                GroundSpreadScale = new Vector2(1.12f, 0.92f),
                GroundSpreadRotationDegrees = 0f,
                GroundFriction = 0.54f,
                VerticalSpeedRange = new Vector2(7.2f, 10.8f),
                Gravity = 18.2f,
                HeightScreenOffset = 0.18f,
                HeightSizeBoost = 0.28f,
                MaxBounces = 3,
                BounceDamping = 0.44f,
                MinBounceVelocity = 1.05f,
                FragmentSizeRange = new Vector2(0.06f, 0.145f),
                FragmentSpinDegreesRange = new Vector2(-620f, 620f),
                FragmentColorA = new Color(0.27f, 0.24f, 0.21f, 1f),
                FragmentColorB = new Color(1f, 0.58f, 0.18f, 1f),
                HotFragmentChance = 0.24f,
                ContactColor = new Color(0.56f, 0.49f, 0.39f, 0.72f),
            }),
        new(
            "PF_ExplosionDebrisBounce_DiagonalScatter",
            new TopDownDebrisBounceEmitter2D.AuthoringPreset
            {
                MinFragments = 24,
                MaxFragments = 36,
                MaxSimulationSeconds = 2.85f,
                GroundSpeedRange = new Vector2(2.25f, 5.3f),
                GroundSpreadScale = new Vector2(1.78f, 0.58f),
                GroundSpreadRotationDegrees = 35f,
                GroundFriction = 0.6f,
                VerticalSpeedRange = new Vector2(4.7f, 7.3f),
                Gravity = 16.8f,
                HeightScreenOffset = 0.14f,
                HeightSizeBoost = 0.2f,
                MaxBounces = 3,
                BounceDamping = 0.43f,
                MinBounceVelocity = 0.95f,
                FragmentSizeRange = new Vector2(0.045f, 0.12f),
                FragmentSpinDegreesRange = new Vector2(-760f, 760f),
                FragmentColorA = new Color(0.31f, 0.28f, 0.25f, 1f),
                FragmentColorB = new Color(0.96f, 0.48f, 0.16f, 1f),
                HotFragmentChance = 0.2f,
                ContactColor = new Color(0.54f, 0.48f, 0.39f, 0.68f),
            }),
        new(
            "PF_ExplosionDebrisBounce_LowSkitter",
            new TopDownDebrisBounceEmitter2D.AuthoringPreset
            {
                MinFragments = 30,
                MaxFragments = 46,
                MaxSimulationSeconds = 2.25f,
                GroundSpeedRange = new Vector2(3.1f, 6.4f),
                GroundSpreadScale = new Vector2(1.55f, 0.68f),
                GroundSpreadRotationDegrees = 0f,
                GroundFriction = 0.66f,
                VerticalSpeedRange = new Vector2(2.25f, 4.15f),
                Gravity = 15.4f,
                HeightScreenOffset = 0.08f,
                HeightSizeBoost = 0.1f,
                MaxBounces = 4,
                BounceDamping = 0.38f,
                MinBounceVelocity = 0.65f,
                FragmentSizeRange = new Vector2(0.035f, 0.09f),
                FragmentSpinDegreesRange = new Vector2(-900f, 900f),
                FragmentColorA = new Color(0.29f, 0.27f, 0.24f, 1f),
                FragmentColorB = new Color(0.88f, 0.42f, 0.13f, 1f),
                HotFragmentChance = 0.16f,
                ContactColor = new Color(0.5f, 0.45f, 0.37f, 0.62f),
            }),
    };

    private static bool autoBuildQueued;

    private readonly struct PrefabSpec
    {
        public PrefabSpec(
            string prefabName,
            TopDownDebrisBounceEmitter2D.AuthoringPreset preset)
            : this(prefabName, null, preset)
        {
        }

        public PrefabSpec(
            string prefabName,
            string prefabPath,
            TopDownDebrisBounceEmitter2D.AuthoringPreset preset)
        {
            PrefabName = prefabName;
            PrefabPath = string.IsNullOrWhiteSpace(prefabPath)
                ? $"{OutputFolder}/{prefabName}.prefab"
                : prefabPath;
            Preset = preset;
        }

        public string PrefabName { get; }
        public string PrefabPath { get; }
        public TopDownDebrisBounceEmitter2D.AuthoringPreset Preset { get; }
    }

    [InitializeOnLoadMethod]
    private static void QueueAutoBuild()
    {
        if (autoBuildQueued)
            return;

        autoBuildQueued = true;
        EditorApplication.delayCall += AutoBuildIfMissing;
    }

    [MenuItem("Tools/VFX/Rebuild Explosion Debris Bounce Prefabs")]
    private static void RebuildFromMenu()
    {
        RebuildAll(log: true);
    }

    private static void AutoBuildIfMissing()
    {
        autoBuildQueued = false;
        if (Application.isBatchMode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueAutoBuild();
            return;
        }

        CreateMissingPrefabs(log: true);
    }

    private static void RebuildAll(bool log)
    {
        EnsureOutputFolder();
        Material particleMaterial = ResolveParticleMaterial();

        foreach (PrefabSpec spec in Prefabs)
            CreateOrUpdatePrefab(spec, spec.PrefabPath, particleMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (log)
            Debug.Log($"Explosion debris bounce prefabs rebuilt in {OutputFolder}.");
    }

    private static void CreateMissingPrefabs(bool log)
    {
        EnsureOutputFolder();
        Material particleMaterial = ResolveParticleMaterial();
        int createdCount = 0;

        foreach (PrefabSpec spec in Prefabs)
        {
            bool sourceMissing = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath) == null;
            if (!sourceMissing)
                continue;

            CreateOrUpdatePrefab(spec, spec.PrefabPath, particleMaterial);
            createdCount++;
        }

        if (createdCount == 0)
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (log)
            Debug.Log($"Explosion debris bounce builder created {createdCount} missing prefab asset(s). Existing prefab settings were left unchanged.");
    }

    private static void CreateOrUpdatePrefab(PrefabSpec spec, string prefabPath, Material particleMaterial)
    {
        GameObject root = new(spec.PrefabName);
        try
        {
            ParticleSystem debrisParticles = CreateParticleSystem(
                root.transform,
                "DebrisParticles",
                Mathf.Max(1, spec.Preset.MaxFragments),
                spec.Preset.MaxSimulationSeconds,
                sortingOrder: 1,
                particleMaterial);
            ParticleSystem contactParticles = CreateParticleSystem(
                root.transform,
                "ContactPuffs",
                Mathf.Max(16, spec.Preset.MaxFragments * 4),
                spec.Preset.MaxSimulationSeconds,
                sortingOrder: 0,
                particleMaterial);

            ConfigureContactParticleFade(contactParticles);

            TopDownDebrisBounceEmitter2D emitter = root.AddComponent<TopDownDebrisBounceEmitter2D>();
            emitter.ApplyEditorPreset(debrisParticles, contactParticles, spec.Preset);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ParticleSystem CreateParticleSystem(
        Transform parent,
        string name,
        int maxParticles,
        float duration,
        int sortingOrder,
        Material particleMaterial)
    {
        GameObject child = new(name);
        child.SetActive(false);
        child.transform.SetParent(parent, worldPositionStays: false);

        ParticleSystem particleSystem = child.AddComponent<ParticleSystem>();
        particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.1f, duration);
        main.startDelay = 0f;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = Mathf.Max(1, maxParticles);
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = SortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.maxParticleSize = 0.5f;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0f;
        if (particleMaterial != null)
            renderer.sharedMaterial = particleMaterial;

        child.SetActive(true);
        return particleSystem;
    }

    private static void ConfigureContactParticleFade(ParticleSystem particleSystem)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static Material ResolveParticleMaterial()
    {
        try
        {
            Material spriteDefaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(SpriteDefaultMaterialName);
            if (spriteDefaultMaterial != null)
                return spriteDefaultMaterial;

            Material defaultParticleMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(DefaultParticleMaterialName);
            if (defaultParticleMaterial != null)
                return defaultParticleMaterial;
        }
        catch (Exception)
        {
        }

        return AssetDatabase.LoadAssetAtPath<Material>(FallbackParticleMaterialPath);
    }

    private static void EnsureOutputFolder()
    {
        EnsureFolder(OutputFolder);
        EnsureFolder(DemonKingRuntimeOutputFolder);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
