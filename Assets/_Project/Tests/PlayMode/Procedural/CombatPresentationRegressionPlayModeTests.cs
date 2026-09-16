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
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
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
#endif
