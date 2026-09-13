#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CapstoneAudio;
using CapstonePresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies cross-wave visuals/damage, landing pose ownership, detached debris and cleanup.</summary>
public sealed class DragonSlamCrossExplosionPlayModeTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string PrefabPath = "Assets/_Project/Prefabs/VFX/PF_Dragon_CrossExplosion.prefab";
    private const string StrategyPath = "Assets/_Project/Data/Abilities/Strategies/AL_DragonSlam.asset";
    private const string DebrisPrefabPath = "Assets/_Project/Resources/DemonKing/Vfx/PF_ExplosionDebrisBounce_HighArc.prefab";
    private const string BodyControllerPath = "Assets/_Project/Art/Animations/Bosses/DragonBoss/DragonBoss.controller";
    private readonly List<Object> owned = new();
    private float timeScale;

    [SetUp]
    public void SetUp()
    {
        timeScale = Time.timeScale;
        Time.timeScale = 1f;
        Type.GetType("PrewarmTraceRuntime, Editor")?.GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var debris in LiveDebris()) WorldPresentationPlayback.Release(debris);
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (animator.name == "PF_Dragon_CrossExplosion(Clone)") Object.DestroyImmediate(animator.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        Time.timeScale = timeScale;
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

    private AbilityLogic_DragonSlam CreateLogic(out DragonController dragon, out AbilitySpec spec)
    {
        var logic = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<AbilityLogic_DragonSlam>(StrategyPath)));
        Set(logic, "crossExplosionSound", default(SoundRef));
        Set(logic, "crossMaxDistance", 2.7f);
        Set(logic, "crossExplosionWallLayers", (LayerMask)0);
        var actor = Own(new GameObject("CrossWaveDragonTest"));
        actor.SetActive(false);
        dragon = actor.AddComponent<DragonController>();
        typeof(Enemy).GetField("abilitySystem", Private).SetValue(dragon, actor.GetComponent<AbilitySystem>());
        Invoke(dragon.AbilitySystem, "CacheRequiredComponents");
        Invoke(actor.GetComponent<GameplayEffectRunner>(), "Awake");
        spec = new AbilitySpec(null);
        typeof(AbilitySpec).GetProperty("Token").SetValue(spec, new AbilityCancellationToken());
        return logic;
    }

    private Animator CreateBodyAnimator(DragonController dragon)
    {
        var body = Own(new GameObject("DragonLandingPoseTest"));
        body.AddComponent<SpriteRenderer>();
        var animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BodyControllerPath);
        typeof(Enemy).GetField("animator", Private).SetValue(dragon, animator);
        animator.Rebind();
        animator.Update(0f);
        return animator;
    }

    [Test]
    public void AuthoredEffect_UsesDragonFramesAndDarkLordHitTiming_WithoutDemonComponents()
    {
        Assert.That(Prefab, Is.Not.Null);
        Assert.That(Prefab.GetComponent<ITimedHitEffect2D>(), Is.Not.Null);
        var collider = Prefab.GetComponent<CircleCollider2D>();
        Assert.That(collider.enabled, Is.False);
        Assert.That(collider.isTrigger, Is.True);
        Assert.That(collider.offset.y, Is.LessThan(0f), "The authored collider marks the ground below the sprite center.");
        Assert.That(Prefab.GetComponentInChildren<SpriteRenderer>().sharedMaterial, Is.Not.Null);
        var clip = Prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips[0];
        Assert.That(clip.isLooping, Is.False);
        Assert.That(clip.frameRate, Is.EqualTo(12f));
        Assert.That(clip.length, Is.EqualTo(0.5f).Within(0.001f));
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        Assert.That(frames.Length, Is.EqualTo(6));
        for (int i = 0; i < frames.Length; i++)
        {
            Assert.That(frames[i].value, Is.Not.Null);
            Assert.That(frames[i].value.name, Is.EqualTo("DragonBossExplosion_" + i));
            Assert.That(AssetDatabase.GetAssetPath(frames[i].value),
                Is.EqualTo("Assets/_Project/Art/Sprites/Bosses/DarkLord/DragonBossExplosion.png"));
        }
        Assert.That(Prefab.GetComponentInChildren<SpriteRenderer>().sprite, Is.EqualTo(frames[0].value));
        Assert.That(clip.events[0].functionName, Is.EqualTo("EnableHitCollision"));
        Assert.That(clip.events[0].time, Is.EqualTo(1f / 12f).Within(0.001f));
        Assert.That(clip.events[1].functionName, Is.EqualTo("DisableHitCollision"));
        Assert.That(clip.events[1].time, Is.EqualTo(2f / 12f).Within(0.001f));
        foreach (var component in Prefab.GetComponentsInChildren<MonoBehaviour>(true))
            Assert.That(component.GetType().Name, Does.Not.Contain("DemonKing"));
        var strategy = AssetDatabase.LoadAssetAtPath<AbilityLogic_DragonSlam>(StrategyPath);
        var data = new SerializedObject(strategy);
        Assert.That(data.FindProperty("crossExplosionPrefab").objectReferenceValue, Is.EqualTo(Prefab));
        var particle = data.FindProperty("crossExplosionParticle");
        var debrisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisPrefabPath);
        Assert.That(particle.FindPropertyRelative("prefab").objectReferenceValue, Is.EqualTo(debrisPrefab));
        Assert.That(debrisPrefab.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
        Assert.That(debrisPrefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(particle.FindPropertyRelative("attachToTarget").boolValue, Is.False);
        Assert.That(particle.FindPropertyRelative("scaleMultiplier").vector3Value, Is.EqualTo(Vector3.one));
        Assert.That(particle.FindPropertyRelative("lifetimeMode").intValue, Is.EqualTo((int)PresentationLifetimeMode.AutoDetect));
        Assert.That(data.FindProperty("scatteredKegCount").intValue, Is.EqualTo(4));
        Assert.That(data.FindProperty("crossExplosionWallLayers").intValue, Is.EqualTo(LayerMask.GetMask("Wall")));
        Assert.That(data.FindProperty("knockbackImpulse").floatValue, Is.EqualTo(1500f));
        Assert.That(data.FindProperty("crossExplosionKnockback").floatValue, Is.EqualTo(12f));
        Assert.That(data.FindProperty("crossWarningSeconds").floatValue, Is.EqualTo(0.3f));
        Assert.That(data.FindProperty("crossExplosionStepInterval").floatValue, Is.EqualTo(0.04f));
        Assert.That(data.FindProperty("crossExplosionLifetime").floatValue, Is.EqualTo(0.5f));
    }

    [TestCase(1.35f)]
    [TestCase(2f)]
    public void EnlargedVisual_PreservesCircularDamageSizeAndGroundAnchor(float diameter)
    {
        var logic = CreateLogic(out var dragon, out var spec);
        Set(logic, "crossExplosionDiameter", diameter);
        var payload = (CombatHitPayload)Invoke(logic, "MakeCrossExplosionPayload", dragon, spec);
        var center = new Vector2(100f, 100f);
        var effect = Own((GameObject)Invoke(logic, "SpawnCrossExplosion", Prefab, center,
            payload, new SharedHitRegistry2D(), (LayerMask)8, null));
        var visualScale = effect.transform.localScale;
        const float previousScale = 1.1764706f;
        Assert.That(visualScale.x, Is.EqualTo(previousScale * 1.35f * 1.5f * diameter).Within(0.0001f));
        Assert.That(visualScale.y, Is.EqualTo(previousScale * 2f * 1.5f * diameter).Within(0.0001f));

        var collider = effect.GetComponent<CircleCollider2D>();
        effect.SendMessage("EnableHitCollision");
        Physics2D.SyncTransforms();
        Assert.That(collider.bounds.center.x, Is.EqualTo(center.x).Within(0.001f));
        Assert.That(collider.bounds.center.y, Is.EqualTo(center.y).Within(0.001f));
        Assert.That(collider.bounds.size.x, Is.EqualTo(diameter).Within(0.001f));
        Assert.That(collider.bounds.size.y, Is.EqualTo(diameter).Within(0.001f));
        foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
        {
            Assert.That(collider.OverlapPoint(center + direction * diameter * 0.49f), Is.True);
            Assert.That(collider.OverlapPoint(center + direction * diameter * 0.51f), Is.False);
        }
    }

    [Test]
    public void CrossLengths_StopBeforeSolidWalls_ButIgnoreTriggerVolumes()
    {
        var logic = CreateLogic(out _, out _);
        Set(logic, "crossMaxDistance", 8f);
        Set(logic, "crossExplosionWallLayers", (LayerMask)1);
        var wall = Own(new GameObject("CrossWallTest"));
        wall.transform.position = Vector3.right * 4f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 10f);
        var trigger = Own(new GameObject("CrossTriggerTest"));
        trigger.transform.position = Vector3.up * 2f;
        var triggerCollider = trigger.AddComponent<BoxCollider2D>();
        triggerCollider.size = new Vector2(5f, 1f);
        triggerCollider.isTrigger = true;
        Physics2D.SyncTransforms();
        var lengths = (float[])Invoke(logic, "BuildCrossExplosionLengths", Vector2.zero);
        Assert.That(lengths, Has.Length.EqualTo(4));
        Assert.That(lengths[0], Is.EqualTo(8f));
        Assert.That(lengths[1], Is.EqualTo(8f));
        Assert.That(lengths[2], Is.EqualTo(8f));
        // Unity's shape-cast tolerance may differ by a few millimetres; the full footprint must stay inside.
        Assert.That(lengths[3], Is.EqualTo(4f - 0.5f - 0.675f - 0.03f).Within(0.01f));
        Assert.That(lengths[3] + 0.675f, Is.LessThan(3.5f));
    }

    [Test]
    public void Warning_CoversFirstThroughLastExplosionFootprints()
    {
        var logic = CreateLogic(out _, out _);
        var warning = (AttackTelegraphSpec)Invoke(logic, "CreateCrossWarning", Vector2.zero, Vector2.right, 2);
        Assert.That(warning.center.x, Is.EqualTo((1.35f + 2.7f) * 0.5f).Within(0.001f));
        Assert.That(warning.size, Is.EqualTo(new Vector2(2.7f, 1.35f)));
        Assert.That(warning.useMeshOutline, Is.True);
        Assert.That(warning.useWallClipping, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WarningCleanup_OnCancellationOrDirectDisposal(bool dispose)
    {
        var logic = CreateLogic(out var dragon, out var spec);
        var animator = CreateBodyAnimator(dragon);
        var presenter = new RecordingTelegraphs();
        var routine = (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, presenter, Vector2.zero, spec);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.True);
        Assert.That(presenter.handles.Count, Is.EqualTo(4));
        if (dispose) ((IDisposable)routine).Dispose();
        else { spec.Token.Cancel(); Assert.That(routine.MoveNext(), Is.False); }
        foreach (var handle in presenter.handles) Assert.That(handle.IsVisible, Is.False);
        Assert.That(LiveExplosions(), Is.Empty);
        Assert.That(LiveDebris(), Is.Empty);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.False);
    }

    [UnityTest]
    public IEnumerator LandingPose_IsHeldUntilFinalExplosion_ButNotDebrisTail()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        var animator = CreateBodyAnimator(dragon);
        Set(logic, "crossWarningSeconds", 0f);
        Set(logic, "crossMaxDistance", 1.35f);
        dragon.PlayPatternTrigger(DragonAnimationKeys.Landing);
        var routine = (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, null, Vector2.right * 100f, spec);
        Assert.That(routine.MoveNext(), Is.True);
        animator.Update(0f);
        animator.Update(0.2f);
        animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dragon_Landing"), Is.True);
        Assert.That(animator.speed, Is.EqualTo(1f));
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(LiveExplosions().Count, Is.EqualTo(4));
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.True);
        yield return routine.Current;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dragon_Landing"), Is.True);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.False);
        animator.Update(0.01f);
        animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dragon_Idle"), Is.True);
        Assert.That(LiveDebris().Count, Is.EqualTo(4));
    }

    [Test]
    public void UnheldLanding_ReturnsToIdle_ForOtherJumpPatterns()
    {
        CreateLogic(out var dragon, out _);
        var animator = CreateBodyAnimator(dragon);
        dragon.PlayPatternTrigger(DragonAnimationKeys.Landing);
        animator.Update(0f);
        animator.Update(0.2f);
        animator.Update(0f);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dragon_Idle"), Is.True);
    }

    [TestCase("groggy", "Dragon_Groggy")]
    [TestCase("dead1", "Dragon_DeadEnter")]
    public void LandingHold_DoesNotBlockReactiveStates_OrOverwriteThemOnRelease(string trigger, string state)
    {
        CreateLogic(out var dragon, out _);
        var animator = CreateBodyAnimator(dragon);
        dragon.SetLandingPoseHeld(true);
        dragon.PlayPatternTrigger(DragonAnimationKeys.Landing);
        animator.Update(0.01f);
        animator.SetTrigger(trigger);
        animator.Update(0.01f);
        animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True);
        dragon.SetLandingPoseHeld(false);
        animator.Update(0.01f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True);
        Assert.That(animator.speed, Is.EqualTo(1f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LandingHold_FailsafeClearsOnPatternEndOrDisable(bool disable)
    {
        CreateLogic(out var dragon, out _);
        var animator = CreateBodyAnimator(dragon);
        dragon.SetLandingPoseHeld(true);
        if (disable) Invoke(dragon, "OnDisable");
        else Invoke(dragon, "OnPatternEnd", null, true);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.False);
    }

    [Test]
    public void MissingExplosion_DoesNotLeaveLandingHeld()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        var animator = CreateBodyAnimator(dragon);
        Set(logic, "crossExplosionPrefab", null);
        var routine = (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, null, Vector2.zero, spec);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.False);
    }

    [UnityTest]
    public IEnumerator Debris_SpawnsAtGroundPoints_AndFinishesAfterCancelledExplosion()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        Set(logic, "crossWarningSeconds", 0f);
        var origin = Vector2.right * 100f;
        var routine = (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, null, origin, spec);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.MoveNext(), Is.True);
        var debris = LiveDebris();
        Assert.That(debris.Count, Is.EqualTo(4));
        foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            Assert.That(debris.Exists(p => Vector2.Distance(p.transform.position, origin + direction * 1.35f) < 0.001f), Is.True);
        yield return null;
        yield return null;
        foreach (var instance in debris)
        {
            Assert.That(instance.transform.parent, Is.Null);
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(instance.GetComponentInChildren<ParticleSystem>().particleCount, Is.GreaterThan(0));
        }
        spec.Token.Cancel();
        Assert.That(routine.MoveNext(), Is.False);
        yield return new WaitForSeconds(0.6f);
        Assert.That(LiveExplosions(), Is.Empty);
        Assert.That(LiveDebris().Count, Is.EqualTo(4), "Already emitted, harmless debris keeps its own lifetime on cancellation.");
        yield return new WaitForSeconds(3.5f);
        Assert.That(LiveDebris(), Is.Empty, "The presentation service must return every debris instance to its pool.");
    }

    [UnityTest]
    public IEnumerator Wave_SpawnsFourCardinalFronts_AndCancellationDisablesDamageImmediately()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        Set(logic, "crossWarningSeconds", 0f);
        var routine = (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, null, Vector2.zero, spec);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.MoveNext(), Is.True);
        var firstStep = LiveExplosions();
        Assert.That(firstStep.Count, Is.EqualTo(4));
        var points = new List<Vector2>();
        foreach (var effect in firstStep)
            points.Add(effect.transform.TransformPoint(effect.GetComponent<CircleCollider2D>().offset));
        foreach (Vector2 expected in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            Assert.That(points.Exists(p => Vector2.Distance(p, expected * 1.35f) < 0.001f), Is.True);
        spec.Token.Cancel();
        Assert.That(routine.MoveNext(), Is.False);
        foreach (var effect in firstStep) Assert.That(effect.activeSelf, Is.False);
        yield return null;
        foreach (var effect in firstStep) Assert.That(effect == null, Is.True);
    }

    [UnityTest]
    public IEnumerator AnimatedHitWindow_OpensAndCloses_ThenEffectIsCollected()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        var payload = (CombatHitPayload)Invoke(logic, "MakeCrossExplosionPayload", dragon, spec);
        int opened = 0;
        var effect = (GameObject)Invoke(logic, "SpawnCrossExplosion", Prefab, Vector2.right * 100f,
            payload, new SharedHitRegistry2D(), (LayerMask)8, (Action)(() => opened++));
        Own(effect);
        var animator = effect.GetComponent<Animator>();
        var collider = effect.GetComponent<CircleCollider2D>();
        animator.Update(0f);
        Assert.That(collider.enabled, Is.False);
        animator.Update(0.09f);
        Assert.That(collider.enabled, Is.True);
        Assert.That(opened, Is.EqualTo(1));
        animator.Update(0.09f);
        Assert.That(collider.enabled, Is.False);
        yield return new WaitForSeconds(0.6f);
        Assert.That(effect == null, Is.True);
    }

    [Test]
    public void OverlappingExplosions_ApplyOneDamageAcrossSharedRegistry()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        var target = Own(new GameObject("CrossDamageTargetTest"));
        target.layer = 3;
        var attributes = target.AddComponent<AttributeSet>();
        var health = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
        Invoke(attributes, "EnsureAttributeExists", health);
        Assert.That(attributes.TrySetBaseValue(health, 10f, null), Is.True);
        target.AddComponent<GameplayEffectRunner>();
        target.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
        target.AddComponent<CombatHurtbox2D>();
        var damage = Own(ScriptableObject.CreateInstance<GE_Damage_Spec>());
        damage.healthAttribute = health;
        damage.fallbackDamage = 1f;
        damage.fallbackStunSeconds = 0f;
        damage.fallbackCameraShake = 0f;
        Set(logic, "damageEffect", damage);
        Set(logic, "crossExplosionKnockback", 0f);
        var payload = (CombatHitPayload)Invoke(logic, "MakeCrossExplosionPayload", dragon, spec);
        var registry = new SharedHitRegistry2D();
        var first = Own((GameObject)Invoke(logic, "SpawnCrossExplosion", Prefab, Vector2.zero, payload, registry, (LayerMask)8, null));
        var second = Own((GameObject)Invoke(logic, "SpawnCrossExplosion", Prefab, Vector2.zero, payload, registry, (LayerMask)8, null));
        Physics2D.SyncTransforms();
        first.SendMessage("EnableHitCollision");
        second.SendMessage("EnableHitCollision");
        Assert.That(attributes.GetAttributeValue(health), Is.EqualTo(9f));
        Assert.That(registry.Contains(target), Is.True);
    }

    [UnityTest]
    public IEnumerator CompletedWave_CleansEveryExplosion()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        Set(logic, "crossWarningSeconds", 0f);
        yield return (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, null, Vector2.right * 100f, spec);
        yield return null;
        Assert.That(LiveExplosions(), Is.Empty);
        Assert.That(LiveDebris().Count, Is.EqualTo(8), "Two outward steps must each emit four independent debris effects.");
        yield return new WaitForSeconds(4f);
        Assert.That(LiveDebris(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator MissingParticle_LeavesTheExplosionWaveWorking()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        Set(logic, "crossWarningSeconds", 0f);
        Set(logic, "crossExplosionParticle", default(SpawnedPresentationHook));
        var routine = (IEnumerator)Invoke(logic, "RunCrossExplosionWave", dragon, null, Vector2.zero, spec);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(LiveExplosions().Count, Is.EqualTo(4));
        Assert.That(LiveDebris(), Is.Empty);
        ((IDisposable)routine).Dispose();
        yield return null;
        Assert.That(LiveExplosions(), Is.Empty);
    }

    [Test]
    public void DisposingLandingFollowups_StopsTheSeparatelyStartedCrossCoroutine()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        var animator = CreateBodyAnimator(dragon);
        Set(logic, "scatteredKegCount", 0);
        var host = Own(new GameObject("CrossFollowupHostTest")).AddComponent<AbilitySystem>();
        var presenter = new RecordingTelegraphs();
        var routine = (IEnumerator)Invoke(logic, "RunLandingFollowups", dragon, host, presenter, Vector2.zero, spec);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(presenter.handles.Count, Is.EqualTo(4), "Cross warnings start before waiting for the keg sequence.");
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.True);
        ((IDisposable)routine).Dispose();
        foreach (var handle in presenter.handles) Assert.That(handle.IsVisible, Is.False);
        Assert.That(LiveExplosions(), Is.Empty);
        Assert.That(animator.GetBool(DragonAnimationKeys.HoldLanding), Is.False);
    }

    [UnityTest]
    public IEnumerator LandingFollowups_WaitsForCrossCompletion_WhenKegsAreDisabled()
    {
        var logic = CreateLogic(out var dragon, out var spec);
        Set(logic, "scatteredKegCount", 0);
        Set(logic, "crossWarningSeconds", 0f);
        var host = Own(new GameObject("CrossFollowupHostTest")).AddComponent<AbilitySystem>();
        yield return (IEnumerator)Invoke(logic, "RunLandingFollowups", dragon, host, null, Vector2.right * 100f, spec);
        yield return null;
        Assert.That(LiveExplosions(), Is.Empty);
    }

    private static List<GameObject> LiveExplosions()
    {
        var result = new List<GameObject>();
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (animator.name == "PF_Dragon_CrossExplosion(Clone)") result.Add(animator.gameObject);
        return result;
    }

    private static List<GameObject> LiveDebris()
    {
        var result = new List<GameObject>();
        foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (component.name == "PF_ExplosionDebrisBounce_HighArc(Clone)"
                && component.GetType().FullName == "CapstonePresentation.TopDownDebrisBounceEmitter2D")
                result.Add(component.gameObject);
        return result;
    }

    /// <summary>Records requested warnings without depending on runtime UI or renderer services.</summary>
    private sealed class RecordingTelegraphs : IAttackTelegraphPresenter
    {
        public readonly List<WarningHandle> handles = new();
        public bool HasActiveTelegraph => handles.Exists(h => h.IsVisible);
        public void Show(AttackTelegraphSpec spec) { SpawnDetachedView(spec); }
        public void UpdateCurrentGeometry(AttackTelegraphSpec spec) { }
        public void HideCurrent() { ClearAll(); }
        public void ClearAll() { foreach (var handle in handles) handle.HideImmediate(); }
        public IAttackTelegraphHandle SpawnDetachedView(AttackTelegraphSpec spec, Transform parent = null)
        {
            var handle = new WarningHandle();
            handle.Show(spec);
            handles.Add(handle);
            return handle;
        }
    }

    /// <summary>Tracks a test warning's visibility for cancellation/disposal assertions.</summary>
    private sealed class WarningHandle : IAttackTelegraphHandle
    {
        public bool IsVisible { get; private set; }
        public void Show(AttackTelegraphSpec spec) { IsVisible = true; }
        public void UpdateGeometry(AttackTelegraphSpec spec) { }
        public void HideImmediate() { IsVisible = false; }
        public void Release() { IsVisible = false; }
    }
}
#endif
