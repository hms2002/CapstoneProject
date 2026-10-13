#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies terminal monster death cues and terrain-independent thin telegraphs using authored assets.</summary>
public sealed class CombatPresentationRegressionPlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ControllerPath = "Assets/_Project/Art/Sprites/Monsters/CommonMonster/AC_GoblinWarrior.controller";
    private const string TelegraphPath = "Assets/_Project/Prefabs/VFX/Telegraphs/AttackTelegraphView.prefab";
    private readonly List<Object> owned = new();
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    [SetUp]
    public void SetUp()
    {
        Type.GetType("PrewarmTraceRuntime, Editor")?.GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (var visual in Object.FindObjectsByType<ConeParticlePatternVisual2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (visual.name.StartsWith("BreathLockTestVisual")) Object.DestroyImmediate(visual.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [UnityTest]
    public IEnumerator DragonBreath_BlinkLocksWarningFacingAndFireDirection()
    {
        yield return VerifyDragonBreathBlinkLock(cancelDuringBlink: false);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public void Hurtbox_BlankAuthoredColliderArrayFallsBackToOwnCollider(int blankSlots)
    {
        var actor = Own(new GameObject("BlankHurtboxOwnershipTest"));
        actor.SetActive(false);
        var collider = actor.AddComponent<CapsuleCollider2D>();
        var hurtbox = actor.AddComponent<CombatHurtbox2D>();
        typeof(CombatHurtbox2D).GetField("ownedColliders", PrivateInstance).SetValue(hurtbox, new Collider2D[blankSlots]);
        actor.SetActive(true);
        Assert.That(hurtbox.OwnsCollider(collider), Is.True);
        Assert.That(CombatTargetResolver2D.ResolveDamageTarget(collider), Is.SameAs(actor),
            "An entirely blank authored array must retain the implicit own-collider contract.");
    }

    [Test]
    public void Hurtbox_ExplicitOwnershipListDoesNotAdoptUnlistedCollider()
    {
        var actor = Own(new GameObject("ExplicitHurtboxOwnershipTest"));
        actor.SetActive(false);
        var unlisted = actor.AddComponent<CapsuleCollider2D>();
        var listed = actor.AddComponent<BoxCollider2D>();
        var hurtbox = actor.AddComponent<CombatHurtbox2D>();
        typeof(CombatHurtbox2D).GetField("ownedColliders", PrivateInstance).SetValue(hurtbox, new Collider2D[] { null, listed });
        actor.SetActive(true);
        Assert.That(CombatTargetResolver2D.ResolveDamageTarget(listed), Is.SameAs(actor));
        Assert.That(CombatTargetResolver2D.ResolveDamageTarget(unlisted), Is.Null,
            "A list containing an authored collider must preserve its explicit ownership boundary.");
    }

    [UnityTest]
    public IEnumerator DragonBreath_CancellationDuringBlinkReleasesFacingLock()
    {
        yield return VerifyDragonBreathBlinkLock(cancelDuringBlink: true);
    }

    private IEnumerator VerifyDragonBreathBlinkLock(bool cancelDuringBlink)
    {
        var actor = Own(new GameObject("BreathLockTestDragon", typeof(SpriteRenderer), typeof(DragonController)));
        actor.transform.position = new Vector3(10000f, 10000f, 0f);
        var stationaryBody = actor.GetComponent<Rigidbody2D>();
        stationaryBody.gravityScale = 0f;
        stationaryBody.constraints = RigidbodyConstraints2D.FreezeAll;
        var dragon = actor.GetComponent<DragonController>();
        dragon.SetCombatActive(false);
        var target = Own(new GameObject("BreathLockTestTarget")).transform;
        target.position = actor.transform.position + Vector3.right * 5f;
        yield return null; // Let Start complete before assigning the isolated combat target.
        dragon.SetCombatTarget(target);

        var logic = Own(ScriptableObject.CreateInstance<AbilityLogic_DragonFireBreath>());
        var authoredStyle = AssetDatabase.LoadAssetAtPath<AttackTelegraphStyle>(
            "Assets/_Project/Data/Abilities/TelegraphStyles/Monsters/AttackTelegraphStyle.asset");
        Assert.That(authoredStyle.blinkFrequency, Is.GreaterThan(0f));
        typeof(AbilityLogic_DragonFireBreath).GetField("warningTelegraphStyle", PrivateInstance).SetValue(logic, authoredStyle);
        typeof(AbilityLogic_DragonFireBreath).GetField("activeSeconds", PrivateInstance).SetValue(logic, 0.15f);
        var fireSource = Own(new GameObject("BreathLockTestVisual", typeof(ConeParticlePatternVisual2D)));
        typeof(AbilityLogic_DragonFireBreath).GetField("fireBreathVisualPrefab", PrivateInstance).SetValue(logic, fireSource);
        var viewObject = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath)));
        var view = (IAttackTelegraphHandle)viewObject.GetComponent(Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true));
        var presenter = new BreathWarningRecorder(view);
        var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var spec = new AbilitySpec(definition);
        var token = new AbilityCancellationToken();
        typeof(AbilitySpec).GetProperty("Token").SetValue(spec, token);
        var sequence = (IEnumerator)typeof(AbilityLogic_DragonFireBreath).GetMethod("RunFireBreathSequence", PrivateInstance)
            .Invoke(logic, new object[] { dragon, presenter, spec });
        Coroutine execution = dragon.StartCoroutine(sequence);
        FieldInfo lockField = typeof(DragonController).GetField("faceTargetLockCount", PrivateInstance);
        var body = actor.GetComponent<SpriteRenderer>();
        try
        {
            Assert.That(presenter.Spec.rotationDeg, Is.EqualTo(0f).Within(0.01f));
            target.position = actor.transform.position + Vector3.up * 5f;
            yield return null;
            yield return null;
            Assert.That(presenter.Spec.rotationDeg, Is.EqualTo(90f).Within(0.01f), "Before blinking, the warning must track the moved target.");
            Assert.That((int)lockField.GetValue(dragon), Is.Zero);

            float blinkAt = presenter.StartedAt + presenter.Spec.duration * authoredStyle.blinkStartNormalized;
            float deadline = presenter.StartedAt + presenter.Spec.duration + 1f;
            while ((int)lockField.GetValue(dragon) == 0 && Time.time < deadline) yield return null;
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(blinkAt));
            Assert.That(Time.time, Is.LessThan(presenter.StartedAt + presenter.Spec.duration),
                "Aim must lock at blink start, before the tracking preparation ends.");
            var committed = presenter.Spec;
            Vector2 committedDirection = new(Mathf.Cos(committed.rotationDeg * Mathf.Deg2Rad), Mathf.Sin(committed.rotationDeg * Mathf.Deg2Rad));
            float mouthOffset = (float)typeof(AbilityLogic_DragonFireBreath).GetField("mouthFallbackForwardOffset", PrivateInstance).GetValue(logic);
            Vector2 committedMouthPosition = dragon.ResolveFireBreathMouthPosition(committedDirection, mouthOffset);
            bool committedFlip = body.flipX;
            target.position = actor.transform.position + Vector3.left * 5f;

            if (cancelDuringBlink)
            {
                token.Cancel();
                yield return null;
                yield return null;
                Assert.That((int)lockField.GetValue(dragon), Is.Zero, "Cancellation must balance the early facing lock.");
                Assert.That(presenter.HiddenAt, Is.LessThan(0f), "Cancellation cannot advance to the fire commit.");
                foreach (var candidate in Object.FindObjectsByType<ConeParticlePatternVisual2D>(FindObjectsSortMode.None))
                    Assert.That(candidate.name, Is.Not.EqualTo("BreathLockTestVisual(Clone)"),
                        "Cancellation cannot spawn the fire visual.");
                yield break;
            }

            while (presenter.HiddenAt < 0f && Time.time < deadline)
            {
                Assert.That(presenter.Spec.center, Is.EqualTo(committed.center));
                Assert.That(presenter.Spec.rotationDeg, Is.EqualTo(committed.rotationDeg));
                Assert.That(body.flipX, Is.EqualTo(committedFlip), "Blinking and pre-fire delay must hold the committed side.");
                yield return null;
            }
            Assert.That(presenter.HiddenAt - presenter.StartedAt, Is.EqualTo(2.2f).Within(0.12f),
                "Preparation plus pre-fire delay must retain the original attack cadence.");
            ConeParticlePatternVisual2D fire = null;
            foreach (var candidate in Object.FindObjectsByType<ConeParticlePatternVisual2D>(FindObjectsSortMode.None))
                if (candidate.name == "BreathLockTestVisual(Clone)") fire = candidate;
            Assert.That(fire, Is.Not.Null);
            Assert.That(Vector2.Distance(fire.transform.position, committedMouthPosition), Is.LessThan(0.001f),
                "The fire must keep the committed mouth origin, distinct from the ground warning origin.");
            Assert.That(Mathf.DeltaAngle(fire.transform.eulerAngles.z, committed.rotationDeg), Is.EqualTo(0f).Within(0.01f),
                "Fire must consume the same snapshot shown throughout blinking.");
            while ((int)lockField.GetValue(dragon) > 0 && Time.time < deadline) yield return null;
            Assert.That((int)lockField.GetValue(dragon), Is.Zero);
        }
        finally
        {
            dragon.StopCoroutine(execution);
            ((IDisposable)sequence).Dispose();
        }
    }

    private sealed class BreathWarningRecorder : IAttackTelegraphPresenter
    {
        private readonly IAttackTelegraphHandle view;
        public AttackTelegraphSpec Spec;
        public float StartedAt;
        public float HiddenAt = -1f;
        public bool HasActiveTelegraph => view.IsVisible;
        public BreathWarningRecorder(IAttackTelegraphHandle view) => this.view = view;
        public void Show(AttackTelegraphSpec spec) { Spec = spec; StartedAt = Time.time; view.Show(spec); }
        public void UpdateCurrentGeometry(AttackTelegraphSpec spec) { Spec = spec; view.UpdateGeometry(spec); }
        public void HideCurrent() { HiddenAt = Time.time; view.HideImmediate(); }
        public void ClearAll() => view.HideImmediate();
        public IAttackTelegraphHandle SpawnDetachedView(AttackTelegraphSpec spec, Transform parent = null) => throw new NotSupportedException();
    }

    [Test]
    public void GoblinController_PrioritizesDeathOverActionTransitions()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
        Assert.That(controller.layers[0].stateMachine.anyStateTransitions[0].destinationState.name, Is.EqualTo("Die"));
    }

    [TestCase("Idle")]
    [TestCase("Walk")]
    [TestCase("AttackReady")]
    [TestCase("Attack")]
    [TestCase("Recover")]
    public void GoblinDeath_ClearsPendingAttackCues_AndRemainsTerminal(string startState)
    {
        var actor = Own(new GameObject("GoblinAnimationTest", typeof(SpriteRenderer), typeof(Animator)));
        var animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var bridge = actor.AddComponent<CommonMonsterAnimatorBridge>();
        bridge.Configure(animator, "GoblinWarrior_AttackReady", "GoblinWarrior_Attack", "GoblinWarrior_Recover", "GoblinWarrior_Die", null, null);
        animator.Play("Base Layer." + startState, 0, 0.1f);
        animator.Update(0f);
        bridge.TriggerAttackReady();
        bridge.TriggerAttack();
        bridge.TriggerRecover();
        bridge.TriggerDie();
        animator.Update(0.02f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), Is.True, startState);

        // Late runner callbacks must not reopen an attack after death has consumed its trigger.
        for (int frame = 0; frame < 40; frame++)
        {
            bridge.TriggerAttackReady();
            bridge.TriggerAttack();
            bridge.TriggerRecover();
            bridge.TriggerDie();
            animator.Update(0.02f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), Is.True, $"{startState}, frame={frame}");
        }
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1f));
    }

    [UnityTest]
    public IEnumerator GoblinDeath_DuringAttackAndHitPause_RemovesBeforeFiveSecondFallback()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab");
        var actor = Own(Object.Instantiate(prefab));
        var enemy = actor.GetComponent<GoblinWarrior>();
        enemy.SuppressMonsterLootDrop();
        enemy.enabled = false; // Suppress AI/target acquisition, not the active object's death coroutine.
        var animator = actor.GetComponentInChildren<Animator>();
        animator.Play("Base Layer.Attack", 0, 0.1f);
        animator.Update(0f);
        var bridge = actor.GetComponent<CommonMonsterAnimatorBridge>();
        bridge.TriggerAttack();
        CombatHitPause2D.Apply(actor, 0.08f);
        enemy.RequestDeath();
        Assert.That(enemy.IsDead, Is.True);
        float deadline = Time.time + 2f;
        while (actor != null && Time.time < deadline) yield return null;
        Assert.That(actor == null, Is.True, "Death should finish its 0.67s clip, not wait for the 5s fail-safe.");
    }

    [Test]
    public void LineTelegraph_HollowRectangleFadesInAndExtendsToWall()
    {
        Type viewType = Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true);
        var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath)));
        Component view = actor.GetComponent(viewType);
        var style = Own(ScriptableObject.CreateInstance<AttackTelegraphStyle>());
        style.fillColorStart = style.fillColorEnd = Color.clear;
        style.borderColorStart = style.borderColorEnd = Color.red;
        style.blinkFrequency = 0f;
        var wall = Own(new GameObject("AngledLineWall", typeof(BoxCollider2D)));
        wall.layer = LayerMask.NameToLayer("Wall");
        wall.transform.position = Vector3.right * 12f;
        wall.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
        var collider = wall.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(0.4f, 10f);
        Physics2D.SyncTransforms();
        var spec = AttackTelegraphSpec.CreateLine(Vector3.zero, Vector3.right * 6f, 0.12f, 5f, style);
        viewType.GetMethod("Show", new[] { typeof(AttackTelegraphSpec), typeof(AttackTelegraphStyle) })
            .Invoke(view, new object[] { spec, null });
        var mesh = actor.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh.bounds.size.y, Is.EqualTo(0.12f).Within(0.0001f));
        var material = actor.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.That(material.color.a, Is.EqualTo(0.2f).Within(0.0001f), "Start at 80% transparency.");
        var outline = actor.GetComponent<LineRenderer>();
        Assert.That(outline.enabled, Is.True);
        Assert.That(outline.widthMultiplier, Is.LessThan(mesh.bounds.size.y * 0.2f));
        Component meshView = actor.GetComponent(Type.GetType("UnityGAS.AttackTelegraphWallClippedMeshView, Presentation", true));
        var applyStyle = meshView.GetType().GetMethod("ApplyStyle");
        applyStyle.Invoke(meshView, new object[] { style, 0.5f });
        Assert.That(material.color.a, Is.EqualTo(0.6f).Within(0.0001f));
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        float imminentPulse = Mathf.Lerp(0.45f, 1f, (Mathf.Sin(Time.time * 4f * Mathf.PI * 2f) + 1f) * 0.5f);
        Assert.That(material.color.a, Is.EqualTo(imminentPulse).Within(0.0001f));
        Assert.That(outline.sharedMaterial.color.a, Is.EqualTo(imminentPulse).Within(0.0001f),
            "Interior and perimeter pulse together even when the old line style disabled blinking.");
        Assert.That(mesh.bounds.size.x, Is.GreaterThan(6f), "Extend past the supplied endpoint to the wall.");
        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i += 2)
        {
            Vector3 start = actor.transform.TransformPoint(vertices[i]);
            Vector3 end = actor.transform.TransformPoint(vertices[i + 1]);
            var hit = Physics2D.Raycast(start, Vector2.right, Mathf.Infinity, 1 << wall.layer);
            Assert.That(hit.collider, Is.EqualTo(collider));
            Assert.That(end.x, Is.EqualTo(hit.point.x - 0.03f).Within(0.001f));
        }
        Assert.That(vertices[1].x, Is.Not.EqualTo(vertices[vertices.Length - 1].x).Within(0.001f));
        collider.isTrigger = true;
        Physics2D.SyncTransforms();
        viewType.GetMethod("UpdateGeometry").Invoke(view, new object[] { spec });
        Assert.That(mesh.bounds.size.x, Is.EqualTo(40f).Within(0.001f));
        Assert.That(mesh.bounds.size.y, Is.EqualTo(0.12f).Within(0.0001f), "Updates must preserve the authored width.");
        viewType.GetMethod("HideImmediate").Invoke(view, null);
        Assert.That(actor.GetComponent<MeshRenderer>().enabled, Is.False);
    }

    [TestCase(AttackTelegraphShape.Rectangle, false)]
    [TestCase(AttackTelegraphShape.Rectangle, true)]
    [TestCase(AttackTelegraphShape.Circle, false)]
    [TestCase(AttackTelegraphShape.Circle, true)]
    public void ThinTelegraph_PreservesMeshStyleAndFullGeometry_AcrossWallsAndUpdates(AttackTelegraphShape shape, bool requestClipping)
    {
        Type viewType = Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true);
        var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath)));
        Component view = actor.GetComponent(viewType);
        var style = Own(ScriptableObject.CreateInstance<AttackTelegraphStyle>());
        style.scaleFillWithProgress = true;
        style.fillScaleStart = 0.1f;
        style.fillScaleEnd = 1f;
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkFrequency = 0f;
        style.fillColorEnd = new Color(0.8f, 0.2f, 0.1f, 0.4f);
        var spec = shape == AttackTelegraphShape.Rectangle
            ? AttackTelegraphSpec.CreateRectangle(Vector3.right * 2f, new Vector2(4f, 2f), 0f, 2f, style)
            : AttackTelegraphSpec.CreateCircle(Vector3.zero, 4f, 2f, style);
        if (requestClipping) spec = spec.WithWallClipping(1, 48);
        var show = viewType.GetMethod("Show", new[] { typeof(AttackTelegraphSpec), typeof(AttackTelegraphStyle) });
        show.Invoke(view, new object[] { spec, null });
        var meshFilter = actor.GetComponent<MeshFilter>();
        Assert.That(meshFilter, Is.Not.Null, "Thin mesh rendering must not fall back to the thick sprite border.");
        Component meshView = actor.GetComponent(Type.GetType("UnityGAS.AttackTelegraphWallClippedMeshView, Presentation", true));
        var applyStyle = meshView.GetType().GetMethod("ApplyStyle");
        float startWidth = meshFilter.sharedMesh.bounds.size.x;
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        var baseline = meshFilter.sharedMesh.vertices;
        Assert.That(meshFilter.sharedMesh.bounds.size.x, Is.GreaterThan(startWidth));
        Assert.That(actor.GetComponent<LineRenderer>().widthMultiplier, Is.EqualTo(0.045f).Within(0.0001f));
        Color actualColor = actor.GetComponent<MeshRenderer>().sharedMaterial.color;
        Assert.That(Vector4.Distance(actualColor, style.fillColorEnd), Is.LessThan(0.0001f));
        Assert.That((bool)viewType.GetField("activeUseWallClipping", PrivateInstance).GetValue(view), Is.False);

        var wall = Own(new GameObject("TelegraphWall", typeof(BoxCollider2D)));
        wall.transform.position = Vector3.right;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 10f);
        Physics2D.SyncTransforms();
        show.Invoke(view, new object[] { spec, null });
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        CollectionAssert.AreEqual(baseline, meshFilter.sharedMesh.vertices);
        // Explicit and inherited geometry updates must keep the same no-terrain policy.
        viewType.GetMethod("UpdateGeometry").Invoke(view, new object[] { spec.WithWallClipping(1, 48) });
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        CollectionAssert.AreEqual(baseline, meshFilter.sharedMesh.vertices);
        spec.useWallClipping = false;
        spec.useMeshOutline = false;
        viewType.GetMethod("UpdateGeometry").Invoke(view, new object[] { spec });
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        CollectionAssert.AreEqual(baseline, meshFilter.sharedMesh.vertices);
    }
    [UnityTest]
    public IEnumerator ApprenticeCharge_RevealsProgressively_WithoutVisionMaskChangingCoverage()
    {
        var data = AssetDatabase.LoadAssetAtPath<ApprenticeHeroSwordChargeSpinData>(
            "Assets/_Project/Data/Items/Weapons/LogicData/ALData_ApprenticeHeroSwordChargeSpin.asset");
        var scope = Own(new GameObject("ChargeMaskRegression", typeof(UnityEngine.Rendering.SortingGroup)));
        scope.transform.position = new Vector3(10000f, 10000f, 0f);
        scope.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingLayerName = "Entity";
        var weapon = new GameObject("Weapon", typeof(SpriteRenderer));
        weapon.transform.SetParent(scope.transform, false);
        var source = weapon.GetComponent<SpriteRenderer>();
        source.sprite = data.ChargeRevealSprite;
        source.sortingLayerName = "Entity";
        source.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        source.enabled = false;

        Type runtimeType = typeof(AbilityLogic_ApprenticeHeroSwordChargeSpin).GetNestedType(
            "ApprenticeHeroSwordChargePresentationRuntime", BindingFlags.NonPublic);
        object[] args = { source, data, source.sprite, null, null, null };
        runtimeType.GetMethod("CreateReveal", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        var revealRoot = (GameObject)args[3];
        Assert.That(revealRoot, Is.Not.Null);
        object runtime = runtimeType.GetConstructors(PrivateInstance)[0].Invoke(
            new object[] { data, null, revealRoot, args[4], args[5], source.sprite, null });
        MethodInfo update = runtimeType.GetMethod("Update");

        var vision = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Map/Gimmicks/Witch/PlayerVisionMask.prefab")));
        vision.transform.position = scope.transform.position;
        foreach (string path in new[]
        {
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/Candlestick.prefab",
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/LightBead.prefab",
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/Dead'sSkeleton.prefab"
        })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var authored in prefab.GetComponentsInChildren<SpriteMask>(true))
            {
                Assert.That(authored.isCustomRangeActive, Is.True, path);
                var mask = new GameObject("ExternalSightMask", typeof(SpriteMask)).GetComponent<SpriteMask>();
                mask.transform.SetParent(vision.transform, false);
                mask.sprite = authored.sprite;
                mask.alphaCutoff = authored.alphaCutoff;
                mask.renderingLayerMask = authored.renderingLayerMask;
                mask.isCustomRangeActive = authored.isCustomRangeActive;
                mask.frontSortingLayerID = authored.frontSortingLayerID;
                mask.backSortingLayerID = authored.backSortingLayerID;
                mask.frontSortingOrder = authored.frontSortingOrder;
                mask.backSortingOrder = authored.backSortingOrder;
            }
        }
        var cameraObject = Own(new GameObject("ChargeMaskCamera", typeof(Camera)));
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = scope.transform.position + new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        var target = Own(new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32));
        target.Create();
        camera.targetTexture = target;
        var readback = Own(new Texture2D(256, 256, TextureFormat.RGBA32, false));
        int previousCoverage = -1; // Transparent sprite margins can leave the first quarter empty.
        foreach (float ratio in new[] { 0.25f, 0.65f, 1f })
        {
            update.Invoke(runtime, new object[] { ratio });
            vision.SetActive(false);
            yield return null;
            yield return null; // Read the previous completed GPU frame, including in batch mode.
            int withoutVision = CountChargePixels(target, readback);
            Assert.That(withoutVision, Is.GreaterThan(previousCoverage), "Charge coverage must grow with charge ratio.");
            vision.SetActive(true);
            yield return null;
            yield return null; // Read the previous completed GPU frame, including in batch mode.
            Assert.That(CountChargePixels(target, readback), Is.EqualTo(withoutVision),
                "Entity sight mask must not expose uncharged blade pixels.");
            previousCoverage = withoutVision;
        }
        camera.targetTexture = null;
    }


    [TestCase(typeof(AbilityLogic_ApprenticeHeroSwordChargeSpin))]
    [TestCase(typeof(AbilityLogic_ApprenticeHeroSwordDashStab))]
    public void ApprenticeSkillAim_LocksAngleAndSide_AndSceneCleanupReleasesImmediately(Type logicType)
    {
        var actor = Own(new GameObject("SkillAimOwner", typeof(AbilitySystem), typeof(PlayerAim2D)));
        var system = actor.GetComponent<AbilitySystem>();
        var aim = actor.GetComponent<PlayerAim2D>();
        aim.enabled = false;
        var rigObject = new GameObject("WeaponRig");
        rigObject.transform.SetParent(actor.transform, false);
        var aimRoot = new GameObject("AimRoot").transform;
        aimRoot.SetParent(rigObject.transform, false);
        var rig = rigObject.AddComponent<WeaponPresentationRig2D>();
        typeof(WeaponPresentationRig2D).GetField("aimRoot", PrivateInstance).SetValue(rig, aimRoot);
        typeof(WeaponPresentationRig2D).GetField("sideOffsetRoot", PrivateInstance).SetValue(rig, rigObject.transform);
        aim.SetAimDirectionForPresentation(Vector2.up + Vector2.right);
        rig.RefreshNow();
        var logic = Own((AbilityLogic)ScriptableObject.CreateInstance(logicType));
        var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var spec = new AbilitySpec(definition);
        logicType.GetMethod("BeginSkillAimLock", PrivateInstance).Invoke(logic,
            new object[] { system, spec, aim.AimDirection, 10f });
        aim.SetAimDirectionForPresentation(Vector2.left);
        rig.RefreshNow();
        Assert.That(Mathf.DeltaAngle(aimRoot.eulerAngles.z, 45f), Is.EqualTo(0f).Within(.01f));
        Assert.That(rig.CurrentSideSign, Is.EqualTo(1));
        logic.CleanupForSceneTransition(system, spec, null);
        rig.RefreshNow();
        Assert.That(Mathf.DeltaAngle(aimRoot.eulerAngles.z, 180f), Is.EqualTo(0f).Within(.01f));
        Assert.That(rig.CurrentSideSign, Is.EqualTo(-1));

        int oldToken = rig.BeginAimPresentationOverride(WeaponAimPresentationMode.LockedAtCast, Vector2.up, 10f);
        rig.BeginAimPresentationOverride(WeaponAimPresentationMode.LockedAtCast, Vector2.down, 10f);
        rig.CancelAimPresentationOverride(oldToken);
        rig.RefreshNow();
        Assert.That(Mathf.DeltaAngle(aimRoot.eulerAngles.z, 270f), Is.EqualTo(0f).Within(.01f),
            "A previous skill's cleanup must not release a newer skill's direction lock.");
    }
    private static int CountChargePixels(RenderTexture target, Texture2D readback)
    {
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            int count = 0;
            foreach (Color32 pixel in readback.GetPixels32())
                if (pixel.a > 32) count++;
            return count;
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }
}
/// <summary>Verifies elemental direct-hit HP channels without changing gauge or status ownership.</summary>
public sealed class ElementHitDamagePlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new();
    private readonly List<Action<GameObject, GameObject, float>> damageListeners = new();
    private IDamagePopupBackend previousPopupBackend;
    private PopupRecorder popups;
    private UnityEngine.Random.State previousRandomState;

    [SetUp]
    public void SetUp()
    {
        previousPopupBackend = (IDamagePopupBackend)typeof(DamagePopupPlayback)
            .GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        popups = new PopupRecorder();
        DamagePopupPlayback.RegisterBackend(popups);
        previousRandomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(42);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var listener in damageListeners) CombatActivityEvents.DamageApplied -= listener;
        damageListeners.Clear();
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        DamagePopupPlayback.RegisterBackend(previousPopupBackend);
        UnityEngine.Random.state = previousRandomState;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ElementSnapshot_ReusesPhysicalCritAndFinalMultiplier_AndFreezesAttackStats(bool critical)
    {
        var hit = CreateHit(500f);
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 2f,
            [StatId.BleedFinal] = 3f,
            [StatId.PoisonFinal] = 5f,
            [StatId.ElectricFinal] = 7f,
            [StatId.FinalMul] = 1.5f,
            [StatId.CritMultiplier] = 2f,
            [StatId.CritChanceFinal] = critical ? 1f : 0f
        };
        var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 10f, 4f, 3f, hit.Source.gameObject);
        float factor = critical ? 3f : 1.5f;
        Assert.That(snapshot.IsCriticalHit, Is.EqualTo(critical));
        Assert.That(snapshot.FinalHpDamage, Is.EqualTo(10f * factor).Within(0.0001f));
        Assert.That(snapshot.FinalStaggerBuildUp, Is.EqualTo(6f).Within(0.0001f), "Critical scaling must not leak into stagger.");
        Assert.That(snapshot.FinalKnockbackImpulse, Is.EqualTo(3f));
        Assert.That(snapshot.FinalElementHpDamages, Has.Length.EqualTo(4));
        AssertElement(snapshot.FinalElementHpDamages, "Element.Fire", 2f * factor);
        AssertElement(snapshot.FinalElementHpDamages, "Element.Blood", 3f * factor);
        AssertElement(snapshot.FinalElementHpDamages, "Element.Poison", 5f * factor);
        AssertElement(snapshot.FinalElementHpDamages, "Element.Electric", 7f * factor);
        Assert.That(snapshot.TotalHpDamage, Is.EqualTo(27f * factor).Within(0.0001f));

        stats[StatId.FireFinal] = stats[StatId.BleedFinal] = stats[StatId.PoisonFinal] = stats[StatId.ElectricFinal] = 999f;
        stats[StatId.FinalMul] = 50f;
        stats[StatId.CritMultiplier] = 100f;
        stats[StatId.CritChanceFinal] = critical ? 0f : 1f;
        Apply(hit, snapshot);

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(500f - 27f * factor).Within(0.0001f));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(hit.LastHit.IsCriticalHit, Is.EqualTo(critical));
        Assert.That(popups.Requests, Has.Count.EqualTo(5));
        Assert.That(SumPopups(), Is.EqualTo(27f * factor).Within(0.0001f));
        foreach (var popup in popups.Requests) Assert.That(popup.IsCritical, Is.EqualTo(critical));
        var physical = popups.Requests.Find(request => request.ElementTag == null);
        Assert.That(physical.SizeScale, Is.EqualTo(1f));
        AssertPopupElement("Element.Fire", 2f * factor, ExpectedSecondaryScale(2f, 10f));
        AssertPopupElement("Element.Blood", 3f * factor, ExpectedSecondaryScale(3f, 10f));
        AssertPopupElement("Element.Poison", 5f * factor, ExpectedSecondaryScale(5f, 10f));
        AssertPopupElement("Element.Electric", 7f * factor, ExpectedSecondaryScale(7f, 10f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FirePrimary_MergesMatchingElementAndKeepsOtherElementsProportionatelySmaller(bool critical)
    {
        var hit = CreateHit(1000f);
        var fire = LoadTag("Element.Fire");
        var electric = LoadTag("Element.Electric");
        var gauge = CreateGauge(hit, fire, electric);
        int gaugeChanges = 0;
        int gaugeTriggers = 0;
        gauge.OnGaugeChanged += (element, previous, current) => gaugeChanges++;
        gauge.OnGaugeTriggered += _ => gaugeTriggers++;
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 40f, [StatId.BleedFinal] = 5f,
            [StatId.PoisonFinal] = 10f, [StatId.ElectricFinal] = 20f,
            [StatId.FinalMul] = 1.5f, [StatId.CritMultiplier] = 2f,
            [StatId.CritChanceFinal] = critical ? 1f : 0f
        };
        var calculated = DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 100f, 0f, 0f, hit.Source.gameObject);
        var snapshot = new CombatDamageSnapshot(calculated.FinalHpDamage, 0f, 0f, calculated.IsCriticalHit,
            new[]
            {
                new ElementDamageResult { elementType = fire, damage = 3f },
                new ElementDamageResult { elementType = electric, damage = 4f }
            }, true, calculated.FinalElementHpDamages);
        float factor = critical ? 3f : 1.5f;

        Apply(hit, snapshot, damagePopupElementTag: fire);

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(1000f - 175f * factor).Within(0.0001f));
        Assert.That(popups.Requests, Has.Count.EqualTo(4), "The primary Fire damage and its Fire addition share one popup.");
        Assert.That(popups.Requests.Exists(request => request.ElementTag == null), Is.False);
        AssertPopupElement("Element.Fire", 140f * factor, 1f);
        AssertPopupElement("Element.Electric", 20f * factor, ExpectedSecondaryScale(20f, 140f));
        AssertPopupElement("Element.Poison", 10f * factor, ExpectedSecondaryScale(10f, 140f));
        AssertPopupElement("Element.Blood", 5f * factor, ExpectedSecondaryScale(5f, 140f));
        Assert.That(SumPopups(), Is.EqualTo(175f * factor).Within(0.0001f));
        foreach (var popup in popups.Requests) Assert.That(popup.IsCritical, Is.EqualTo(critical));
        Assert.That(hit.HealthChangeCount, Is.EqualTo(1));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(hit.DamageTakenCount, Is.EqualTo(1));
        Assert.That(hit.ActivityCount, Is.EqualTo(1));
        Assert.That(gauge.GetValue(fire), Is.EqualTo(3f));
        Assert.That(gauge.GetValue(electric), Is.EqualTo(4f));
        Assert.That(gaugeChanges, Is.EqualTo(2), "Direct HP channels must not create additional gauge applications.");
        Assert.That(gaugeTriggers, Is.Zero);
    }

    [Test]
    public void DuplicateElementChannels_MergeOncePerTagAndCapSecondarySizeBelowPrimary()
    {
        var hit = CreateHit(100f);
        var fire = LoadTag("Element.Fire");
        var electric = LoadTag("Element.Electric");
        var snapshot = new CombatDamageSnapshot(10f, 0f, 0f, false,
            Array.Empty<ElementDamageResult>(), true,
            new[]
            {
                new ElementDamageResult { elementType = fire, damage = 2f },
                new ElementDamageResult { elementType = electric, damage = 9f },
                new ElementDamageResult { elementType = fire, damage = 3f },
                new ElementDamageResult { elementType = electric, damage = 11f }
            });

        Apply(hit, snapshot, damagePopupElementTag: fire);

        Assert.That(popups.Requests, Has.Count.EqualTo(2));
        AssertPopupElement("Element.Fire", 15f, 1f);
        AssertPopupElement("Element.Electric", 20f, 0.85f);
        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(65f));
        Assert.That(SumPopups(), Is.EqualTo(35f));
        Assert.That(hit.HealthChangeCount, Is.EqualTo(1));
        Assert.That(hit.HitCount, Is.EqualTo(1));
    }

    [TestCase(500f, 432.5f, 67.5f)]
    [TestCase(30f, 0f, 30f)]
    public void MergedFirePrimary_IncomingModifierShieldAndLethalClampPreserveActualPopupTotals(
        float initialHp, float expectedHp, float expectedDamage)
    {
        var hit = CreateHit(initialHp, 20f);
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 40f, [StatId.BleedFinal] = 5f,
            [StatId.PoisonFinal] = 10f, [StatId.ElectricFinal] = 20f,
            [StatId.FinalMul] = 1f
        };
        var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 100f, 0f, 0f, hit.Source.gameObject);
        int incomingCount = 0;
        using (CombatIncomingDamageModifiers.Register(context =>
        {
            if (context.Target != hit.Target) return context.BaseDamage;
            incomingCount++;
            return context.BaseDamage * 0.5f;
        }))
            Apply(hit, snapshot, damagePopupElementTag: LoadTag("Element.Fire"));

        Assert.That(incomingCount, Is.EqualTo(1));
        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(expectedHp).Within(0.0001f));
        Assert.That(hit.Attributes.GetAttributeValue(hit.Shield), Is.Zero);
        Assert.That(popups.Requests, Has.Count.EqualTo(4));
        Assert.That(SumPopups(), Is.EqualTo(expectedDamage).Within(0.0001f));
        AssertPopupElement("Element.Fire", expectedDamage * 140f / 175f, 1f);
        AssertPopupElement("Element.Electric", expectedDamage * 20f / 175f, ExpectedSecondaryScale(20f, 140f));
        AssertPopupElement("Element.Poison", expectedDamage * 10f / 175f, ExpectedSecondaryScale(10f, 140f));
        AssertPopupElement("Element.Blood", expectedDamage * 5f / 175f, ExpectedSecondaryScale(5f, 140f));
        Assert.That(hit.HealthChangeCount, Is.EqualTo(1));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(hit.DamageTakenCount, Is.EqualTo(1));
        Assert.That(hit.ActivityCount, Is.EqualTo(1));
        Assert.That(hit.KillCount, Is.EqualTo(expectedHp <= 0f ? 1 : 0));
    }

    [Test]
    public void ElementOnlyHit_UsesLargestElementAsFullSizePrimary()
    {
        var hit = CreateHit(100f);
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 4f, [StatId.ElectricFinal] = 10f,
            [StatId.FinalMul] = 1f, [StatId.CritMultiplier] = 2f
        };
        Apply(hit, DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 0f, 0f, 0f, hit.Source.gameObject));

        Assert.That(popups.Requests, Has.Count.EqualTo(2));
        AssertPopupElement("Element.Electric", 10f, 1f);
        AssertPopupElement("Element.Fire", 4f, ExpectedSecondaryScale(4f, 10f));
        Assert.That(SumPopups(), Is.EqualTo(14f));
        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(86f));
        Assert.That(hit.HitCount, Is.EqualTo(1));
    }

    [Test]
    public void ElementOnlyHit_ZeroPhysicalDamageStillReducesHpAndConfirmsOnce()
    {
        var hit = CreateHit(100f);
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 4f,
            [StatId.FinalMul] = 1.5f,
            [StatId.CritMultiplier] = 2f,
            [StatId.CritChanceFinal] = 1f
        };
        var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 0f, 0f, 0f, hit.Source.gameObject);
        Assert.That(snapshot.FinalHpDamage, Is.Zero);
        Assert.That(snapshot.TotalHpDamage, Is.EqualTo(12f).Within(0.0001f));
        Apply(hit, snapshot);

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(88f).Within(0.0001f));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(hit.DamageTakenCount, Is.EqualTo(1));
        Assert.That(popups.Requests, Has.Count.EqualTo(1));
        Assert.That(popups.Requests[0].Kind, Is.EqualTo(DamagePopupKind.Element));
        Assert.That(popups.Requests[0].ElementTag.Path, Is.EqualTo("Element.Fire"));
        Assert.That(popups.Requests[0].Amount, Is.EqualTo(12f).Within(0.0001f));
        Assert.That(popups.Requests[0].IsCritical, Is.True);
        Assert.That(popups.Requests[0].SizeScale, Is.EqualTo(1f));
    }

    [Test]
    public void OneHitShield_ConsumesOneChargeAndBlocksPhysicalAndAllElementHp()
    {
        var hit = CreateHit(100f);
        hit.Damage.oneHitShieldTag = LoadTag("State.Status.Shielded");
        var tags = hit.Target.GetComponent<TagSystem>();
        tags.AddTag(hit.Damage.oneHitShieldTag);
        Assert.That(tags.GetTagCount(hit.Damage.oneHitShieldTag), Is.EqualTo(1));
        Apply(hit, CriticalTwentyDamage(hit.Source));

        Assert.That(tags.GetTagCount(hit.Damage.oneHitShieldTag), Is.Zero);
        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(100f));
        Assert.That(hit.HealthChangeCount, Is.Zero);
        Assert.That(hit.DamageTakenCount, Is.Zero);
        Assert.That(hit.HitCount, Is.EqualTo(1), "The blocked hit must not emit separate confirmations for its channels.");
        Assert.That(popups.Requests, Is.Empty);
    }

    [Test]
    public void Invulnerability_BlocksWholeHitAndAllElementPopups()
    {
        var hit = CreateHit(100f);
        hit.Damage.invulnerableTag = LoadTag("State.Invulnerable");
        hit.Target.GetComponent<TagSystem>().AddTag(hit.Damage.invulnerableTag);
        Apply(hit, CriticalTwentyDamage(hit.Source), expectedAccepted: false);

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(100f));
        Assert.That(hit.HitCount, Is.Zero);
        Assert.That(hit.DamageTakenCount, Is.Zero);
        Assert.That(popups.Requests, Is.Empty);
    }

    [TestCase(100f, 5f, 85f, 15f, false)]
    [TestCase(6f, 5f, 0f, 6f, true)]
    [TestCase(100f, 25f, 100f, 0f, false)]
    public void AbsorbShieldAndLethalClamp_ReportActualHpLossPerChannel_WithSingleEvents(
        float initialHp, float shield, float expectedHp, float expectedDamage, bool lethal)
    {
        var hit = CreateHit(initialHp, shield);
        Apply(hit, CriticalTwentyDamage(hit.Source));

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(expectedHp).Within(0.0001f));
        Assert.That(hit.Attributes.GetAttributeValue(hit.Shield), Is.EqualTo(Mathf.Max(0f, shield - 20f)).Within(0.0001f));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(hit.HealthChangeCount, Is.EqualTo(expectedDamage > 0f ? 1 : 0));
        Assert.That(hit.DamageTakenCount, Is.EqualTo(expectedDamage > 0f ? 1 : 0));
        Assert.That(hit.ActivityCount, Is.EqualTo(expectedDamage > 0f ? 1 : 0));
        Assert.That(hit.ActivityDamage, Is.EqualTo(expectedDamage).Within(0.0001f));
        Assert.That(hit.KillCount, Is.EqualTo(lethal ? 1 : 0));
        Assert.That(SumPopups(), Is.EqualTo(expectedDamage).Within(0.0001f), "Shield absorption and overkill cannot inflate the popup total.");
        Assert.That(popups.Requests, Has.Count.EqualTo(expectedDamage > 0f ? 3 : 0));
        if (expectedDamage <= 0f) return;

        var physical = popups.Requests.Find(request => request.ElementTag == null);
        Assert.That(physical.Kind, Is.EqualTo(DamagePopupKind.Critical));
        Assert.That(physical.Amount, Is.EqualTo(expectedDamage * 0.5f).Within(0.0001f));
        Assert.That(physical.SizeScale, Is.EqualTo(1f));
        AssertPopupElement("Element.Fire", expectedDamage * 0.2f, ExpectedSecondaryScale(4f, 10f));
        AssertPopupElement("Element.Blood", expectedDamage * 0.3f, ExpectedSecondaryScale(6f, 10f));
        foreach (var popup in popups.Requests) Assert.That(popup.IsCritical, Is.True);
    }

    [Test]
    public void HitDamageModifiers_ApplyOnceToCombinedPhysicalAndElementHp()
    {
        var hit = CreateHit(100f);
        int outgoingCount = 0;
        int incomingCount = 0;
        using (CombatOutgoingDamageModifiers.Register(context =>
        {
            if (context.SourceSystem != hit.Source) return context.BaseDamage;
            outgoingCount++;
            return context.BaseDamage + 2f;
        }))
        using (CombatIncomingDamageModifiers.Register(context =>
        {
            if (context.Target != hit.Target) return context.BaseDamage;
            incomingCount++;
            return context.BaseDamage * 0.5f;
        }))
            Apply(hit, CriticalTwentyDamage(hit.Source));

        Assert.That(outgoingCount, Is.EqualTo(1));
        Assert.That(incomingCount, Is.EqualTo(1));
        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(89f).Within(0.0001f));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(SumPopups(), Is.EqualTo(11f).Within(0.0001f));
        AssertPopupElement("Element.Fire", 2.2f, ExpectedSecondaryScale(4f, 10f));
        AssertPopupElement("Element.Blood", 3.3f, ExpectedSecondaryScale(6f, 10f));
    }

    [Test]
    public void LegacyDirectCall_DefaultNullElementChannelsDoNotAddAttackerElementStats()
    {
        var hit = CreateHit(100f);
        AddLiveFireStats(hit.Source, 50f);
        Assert.That(hit.Source.GetComponent<IStatProvider>().Get(StatId.FireFinal), Is.EqualTo(50f));
        CombatDamageAction.ApplyDamageAndEmitHit(hit.Source, null, hit.Damage, hit.Target, 5f, hit.HitTag, hit.Source.gameObject);

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(95f).Within(0.0001f));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(popups.Requests, Has.Count.EqualTo(1));
        Assert.That(popups.Requests[0].Kind, Is.EqualTo(DamagePopupKind.Normal));
        Assert.That(popups.Requests[0].Amount, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(popups.Requests[0].SizeScale, Is.EqualTo(1f));
    }

    [Test]
    public void FourElementCriticalPopups_DefaultResourceProfileKeepsElementColorsAndCriticalFormat()
    {
        var hit = CreateHit(100f);
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 2f, [StatId.BleedFinal] = 3f,
            [StatId.PoisonFinal] = 5f, [StatId.ElectricFinal] = 7f,
            [StatId.FinalMul] = 1f, [StatId.CritMultiplier] = 2f, [StatId.CritChanceFinal] = 1f
        };
        Apply(hit, DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 10f, 0f, 0f, hit.Source.gameObject));
        Type profileType = Type.GetType("DamagePopupFormatProfileSO, UI", true);
        var profile = Resources.Load("DamagePopup/DamagePopupFormatProfile_Default", profileType);
        Assert.That(profile, Is.Not.Null);
        MethodInfo build = profileType.GetMethod("BuildViewModel");
        object criticalStyle = build.Invoke(profile, new object[] { DamagePopupRequest.Damage(1f, Vector3.zero, true) });
        var entries = (Array)profileType.GetField("elementEntries", PrivateInstance).GetValue(profile);
        var authoredColors = new Dictionary<GameplayTag, Color>();
        foreach (object entry in entries)
            authoredColors[(GameplayTag)entry.GetType().GetField("elementTag").GetValue(entry)] =
                (Color)entry.GetType().GetField("textColor").GetValue(entry);

        int elementCount = 0;
        var usedColors = new HashSet<Color>();
        foreach (var request in popups.Requests)
        {
            if (request.Kind != DamagePopupKind.Element) continue;
            elementCount++;
            Assert.That(request.IsCritical, Is.True);
            Assert.That(authoredColors.ContainsKey(request.ElementTag), Is.True, request.ElementTag.Path);
            object view = build.Invoke(profile, new object[] { request });
            Type viewType = view.GetType();
            Color color = (Color)viewType.GetField("TextColor").GetValue(view);
            Assert.That(color, Is.EqualTo(authoredColors[request.ElementTag]), request.ElementTag.Path);
            usedColors.Add(color);
            Assert.That((string)viewType.GetField("Text").GetValue(view), Is.EqualTo(Mathf.CeilToInt(request.Amount) + "!"));
            foreach (string field in new[] { "MoveVelocity", "Lifetime" })
                Assert.That(viewType.GetField(field).GetValue(view), Is.EqualTo(viewType.GetField(field).GetValue(criticalStyle)), field);
            foreach (string field in new[] { "StartScale", "EndScale" })
                Assert.That((float)viewType.GetField(field).GetValue(view),
                    Is.EqualTo((float)viewType.GetField(field).GetValue(criticalStyle) * request.SizeScale).Within(0.0001f), field);
        }
        Assert.That(elementCount, Is.EqualTo(4));
        Assert.That(usedColors, Has.Count.EqualTo(4), "Each element keeps its distinct authored color on critical hits.");
    }

    [Test]
    public void SynchronousTargetHeal_ShowsConstituentPopupsAndSuppressesExactIncomingDamageFallback()
    {
        var hit = CreateHit(100f);
        bool suppressed = false;
        DamagePopupSuppressionKind suppressionKind = default;
        hit.Attributes.OnAttributeChanged += (attribute, oldValue, newValue) =>
        {
            if (attribute != hit.Health || newValue >= oldValue) return;
            float amount = oldValue - newValue;
            suppressed = DamagePopupDuplicateSuppressor.TryConsume(hit.Target, amount, out suppressionKind);
            if (!suppressed) DamagePopupPlayback.Show(DamagePopupRequest.Damage(amount, hit.Target.transform.position));
            Assert.That(hit.Attributes.TrySetBaseValue(hit.Health, oldValue, hit.Target), Is.True);
        };
        using (CombatIncomingDamageModifiers.Register(context =>
            context.Target == hit.Target ? context.BaseDamage * 1.5f : context.BaseDamage))
            Apply(hit, CriticalTwentyDamage(hit.Source));

        Assert.That(hit.Attributes.GetAttributeValue(hit.Health), Is.EqualTo(100f));
        Assert.That(suppressed, Is.True, "GE must reserve the actual incoming damage before synchronous attribute listeners run.");
        Assert.That(suppressionKind, Is.EqualTo(DamagePopupSuppressionKind.Element));
        Assert.That(hit.HealthChangeCount, Is.EqualTo(1));
        Assert.That(hit.HitCount, Is.EqualTo(1));
        Assert.That(popups.Requests, Has.Count.EqualTo(3), "Synchronous healing cannot add a fallback or combined popup.");
        Assert.That(SumPopups(), Is.EqualTo(30f).Within(0.0001f));
        AssertPopupElement("Element.Fire", 6f);
        AssertPopupElement("Element.Blood", 9f);
    }

    private CombatDamageSnapshot CriticalTwentyDamage(AbilitySystem source)
    {
        var stats = new MutableStats
        {
            [StatId.FireFinal] = 2f, [StatId.BleedFinal] = 3f,
            [StatId.FinalMul] = 1f, [StatId.CritMultiplier] = 2f, [StatId.CritChanceFinal] = 1f
        };
        return DamageSnapshotBuilder.BuildFromBaseValues(stats, null, 5f, 0f, 0f, source.gameObject);
    }

    private void Apply(HitContext hit, CombatDamageSnapshot snapshot, bool expectedAccepted = true,
        GameplayTag damagePopupElementTag = null)
    {
        var payload = CombatHitPayload.FromSnapshot(hit.Source, null, hit.Damage, null, snapshot, hit.HitTag, hit.Source.gameObject);
        payload.damagePopupElementTag = damagePopupElementTag;
        Assert.That(payload.TotalHpDamage, Is.EqualTo(snapshot.TotalHpDamage).Within(0.0001f));
        Assert.That(CombatHitPayloadApplier.Apply(hit.Target, payload, hit.Target.transform.position), Is.EqualTo(expectedAccepted));
    }

    private HitContext CreateHit(float health, float shield = 0f)
    {
        var hit = new HitContext
        {
            Health = CreateAttribute("ElementHitHealth", health),
            Shield = CreateAttribute("ElementHitShield", shield),
            HitTag = CreateTag("ElementHitTest.Hit"),
            KillTag = CreateTag("ElementHitTest.Kill"),
            DamagedTag = CreateTag("ElementHitTest.Damaged")
        };
        var catalog = CreateCatalog(hit.Health, hit.Shield);
        hit.Target = Own(new GameObject("ElementHitTarget"));
        hit.Target.SetActive(false);
        hit.Target.transform.position = new Vector3(10000f, 10000f, 0f);
        hit.Attributes = hit.Target.AddComponent<AttributeSet>();
        SetPrivate(hit.Attributes, "attributeCatalog", catalog);
        hit.Target.AddComponent<TagSystem>();
        hit.Target.AddComponent<GameplayEffectRunner>();
        var targetSystem = hit.Target.AddComponent<AbilitySystem>();
        SetPrivate(targetSystem, "damagedTag", hit.DamagedTag);
        hit.Target.SetActive(true);
        hit.Attributes.OnAttributeChanged += (attribute, oldValue, newValue) =>
        {
            if (attribute == hit.Health && newValue < oldValue) hit.HealthChangeCount++;
        };
        targetSystem.GameplayEventRaised += (tag, data) => { if (tag == hit.DamagedTag) hit.DamageTakenCount++; };

        var source = Own(new GameObject("ElementHitSource"));
        source.SetActive(false);
        source.AddComponent<TagSystem>();
        source.AddComponent<GameplayEffectRunner>();
        hit.Source = source.AddComponent<AbilitySystem>();
        SetPrivate(hit.Source, "killConfirmedTag", hit.KillTag);
        source.SetActive(true);
        hit.Source.GameplayEventRaised += (tag, data) =>
        {
            if (tag == hit.HitTag) { hit.HitCount++; hit.LastHit = data; }
            if (tag == hit.KillTag) hit.KillCount++;
        };
        Action<GameObject, GameObject, float> listener = (attacker, target, amount) =>
        {
            if (attacker != source || target != hit.Target) return;
            hit.ActivityCount++;
            hit.ActivityDamage += amount;
        };
        CombatActivityEvents.DamageApplied += listener;
        damageListeners.Add(listener);
        hit.Damage = Own(ScriptableObject.CreateInstance<GE_Damage_Spec>());
        hit.Damage.healthAttribute = hit.Health;
        hit.Damage.absorbShieldAttribute = hit.Shield;
        hit.Damage.damageKey = LoadTag("Data.Damage");
        hit.Damage.fallbackStunSeconds = hit.Damage.fallbackCameraShake = 0f;
        SetPrivate(hit.Damage, "logElementDamagePopup", false);
        return hit;
    }

    private void AddLiveFireStats(AbilitySystem source, float fire)
    {
        var fireAttribute = CreateAttribute("LegacyCallFire", fire);
        var bindings = Own(ScriptableObject.CreateInstance<StatTypeBindings>());
        SetPrivate(bindings, "bindings", new List<StatTypeBindings.Binding>
        {
            new() { id = StatId.FireFinal, attribute = fireAttribute }
        });
        source.gameObject.SetActive(false);
        var attributes = source.gameObject.AddComponent<AttributeSet>();
        SetPrivate(attributes, "attributeCatalog", CreateCatalog(fireAttribute));
        var stats = source.gameObject.AddComponent<AttributeStatSource>();
        SetPrivate(stats, "statBindingsOverride", bindings);
        source.gameObject.SetActive(true);
    }

    private ElementGaugeSystem CreateGauge(HitContext hit, params GameplayTag[] elements)
    {
        var catalog = Own(ScriptableObject.CreateInstance<ElementGaugeCatalog>());
        catalog.definitions = new ElementGaugeDefinition[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            var definition = Own(ScriptableObject.CreateInstance<ElementGaugeDefinition>());
            definition.elementTag = elements[i];
            catalog.definitions[i] = definition;
        }
        var gauge = hit.Target.AddComponent<ElementGaugeSystem>();
        SetPrivate(gauge, "catalog", catalog);
        SetPrivate(gauge, "useDecay", false);
        gauge.RebuildRuntimeStates();
        return gauge;
    }

    private AttributeDefinition CreateAttribute(string name, float initial)
    {
        var attribute = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
        attribute.name = name;
        attribute.minValue = 0f;
        attribute.maxValue = 1000f;
        attribute.defaultBaseValue = initial;
        return attribute;
    }

    private AttributeCatalogSO CreateCatalog(params AttributeDefinition[] attributes)
    {
        var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
        SetPrivate(catalog, "attributes", attributes);
        return catalog;
    }

    private GameplayTag CreateTag(string path)
    {
        var tag = Own(ScriptableObject.CreateInstance<GameplayTag>());
        tag.name = path;
        return tag;
    }

    private static GameplayTag LoadTag(string path)
    {
        var tag = Resources.Load<GameplayTag>("Tags/" + path);
        Assert.That(tag, Is.Not.Null, path);
        return tag;
    }

    private static void AssertElement(ElementDamageResult[] values, string path, float expected)
    {
        int index = Array.FindIndex(values, element => element.elementType != null && element.elementType.Path == path);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), path);
        Assert.That(values[index].damage, Is.EqualTo(expected).Within(0.0001f), path);
    }

    private void AssertPopupElement(string path, float expected, float? expectedScale = null)
    {
        var matching = popups.Requests.FindAll(request => request.ElementTag != null && request.ElementTag.Path == path);
        Assert.That(matching, Has.Count.EqualTo(1), path);
        Assert.That(matching[0].Kind, Is.EqualTo(DamagePopupKind.Element), path);
        Assert.That(matching[0].Amount, Is.EqualTo(expected).Within(0.0001f), path);
        if (expectedScale.HasValue)
            Assert.That(matching[0].SizeScale, Is.EqualTo(expectedScale.Value).Within(0.0001f), path);
    }

    private static float ExpectedSecondaryScale(float amount, float primaryAmount) =>
        Mathf.Min(0.85f, 0.42f + 0.43f * Mathf.Sqrt(Mathf.Min(1f, amount / primaryAmount)));

    private float SumPopups()
    {
        float sum = 0f;
        foreach (var popup in popups.Requests) sum += popup.Amount;
        return sum;
    }

    private static void SetPrivate(object target, string field, object value) =>
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    private sealed class MutableStats : IStatProvider
    {
        private readonly Dictionary<StatId, float> values = new();
        public float this[StatId id] { set => values[id] = value; }
        public float Get(StatId id) => values.TryGetValue(id, out float value) ? value : 0f;
    }

    private sealed class PopupRecorder : IDamagePopupBackend
    {
        public readonly List<DamagePopupRequest> Requests = new();
        public void Show(DamagePopupRequest request) => Requests.Add(request);
    }

    private sealed class HitContext
    {
        public AbilitySystem Source;
        public GameObject Target;
        public AttributeSet Attributes;
        public AttributeDefinition Health;
        public AttributeDefinition Shield;
        public GE_Damage_Spec Damage;
        public GameplayTag HitTag;
        public GameplayTag KillTag;
        public GameplayTag DamagedTag;
        public AbilityEventData LastHit;
        public int HitCount;
        public int KillCount;
        public int DamageTakenCount;
        public int HealthChangeCount;
        public int ActivityCount;
        public float ActivityDamage;
    }
}
#endif
