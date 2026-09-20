// Run in an isolated Unity 6000.4 project with fresh project assemblies and source sprite.
// Author writes only inside that isolated project's Assets; copy the reviewed prefab back separately.
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class KillLockIdleParticleRegression
{
    private const string PrefabPath = "Assets/KillLockUnlockedIdle.prefab";
    private const string Pending = "KillLockIdleParticleRegression.Pending";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void AuthorAndRun()
    {
        var root = new GameObject("KillLockUnlockedIdle");
        var particle = root.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particle.main;
        main.duration = 2.2f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = 2.2f;
        main.startSpeed = 0f;
        // Preview: 7 pixel sprite height, 16 pixels per unit, original scale .25-.47, enlarged twice.
        main.startSize = new ParticleSystem.MinMaxCurve(7f / 16f * .25f * 2.25f, 7f / 16f * .47f * 2.25f);
        main.startColor = new Color(1f, 229f / 255f, 107f / 255f, 1f);
        main.maxParticles = 32;
        main.useUnscaledTime = false;
        var emission = particle.emission;
        emission.rateOverTime = 10f;
        emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
        var shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.25f, .04f, 0f);
        var velocity = particle.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-.04f, .04f);
        velocity.y = new ParticleSystem.MinMaxCurve(20f / 16f / 2.2f, 29f / 16f / 2.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var size = particle.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .55f));
        var color = particle.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f),
                new GradientAlphaKey(.65f, .4f), new GradientAlphaKey(.75f, .55f),
                new GradientAlphaKey(.35f, .8f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(gradient);
        var sheet = particle.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(AssetDatabase.LoadAllAssetsAtPath("Assets/Spark.png").OfType<Sprite>()
            .Single(s => s.name == "Spark (White Ver.) 7x5_0"));
        sheet.frameOverTime = 0f;
        var renderer = particle.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ParticleMaterial.mat");
        renderer.sortingLayerID = unchecked((int)3255220471);
        renderer.sortingOrder = 1;
        renderer.maxParticleSize = 1f;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Run;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // In the isolated fixture only, source MonoScripts are mapped to their compiled DLL types.
    public static void ValidateWiredPrefabs()
    {
        try
        {
            var map = new[] {
                ("101f39c8e2ce9284faa8ab3821a87f2c", typeof(ChestMonsterKillLockView)),
                ("5ffb80732f3ba36419c67d43f5c83c39", typeof(TreasureChest)),
                ("e70ff4a1c8da75a4fad7c6bbd18d056d", typeof(ChestMonsterKillLock)) };
            foreach (string path in System.IO.Directory.GetFiles("Assets/Fixture", "*.prefab", System.IO.SearchOption.AllDirectories))
            {
                string text = System.IO.File.ReadAllText(path);
                foreach (var entry in map)
                {
                    MonoScript script = MonoImporter.GetAllRuntimeMonoScripts().Single(s => s.GetClass() == entry.Item2);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long id);
                    text = text.Replace("fileID: 11500000, guid: " + entry.Item1, "fileID: " + id + ", guid: " + guid);
                }
                System.IO.File.WriteAllText(path, text);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string name in new[] { "KillLockTresureChest", "LowRewardKillLockTreasureChest" })
            {
                string path = "Assets/Fixture/Assets/_Project/Prefabs/Items/Chests/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<ChestMonsterKillLockView>();
                    Check(view != null, name + " view binding");
                    var particle = (ParticleSystem)typeof(ChestMonsterKillLockView).GetField("unlockedIdleParticle", Private).GetValue(view);
                    Check(particle != null && particle.transform.parent == root.transform, name + " nested child binding");
                    Check(Mathf.Abs(particle.transform.localPosition.y + .06f) < .0001f, name + " lower origin");
                    Check(particle.main.loop && !particle.main.playOnAwake && particle.main.maxParticles == 32, name + " loop policy");
                    Check(Mathf.Abs(particle.shape.scale.x - 1.25f) < .0001f, name + " emission width");
                    Check(particle.textureSheetAnimation.GetSprite(0) != null, name + " Spark reference");
                    Check(particle.GetComponent<ParticleSystemRenderer>().sharedMaterial != null, name + " material reference");
                    Check(root.GetComponent<TreasureChest>() != null && root.GetComponent<ChestMonsterKillLock>() != null, name + " state owners");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("KILLLOCK_IDLE_WIRING_PASS: both production prefab variants import with nested particle, state-owner, sprite/material and lower-origin bindings.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.update -= Run;
        try
        {
            var root = new GameObject("KillLock idle lifecycle fixture");
            root.SetActive(false);
            var chest = root.AddComponent<TreasureChest>();
            var killLock = root.AddComponent<ChestMonsterKillLock>();
            var view = root.AddComponent<ChestMonsterKillLockView>();
            var effect = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
            var particle = effect.GetComponent<ParticleSystem>();
            typeof(ChestMonsterKillLockView).GetField("unlockedIdleParticle", Private).SetValue(view, particle);
            root.SetActive(true);
            Check(particle.isPlaying, "Initially unlocked closed chest must play");
            particle.Simulate(1.1f, true, false);
            Check(particle.particleCount > 0 && particle.particleCount <= 32, "Continuous emission is bounded");
            particle.Play();
            killLock.ReservePendingMonster();
            Check(!particle.isPlaying && particle.particleCount == 0, "Relock clears existing particles");
            killLock.ReleasePendingMonster();
            Check(particle.isPlaying, "Unlock starts loop");
            view.enabled = false;
            Check(!particle.isPlaying && particle.particleCount == 0, "View disable clears loop");
            view.enabled = true;
            Check(particle.isPlaying, "View re-enable resynchronizes");
            root.SetActive(false);
            Check(!particle.isPlaying && particle.particleCount == 0, "Root disable clears loop");
            root.SetActive(true);
            Check(particle.isPlaying, "Root re-enable restarts eligible loop");
            chest.RestoreOpenedStateForDungeon();
            Check(!particle.isPlaying && particle.particleCount == 0, "Opened state clears without UI success event");
            root.SetActive(false);
            root.SetActive(true);
            Check(!particle.isPlaying, "Restored opened chest must stay silent");
            killLock.ReservePendingMonster();
            killLock.ReleasePendingMonster();
            Check(!particle.isPlaying, "Unlock must not restart an opened chest");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("KILLLOCK_IDLE_PARTICLE_PASS: emission, unlock/relock, view/root disable/re-enable, opened restore, destroy cleanup.");
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(1);
        }
    }
}
