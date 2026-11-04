#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class CrimsonChargePlayModeTests
{
    private const string DataPath = "Assets/_Project/Data/Items/Weapons/LogicData/ALData_CrimsonBoundaryCharge.asset";
    private const string AbilityPath = "Assets/_Project/Data/Abilities/Definitions/AD_CrimsonBoundaryIgnite.asset";
    private readonly List<Object> owned = new();
    private AbilitySystem system;
    private CrimsonBoundaryRuntimeState runtime;
    private AbilityDefinition ability;
    private CrimsonBoundaryChargeData data;
    private InputStub input;
    private CursorStub cursor;
    private RecordingDamage damage;
    private GameObject victim;
    private float previousScale, previousMaximum;
    private IInputActionQueryBackend previousInput;
    private Camera previousMainCamera;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    [SetUp]
    public void Setup()
    {
        previousScale = Time.timeScale; previousMaximum = Time.maximumDeltaTime;
        Time.timeScale = 1; Time.maximumDeltaTime = 0.04f;
        previousInput = (IInputActionQueryBackend)typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        previousMainCamera = Camera.main;
        if (previousMainCamera != null) previousMainCamera.tag = "Untagged";
        var cameraObject = Own(new GameObject("Crimson fixture camera"));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 7;
        camera.transform.position = new Vector3(1200, 1200, -10);
        data = Own(Object.Instantiate(Load<CrimsonBoundaryChargeData>(DataPath)));
        data.weaponData = Own(Object.Instantiate(data.weaponData));
        data.weaponData.damageLayers = 1;
        data.weaponData.burnTickPrefab = null; data.weaponData.burnSustainPrefab = null;
        damage = Own(ScriptableObject.CreateInstance<RecordingDamage>());
        damage.damageKey = Own(ScriptableObject.CreateInstance<GameplayTag>());
        data.weaponData.damageEffect = damage;
        ability = Own(Object.Instantiate(Load<AbilityDefinition>(AbilityPath)));
        ability.sourceObject = data;
        ability.hitFeel = default;
        var fire = Own(ScriptableObject.CreateInstance<AttributeDefinition>()); fire.defaultBaseValue = 10;
        var final = Own(ScriptableObject.CreateInstance<AttributeDefinition>()); final.defaultBaseValue = 1;
        var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
        Set(catalog, "attributes", new[] { fire, final });
        var bindings = Own(ScriptableObject.CreateInstance<StatTypeBindings>());
        Set(bindings, "bindings", new List<StatTypeBindings.Binding> {
            new() { id = StatId.FireFinal, attribute = fire }, new() { id = StatId.FinalMul, attribute = final }
        });
        var profile = Own(ScriptableObject.CreateInstance<DamageProfileDefinition>()); profile.statBindings = bindings;
        var actor = Own(new GameObject("Crimson test caster")); actor.SetActive(false);
        actor.transform.position = new Vector3(1200, 1200, 0);
        var attributes = actor.AddComponent<AttributeSet>(); Set(attributes, "attributeCatalog", catalog);
        actor.AddComponent<TagSystem>(); actor.AddComponent<GameplayEffectRunner>();
        system = actor.AddComponent<AbilitySystem>(); Set(system, "damageProfile", profile);
        cursor = actor.AddComponent<CursorStub>(); cursor.Position = new Vector2(1203, 1200);
        input = new InputStub(actor.transform); InputActionQuery.RegisterBackend(input);
        var weapon = new GameObject("Crimson test weapon"); weapon.transform.SetParent(actor.transform, false);
        runtime = weapon.AddComponent<CrimsonBoundaryRuntimeState>();
        actor.SetActive(true);
        Assert.That(CrimsonBoundaryUtility.ReadFire(system), Is.EqualTo(10f), "Fixture Fire source.");
        Assert.That(AbilityStatProviderFactory.Create(system).Get(StatId.FinalMul), Is.EqualTo(1f), "Fixture final multiplier.");
        system.GiveAbility(ability);
        victim = Own(new GameObject("Crimson test target")); victim.transform.position = cursor.Position;
        victim.AddComponent<BoxCollider2D>(); victim.AddComponent<CombatHurtbox2D>();
        var secondHurtbox = new GameObject("Second hurtbox"); secondHurtbox.transform.SetParent(victim.transform, false);
        secondHurtbox.AddComponent<BoxCollider2D>(); secondHurtbox.AddComponent<CombatHurtbox2D>();
        Physics2D.SyncTransforms();
        Assert.That(Vector2.Distance(AbilityLogic_CrimsonBoundaryCharge.ResolveTarget(system, Camera.main), cursor.Position),
            Is.LessThan(0.01f), "Fixture cursor must be inside the current viewport.");
    }

    [TearDown]
    public void Teardown()
    {
        if (system != null) system.CancelExecution(true);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] is GameObject go && go != null) Object.DestroyImmediate(go);
        foreach (var view in Views()) Object.DestroyImmediate(view.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        InputActionQuery.RegisterBackend(previousInput);
        if (previousMainCamera != null) previousMainCamera.tag = "MainCamera";
        Time.timeScale = previousScale; Time.maximumDeltaTime = previousMaximum;
    }

    private static MonoBehaviour[] Views() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(x => x is ICrimsonBoundaryChargePresentation).ToArray();
    private static IEnumerator Wait(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }
    private void Begin() { input.Held = true; Assert.That(system.TryActivateAbility(ability), Is.True); }

    [Test]
    public void AuthoredBinding_ThresholdsAndVisualSeparation()
    {
        Assert.That(ability.logic, Is.TypeOf<AbilityLogic_CrimsonBoundaryCharge>());
        Assert.That(ability.cooldown, Is.EqualTo(8));
        Assert.That(ability.startCooldownOnEnd, Is.True);
        Assert.That(data.CanRelease(0.699f), Is.False);
        Assert.That(data.CanRelease(0.7f), Is.True);
        Assert.That(data.ChargeDiameter(0.7f), Is.EqualTo(4.03125f).Within(.0001f));
        Assert.That(data.IsFull(1.4f), Is.True);
        Assert.That(data.presentationPrefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(data.presentationPrefab.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        Assert.That(data.presentationPrefab.GetComponentsInChildren<MonoBehaviour>(true).Any(x => x is IAttackCollisionSource2D), Is.False);
        Assert.That(data.presentationPrefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(9));
        Assert.That(data.presentationPrefab.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(6));
        Assert.That(data.presentationPrefab.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
        Assert.That(runtime.TryHandleAbilityInput(null, WeaponAbilitySlot.Skill1, ability), Is.False,
            "The new Skill1 must activate without already-burning targets.");
        var q = Load<AbilityDefinition>("Assets/_Project/Data/Abilities/Definitions/AD_CrimsonBoundaryBigExplosion.asset");
        Assert.That(q.logic, Is.TypeOf<AbilityLogic_CrimsonBoundaryBigExplosion>());
        Assert.That(q.cooldown, Is.EqualTo(12));
    }

    [UnityTest]
    public IEnumerator MovingAim_StaysClose_AndReleasedVisualMatchesDamageCenter()
    {
        Begin(); yield return Wait(.8f);
        var view = Views().Single();
        Transform warning = view.transform.Find("ChargeWarning");
        cursor.Position = new Vector2(1197f, 1203f);
        yield return null; yield return null;
        Vector3 cursorTarget = AbilityLogic_CrimsonBoundaryCharge.ResolveTarget(system, Camera.main);
        Assert.That(Vector3.Distance(warning.position, cursorTarget), Is.InRange(.01f, .7501f));
        Assert.That(warning.Find("InnerCircle").GetComponent<SpriteRenderer>().color.a, Is.EqualTo(1f));
        input.Held = false; yield return null; yield return null;
        Vector3 landing = warning.position;
        Assert.That(Vector3.Distance(landing, cursorTarget), Is.LessThanOrEqualTo(.7501f));
        victim.transform.position = landing;
        Physics2D.SyncTransforms();
        cursor.Position = new Vector2(1205f, 1196f);
        yield return Wait(.54f);
        Assert.That(warning.position, Is.EqualTo(landing), "Release locks the displayed center, not the later cursor.");
        Assert.That(view.transform.Find("Impact").position, Is.EqualTo(landing));
        Assert.That(damage.Hits.Count, Is.EqualTo(1));
        Assert.That(damage.Hits[0].target, Is.EqualTo(victim));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void AimDamping_IsSmooth_Bounded_AndIndependentOfFrameRate(int fps)
    {
        Vector3 target = new(.5f, 0f, 0f);
        Vector3 current = Vector3.zero;
        for (int i = 0; i < fps / 5; i++)
        {
            float oldDistance = Vector3.Distance(current, target);
            current = AbilityLogic_CrimsonBoundaryCharge.DampChargeTarget(current, target, 1f / fps);
            Assert.That(Vector3.Distance(current, target), Is.LessThan(oldDistance));
            Assert.That(current.x, Is.LessThan(target.x), "No overshoot or immediate snap at nearby distances.");
        }
        Assert.That(current.x, Is.EqualTo(.482163f).Within(.0001f), "Same approach after0.2 gameplay seconds.");
        target = new Vector3(100f, -80f, 0f);
        current = AbilityLogic_CrimsonBoundaryCharge.DampChargeTarget(current, target, 1f / fps);
        Assert.That(Vector3.Distance(current, target), Is.LessThanOrEqualTo(.7501f), "Fast cursor motion cannot leave the circle far behind.");
        Assert.That(AbilityLogic_CrimsonBoundaryCharge.DampChargeTarget(current, -target, 0f), Is.EqualTo(current), "Pause freezes damping.");
        target = -target;
        current = AbilityLogic_CrimsonBoundaryCharge.DampChargeTarget(current, target, 1f / fps);
        Assert.That(Vector3.Distance(current, target), Is.LessThanOrEqualTo(.7501f), "A sudden reversal also respects the lag limit.");
    }

    [UnityTest]
    public IEnumerator EnlargedHalfChargeRange_HitsPastOldRadius_AndStillExcludesOutside()
    {
        victim.transform.position = cursor.Position + Vector2.right * 1.9f;
        foreach (BoxCollider2D collider in victim.GetComponentsInChildren<BoxCollider2D>()) collider.size = Vector2.one * .02f;
        var outside = Own(new GameObject("Outside partial circle"));
        outside.transform.position = cursor.Position + Vector2.right * 2.6f;
        outside.AddComponent<BoxCollider2D>().size = Vector2.one * .02f;
        outside.AddComponent<CombatHurtbox2D>();
        Physics2D.SyncTransforms();
        Begin(); yield return Wait(.72f); input.Held = false; yield return Wait(.6f);
        Assert.That(damage.Hits.Count, Is.EqualTo(1));
        Assert.That(damage.Hits[0].target, Is.EqualTo(victim), "Approved inner circle extends beyond the old 1.5-unit radius.");
        Assert.That(outside.GetComponent<BurnStatus2D>(), Is.Null);
    }

    [UnityTest]
    public IEnumerator ImpactFade_PausesWithGameplay_AndWeaponDisableCleansTail()
    {
        Begin(); yield return Wait(.8f); input.Held = false; yield return Wait(.56f);
        var view = Views().Single();
        var inner = view.transform.Find("ChargeWarning/InnerCircle").GetComponent<SpriteRenderer>();
        Assert.That(inner.color.a, Is.GreaterThan(0));
        float alpha = inner.color.a;
        Quaternion rotation = inner.transform.localRotation;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(inner.color.a, Is.EqualTo(alpha));
        Assert.That(inner.transform.localRotation, Is.EqualTo(rotation));
        Time.timeScale = 1;
        runtime.gameObject.SetActive(false);
        yield return null; yield return null;
        Assert.That(Views(), Is.Empty, "Owner cleanup removes both fading circles and the impact tail.");
    }

    [UnityTest]
    public IEnumerator EarlyRelease_CancelsWithoutDamageOrCooldown_AndCanRetry()
    {
        Begin(); yield return Wait(0.2f); input.Held = false; yield return Wait(0.08f);
        Assert.That(system.GetCooldownRemaining(ability), Is.Zero);
        Assert.That(damage.Hits, Is.Empty); Assert.That(Views(), Is.Empty);
        Begin(); yield return null;
        Assert.That(Views().Length, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PartialRelease_CommitsModifiedCooldown_LocksAim_HitsOnceThroughWall()
    {
        using var modifier = system.AddScopedCooldownDurationMultiplier(def => def == ability, 0.5f);
        var wall = Own(new GameObject("Intervening wall")); wall.layer = LayerMask.NameToLayer("Wall");
        if (wall.layer < 0) wall.layer = 8;
        wall.transform.position = new Vector3(1204.5f, 1200, 0);
        wall.AddComponent<BoxCollider2D>();
        Begin(); yield return Wait(0.8f);
        Assert.That(system.GetCooldownRemaining(ability), Is.Zero);
        input.Held = false; yield return null; yield return null;
        float committed = system.GetCooldownRemaining(ability);
        Assert.That(committed, Is.InRange(3.8f, 4f));
        Assert.That(damage.Hits, Is.Empty);
        var view = Views().Single();
        Transform warning = view.transform.Find("ChargeWarning");
        Assert.That(warning.gameObject.activeInHierarchy, Is.True, "Landing warning must remain after release.");
        Assert.That(view.transform.Find("HeadChargeGauge").gameObject.activeSelf, Is.False, "Only the head gauge hides on release.");
        Vector3 landing = warning.position;
        Vector3 innerScale = warning.Find("InnerCircle").localScale;
        Vector3 outerScale = warning.Find("OuterCircle").localScale;
        Quaternion innerRotation = warning.Find("InnerCircle").localRotation;
        cursor.Position += Vector2.up * 20;
        yield return Wait(0.35f); Assert.That(damage.Hits, Is.Empty, "No hits during flight.");
        Assert.That(warning.gameObject.activeInHierarchy, Is.True, "Warning must remain until the impact transition.");
        Assert.That(warning.position, Is.EqualTo(landing), "Warning must keep the released target despite cursor movement.");
        Assert.That(warning.Find("InnerCircle").localScale, Is.EqualTo(innerScale), "Inner circle keeps its released size.");
        Assert.That(warning.Find("OuterCircle").localScale, Is.EqualTo(outerScale), "Outer circle keeps its released size.");
        Assert.That(Quaternion.Angle(innerRotation, warning.Find("InnerCircle").localRotation), Is.GreaterThan(1f), "Circles continue rotating during flight.");
        yield return Wait(0.2f);
        AssertImpactReached();
        Assert.That(damage.Hits.Count, Is.EqualTo(1), "Multiple hurtboxes must resolve to one impact.");
        Assert.That(damage.Hits[0].target, Is.EqualTo(victim));
        Assert.That(damage.Hits[0].amount, Is.EqualTo(40f), "30 base Fire damage + the shared 10 Fire element damage.");
        Assert.That(victim.GetComponent<BurnStatus2D>().CurrentStacks, Is.EqualTo(10));
        Assert.That(system.GetCooldownRemaining(ability), Is.LessThan(committed - 0.4f), "Impact must not restart cooldown.");
    }

    [UnityTest]
    public IEnumerator FullCharge_WaitsForRelease_UsesFullDamageAndBurn_ThenExpires()
    {
        Begin(); yield return Wait(1.65f);
        Assert.That(system.GetCooldownRemaining(ability), Is.Zero); Assert.That(damage.Hits, Is.Empty);
        input.Held = false; yield return Wait(0.57f);
        AssertImpactReached();
        Assert.That(damage.Hits.Count, Is.EqualTo(1));
        Assert.That(damage.Hits[0].amount, Is.EqualTo(50f));
        Assert.That(victim.GetComponent<BurnStatus2D>().CurrentStacks, Is.EqualTo(12));
        victim.SetActive(false); // Stop unrelated burn ticks while waiting for the visual tail.
        yield return Wait(2.2f); Assert.That(Views(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator DeferredCooldown_UsesGameplayEffectModifiers_AndSurvivesPostReleaseCancel()
    {
        var cooldown = Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_Cooldown.asset");
        Set(system, "cooldownController", new AbilityCooldownController(system, system.EffectRunner,
            system.AttributeSet, cooldown, null, null, 0.01f));
        using var modifier = system.AddScopedCooldownDurationMultiplier(def => def == ability, 0.75f);
        Begin(); yield return Wait(0.8f);
        Assert.That(system.EffectRunner.HasActiveEffect(cooldown, system.gameObject, ability), Is.False);
        input.Held = false; yield return Wait(0.06f);
        Assert.That(system.EffectRunner.HasActiveEffect(cooldown, system.gameObject, ability), Is.True);
        float remaining = system.GetCooldownRemaining(ability);
        Assert.That(remaining, Is.InRange(5.8f, 6f));
        Assert.That(system.CommitDeferredCooldown(system.FindSpec(ability)), Is.False, "Cannot commit twice.");
        system.CancelExecution(true); yield return Wait(0.1f);
        Assert.That(system.GetCooldownRemaining(ability), Is.InRange(5.6f, remaining));
        Assert.That(damage.Hits, Is.Empty); Assert.That(Views(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator WeaponDisableDuringFlight_RemovesAllVisualsAndCancelsImpact()
    {
        Begin(); yield return Wait(0.8f); input.Held = false; yield return Wait(0.1f);
        Assert.That(Views().Single().transform.Find("ChargeWarning").gameObject.activeInHierarchy, Is.True);
        runtime.gameObject.SetActive(false); yield return Wait(0.6f);
        Assert.That(Views(), Is.Empty); Assert.That(damage.Hits, Is.Empty);
        Assert.That(system.GetCooldownRemaining(ability), Is.GreaterThan(7f));
    }

    [UnityTest]
    public IEnumerator CancelAndSceneCleanupDuringHold_LeaveNoCooldownOrVisuals()
    {
        Begin(); yield return Wait(0.2f);
        system.CancelExecution(true); yield return Wait(0.06f);
        Assert.That(Views(), Is.Empty); Assert.That(system.GetCooldownRemaining(ability), Is.Zero);
        Begin(); yield return Wait(0.1f);
        ability.logic.CleanupForSceneTransition(system, system.FindSpec(ability), null);
        yield return Wait(0.06f);
        Assert.That(Views(), Is.Empty); Assert.That(system.GetCooldownRemaining(ability), Is.Zero);
    }

    [Test]
    public void TargetClampAndFlightStart_RespectCameraViewport()
    {
        var cameraObject = Own(new GameObject("Crimson test camera"));
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
        camera.orthographicSize = 5; camera.aspect = 2;
        camera.transform.position = new Vector3(1200, 1200, -10);
        cursor.Position = new Vector2(1400, 1400);
        Vector3 target = AbilityLogic_CrimsonBoundaryCharge.ResolveTarget(system, camera);
        Vector3 viewport = camera.WorldToViewportPoint(target);
        Assert.That(viewport.x, Is.EqualTo(1).Within(0.001f));
        Assert.That(viewport.y, Is.EqualTo(1).Within(0.001f));
        Assert.That(camera.WorldToViewportPoint(AbilityLogic_CrimsonBoundaryCharge.ResolveFlightStart(target, camera)).x, Is.GreaterThan(1));

        foreach (Vector2 sample in new[] { new Vector2(0.05f, 0.05f), new Vector2(0.05f, 0.95f),
            new Vector2(0.5f, 0.5f), new Vector2(0.95f, 0.05f), new Vector2(0.95f, 0.95f) })
        {
            Vector3 landing = camera.ViewportToWorldPoint(new Vector3(sample.x, sample.y, viewport.z));
            Vector3 start = AbilityLogic_CrimsonBoundaryCharge.ResolveFlightStart(landing, camera);
            Vector3 startViewport = camera.WorldToViewportPoint(start);
            Vector3 approach = start - landing;
            Assert.That(startViewport.x > 1f || startViewport.y > 1f, Is.True, "Meteor begins outside the viewport.");
            Assert.That(approach.x, Is.GreaterThan(0));
            Assert.That(approach.y, Is.EqualTo(approach.x).Within(0.001f), "Consistent upper-right 45-degree approach.");
            Assert.That(start.z, Is.EqualTo(landing.z));
        }
        Vector3 fallback = AbilityLogic_CrimsonBoundaryCharge.ResolveFlightStart(target, null) - target;
        Assert.That(fallback.x, Is.GreaterThan(0));
        Assert.That(fallback.y, Is.EqualTo(fallback.x));
    }

    public sealed class CursorStub : MonoBehaviour, ICursorWorldSource2D
    {
        public Vector2 Position;
        public Vector2 CursorWorld => Position;
    }

    private void AssertImpactReached()
    {
        Assert.That(Views().Length, Is.EqualTo(1), "The released presentation must remain through impact.");
        Transform impact = Views()[0].transform.Find("Impact");
        Assert.That(impact.gameObject.activeSelf, Is.True, "Flight must finish at 0.5 seconds.");
        Assert.That(Views().Single().transform.Find("ChargeWarning").gameObject.activeSelf, Is.True,
            "The warning must remain briefly after impact while fading.");
        Assert.That(Vector2.Distance(impact.position, victim.transform.position), Is.LessThan(0.01f), "Locked impact target.");
        Assert.That(Physics2D.OverlapCircleAll(impact.position, 1f, data.weaponData.damageLayers).Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(victim.GetComponent<BurnStatus2D>(), Is.Not.Null, "Impact reached target's burn application.");
    }
    public sealed class RecordingDamage : GE_Damage_Spec, ISpecGameplayEffect
    {
        public readonly List<(GameObject target, float amount)> Hits = new();
        void ISpecGameplayEffect.Apply(GameplayEffectSpec spec, GameObject target)
        {
            Assert.That(spec.TryGetSetByCallerMagnitude(damageKey, out float amount), Is.True);
            Hits.Add((target, amount));
        }
    }
    private sealed class InputStub : IInputActionQueryBackend
    {
        public InputStub(Component component) => BackendComponent = component;
        public Component BackendComponent { get; }
        public bool Held;
        public bool IsPressed(InputActionId action) => action == InputActionId.Skill1 && Held;
        public bool WasPressedThisFrame(InputActionId action) => false;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public Sprite GetBindingIcon(InputActionId action) => null;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0) => Vector3.zero;
    }
}
#endif
